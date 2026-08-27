#!/usr/bin/env bash
set -Eeuo pipefail

deployment_mode="${1:-}"
app_env_file="${APP_ENV_FILE:-app.env}"
transaction_state_file="${DEPLOY_STATE_FILE:-.deployment-transaction.env}"
deployment_lock_directory="${DEPLOY_LOCK_DIRECTORY:-.deployment.lock}"
configuration_snapshot_directory="${DEPLOY_CONFIG_SNAPSHOT_DIR:-.deployment-config-rollback}"
staged_deployment_directory="${DEPLOY_STAGING_DIR:-}"
requested_staged_deployment_directory="${DEPLOY_STAGING_DIR:-}"
frontend_network_name="zombie_survival_frontend"
docker_config_directory=""
deployment_lock_acquired="false"
transaction_started_by_process="false"
configuration_snapshot_created_by_process="false"
configuration_snapshot_pending_directory=""
compose=(docker compose --env-file "$app_env_file")

usage() {
  cat >&2 <<'EOF'
사용법: bash deployment/scripts/deploy.sh <prepare|confirm|rollback|validate>

  prepare   새 앱과 Caddy를 시작하고 외부 검증 전 트랜잭션을 유지합니다.
  confirm   외부 HTTPS 검증에 성공한 배포를 확정합니다.
  rollback  보류 중인 배포를 이전 앱으로 복구합니다.
  validate  운영 APP_DOMAIN/APP_SCHEME 형식만 검증합니다.
EOF
}

validate_deployment_root() {
  local deployment_root
  local root_owner
  local root_mode
  local root_mode_value

  deployment_root="$(pwd -P)"
  root_owner="$(stat -c '%u' "$deployment_root")"
  root_mode="$(stat -c '%a' "$deployment_root")"

  if [[ "$root_owner" != "$EUID" ]]; then
    echo "배포 루트의 소유자가 현재 배포 계정과 다릅니다: ${deployment_root}" >&2
    return 1
  fi
  if [[ ! "$root_mode" =~ ^[0-7]{3,4}$ ]]; then
    echo "배포 루트 권한을 확인할 수 없습니다: ${deployment_root}" >&2
    return 1
  fi

  root_mode_value=$((8#$root_mode))
  if (( (root_mode_value & 0022) != 0 )); then
    echo "배포 루트는 group/other 쓰기 권한을 허용할 수 없습니다: ${deployment_root}" >&2
    return 1
  fi
}

validate_staging_directory_path() {
  local staging_directory="$1"

  [[ "$staging_directory" =~ ^\.deployment-stage-[a-f0-9]{40}-[0-9]+-[0-9]+$ ]]
}

validate_backups_directory() {
  local backups_directory="deployment/backups"

  if [[ ! -e "$backups_directory" && ! -L "$backups_directory" ]]; then
    return 0
  fi
  if [[ -L "$backups_directory" || ! -d "$backups_directory" ]]; then
    echo "백업 보존 경로가 실제 디렉터리가 아닙니다: ${backups_directory}" >&2
    return 1
  fi
  if [[ "$(stat -c '%u' "$backups_directory")" != "$EUID" ]]; then
    echo "백업 보존 경로의 소유자가 현재 배포 계정과 다릅니다: ${backups_directory}" >&2
    return 1
  fi
}

validate_staging_directory() {
  local unsafe_entry
  local unowned_entry
  local staging_mode
  local required_file
  local -a required_files=(
    compose.yaml
    deployment/caddy/Caddyfile
    deployment/scripts/deploy.sh
    deployment/scripts/check-migration-readiness.sh
    deployment/scripts/migration-common.sh
  )

  if ! validate_staging_directory_path "$staged_deployment_directory"; then
    echo "DEPLOY_STAGING_DIR은 배포 루트 직속의 고유한 상대 경로여야 합니다." >&2
    return 1
  fi
  if [[ -L "$staged_deployment_directory" || ! -d "$staged_deployment_directory" ]]; then
    echo "스테이징 경로가 실제 디렉터리가 아닙니다: ${staged_deployment_directory}" >&2
    return 1
  fi
  if [[ "$(stat -c '%u' "$staged_deployment_directory")" != "$EUID" ]]; then
    echo "스테이징 디렉터리의 소유자가 현재 배포 계정과 다릅니다." >&2
    return 1
  fi
  staging_mode="$(stat -c '%a' "$staged_deployment_directory")"
  if [[ "$staging_mode" != "700" ]]; then
    echo "스테이징 디렉터리 권한은 0700이어야 합니다: ${staging_mode}" >&2
    return 1
  fi

  unsafe_entry="$(find "$staged_deployment_directory" -mindepth 1 ! \( -type f -o -type d \) -print -quit)"
  if [[ -n "$unsafe_entry" ]]; then
    echo "스테이징에 심볼릭 링크 또는 특수 파일이 포함되어 있습니다: ${unsafe_entry}" >&2
    return 1
  fi
  unowned_entry="$(find "$staged_deployment_directory" -mindepth 1 ! -uid "$EUID" -print -quit)"
  if [[ -n "$unowned_entry" ]]; then
    echo "스테이징에 다른 계정이 소유한 항목이 포함되어 있습니다: ${unowned_entry}" >&2
    return 1
  fi
  if [[ -e "${staged_deployment_directory}/deployment/backups" \
    || -L "${staged_deployment_directory}/deployment/backups" ]]; then
    echo "배포 후보에는 deployment/backups를 포함할 수 없습니다." >&2
    return 1
  fi

  for required_file in "${required_files[@]}"; do
    if [[ ! -f "${staged_deployment_directory}/${required_file}" \
      || -L "${staged_deployment_directory}/${required_file}" ]]; then
      echo "스테이징된 필수 배포 파일이 없습니다: ${required_file}" >&2
      return 1
    fi
  done
}

cleanup_process_resources() {
  if [[ -n "$docker_config_directory" && -d "$docker_config_directory" ]]; then
    find "$docker_config_directory" -depth -type f -delete 2>/dev/null || true
    find "$docker_config_directory" -depth -type d -empty -delete 2>/dev/null || true
  fi

  if [[ "$deployment_lock_acquired" == "true" ]]; then
    rm -f "${deployment_lock_directory}/owner"
    rmdir "$deployment_lock_directory" 2>/dev/null || true
  fi

  if [[ -n "$configuration_snapshot_pending_directory" && -d "$configuration_snapshot_pending_directory" ]]; then
    find "$configuration_snapshot_pending_directory" -depth -type f -delete 2>/dev/null || true
    find "$configuration_snapshot_pending_directory" -depth -type l -delete 2>/dev/null || true
    find "$configuration_snapshot_pending_directory" -depth -type d -empty -delete 2>/dev/null || true
  fi
}
trap cleanup_process_resources EXIT

read_env_value() {
  local key="$1"

  awk -F= -v key="$key" '
    $1 == key {
      value = substr($0, index($0, "=") + 1)
    }
    END {
      sub(/\r$/, "", value)
      print value
    }
  ' "$app_env_file"
}

validate_app_environment_file() {
  local environment_mode

  if [[ -L "$app_env_file" || ! -f "$app_env_file" ]]; then
    echo "배포 환경 파일이 안전한 일반 파일이 아닙니다: ${app_env_file}" >&2
    return 1
  fi
  if [[ "$(stat -c '%u' "$app_env_file")" != "$EUID" ]]; then
    echo "배포 환경 파일의 소유자가 현재 배포 계정과 다릅니다." >&2
    return 1
  fi
  environment_mode="$(stat -c '%a' "$app_env_file")"
  if [[ "$environment_mode" != "600" ]]; then
    echo "배포 환경 파일 권한은 0600이어야 합니다: ${environment_mode}" >&2
    return 1
  fi
}

validate_fqdn() {
  local domain="$1"
  local label
  local -a labels

  if [[ -z "$domain" || ${#domain} -gt 253 || "$domain" == .* || "$domain" == *. ]]; then
    return 1
  fi

  IFS='.' read -r -a labels <<< "$domain"
  if (( ${#labels[@]} < 2 )); then
    return 1
  fi

  for label in "${labels[@]}"; do
    if [[ ! "$label" =~ ^[A-Za-z0-9]$ && ! "$label" =~ ^[A-Za-z0-9][A-Za-z0-9-]{0,61}[A-Za-z0-9]$ ]]; then
      return 1
    fi
  done

  [[ "${labels[-1]}" =~ ^[A-Za-z]{2,63}$ ]]
}

validate_ipv4_cidr() {
  local cidr="$1"
  local address
  local prefix
  local octet
  local -a octets

  if [[ "$cidr" != */* ]]; then
    return 1
  fi

  address="${cidr%/*}"
  prefix="${cidr##*/}"
  if [[ ! "$prefix" =~ ^[0-9]+$ ]] || (( 10#$prefix < 24 || 10#$prefix > 29 )); then
    return 1
  fi

  IFS='.' read -r -a octets <<< "$address"
  if (( ${#octets[@]} != 4 )); then
    return 1
  fi

  for octet in "${octets[@]}"; do
    if [[ ! "$octet" =~ ^[0-9]{1,3}$ ]] || (( 10#$octet > 255 )); then
      return 1
    fi
  done

  local block_size=$((1 << (32 - 10#$prefix)))
  if (( 10#${octets[3]} % block_size != 0 )); then
    return 1
  fi

  local first_octet=$((10#${octets[0]}))
  local second_octet=$((10#${octets[1]}))
  if (( first_octet == 10 )); then
    return 0
  fi
  if (( first_octet == 172 && second_octet >= 16 && second_octet <= 31 )); then
    return 0
  fi
  if (( first_octet == 192 && second_octet == 168 )); then
    return 0
  fi

  return 1
}

load_and_validate_proxy_settings() {
  validate_app_environment_file

  local configured_domain
  local configured_scheme
  local configured_frontend_subnet
  local expected_domain="${APP_DOMAIN:-}"
  local expected_scheme="${APP_SCHEME:-}"
  configured_domain="$(read_env_value APP_DOMAIN)"
  configured_scheme="$(read_env_value APP_SCHEME)"
  configured_frontend_subnet="$(read_env_value FRONTEND_SUBNET)"

  configured_scheme="${configured_scheme:-https}"
  configured_frontend_subnet="${configured_frontend_subnet:-172.29.0.0/24}"

  if [[ -n "$expected_domain" && "${expected_domain,,}" != "${configured_domain,,}" ]]; then
    echo "GitHub production APP_DOMAIN과 서버 app.env의 APP_DOMAIN이 다릅니다." >&2
    return 1
  fi
  if [[ -n "$expected_scheme" && "$expected_scheme" != "$configured_scheme" ]]; then
    echo "GitHub production APP_SCHEME과 서버 app.env의 APP_SCHEME이 다릅니다." >&2
    return 1
  fi

  if ! validate_fqdn "$configured_domain"; then
    echo "APP_DOMAIN은 스킴, 포트, 경로가 없는 공개 FQDN이어야 합니다." >&2
    return 1
  fi

  if [[ "$configured_scheme" != "https" ]]; then
    echo "운영 배포의 APP_SCHEME은 https만 허용합니다." >&2
    return 1
  fi

  if ! validate_ipv4_cidr "$configured_frontend_subnet"; then
    echo "FRONTEND_SUBNET은 네트워크 주소로 정렬된 RFC1918 사설 IPv4 /24~29 CIDR이어야 합니다." >&2
    return 1
  fi

  APP_DOMAIN="${configured_domain,,}"
  APP_SCHEME="$configured_scheme"
  FRONTEND_SUBNET="$configured_frontend_subnet"
  export APP_DOMAIN APP_SCHEME FRONTEND_SUBNET
}

acquire_deployment_lock() {
  local lock_pid="$BASHPID"
  local current_boot_id
  local current_start_time

  current_boot_id="$(cat /proc/sys/kernel/random/boot_id 2>/dev/null || true)"
  current_start_time="$(awk '{print $22}' "/proc/${lock_pid}/stat" 2>/dev/null || true)"
  if [[ -z "$current_boot_id" || -z "$current_start_time" ]]; then
    echo "배포 잠금을 확인할 Linux 프로세스 정보가 없습니다." >&2
    return 1
  fi

  if ! mkdir -m 700 "$deployment_lock_directory" 2>/dev/null; then
    if [[ -L "$deployment_lock_directory" || ! -d "$deployment_lock_directory" ]]; then
      echo "배포 잠금 경로가 안전한 디렉터리가 아닙니다: ${deployment_lock_directory}" >&2
      return 1
    fi

    local lock_owner
    lock_owner="$(stat -c '%u' "$deployment_lock_directory")"
    if [[ "$lock_owner" != "$EUID" ]]; then
      echo "배포 잠금 디렉터리의 소유자가 현재 배포 계정과 다릅니다." >&2
      return 1
    fi

    local recorded_pid=""
    local recorded_boot_id=""
    local recorded_start_time=""
    local lock_age_seconds
    local owner_complete="false"
    lock_age_seconds=$(( $(date +%s) - $(stat -c '%Y' "$deployment_lock_directory") ))
    if [[ -f "${deployment_lock_directory}/owner" && ! -L "${deployment_lock_directory}/owner" ]]; then
      recorded_pid="$(awk -F= '$1 == "PID" { print $2 }' "${deployment_lock_directory}/owner")"
      recorded_boot_id="$(awk -F= '$1 == "BOOT_ID" { print $2 }' "${deployment_lock_directory}/owner")"
      recorded_start_time="$(awk -F= '$1 == "START_TIME" { print $2 }' "${deployment_lock_directory}/owner")"
      if [[ "$recorded_pid" =~ ^[0-9]+$ && -n "$recorded_boot_id" && "$recorded_start_time" =~ ^[0-9]+$ ]]; then
        owner_complete="true"
      fi
    fi

    local live_start_time=""
    if [[ "$recorded_pid" =~ ^[0-9]+$ && -r "/proc/${recorded_pid}/stat" ]]; then
      live_start_time="$(awk '{print $22}' "/proc/${recorded_pid}/stat" 2>/dev/null || true)"
    fi

    if [[ "$owner_complete" == "true" && "$recorded_boot_id" == "$current_boot_id"
      && -n "$recorded_start_time" && "$recorded_start_time" == "$live_start_time" ]]; then
      echo "다른 배포 작업이 실행 중입니다: ${deployment_lock_directory}" >&2
      return 1
    fi
    if (( lock_age_seconds < 300 )); then
      echo "생성 후 5분이 지나지 않은 배포 잠금은 자동 정리하지 않습니다: ${deployment_lock_directory}" >&2
      return 1
    fi

    local stale_lock_directory="${deployment_lock_directory}.stale.${lock_pid}"
    if ! mv "$deployment_lock_directory" "$stale_lock_directory" 2>/dev/null; then
      echo "다른 프로세스가 배포 잠금을 갱신했습니다. 다시 시도하세요." >&2
      return 1
    fi
    find "$stale_lock_directory" -mindepth 1 -maxdepth 1 -type f -delete
    find "$stale_lock_directory" -mindepth 1 -maxdepth 1 -type l -delete
    if ! rmdir "$stale_lock_directory"; then
      mv "$stale_lock_directory" "$deployment_lock_directory" 2>/dev/null || true
      echo "오래된 배포 잠금에 알 수 없는 항목이 있어 자동 정리를 중단합니다." >&2
      return 1
    fi
    if ! mkdir -m 700 "$deployment_lock_directory" 2>/dev/null; then
      echo "다른 프로세스가 배포 잠금을 먼저 획득했습니다." >&2
      return 1
    fi
  fi

  chmod 700 "$deployment_lock_directory"
  umask 077
  printf 'PID=%s\nBOOT_ID=%s\nSTART_TIME=%s\n' \
    "$lock_pid" "$current_boot_id" "$current_start_time" \
    > "${deployment_lock_directory}/owner"
  chmod 600 "${deployment_lock_directory}/owner"
  deployment_lock_acquired="true"
}

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
    if [[ "$status" == "exited" || "$status" == "dead" ]]; then
      return 1
    fi
    sleep 2
  done

  return 1
}

wait_for_running() {
  local container_name="$1"
  local timeout_seconds="$2"
  local deadline=$((SECONDS + timeout_seconds))

  while (( SECONDS < deadline )); do
    local status
    status="$(docker inspect -f '{{.State.Status}}' "$container_name" 2>/dev/null || true)"
    if [[ "$status" == "running" ]]; then
      return 0
    fi
    if [[ "$status" == "exited" || "$status" == "dead" ]]; then
      return 1
    fi
    sleep 2
  done

  return 1
}

container_exists() {
  docker container inspect "$1" >/dev/null 2>&1
}

current_frontend_subnet() {
  if ! docker network inspect "$frontend_network_name" >/dev/null 2>&1; then
    return 0
  fi

  docker network inspect \
    --format '{{range .IPAM.Config}}{{println .Subnet}}{{end}}' \
    "$frontend_network_name" | sed -n '1p'
}

assert_frontend_network_can_be_migrated() {
  local current_subnet="$1"
  local container_name
  local -a attached_containers

  if [[ -z "$current_subnet" || "$current_subnet" == "$FRONTEND_SUBNET" ]]; then
    return 0
  fi

  mapfile -t attached_containers < <(
    docker network inspect \
      --format '{{range .Containers}}{{println .Name}}{{end}}' \
      "$frontend_network_name"
  )

  for container_name in "${attached_containers[@]}"; do
    case "$container_name" in
      ""|game-server|game-caddy)
        ;;
      *)
        echo "frontend 네트워크에 알 수 없는 컨테이너가 연결되어 있어 자동 재생성을 중단합니다: ${container_name}" >&2
        return 1
        ;;
    esac
  done
}

write_transaction_state() {
  local previous_app_image="$1"
  local previous_app_present="$2"
  local previous_caddy_present="$3"
  local previous_backup_image="$4"
  local previous_backup_present="$5"
  local previous_frontend_subnet="$6"
  local frontend_migration_required="$7"
  local candidate_backup_enabled="$8"
  local active_backup_image="$9"
  local state_directory
  local state_filename
  local temporary_state_file

  state_directory="$(dirname -- "$transaction_state_file")"
  state_filename="$(basename -- "$transaction_state_file")"
  temporary_state_file="$(mktemp "${state_directory}/.${state_filename}.tmp.XXXXXX")"

  umask 077
  {
    printf 'TRANSACTION_VERSION=%q\n' "3"
    printf 'CANDIDATE_APP_IMAGE=%q\n' "$DEPLOY_APP_IMAGE"
    printf 'CANDIDATE_BACKUP_IMAGE=%q\n' "$DEPLOY_BACKUP_IMAGE"
    printf 'CANDIDATE_BACKUP_ENABLED=%q\n' "$candidate_backup_enabled"
    printf 'ACTIVE_BACKUP_IMAGE=%q\n' "$active_backup_image"
    printf 'PREVIOUS_APP_IMAGE=%q\n' "$previous_app_image"
    printf 'PREVIOUS_APP_PRESENT=%q\n' "$previous_app_present"
    printf 'PREVIOUS_CADDY_PRESENT=%q\n' "$previous_caddy_present"
    printf 'PREVIOUS_BACKUP_IMAGE=%q\n' "$previous_backup_image"
    printf 'PREVIOUS_BACKUP_PRESENT=%q\n' "$previous_backup_present"
    printf 'PREVIOUS_FRONTEND_SUBNET=%q\n' "$previous_frontend_subnet"
    printf 'FRONTEND_NETWORK_MIGRATION_REQUIRED=%q\n' "$frontend_migration_required"
    printf 'TRANSACTION_APP_DOMAIN=%q\n' "$APP_DOMAIN"
    printf 'TRANSACTION_APP_SCHEME=%q\n' "$APP_SCHEME"
    printf 'TRANSACTION_FRONTEND_SUBNET=%q\n' "$FRONTEND_SUBNET"
    printf 'TRANSACTION_STAGING_DIRECTORY=%q\n' "$staged_deployment_directory"
  } > "$temporary_state_file"
  chmod 600 "$temporary_state_file"
  mv -f "$temporary_state_file" "$transaction_state_file"
}

load_transaction_state() {
  if [[ -L "$transaction_state_file" ]]; then
    echo "배포 트랜잭션 상태 파일이 심볼릭 링크여서 읽지 않습니다: ${transaction_state_file}" >&2
    return 1
  fi

  if [[ ! -f "$transaction_state_file" ]]; then
    remove_staged_deployment
    echo "미확정 배포 상태 파일이 없습니다: ${transaction_state_file}" >&2
    return 1
  fi

  local state_mode
  local state_owner
  state_mode="$(stat -c '%a' "$transaction_state_file")"
  state_owner="$(stat -c '%u' "$transaction_state_file")"
  if [[ "$state_mode" != "600" || "$state_owner" != "$EUID" ]]; then
    echo "배포 트랜잭션 상태 파일의 소유자 또는 권한이 안전하지 않습니다." >&2
    return 1
  fi

  # 배포 계정이 0600으로 직접 생성한 상태 파일만 읽습니다.
  # shellcheck disable=SC1090
  source "$transaction_state_file"

  if [[ "${TRANSACTION_VERSION:-}" != "1" \
    && "${TRANSACTION_VERSION:-}" != "2" \
    && "${TRANSACTION_VERSION:-}" != "3" ]]; then
    echo "지원하지 않는 배포 트랜잭션 버전입니다." >&2
    return 1
  fi
  if [[ -z "${CANDIDATE_APP_IMAGE:-}" || -z "${TRANSACTION_APP_DOMAIN:-}" ]]; then
    echo "배포 트랜잭션 상태 파일이 올바르지 않습니다." >&2
    return 1
  fi

  APP_DOMAIN="$TRANSACTION_APP_DOMAIN"
  APP_SCHEME="${TRANSACTION_APP_SCHEME:-https}"
  FRONTEND_SUBNET="${TRANSACTION_FRONTEND_SUBNET:-172.29.0.0/24}"
  if [[ "${TRANSACTION_VERSION:-}" == "3" ]]; then
    if ! validate_staging_directory_path "${TRANSACTION_STAGING_DIRECTORY:-}"; then
      echo "트랜잭션의 스테이징 경로가 안전하지 않습니다." >&2
      return 1
    fi
    if [[ -n "$requested_staged_deployment_directory" \
      && "$requested_staged_deployment_directory" != "$TRANSACTION_STAGING_DIRECTORY" ]]; then
      echo "요청된 스테이징 경로가 트랜잭션 기록과 다릅니다." >&2
      return 1
    fi
    staged_deployment_directory="$TRANSACTION_STAGING_DIRECTORY"
  fi
  export APP_DOMAIN APP_SCHEME FRONTEND_SUBNET
}

configuration_snapshot_exists() {
  [[ -d "$configuration_snapshot_directory" ]]
}

clear_directory_contents() {
  local target_directory="$1"

  if [[ -L "$target_directory" || ! -d "$target_directory" ]]; then
    echo "정리 대상이 안전한 디렉터리가 아닙니다: ${target_directory}" >&2
    return 1
  fi

  find "$target_directory" -mindepth 1 -depth -type f -delete
  find "$target_directory" -mindepth 1 -depth -type l -delete
  find "$target_directory" -mindepth 1 -depth -type d -empty -delete
}

copy_managed_deployment() {
  local source_directory="$1"
  local target_directory="$2"
  local entry
  local entry_name
  local -a entries

  mkdir -p "$target_directory"
  shopt -s dotglob nullglob
  entries=("$source_directory"/*)
  shopt -u dotglob nullglob

  for entry in "${entries[@]}"; do
    entry_name="$(basename -- "$entry")"
    if [[ "$entry_name" == "backups" ]]; then
      continue
    fi
    cp -a -- "$entry" "$target_directory/"
  done
}

clear_managed_deployment() {
  local entry
  local entry_name
  local -a entries

  if [[ ! -e deployment && ! -L deployment ]]; then
    return 0
  fi
  if [[ -L deployment || ! -d deployment ]]; then
    echo "deployment 대상 경로가 실제 디렉터리가 아닙니다." >&2
    return 1
  fi
  if [[ "$(stat -c '%u' deployment)" != "$EUID" ]]; then
    echo "deployment 대상 경로의 소유자가 현재 배포 계정과 다릅니다." >&2
    return 1
  fi
  validate_backups_directory

  shopt -s dotglob nullglob
  entries=(deployment/*)
  shopt -u dotglob nullglob

  for entry in "${entries[@]}"; do
    entry_name="$(basename -- "$entry")"
    if [[ "$entry_name" == "backups" ]]; then
      continue
    fi
    if [[ -d "$entry" && ! -L "$entry" ]]; then
      clear_directory_contents "$entry"
      rmdir "$entry"
    else
      rm -f -- "$entry"
    fi
  done
}

validate_configuration_snapshot() {
  local snapshot_mode

  if [[ -L "$configuration_snapshot_directory" || ! -d "$configuration_snapshot_directory" ]]; then
    echo "배포 설정 스냅샷 경로가 실제 디렉터리가 아닙니다." >&2
    return 1
  fi
  if [[ "$(stat -c '%u' "$configuration_snapshot_directory")" != "$EUID" ]]; then
    echo "배포 설정 스냅샷의 소유자가 현재 배포 계정과 다릅니다." >&2
    return 1
  fi
  snapshot_mode="$(stat -c '%a' "$configuration_snapshot_directory")"
  if [[ "$snapshot_mode" != "700" ]]; then
    echo "배포 설정 스냅샷 권한은 0700이어야 합니다." >&2
    return 1
  fi
}

create_configuration_snapshot() {
  if [[ -L "$configuration_snapshot_directory" \
    || ( -e "$configuration_snapshot_directory" && ! -d "$configuration_snapshot_directory" ) ]]; then
    echo "배포 설정 스냅샷 경로가 안전한 디렉터리가 아닙니다." >&2
    return 1
  fi
  if configuration_snapshot_exists; then
    validate_configuration_snapshot
    if [[ ! -f "${configuration_snapshot_directory}/confirmed" ]]; then
      echo "미확정 배포 설정 스냅샷이 남아 있습니다." >&2
      return 1
    fi
    remove_configuration_snapshot
  fi

  umask 077
  configuration_snapshot_pending_directory="$(mktemp -d "${configuration_snapshot_directory}.pending.XXXXXX")"
  chmod 700 "$configuration_snapshot_pending_directory"
  printf '%s\n' "$staged_deployment_directory" \
    > "${configuration_snapshot_pending_directory}/staging-directory"
  chmod 600 "${configuration_snapshot_pending_directory}/staging-directory"

  if [[ -f compose.yaml && ! -L compose.yaml ]]; then
    install -m 600 compose.yaml "${configuration_snapshot_pending_directory}/compose.yaml"
    touch "${configuration_snapshot_pending_directory}/compose.yaml.present"
  elif [[ -e compose.yaml || -L compose.yaml ]]; then
    echo "기존 compose.yaml이 일반 파일이 아닙니다." >&2
    return 1
  fi

  if [[ -f deployment.env && ! -L deployment.env ]]; then
    install -m 600 deployment.env "${configuration_snapshot_pending_directory}/deployment.env"
    touch "${configuration_snapshot_pending_directory}/deployment.env.present"
  elif [[ -e deployment.env || -L deployment.env ]]; then
    echo "기존 deployment.env가 일반 파일이 아닙니다." >&2
    return 1
  fi

  if [[ -d deployment && ! -L deployment ]]; then
    if [[ "$(stat -c '%u' deployment)" != "$EUID" ]]; then
      echo "기존 deployment 디렉터리의 소유자가 현재 배포 계정과 다릅니다." >&2
      return 1
    fi
    validate_backups_directory
    mkdir -m 700 "${configuration_snapshot_pending_directory}/deployment"
    copy_managed_deployment deployment "${configuration_snapshot_pending_directory}/deployment"
    touch "${configuration_snapshot_pending_directory}/deployment.present"
  elif [[ -e deployment || -L deployment ]]; then
    echo "기존 deployment 경로가 실제 디렉터리가 아닙니다." >&2
    return 1
  fi

  mv "$configuration_snapshot_pending_directory" "$configuration_snapshot_directory"
  configuration_snapshot_pending_directory=""
  configuration_snapshot_created_by_process="true"
}

promote_staged_deployment() {
  install -m 600 "${staged_deployment_directory}/compose.yaml" compose.yaml

  if [[ ! -e deployment && ! -L deployment ]]; then
    mkdir -m 700 deployment
  fi
  clear_managed_deployment
  copy_managed_deployment "${staged_deployment_directory}/deployment" deployment
  validate_backups_directory
}

restore_configuration_snapshot() {
  if ! configuration_snapshot_exists; then
    return 0
  fi

  validate_configuration_snapshot

  if [[ -f "${configuration_snapshot_directory}/compose.yaml.present" ]]; then
    install -m 600 "${configuration_snapshot_directory}/compose.yaml" compose.yaml
  fi

  if [[ -f "${configuration_snapshot_directory}/deployment.present" ]]; then
    if [[ ! -d "${configuration_snapshot_directory}/deployment" ]]; then
      echo "배포 디렉터리 스냅샷이 손상되었습니다." >&2
      return 1
    fi
    if [[ ! -e deployment && ! -L deployment ]]; then
      mkdir -m 700 deployment
    fi
    clear_managed_deployment
    copy_managed_deployment "${configuration_snapshot_directory}/deployment" deployment
  elif [[ -f "${configuration_snapshot_directory}/caddyfile.present" ]]; then
    # 버전 1 트랜잭션 스냅샷과의 수동 복구 호환성입니다.
    mkdir -p deployment/caddy
    install -m 600 \
      "${configuration_snapshot_directory}/Caddyfile" \
      deployment/caddy/Caddyfile
  fi

  if [[ -f "${configuration_snapshot_directory}/deployment.env.present" ]]; then
    install -m 600 \
      "${configuration_snapshot_directory}/deployment.env" \
      deployment.env
  fi
}

remove_configuration_snapshot() {
  if ! configuration_snapshot_exists; then
    return 0
  fi

  validate_configuration_snapshot
  find "$configuration_snapshot_directory" -depth -type f -delete
  find "$configuration_snapshot_directory" -depth -type l -delete
  find "$configuration_snapshot_directory" -depth -type d -empty -delete
}

remove_staged_deployment() {
  if [[ -z "$staged_deployment_directory" ]]; then
    return 0
  fi
  if ! validate_staging_directory_path "$staged_deployment_directory"; then
    echo "정리할 스테이징 경로가 안전하지 않습니다: ${staged_deployment_directory}" >&2
    return 1
  fi
  if [[ ! -e "$staged_deployment_directory" && ! -L "$staged_deployment_directory" ]]; then
    return 0
  fi
  if [[ -L "$staged_deployment_directory" || ! -d "$staged_deployment_directory" ]]; then
    echo "정리할 스테이징 경로가 실제 디렉터리가 아닙니다." >&2
    return 1
  fi
  if [[ "$(stat -c '%u' "$staged_deployment_directory")" != "$EUID" ]]; then
    echo "정리할 스테이징 경로의 소유자가 현재 배포 계정과 다릅니다." >&2
    return 1
  fi

  find "$staged_deployment_directory" -depth -type f -delete
  find "$staged_deployment_directory" -depth -type l -delete
  find "$staged_deployment_directory" -depth -type d -empty -delete
}

finalize_configuration_rollback() {
  if ! configuration_snapshot_exists; then
    return 0
  fi

  if [[ ! -f "${configuration_snapshot_directory}/compose.yaml.present" ]]; then
    rm -f compose.yaml
  fi
  if [[ ! -f "${configuration_snapshot_directory}/deployment.present" ]]; then
    if [[ -f "${configuration_snapshot_directory}/caddyfile.present" ]]; then
      :
    elif [[ -d deployment && ! -L deployment ]]; then
      clear_managed_deployment
      rmdir deployment 2>/dev/null || true
    fi
  fi
  if [[ ! -f "${configuration_snapshot_directory}/deployment.env.present" ]]; then
    rm -f deployment.env
  fi

  remove_configuration_snapshot
}

migrate_frontend_network_if_needed() {
  local previous_frontend_subnet="$1"

  if [[ -z "$previous_frontend_subnet" || "$previous_frontend_subnet" == "$FRONTEND_SUBNET" ]]; then
    return 0
  fi

  echo "frontend 네트워크 CIDR을 ${previous_frontend_subnet}에서 ${FRONTEND_SUBNET}(으)로 재생성합니다."

  local container_name
  for container_name in game-caddy game-server; do
    if container_exists "$container_name"; then
      docker container stop "$container_name" >/dev/null
      docker network disconnect --force "$frontend_network_name" "$container_name" >/dev/null 2>&1 || true
    fi
  done

  docker network rm "$frontend_network_name" >/dev/null
}

restore_previous_frontend_network() {
  if [[ -z "${PREVIOUS_FRONTEND_SUBNET:-}" ]]; then
    return 0
  fi

  if [[ "${FRONTEND_NETWORK_MIGRATION_REQUIRED:-false}" == "true" ]]; then
    local container_name
    local -a attached_containers

    if docker network inspect "$frontend_network_name" >/dev/null 2>&1; then
      mapfile -t attached_containers < <(
        docker network inspect \
          --format '{{range .Containers}}{{println .Name}}{{end}}' \
          "$frontend_network_name"
      )
      for container_name in "${attached_containers[@]}"; do
        case "$container_name" in
          ""|game-server|game-caddy)
            ;;
          *)
            echo "frontend 롤백을 막는 알 수 없는 컨테이너가 있습니다: ${container_name}" >&2
            return 1
            ;;
        esac
      done

      for container_name in game-caddy game-server; do
        if container_exists "$container_name"; then
          docker container stop "$container_name" >/dev/null
          docker network disconnect --force "$frontend_network_name" "$container_name" >/dev/null 2>&1 || true
        fi
      done
      docker network rm "$frontend_network_name" >/dev/null
    fi
  elif docker network inspect "$frontend_network_name" >/dev/null 2>&1; then
    return 0
  fi

  echo "이전 frontend 네트워크를 복구합니다: ${PREVIOUS_FRONTEND_SUBNET}" >&2
  docker network create \
    --driver bridge \
    --subnet "$PREVIOUS_FRONTEND_SUBNET" \
    --label com.docker.compose.network=frontend \
    --label com.docker.compose.project=zombie-survival-server \
    "$frontend_network_name" >/dev/null

  if [[ "${FRONTEND_NETWORK_MIGRATION_REQUIRED:-false}" == "true" ]]; then
    FRONTEND_SUBNET="$PREVIOUS_FRONTEND_SUBNET"
    export FRONTEND_SUBNET
  fi
}

verify_local_https_proxy() {
  local http_url="http://${APP_DOMAIN}/live"
  local live_url="https://${APP_DOMAIN}/live"
  local health_url="https://${APP_DOMAIN}/health"
  local health_slash_url="https://${APP_DOMAIN}/health/"
  local redirect_status
  local redirect_target
  local health_status
  local health_slash_status
  local blocked_health_path
  local blocked_health_status

  redirect_status="$(curl \
    --silent \
    --show-error \
    --connect-timeout 5 \
    --max-time 15 \
    --output /dev/null \
    --write-out '%{http_code}' \
    --resolve "${APP_DOMAIN}:80:127.0.0.1" \
    "$http_url")"
  redirect_target="$(curl \
    --silent \
    --show-error \
    --connect-timeout 5 \
    --max-time 15 \
    --output /dev/null \
    --write-out '%{redirect_url}' \
    --resolve "${APP_DOMAIN}:80:127.0.0.1" \
    "$http_url")"

  if [[ "$redirect_status" != "308" || "$redirect_target" != "$live_url" ]]; then
    echo "HTTP /live가 표준 HTTPS 주소로 리다이렉트되지 않았습니다: ${redirect_status} ${redirect_target}" >&2
    return 1
  fi

  if ! curl \
    --fail \
    --silent \
    --show-error \
    --retry 12 \
    --retry-all-errors \
    --retry-delay 5 \
    --connect-timeout 5 \
    --max-time 15 \
    --resolve "${APP_DOMAIN}:443:127.0.0.1" \
    "$live_url" >/dev/null; then
    echo "로컬 HTTPS 프록시의 /live 검증에 실패했습니다." >&2
    return 1
  fi

  health_status="$(curl \
    --silent \
    --show-error \
    --connect-timeout 5 \
    --max-time 15 \
    --output /dev/null \
    --write-out '%{http_code}' \
    --resolve "${APP_DOMAIN}:443:127.0.0.1" \
    "$health_url")"

  if [[ "$health_status" != "404" ]]; then
    echo "공개 /health 경로가 차단되지 않았습니다. HTTP 상태: ${health_status}" >&2
    return 1
  fi

  health_slash_status="$(curl \
    --silent \
    --show-error \
    --connect-timeout 5 \
    --max-time 15 \
    --output /dev/null \
    --write-out '%{http_code}' \
    --resolve "${APP_DOMAIN}:443:127.0.0.1" \
    "$health_slash_url")"

  if [[ "$health_slash_status" != "404" ]]; then
    echo "공개 /health/ 경로가 차단되지 않았습니다. HTTP 상태: ${health_slash_status}" >&2
    return 1
  fi

  for blocked_health_path in /Health /HEALTH/ /health%2F; do
    blocked_health_status="$(curl \
      --silent \
      --show-error \
      --connect-timeout 5 \
      --max-time 15 \
      --output /dev/null \
      --write-out '%{http_code}' \
      --resolve "${APP_DOMAIN}:443:127.0.0.1" \
      "https://${APP_DOMAIN}${blocked_health_path}")"
    if [[ "$blocked_health_status" != "404" ]]; then
      echo "공개 ${blocked_health_path} 경로가 차단되지 않았습니다. HTTP 상태: ${blocked_health_status}" >&2
      return 1
    fi
  done
}

rollback_deployment() {
  local rollback_failed="false"
  local rollback_backup_image
  local rollback_app_image

  if [[ ! -f "$transaction_state_file" ]]; then
    if configuration_snapshot_exists && [[ ! -f "${configuration_snapshot_directory}/confirmed" ]]; then
      local snapshot_staging_directory=""
      if [[ -f "${configuration_snapshot_directory}/staging-directory" ]]; then
        snapshot_staging_directory="$(<"${configuration_snapshot_directory}/staging-directory")"
      fi
      if [[ -n "$requested_staged_deployment_directory" \
        && "$requested_staged_deployment_directory" != "$snapshot_staging_directory" ]]; then
        echo "요청된 스테이징 경로가 미확정 설정 스냅샷과 다릅니다." >&2
        return 1
      fi
      if [[ -n "$snapshot_staging_directory" ]]; then
        if ! validate_staging_directory_path "$snapshot_staging_directory"; then
          echo "설정 스냅샷의 스테이징 경로가 안전하지 않습니다." >&2
          return 1
        fi
        staged_deployment_directory="$snapshot_staging_directory"
      fi
      echo "앱 교체 전 실패한 배포 설정을 복구합니다." >&2
      restore_configuration_snapshot
      finalize_configuration_rollback
      remove_staged_deployment
      echo "이전 배포 설정을 복구했습니다." >&2
      return 0
    fi

    remove_staged_deployment
    echo "보류 중인 배포 트랜잭션이 없어 추가 롤백 없이 종료합니다." >&2
    return 0
  fi

  load_transaction_state

  rollback_backup_image="${PREVIOUS_BACKUP_IMAGE:-${ACTIVE_BACKUP_IMAGE:-${CANDIDATE_BACKUP_IMAGE:-}}}"
  rollback_app_image="${PREVIOUS_APP_IMAGE:-${CANDIDATE_APP_IMAGE}}"

  if ! restore_configuration_snapshot; then
    rollback_failed="true"
  fi

  if ! restore_previous_frontend_network; then
    rollback_failed="true"
  fi

  if [[ "${PREVIOUS_APP_PRESENT:-false}" == "true" && -n "${PREVIOUS_APP_IMAGE:-}" ]]; then
    echo "이전 앱 이미지로 롤백합니다: ${PREVIOUS_APP_IMAGE}" >&2
    if ! APP_IMAGE="$PREVIOUS_APP_IMAGE" BACKUP_IMAGE="$rollback_backup_image" "${compose[@]}" up \
      -d --no-deps --force-recreate app; then
      rollback_failed="true"
    elif ! wait_for_health game-server 90; then
      docker logs --tail 200 game-server || true
      rollback_failed="true"
    fi
  else
    echo "이전 앱이 없으므로 후보 앱 컨테이너를 제거합니다." >&2
    docker container remove --force game-server >/dev/null 2>&1 || true
  fi

  if [[ "$rollback_failed" == "false" && "${PREVIOUS_CADDY_PRESENT:-false}" == "true" ]]; then
    if ! APP_IMAGE="$rollback_app_image" BACKUP_IMAGE="$rollback_backup_image" "${compose[@]}" up \
      -d --no-deps --force-recreate caddy; then
      rollback_failed="true"
    elif [[ "${PREVIOUS_APP_PRESENT:-false}" == "true" ]] && ! wait_for_health game-caddy 60; then
      docker logs --tail 200 game-caddy || true
      rollback_failed="true"
    elif [[ "${PREVIOUS_APP_PRESENT:-false}" != "true" ]] && ! wait_for_running game-caddy 30; then
      docker logs --tail 200 game-caddy || true
      rollback_failed="true"
    fi
  elif [[ "${PREVIOUS_CADDY_PRESENT:-false}" != "true" ]]; then
    docker container remove --force game-caddy >/dev/null 2>&1 || true
  fi

  if [[ "$rollback_failed" == "false" && "${PREVIOUS_BACKUP_PRESENT:-false}" == "true" && -n "${PREVIOUS_BACKUP_IMAGE:-}" ]]; then
    echo "이전 백업 이미지로 롤백합니다: ${PREVIOUS_BACKUP_IMAGE}" >&2
    if ! APP_IMAGE="$rollback_app_image" BACKUP_IMAGE="$PREVIOUS_BACKUP_IMAGE" "${compose[@]}" up \
      -d --no-deps --force-recreate backup; then
      rollback_failed="true"
    elif ! wait_for_running game-mysql-backup 30; then
      docker logs --tail 200 game-mysql-backup || true
      rollback_failed="true"
    fi
  elif [[ "${PREVIOUS_BACKUP_PRESENT:-false}" != "true" ]]; then
    docker container remove --force game-mysql-backup >/dev/null 2>&1 || true
  fi

  if [[ "$rollback_failed" == "true" ]]; then
    echo "이전 배포를 완전히 복구하지 못했습니다. 상태 파일을 유지합니다: ${transaction_state_file}" >&2
    return 1
  fi

  if ! finalize_configuration_rollback; then
    echo "이전 배포 설정 스냅샷을 정리하지 못했습니다. 상태 파일을 유지합니다." >&2
    return 1
  fi
  if ! remove_staged_deployment; then
    echo "스테이징 배포 파일을 정리하지 못했습니다. 상태 파일을 유지합니다." >&2
    return 1
  fi
  rm -f "$transaction_state_file"
  echo "이전 앱 배포로 복구했습니다." >&2
}

handle_prepare_failure() {
  local failure_status=$?
  trap - ERR

  echo "배포 준비 중 오류가 발생했습니다. 공통 롤백 경로를 실행합니다." >&2
  if [[ "$transaction_started_by_process" == "true" \
    || "$configuration_snapshot_created_by_process" == "true" ]]; then
    set +e
    rollback_deployment
    local rollback_status=$?
    set -e
    if (( rollback_status != 0 )); then
      echo "자동 롤백에 실패했습니다. 수동 복구가 필요합니다." >&2
    fi
  fi

  exit "$failure_status"
}

prepare_deployment() {
  local variable_name
  local required_variables=(DEPLOY_APP_IMAGE DEPLOY_BACKUP_IMAGE GHCR_USERNAME GHCR_PAT)
  for variable_name in "${required_variables[@]}"; do
    if [[ -z "${!variable_name:-}" ]]; then
      echo "필수 배포 변수가 비어 있습니다: ${variable_name}" >&2
      return 1
    fi
  done

  acquire_deployment_lock

  validate_staging_directory

  if ! command -v docker >/dev/null 2>&1 || ! command -v curl >/dev/null 2>&1; then
    echo "운영 배포에는 docker와 curl 명령이 필요합니다." >&2
    return 1
  fi

  if [[ -e "$transaction_state_file" ]]; then
    echo "확정 또는 롤백되지 않은 배포 트랜잭션이 있습니다: ${transaction_state_file}" >&2
    return 1
  fi

  create_configuration_snapshot
  promote_staged_deployment
  load_and_validate_proxy_settings

  APP_IMAGE="$DEPLOY_APP_IMAGE" BACKUP_IMAGE="$DEPLOY_BACKUP_IMAGE" "${compose[@]}" config --quiet

  docker_config_directory="$(mktemp -d)"
  chmod 700 "$docker_config_directory"
  printf '%s' "$GHCR_PAT" | DOCKER_CONFIG="$docker_config_directory" docker login \
    ghcr.io --username "$GHCR_USERNAME" --password-stdin
  DOCKER_CONFIG="$docker_config_directory" docker pull "$DEPLOY_APP_IMAGE"
  DOCKER_CONFIG="$docker_config_directory" APP_IMAGE="$DEPLOY_APP_IMAGE" BACKUP_IMAGE="$DEPLOY_BACKUP_IMAGE" \
    "${compose[@]}" pull caddy

  local backup_enabled
  backup_enabled="$(read_env_value BACKUP_ENABLED)"
  backup_enabled="${backup_enabled,,}"
  backup_enabled="${backup_enabled:-false}"

  if [[ "$backup_enabled" == "true" ]]; then
    DOCKER_CONFIG="$docker_config_directory" docker pull "$DEPLOY_BACKUP_IMAGE"
  fi
  unset GHCR_PAT

  if [[ "$backup_enabled" == "true" ]]; then
    echo "기존 앱을 유지한 채 S3 쓰기 권한을 검증합니다."
    BACKUP_IMAGE="$DEPLOY_BACKUP_IMAGE" "${compose[@]}" run \
      --rm \
      --no-deps \
      --entrypoint /usr/local/bin/s3-write-check.sh \
      backup
  else
    echo "S3 백업이 비활성화되어 백업 이미지와 권한 검사를 건너뜁니다."
  fi

  if container_exists game-mysql; then
    docker start game-mysql >/dev/null
  else
    APP_IMAGE="$DEPLOY_APP_IMAGE" BACKUP_IMAGE="$DEPLOY_BACKUP_IMAGE" "${compose[@]}" up -d mysql
  fi

  if ! wait_for_health game-mysql 120; then
    docker logs --tail 200 game-mysql || true
    echo "MySQL이 제한 시간 안에 healthy 상태가 되지 않았습니다." >&2
    return 1
  fi

  echo "EF Migration 적용 가능 여부를 확인합니다."
  APP_ENV_FILE="$app_env_file" bash deployment/scripts/check-migration-readiness.sh

  local previous_app_image
  local previous_app_present="false"
  local previous_caddy_present="false"
  local previous_backup_image
  local previous_backup_present="false"
  local previous_frontend_subnet
  local frontend_migration_required="false"
  local active_backup_image="$DEPLOY_BACKUP_IMAGE"

  previous_app_image="$(docker inspect -f '{{.Config.Image}}' game-server 2>/dev/null || true)"
  if [[ -n "$previous_app_image" ]]; then
    previous_app_present="true"
  fi
  if container_exists game-caddy; then
    previous_caddy_present="true"
  fi
  previous_backup_image="$(docker inspect -f '{{.Config.Image}}' game-mysql-backup 2>/dev/null || true)"
  if [[ -n "$previous_backup_image" ]]; then
    previous_backup_present="true"
  fi

  previous_frontend_subnet="$(current_frontend_subnet)"
  assert_frontend_network_can_be_migrated "$previous_frontend_subnet"
  if [[ -n "$previous_frontend_subnet" && "$previous_frontend_subnet" != "$FRONTEND_SUBNET" ]]; then
    frontend_migration_required="true"
  fi

  write_transaction_state \
    "$previous_app_image" \
    "$previous_app_present" \
    "$previous_caddy_present" \
    "$previous_backup_image" \
    "$previous_backup_present" \
    "$previous_frontend_subnet" \
    "$frontend_migration_required" \
    "$backup_enabled" \
    "$active_backup_image"
  transaction_started_by_process="true"

  if [[ "$backup_enabled" == "true" ]]; then
    APP_IMAGE="$DEPLOY_APP_IMAGE" BACKUP_IMAGE="$DEPLOY_BACKUP_IMAGE" "${compose[@]}" up \
      -d --no-deps --force-recreate backup
    if ! wait_for_running game-mysql-backup 30; then
      docker logs --tail 200 game-mysql-backup || true
      echo "백업 컨테이너가 30초 안에 running 상태가 되지 않았습니다." >&2
      return 1
    fi
    active_backup_image="$(docker inspect -f '{{.Config.Image}}' game-mysql-backup)"
    ACTIVE_BACKUP_IMAGE="$active_backup_image"
  elif container_exists game-mysql-backup; then
    echo "S3 백업이 비활성화되어 기존 백업 컨테이너를 제거합니다."
    docker container remove --force game-mysql-backup >/dev/null
  fi

  migrate_frontend_network_if_needed "$previous_frontend_subnet"

  local app_compose_service
  app_compose_service="$(docker inspect -f '{{index .Config.Labels "com.docker.compose.service"}}' game-server 2>/dev/null || true)"
  if [[ "$previous_app_present" == "true" && "$app_compose_service" != "app" ]]; then
    docker container remove --force game-server >/dev/null
  fi

  echo "새 앱 이미지를 배포합니다: ${DEPLOY_APP_IMAGE}"
  APP_IMAGE="$DEPLOY_APP_IMAGE" BACKUP_IMAGE="$DEPLOY_BACKUP_IMAGE" "${compose[@]}" up \
    -d --no-deps --force-recreate app

  if ! wait_for_health game-server 90; then
    docker logs --tail 200 game-server || true
    echo "새 앱이 90초 안에 healthy 상태가 되지 않았습니다." >&2
    return 1
  fi

  echo "HTTPS 역방향 프록시를 시작합니다."
  APP_IMAGE="$DEPLOY_APP_IMAGE" BACKUP_IMAGE="$DEPLOY_BACKUP_IMAGE" "${compose[@]}" up \
    -d --no-deps --force-recreate caddy

  if ! wait_for_health game-caddy 60; then
    docker logs --tail 200 game-caddy || true
    echo "Caddy 내부 프록시가 제한 시간 안에 healthy 상태가 되지 않았습니다." >&2
    return 1
  fi

  verify_local_https_proxy

  echo "로컬 배포 검증을 통과했습니다. 외부 HTTPS 검증 후 confirm을 실행하세요."
}

confirm_deployment() {
  acquire_deployment_lock
  validate_app_environment_file
  load_transaction_state

  local current_app_image
  current_app_image="$(docker inspect -f '{{.Config.Image}}' game-server 2>/dev/null || true)"
  if [[ "$current_app_image" != "$CANDIDATE_APP_IMAGE" ]]; then
    echo "실행 중인 앱이 확정 대상 이미지와 다릅니다." >&2
    return 1
  fi

  if ! wait_for_health game-server 10 || ! wait_for_health game-caddy 10; then
    echo "앱 또는 Caddy가 healthy 상태가 아니어서 배포를 확정할 수 없습니다." >&2
    return 1
  fi

  if [[ "${CANDIDATE_BACKUP_ENABLED:-false}" == "true" ]]; then
    local current_backup_image
    current_backup_image="$(docker inspect -f '{{.Config.Image}}' game-mysql-backup 2>/dev/null || true)"
    if [[ "$current_backup_image" != "$CANDIDATE_BACKUP_IMAGE" ]] || ! wait_for_running game-mysql-backup 10; then
      echo "백업 컨테이너가 확정 대상 이미지로 실행 중이지 않습니다." >&2
      return 1
    fi
  elif container_exists game-mysql-backup; then
    echo "백업 비활성화 배포인데 백업 컨테이너가 남아 있어 확정할 수 없습니다." >&2
    return 1
  fi

  umask 077
  local temporary_deployment_environment
  temporary_deployment_environment="$(mktemp .deployment.env.tmp.XXXXXX)"
  printf 'APP_IMAGE=%s\nBACKUP_IMAGE=%s\n' \
    "$CANDIDATE_APP_IMAGE" \
    "${ACTIVE_BACKUP_IMAGE:-$CANDIDATE_BACKUP_IMAGE}" \
    > "$temporary_deployment_environment"

  if configuration_snapshot_exists; then
    touch "${configuration_snapshot_directory}/confirmed"
    chmod 600 "${configuration_snapshot_directory}/confirmed"
  fi

  mv -f "$temporary_deployment_environment" deployment.env
  rm -f "$transaction_state_file"
  if ! remove_staged_deployment; then
    echo "배포는 확정되었지만 스테이징 파일 정리에 실패했습니다. 다음 배포 전에 다시 정리합니다." >&2
  fi
  echo "외부 검증이 완료된 배포를 확정했습니다."
}

run_rollback_mode() {
  acquire_deployment_lock
  validate_app_environment_file
  rollback_deployment
}

validate_deployment_root

case "$deployment_mode" in
  validate)
    load_and_validate_proxy_settings
    echo "운영 프록시 설정이 올바릅니다: https://${APP_DOMAIN}"
    ;;
  prepare)
    trap handle_prepare_failure ERR
    prepare_deployment
    trap - ERR
    ;;
  confirm)
    confirm_deployment
    ;;
  rollback)
    run_rollback_mode
    ;;
  *)
    usage
    exit 2
    ;;
esac
