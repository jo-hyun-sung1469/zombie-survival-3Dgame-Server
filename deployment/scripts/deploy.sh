#!/usr/bin/env bash
set -Eeuo pipefail

required_variables=(DEPLOY_APP_IMAGE DEPLOY_BACKUP_IMAGE GHCR_USERNAME GHCR_PAT)
for variable_name in "${required_variables[@]}"; do
  if [[ -z "${!variable_name:-}" ]]; then
    echo "필수 배포 변수가 비어 있습니다: ${variable_name}" >&2
    exit 1
  fi
done

if [[ ! -f app.env ]]; then
  echo "배포 디렉터리에 app.env가 없습니다." >&2
  exit 1
fi

chmod 600 app.env
compose=(docker compose --env-file app.env)

wait_for_health() {
  local container_name="$1"
  local timeout_seconds="$2"
  local deadline=$((SECONDS + timeout_seconds))

  while (( SECONDS < deadline )); do
    local status
    status="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$container_name" 2>/dev/null || true)"
    if [[ "$status" == "healthy" ]]; then
      return 0
    fi
    sleep 2
  done

  return 1
}

printf '%s' "$GHCR_PAT" | docker login ghcr.io --username "$GHCR_USERNAME" --password-stdin
docker pull "$DEPLOY_APP_IMAGE"
APP_IMAGE="$DEPLOY_APP_IMAGE" BACKUP_IMAGE="$DEPLOY_BACKUP_IMAGE" "${compose[@]}" pull caddy
backup_enabled="$(awk -F= '$1 == "BACKUP_ENABLED" { print tolower($2) }' app.env | tail -n 1)"
backup_enabled="${backup_enabled:-false}"

if [[ "$backup_enabled" == "true" ]]; then
  docker pull "$DEPLOY_BACKUP_IMAGE"
  echo "기존 앱을 유지한 채 S3 쓰기 권한을 검증합니다."
  BACKUP_IMAGE="$DEPLOY_BACKUP_IMAGE" "${compose[@]}" run \
    --rm \
    --no-deps \
    --entrypoint /usr/local/bin/s3-write-check.sh \
    backup
else
  echo "S3 백업이 비활성화되어 백업 이미지와 권한 검사를 건너뜁니다."
fi

if docker container inspect game-mysql >/dev/null 2>&1; then
  docker start game-mysql >/dev/null
else
  APP_IMAGE="$DEPLOY_APP_IMAGE" BACKUP_IMAGE="$DEPLOY_BACKUP_IMAGE" "${compose[@]}" up -d mysql
fi

if ! wait_for_health game-mysql 120; then
  docker logs --tail 200 game-mysql || true
  echo "MySQL이 제한 시간 안에 healthy 상태가 되지 않았습니다." >&2
  exit 1
fi

echo "EF Migration 적용 가능 여부를 확인합니다."
bash deployment/scripts/check-migration-readiness.sh

if [[ "$backup_enabled" == "true" ]]; then
  APP_IMAGE="$DEPLOY_APP_IMAGE" BACKUP_IMAGE="$DEPLOY_BACKUP_IMAGE" "${compose[@]}" up \
    -d --no-deps --force-recreate backup
  if ! docker container inspect game-mysql-backup >/dev/null 2>&1; then
    echo "백업 컨테이너를 시작하지 못했습니다." >&2
    exit 1
  fi
elif docker container inspect game-mysql-backup >/dev/null 2>&1; then
  echo "S3 백업이 비활성화되어 기존 백업 컨테이너를 제거합니다."
  docker container remove --force game-mysql-backup >/dev/null
fi

previous_app_image="$(docker inspect -f '{{.Config.Image}}' game-server 2>/dev/null || true)"
app_compose_service="$(docker inspect -f '{{index .Config.Labels "com.docker.compose.service"}}' game-server 2>/dev/null || true)"

if [[ -n "$previous_app_image" && "$app_compose_service" != "app" ]]; then
  docker container remove --force game-server >/dev/null
fi

echo "새 앱 이미지를 배포합니다: ${DEPLOY_APP_IMAGE}"
APP_IMAGE="$DEPLOY_APP_IMAGE" BACKUP_IMAGE="$DEPLOY_BACKUP_IMAGE" "${compose[@]}" up \
  -d --no-deps --force-recreate app

if wait_for_health game-server 90; then
  echo "HTTPS 역방향 프록시를 시작합니다."
  APP_IMAGE="$DEPLOY_APP_IMAGE" BACKUP_IMAGE="$DEPLOY_BACKUP_IMAGE" "${compose[@]}" up \
    -d --no-deps --force-recreate caddy

  if ! wait_for_health game-caddy 60; then
    docker logs --tail 200 game-caddy || true
    echo "Caddy가 제한 시간 안에 healthy 상태가 되지 않았습니다." >&2
    exit 1
  fi

  active_backup_image="$DEPLOY_BACKUP_IMAGE"
  if [[ "$backup_enabled" == "true" ]]; then
    active_backup_image="$(docker inspect -f '{{.Config.Image}}' game-mysql-backup)"
  fi
  umask 077
  printf 'APP_IMAGE=%s\nBACKUP_IMAGE=%s\n' "$DEPLOY_APP_IMAGE" "$active_backup_image" > deployment.env
  echo "배포가 완료되었습니다."
  exit 0
fi

docker logs --tail 200 game-server || true
echo "새 앱이 90초 안에 healthy 상태가 되지 않아 롤백합니다." >&2

if [[ -n "$previous_app_image" ]]; then
  APP_IMAGE="$previous_app_image" BACKUP_IMAGE="$DEPLOY_BACKUP_IMAGE" "${compose[@]}" up \
    -d --no-deps --force-recreate app

  if wait_for_health game-server 90; then
    echo "이전 앱 이미지로 복구했습니다: ${previous_app_image}" >&2
  else
    docker logs --tail 200 game-server || true
    echo "이전 앱 이미지도 healthy 상태로 복구되지 않았습니다." >&2
  fi
else
  echo "복구할 이전 앱 이미지가 없습니다." >&2
fi

exit 1
