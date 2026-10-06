#!/usr/bin/env bash
set -Eeuo pipefail

duckdns_domain="${DUCKDNS_DOMAIN:-zombie-survival-3d-game}"
config_root="${XDG_CONFIG_HOME:-${HOME}/.config}"
credentials_file="${DUCKDNS_CREDENTIALS_FILE:-${config_root}/duckdns/credentials}"
lock_file="${DUCKDNS_LOCK_FILE:-${config_root}/duckdns/update.lock}"

if [[ ! "$duckdns_domain" =~ ^[a-z0-9-]+$ ]]; then
  echo "DUCKDNS_DOMAIN은 DuckDNS 하위 도메인 이름만 사용할 수 있습니다." >&2
  exit 1
fi

if [[ -L "$credentials_file" || ! -f "$credentials_file" ]]; then
  echo "DuckDNS 자격 증명 파일이 없습니다: ${credentials_file}" >&2
  exit 1
fi

if ! command -v flock >/dev/null 2>&1; then
  echo "DuckDNS 중복 실행 방지를 위해 flock 명령이 필요합니다." >&2
  exit 1
fi

credentials_owner="$(stat -c '%u' "$credentials_file")"
if [[ "$credentials_owner" != "$EUID" ]]; then
  echo "DuckDNS 자격 증명 파일의 소유자가 현재 사용자와 다릅니다." >&2
  exit 1
fi

lock_directory="$(dirname -- "$lock_file")"
mkdir -p "$lock_directory"
if [[ -L "$lock_directory" || ! -d "$lock_directory" ]]; then
  echo "DuckDNS 잠금 디렉터리가 안전한 일반 디렉터리가 아닙니다." >&2
  exit 1
fi
lock_directory_owner="$(stat -c '%u' "$lock_directory")"
lock_directory_mode="$(stat -c '%a' "$lock_directory")"
if [[ "$lock_directory_owner" != "$EUID" \
  || ! "$lock_directory_mode" =~ ^[0-7]{3,4}$ \
  || $((8#$lock_directory_mode & 0022)) -ne 0 ]]; then
  echo "DuckDNS 잠금 디렉터리의 소유자 또는 권한이 안전하지 않습니다." >&2
  exit 1
fi
if [[ -L "$lock_file" || ( -e "$lock_file" && ! -f "$lock_file" ) ]]; then
  echo "DuckDNS 잠금 파일 경로가 안전한 일반 파일이 아닙니다." >&2
  exit 1
fi

umask 077
exec 9>"$lock_file"
chmod 600 "$lock_file"
if [[ "$(stat -c '%u' "$lock_file")" != "$EUID" ]]; then
  echo "DuckDNS 잠금 파일의 소유자가 현재 사용자와 다릅니다." >&2
  exit 1
fi
if ! flock -n 9; then
  echo "이전 DuckDNS 갱신 작업이 아직 실행 중이므로 이번 실행을 건너뜁니다."
  exit 0
fi

credentials_mode="$(stat -c '%a' "$credentials_file")"
if [[ "$credentials_mode" != "600" && "$credentials_mode" != "400" ]]; then
  echo "DuckDNS 자격 증명 파일 권한은 600 또는 400이어야 합니다." >&2
  exit 1
fi

duckdns_token="$(awk -F= '$1 == "DUCKDNS_TOKEN" { print $2 }' "$credentials_file" | tail -n 1)"
if [[ ! "$duckdns_token" =~ ^[A-Za-z0-9-]+$ ]]; then
  echo "DUCKDNS_TOKEN이 없거나 형식이 올바르지 않습니다." >&2
  exit 1
fi

response="$({
  printf 'url = "https://www.duckdns.org/update"\n'
  printf 'get\n'
  printf 'connect-timeout = 10\n'
  printf 'max-time = 30\n'
  printf 'data-urlencode = "domains=%s"\n' "$duckdns_domain"
  printf 'data-urlencode = "token=%s"\n' "$duckdns_token"
  printf 'data-urlencode = "ip="\n'
} | curl --disable --fail --silent --show-error --proto '=https' --config -)"

unset duckdns_token

if [[ "$response" != "OK" ]]; then
  echo "DuckDNS 갱신에 실패했습니다: ${response}" >&2
  exit 1
fi

echo "DuckDNS 주소를 갱신했습니다."
