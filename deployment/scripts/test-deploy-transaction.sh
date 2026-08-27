#!/usr/bin/env bash
set -Eeuo pipefail

case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*)
    echo "배포 트랜잭션 테스트는 POSIX 권한을 지원하는 Linux CI에서 실행합니다."
    exit 0
    ;;
esac

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
temporary_directory="$(mktemp -d)"
fake_bin_directory="${temporary_directory}/bin"

cleanup() {
  find "$temporary_directory" -depth -type f -delete 2>/dev/null || true
  find "$temporary_directory" -depth -type l -delete 2>/dev/null || true
  find "$temporary_directory" -depth -type d -empty -delete 2>/dev/null || true
}
trap cleanup EXIT

mkdir -p "$fake_bin_directory"

cat > "${fake_bin_directory}/docker" <<'EOF'
#!/usr/bin/env bash
set -Eeuo pipefail

state="$MOCK_DOCKER_STATE"
mkdir -p "$state"
printf '%q ' docker "$@" >> "${state}/commands.log"
printf '\n' >> "${state}/commands.log"

container_exists() {
  [[ -f "${state}/$1.image" ]]
}

remove_container() {
  local name="$1"
  rm -f "${state}/${name}.image" "${state}/${name}.health"
}

set_container() {
  local name="$1"
  local image="$2"
  local health="$3"
  printf '%s' "$image" > "${state}/${name}.image"
  printf '%s' "$health" > "${state}/${name}.health"
}

command_name="${1:-}"
shift || true

case "$command_name" in
  login)
    cat >/dev/null
    if [[ -f "${state}/fail-login-once" ]]; then
      rm -f "${state}/fail-login-once"
      exit 1
    fi
    ;;
  pull)
    ;;
  start)
    name="$1"
    container_exists "$name"
    printf 'healthy' > "${state}/${name}.health"
    ;;
  inspect)
    if [[ "${1:-}" == "-f" ]]; then
      format="$2"
      name="$3"
      container_exists "$name" || exit 1
      case "$format" in
        *State.Health*) cat "${state}/${name}.health" ;;
        *State.Status*)
          status="$(cat "${state}/${name}.health")"
          if [[ "$status" == "exited" || "$status" == "dead" || "$status" == "stopped" ]]; then
            printf '%s' "$status"
          else
            printf 'running'
          fi
          ;;
        *Config.Image*) cat "${state}/${name}.image" ;;
        *com.docker.compose.service*)
          if [[ "$name" == "game-server" ]]; then
            printf 'app'
          fi
          ;;
      esac
    else
      container_exists "$1"
    fi
    ;;
  logs)
    ;;
  exec)
    printf '0'
    ;;
  container)
    operation="$1"
    shift
    case "$operation" in
      inspect)
        container_exists "$1"
        ;;
      remove)
        name="${*: -1}"
        remove_container "$name"
        ;;
      stop)
        name="$1"
        container_exists "$name"
        printf 'stopped' > "${state}/${name}.health"
        ;;
      *)
        echo "지원하지 않는 mock docker container 명령: ${operation}" >&2
        exit 1
        ;;
    esac
    ;;
  network)
    operation="$1"
    shift
    case "$operation" in
      inspect)
        format=""
        if [[ "${1:-}" == "--format" ]]; then
          format="$2"
          shift 2
        fi
        [[ -f "${state}/frontend.subnet" ]] || exit 1
        if [[ "$format" == *IPAM.Config* ]]; then
          cat "${state}/frontend.subnet"
        elif [[ "$format" == *Containers* ]]; then
          container_exists game-server && printf 'game-server\n'
          container_exists game-caddy && printf 'game-caddy\n'
        fi
        ;;
      disconnect)
        ;;
      rm)
        rm -f "${state}/frontend.subnet"
        ;;
      create)
        subnet=""
        while (( $# > 0 )); do
          if [[ "$1" == "--subnet" ]]; then
            subnet="$2"
            shift 2
          else
            shift
          fi
        done
        printf '%s' "$subnet" > "${state}/frontend.subnet"
        ;;
      *)
        echo "지원하지 않는 mock docker network 명령: ${operation}" >&2
        exit 1
        ;;
    esac
    ;;
  compose)
    if [[ "${1:-}" == "--env-file" ]]; then
      shift 2
    fi
    operation="$1"
    shift
    case "$operation" in
      config|pull|run)
        ;;
      up)
        service="${*: -1}"
        case "$service" in
          mysql)
            set_container game-mysql mysql:mock healthy
            ;;
          app)
            printf '%s' "${FRONTEND_SUBNET:-172.29.0.0/24}" > "${state}/frontend.subnet"
            if [[ -f "${state}/fail-app-once" ]]; then
              rm -f "${state}/fail-app-once"
              set_container game-server "${APP_IMAGE:-app:mock}" exited
            else
              set_container game-server "${APP_IMAGE:-app:mock}" healthy
            fi
            ;;
          caddy)
            if [[ -f "${state}/fail-caddy-once" ]]; then
              rm -f "${state}/fail-caddy-once"
              set_container game-caddy caddy:mock exited
            else
              set_container game-caddy caddy:mock healthy
            fi
            ;;
          backup)
            if [[ -f "${state}/fail-backup-once" ]]; then
              rm -f "${state}/fail-backup-once"
              set_container game-mysql-backup "${BACKUP_IMAGE:-backup:mock}" exited
            else
              set_container game-mysql-backup "${BACKUP_IMAGE:-backup:mock}" running
            fi
            ;;
          *)
            echo "지원하지 않는 mock compose service: ${service}" >&2
            exit 1
            ;;
        esac
        ;;
      *)
        echo "지원하지 않는 mock docker compose 명령: ${operation}" >&2
        exit 1
        ;;
    esac
    ;;
  *)
    echo "지원하지 않는 mock docker 명령: ${command_name}" >&2
    exit 1
    ;;
esac
EOF
chmod 700 "${fake_bin_directory}/docker"

cat > "${fake_bin_directory}/curl" <<'EOF'
#!/usr/bin/env bash
set -Eeuo pipefail

state="$MOCK_DOCKER_STATE"
write_out=""
url="${*: -1}"
printf '%s\n' "$url" >> "${state}/curl.log"
arguments=("$@")
for ((index = 0; index < ${#arguments[@]}; index++)); do
  if [[ "${arguments[$index]}" == "--write-out" ]]; then
    write_out="${arguments[$((index + 1))]}"
  fi
done

if [[ "$url" == https://*/live && -f "${state}/fail-proxy-once" ]]; then
  rm -f "${state}/fail-proxy-once"
  exit 22
fi

case "$write_out" in
  '%{http_code}')
    if [[ "$url" == http://* ]]; then
      printf '308'
    elif [[ "${url,,}" == */health \
      || "${url,,}" == */health/ \
      || "${url,,}" == */health%2f ]]; then
      printf '404'
    else
      printf '200'
    fi
    ;;
  '%{redirect_url}')
    printf 'https://%s/live' "$APP_DOMAIN"
    ;;
esac
EOF
chmod 700 "${fake_bin_directory}/curl"

primary_stage='.deployment-stage-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-1-1'
secondary_stage='.deployment-stage-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb-2-1'

create_candidate() {
  local workspace="$1"
  local stage="$2"
  local version="${3:-2}"
  local stage_root="${workspace}/${stage}"

  mkdir -m 700 "$stage_root"
  mkdir -p "${stage_root}/deployment/scripts" "${stage_root}/deployment/caddy"
  cp "${script_directory}/deploy.sh" "${stage_root}/deployment/scripts/deploy.sh"
  cp "${script_directory}/check-migration-readiness.sh" "${stage_root}/deployment/scripts/check-migration-readiness.sh"
  cp "${script_directory}/migration-common.sh" "${stage_root}/deployment/scripts/migration-common.sh"
  printf 'candidate-compose-%s\n' "$version" > "${stage_root}/compose.yaml"
  printf 'candidate-caddy-%s\n' "$version" > "${stage_root}/deployment/caddy/Caddyfile"
  printf 'candidate-only-%s\n' "$version" > "${stage_root}/deployment/candidate-only.txt"
}

create_scenario() {
  local name="$1"
  local initial_subnet="${2:-172.29.0.0/24}"
  local backup_enabled="${3:-false}"
  local previous_backup_image="${4:-}"
  local workspace="${temporary_directory}/${name}"
  local state="${workspace}/mock-state"

  mkdir -p \
    "${workspace}/deployment/scripts" \
    "${workspace}/deployment/caddy" \
    "${workspace}/deployment/backups" \
    "$state"

  printf '%s\n' \
    'MYSQL_DATABASE=zombie_survival' \
    'MYSQL_USER=zombie_app' \
    'MYSQL_PASSWORD=mock-password' \
    "BACKUP_ENABLED=${backup_enabled}" \
    'APP_DOMAIN=zombie-survival-3d-game.duckdns.org' \
    'APP_SCHEME=https' \
    'FRONTEND_SUBNET=172.29.0.0/24' \
    > "${workspace}/app.env"
  chmod 600 "${workspace}/app.env"

  printf 'old-compose\n' > "${workspace}/compose.yaml"
  printf 'old-caddy\n' > "${workspace}/deployment/caddy/Caddyfile"
  printf 'old-deployment\n' > "${workspace}/deployment/previous-version.txt"
  printf 'preserve-this-backup\n' > "${workspace}/deployment/backups/sentinel.sql.gz"
  printf 'APP_IMAGE=old-app:1\nBACKUP_IMAGE=old-backup:1\n' > "${workspace}/deployment.env"

  printf 'old-app:1' > "${state}/game-server.image"
  printf 'healthy' > "${state}/game-server.health"
  printf 'caddy:old' > "${state}/game-caddy.image"
  printf 'healthy' > "${state}/game-caddy.health"
  printf 'mysql:mock' > "${state}/game-mysql.image"
  printf 'healthy' > "${state}/game-mysql.health"
  if [[ -n "$previous_backup_image" ]]; then
    printf '%s' "$previous_backup_image" > "${state}/game-mysql-backup.image"
    printf 'running' > "${state}/game-mysql-backup.health"
  fi
  printf '%s' "$initial_subnet" > "${state}/frontend.subnet"

  create_candidate "$workspace" "$primary_stage"
  printf '%s' "$workspace"
}

run_deploy() {
  local workspace="$1"
  local stage="$2"
  local mode="$3"
  local app_image="${4:-candidate-app:2}"
  local backup_image="${5:-candidate-backup:2}"

  (
    cd "$workspace"
    env \
      -u APP_DOMAIN \
      -u APP_SCHEME \
      -u FRONTEND_SUBNET \
      PATH="${fake_bin_directory}:${PATH}" \
      MOCK_DOCKER_STATE="${workspace}/mock-state" \
      DEPLOY_STAGING_DIR="$stage" \
      DEPLOY_APP_IMAGE="$app_image" \
      DEPLOY_BACKUP_IMAGE="$backup_image" \
      GHCR_USERNAME='mock-user' \
      GHCR_PAT='mock-token' \
      bash "${stage}/deployment/scripts/deploy.sh" "$mode"
  )
}

assert_equals() {
  local expected="$1"
  local actual="$2"
  local message="$3"
  if [[ "$actual" != "$expected" ]]; then
    echo "${message}: expected=${expected}, actual=${actual}" >&2
    exit 1
  fi
}

backup_inode() {
  stat -c '%i' "$1/deployment/backups/sentinel.sql.gz"
}

assert_backups_preserved() {
  local workspace="$1"
  local expected_inode="$2"
  assert_equals preserve-this-backup \
    "$(tr -d '\r\n' < "${workspace}/deployment/backups/sentinel.sql.gz")" \
    "백업 sentinel 내용이 변경되었습니다"
  assert_equals "$expected_inode" "$(backup_inode "$workspace")" "백업 sentinel inode가 변경되었습니다"
}

assert_rolled_back() {
  local workspace="$1"
  local expected_inode="$2"
  local expected_backup_image="${3:-}"
  assert_equals old-app:1 "$(cat "${workspace}/mock-state/game-server.image")" "이전 앱 이미지가 복구되지 않았습니다"
  assert_equals old-compose "$(tr -d '\r\n' < "${workspace}/compose.yaml")" "이전 Compose 설정이 복구되지 않았습니다"
  assert_equals old-caddy "$(tr -d '\r\n' < "${workspace}/deployment/caddy/Caddyfile")" "이전 Caddy 설정이 복구되지 않았습니다"
  assert_equals old-deployment "$(tr -d '\r\n' < "${workspace}/deployment/previous-version.txt")" "이전 deployment 디렉터리가 복구되지 않았습니다"
  assert_backups_preserved "$workspace" "$expected_inode"
  if [[ -e "${workspace}/deployment/candidate-only.txt" ]]; then
    echo "롤백 후 후보 전용 deployment 파일이 남았습니다." >&2
    exit 1
  fi
  if [[ -n "$expected_backup_image" ]]; then
    assert_equals "$expected_backup_image" "$(cat "${workspace}/mock-state/game-mysql-backup.image")" "이전 백업 이미지가 복구되지 않았습니다"
  elif [[ -e "${workspace}/mock-state/game-mysql-backup.image" ]]; then
    echo "이전에 없던 백업 컨테이너가 롤백 후 남았습니다." >&2
    exit 1
  fi
  if [[ -e "${workspace}/.deployment-transaction.env" || -d "${workspace}/.deployment-config-rollback" ]]; then
    echo "롤백 후 트랜잭션 상태 또는 설정 스냅샷이 남았습니다." >&2
    exit 1
  fi
}

success_workspace="$(create_scenario success 172.28.0.0/24)"
success_backup_inode="$(backup_inode "$success_workspace")"
run_deploy "$success_workspace" "$primary_stage" prepare >/dev/null
assert_equals candidate-app:2 "$(cat "${success_workspace}/mock-state/game-server.image")" "후보 앱이 준비되지 않았습니다"
assert_equals candidate-compose-2 "$(tr -d '\r\n' < "${success_workspace}/compose.yaml")" "후보 Compose가 승격되지 않았습니다"
assert_equals 172.29.0.0/24 "$(cat "${success_workspace}/mock-state/frontend.subnet")" "frontend CIDR이 마이그레이션되지 않았습니다"
assert_backups_preserved "$success_workspace" "$success_backup_inode"
if [[ ! -f "${success_workspace}/.deployment-transaction.env" \
  || -e "${success_workspace}/.deployment-config-rollback/deployment/backups" ]]; then
  echo "prepare 상태 또는 backups 제외 스냅샷이 올바르지 않습니다." >&2
  exit 1
fi
if ! grep -Fxq 'https://zombie-survival-3d-game.duckdns.org/health/' "${success_workspace}/mock-state/curl.log"; then
  echo "로컬 /health/ 차단 검사가 실행되지 않았습니다." >&2
  exit 1
fi
for blocked_health_path in Health HEALTH/ health%2F; do
  if ! grep -Fxq \
    "https://zombie-survival-3d-game.duckdns.org/${blocked_health_path}" \
    "${success_workspace}/mock-state/curl.log"; then
    echo "로컬 /${blocked_health_path} 차단 검사가 실행되지 않았습니다." >&2
    exit 1
  fi
done
assert_equals old-app:1 "$(awk -F= '$1 == "APP_IMAGE" { print $2 }' "${success_workspace}/deployment.env")" "confirm 전에 deployment.env가 변경되었습니다"
run_deploy "$success_workspace" "$primary_stage" confirm >/dev/null
assert_equals candidate-app:2 "$(awk -F= '$1 == "APP_IMAGE" { print $2 }' "${success_workspace}/deployment.env")" "confirm이 후보 이미지를 확정하지 않았습니다"
assert_backups_preserved "$success_workspace" "$success_backup_inode"
if [[ -e "${success_workspace}/.deployment-transaction.env" \
  || ! -f "${success_workspace}/.deployment-config-rollback/confirmed" \
  || -d "${success_workspace}/${primary_stage}" ]]; then
  echo "confirm 후 상태 파일, confirmed marker 또는 staging 정리가 올바르지 않습니다." >&2
  exit 1
fi

create_candidate "$success_workspace" "$secondary_stage" 3
run_deploy "$success_workspace" "$secondary_stage" prepare candidate-app:3 candidate-backup:3 >/dev/null
run_deploy "$success_workspace" "$secondary_stage" confirm candidate-app:3 candidate-backup:3 >/dev/null
assert_equals candidate-app:3 "$(awk -F= '$1 == "APP_IMAGE" { print $2 }' "${success_workspace}/deployment.env")" "다음 배포가 확정되지 않았습니다"
assert_backups_preserved "$success_workspace" "$success_backup_inode"

for failure_kind in app caddy proxy; do
  failure_workspace="$(create_scenario "failure-${failure_kind}")"
  failure_backup_inode="$(backup_inode "$failure_workspace")"
  touch "${failure_workspace}/mock-state/fail-${failure_kind}-once"
  if run_deploy "$failure_workspace" "$primary_stage" prepare >/dev/null 2>&1; then
    echo "${failure_kind} 실패가 prepare 성공으로 처리되었습니다." >&2
    exit 1
  fi
  assert_rolled_back "$failure_workspace" "$failure_backup_inode"
done

explicit_workspace="$(create_scenario explicit-rollback)"
explicit_backup_inode="$(backup_inode "$explicit_workspace")"
run_deploy "$explicit_workspace" "$primary_stage" prepare >/dev/null
run_deploy "$explicit_workspace" "$primary_stage" rollback >/dev/null
assert_rolled_back "$explicit_workspace" "$explicit_backup_inode"

backup_restore_workspace="$(create_scenario backup-disabled-rollback 172.29.0.0/24 false old-backup:1)"
backup_restore_inode="$(backup_inode "$backup_restore_workspace")"
run_deploy "$backup_restore_workspace" "$primary_stage" prepare >/dev/null
if [[ -e "${backup_restore_workspace}/mock-state/game-mysql-backup.image" ]]; then
  echo "백업 비활성화 후보가 기존 백업 컨테이너를 제거하지 않았습니다." >&2
  exit 1
fi
run_deploy "$backup_restore_workspace" "$primary_stage" rollback >/dev/null
assert_rolled_back "$backup_restore_workspace" "$backup_restore_inode" old-backup:1

backup_failure_workspace="$(create_scenario backup-failure 172.29.0.0/24 true old-backup:1)"
backup_failure_inode="$(backup_inode "$backup_failure_workspace")"
touch "${backup_failure_workspace}/mock-state/fail-backup-once"
if run_deploy "$backup_failure_workspace" "$primary_stage" prepare >/dev/null 2>&1; then
  echo "백업 컨테이너 시작 실패가 prepare 성공으로 처리되었습니다." >&2
  exit 1
fi
assert_rolled_back "$backup_failure_workspace" "$backup_failure_inode" old-backup:1

backup_success_workspace="$(create_scenario backup-success 172.29.0.0/24 true)"
backup_success_inode="$(backup_inode "$backup_success_workspace")"
run_deploy "$backup_success_workspace" "$primary_stage" prepare >/dev/null
assert_equals candidate-backup:2 "$(cat "${backup_success_workspace}/mock-state/game-mysql-backup.image")" "후보 백업 이미지가 준비되지 않았습니다"
run_deploy "$backup_success_workspace" "$primary_stage" confirm >/dev/null
assert_equals candidate-backup:2 "$(awk -F= '$1 == "BACKUP_IMAGE" { print $2 }' "${backup_success_workspace}/deployment.env")" "confirm이 후보 백업 이미지를 확정하지 않았습니다"
assert_backups_preserved "$backup_success_workspace" "$backup_success_inode"

caddy_without_app_workspace="$(create_scenario caddy-without-app)"
caddy_without_app_inode="$(backup_inode "$caddy_without_app_workspace")"
rm -f \
  "${caddy_without_app_workspace}/mock-state/game-server.image" \
  "${caddy_without_app_workspace}/mock-state/game-server.health"
touch "${caddy_without_app_workspace}/mock-state/fail-proxy-once"
if run_deploy "$caddy_without_app_workspace" "$primary_stage" prepare >/dev/null 2>&1; then
  echo "프록시 실패가 prepare 성공으로 처리되었습니다." >&2
  exit 1
fi
if [[ -e "${caddy_without_app_workspace}/mock-state/game-server.image" \
  || ! -e "${caddy_without_app_workspace}/mock-state/game-caddy.image" ]]; then
  echo "이전 앱 없음/Caddy 있음 상태가 정확히 복구되지 않았습니다." >&2
  exit 1
fi
assert_backups_preserved "$caddy_without_app_workspace" "$caddy_without_app_inode"

migration_rollback_workspace="$(create_scenario migration-rollback 172.28.0.0/24)"
migration_rollback_inode="$(backup_inode "$migration_rollback_workspace")"
run_deploy "$migration_rollback_workspace" "$primary_stage" prepare >/dev/null
run_deploy "$migration_rollback_workspace" "$primary_stage" rollback >/dev/null
assert_rolled_back "$migration_rollback_workspace" "$migration_rollback_inode"
assert_equals 172.28.0.0/24 "$(cat "${migration_rollback_workspace}/mock-state/frontend.subnet")" "롤백이 이전 frontend CIDR을 복구하지 않았습니다"

stale_transaction_workspace="$(create_scenario stale-transaction)"
stale_transaction_inode="$(backup_inode "$stale_transaction_workspace")"
run_deploy "$stale_transaction_workspace" "$primary_stage" prepare >/dev/null
create_candidate "$stale_transaction_workspace" "$secondary_stage" 3
if run_deploy "$stale_transaction_workspace" "$secondary_stage" prepare candidate-app:3 candidate-backup:3 >/dev/null 2>&1; then
  echo "기존 미확정 트랜잭션이 있는데 두 번째 prepare가 허용되었습니다." >&2
  exit 1
fi
assert_equals candidate-app:2 "$(cat "${stale_transaction_workspace}/mock-state/game-server.image")" "기존 트랜잭션 거부 과정에서 후보 앱이 임의 롤백되었습니다"
run_deploy "$stale_transaction_workspace" "$primary_stage" rollback >/dev/null
assert_rolled_back "$stale_transaction_workspace" "$stale_transaction_inode"

pre_state_workspace="$(create_scenario pre-state-failure)"
pre_state_inode="$(backup_inode "$pre_state_workspace")"
touch "${pre_state_workspace}/mock-state/fail-login-once"
if run_deploy "$pre_state_workspace" "$primary_stage" prepare >/dev/null 2>&1; then
  echo "레지스트리 로그인 실패가 prepare 성공으로 처리되었습니다." >&2
  exit 1
fi
assert_rolled_back "$pre_state_workspace" "$pre_state_inode"

staged_backups_workspace="$(create_scenario staged-backups)"
staged_backups_inode="$(backup_inode "$staged_backups_workspace")"
mkdir -p "${staged_backups_workspace}/${primary_stage}/deployment/backups"
printf 'must-not-promote\n' > "${staged_backups_workspace}/${primary_stage}/deployment/backups/foreign.sql.gz"
if run_deploy "$staged_backups_workspace" "$primary_stage" prepare >/dev/null 2>&1; then
  echo "backups를 포함한 배포 후보가 허용되었습니다." >&2
  exit 1
fi
assert_equals old-compose "$(tr -d '\r\n' < "${staged_backups_workspace}/compose.yaml")" "스테이징 검증 전에 active 설정이 변경되었습니다"
assert_backups_preserved "$staged_backups_workspace" "$staged_backups_inode"

missing_state_workspace="$(create_scenario missing-state-confirm)"
if run_deploy "$missing_state_workspace" "$primary_stage" confirm >/dev/null 2>&1; then
  echo "상태 파일이 없는 confirm이 허용되었습니다." >&2
  exit 1
fi

symlink_state_workspace="$(create_scenario symlink-state-confirm)"
ln -s app.env "${symlink_state_workspace}/.deployment-transaction.env"
if run_deploy "$symlink_state_workspace" "$primary_stage" confirm >/dev/null 2>&1; then
  echo "심볼릭 링크인 상태 파일을 사용한 confirm이 허용되었습니다." >&2
  exit 1
fi
if [[ ! -d "${symlink_state_workspace}/${primary_stage}" ]]; then
  echo "안전하지 않은 상태 파일을 거부하면서 검증되지 않은 staging까지 삭제했습니다." >&2
  exit 1
fi

live_lock_workspace="$(create_scenario live-lock)"
live_lock_inode="$(backup_inode "$live_lock_workspace")"
mkdir -m 700 "${live_lock_workspace}/.deployment.lock"
printf 'PID=%s\nBOOT_ID=%s\nSTART_TIME=%s\n' \
  "$$" \
  "$(cat /proc/sys/kernel/random/boot_id)" \
  "$(awk '{print $22}' "/proc/$$/stat")" \
  > "${live_lock_workspace}/.deployment.lock/owner"
chmod 600 "${live_lock_workspace}/.deployment.lock/owner"
if run_deploy "$live_lock_workspace" "$primary_stage" prepare >/dev/null 2>&1; then
  echo "실행 중인 프로세스의 배포 잠금을 탈취했습니다." >&2
  exit 1
fi
assert_equals old-compose "$(tr -d '\r\n' < "${live_lock_workspace}/compose.yaml")" "잠금 획득 전에 active 설정이 변경되었습니다"
assert_backups_preserved "$live_lock_workspace" "$live_lock_inode"

partial_lock_workspace="$(create_scenario partial-lock)"
partial_lock_inode="$(backup_inode "$partial_lock_workspace")"
mkdir -m 700 "${partial_lock_workspace}/.deployment.lock"
if run_deploy "$partial_lock_workspace" "$primary_stage" prepare >/dev/null 2>&1; then
  echo "5분 미만의 부분 작성 잠금을 회수했습니다." >&2
  exit 1
fi
assert_equals old-compose "$(tr -d '\r\n' < "${partial_lock_workspace}/compose.yaml")" "부분 잠금 확인 전에 active 설정이 변경되었습니다"
assert_backups_preserved "$partial_lock_workspace" "$partial_lock_inode"

stale_lock_workspace="$(create_scenario stale-lock)"
stale_lock_inode="$(backup_inode "$stale_lock_workspace")"
mkdir -m 700 "${stale_lock_workspace}/.deployment.lock"
touch -d '10 minutes ago' "${stale_lock_workspace}/.deployment.lock"
run_deploy "$stale_lock_workspace" "$primary_stage" prepare >/dev/null
run_deploy "$stale_lock_workspace" "$primary_stage" rollback >/dev/null
assert_rolled_back "$stale_lock_workspace" "$stale_lock_inode"

echo "고유 staging 승격, backups 보존, 잠금 및 prepare/confirm/rollback 통합 테스트가 통과했습니다."
