#!/usr/bin/env bash
set -Eeuo pipefail

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_directory="$(cd "${script_directory}/../.." && pwd)"
temporary_directory="$(mktemp -d)"
credentials_file="${temporary_directory}/credentials"
fake_bin_directory="${temporary_directory}/bin"
curl_arguments_file="${temporary_directory}/curl-arguments"
curl_config_file="${temporary_directory}/curl-config"
lock_file="${temporary_directory}/update.lock"
test_token="duckdns-test-token-do-not-log"

cleanup() {
  find "$temporary_directory" -depth -type f -delete 2>/dev/null || true
  find "$temporary_directory" -depth -type d -empty -delete 2>/dev/null || true
}
trap cleanup EXIT

mkdir -p "$fake_bin_directory"
printf 'DUCKDNS_TOKEN=%s\n' "$test_token" > "$credentials_file"
chmod 600 "$credentials_file"

credentials_mode="$(stat -c '%a' "$credentials_file")"
if [[ "$credentials_mode" != "600" && "$credentials_mode" != "400" ]]; then
  case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*)
      echo "Windows 파일 시스템에서는 POSIX 권한 검증 테스트를 건너뜁니다."
      exit 0
      ;;
    *)
      echo "테스트 자격 증명 파일에 0600 권한을 적용하지 못했습니다." >&2
      exit 1
      ;;
  esac
fi

cat > "${fake_bin_directory}/curl" <<'EOF'
#!/usr/bin/env bash
set -Eeuo pipefail
printf '%s\n' "$@" > "$CURL_ARGUMENTS_FILE"
cat > "$CURL_CONFIG_FILE"
if [[ "${CURL_SHOULD_FAIL:-false}" == "true" ]]; then
  exit 22
fi
printf '%s' "${CURL_RESPONSE:-OK}"
EOF
chmod 700 "${fake_bin_directory}/curl"

cat > "${fake_bin_directory}/flock" <<'EOF'
#!/usr/bin/env bash
if [[ "${FLOCK_SHOULD_FAIL:-false}" == "true" ]]; then
  exit 1
fi
exit 0
EOF
chmod 700 "${fake_bin_directory}/flock"

PATH="${fake_bin_directory}:${PATH}" \
CURL_ARGUMENTS_FILE="$curl_arguments_file" \
CURL_CONFIG_FILE="$curl_config_file" \
DUCKDNS_CREDENTIALS_FILE="$credentials_file" \
DUCKDNS_LOCK_FILE="$lock_file" \
DUCKDNS_DOMAIN=zombie-survival-3d-game \
bash "${repository_directory}/deployment/duckdns/update.sh" >/dev/null

if grep -Fq "$test_token" "$curl_arguments_file"; then
  echo "DuckDNS 토큰이 curl 명령 인자에 노출되었습니다." >&2
  exit 1
fi

if [[ "$(sed -n '1p' "$curl_arguments_file")" != "--disable" ]]; then
  echo "DuckDNS updater가 사용자 curlrc를 비활성화하지 않았습니다." >&2
  exit 1
fi
if ! grep -Fxq -- '--proto' "$curl_arguments_file" \
  || ! grep -Fxq -- '=https' "$curl_arguments_file"; then
  echo "DuckDNS updater가 HTTPS 프로토콜만 허용하지 않았습니다." >&2
  exit 1
fi

if ! grep -Fq 'token=duckdns-test-token-do-not-log' "$curl_config_file"; then
  echo "DuckDNS updater가 토큰을 curl 표준 입력으로 전달하지 않았습니다." >&2
  exit 1
fi

if ! grep -Fq 'connect-timeout = 10' "$curl_config_file" \
  || ! grep -Fq 'max-time = 30' "$curl_config_file"; then
  echo "DuckDNS updater에 연결 및 전체 실행 제한 시간이 설정되지 않았습니다." >&2
  exit 1
fi

rm -f "$curl_arguments_file"
PATH="${fake_bin_directory}:${PATH}" \
FLOCK_SHOULD_FAIL=true \
CURL_ARGUMENTS_FILE="$curl_arguments_file" \
CURL_CONFIG_FILE="$curl_config_file" \
DUCKDNS_CREDENTIALS_FILE="$credentials_file" \
DUCKDNS_LOCK_FILE="$lock_file" \
DUCKDNS_DOMAIN=zombie-survival-3d-game \
bash "${repository_directory}/deployment/duckdns/update.sh" >/dev/null

if [[ -e "$curl_arguments_file" ]]; then
  echo "DuckDNS 잠금을 획득하지 못했는데도 curl이 실행되었습니다." >&2
  exit 1
fi

chmod 600 "$credentials_file"
failure_output="${temporary_directory}/failure-output"
if PATH="${fake_bin_directory}:${PATH}" \
  CURL_SHOULD_FAIL=true \
  CURL_ARGUMENTS_FILE="$curl_arguments_file" \
  CURL_CONFIG_FILE="$curl_config_file" \
  DUCKDNS_CREDENTIALS_FILE="$credentials_file" \
  DUCKDNS_LOCK_FILE="$lock_file" \
  DUCKDNS_DOMAIN=zombie-survival-3d-game \
  bash "${repository_directory}/deployment/duckdns/update.sh" >"$failure_output" 2>&1; then
  echo "curl 실패가 DuckDNS 갱신 성공으로 처리되었습니다." >&2
  exit 1
fi
if grep -Fq "$test_token" "$failure_output"; then
  echo "curl 실패 출력에 DuckDNS 토큰이 노출되었습니다." >&2
  exit 1
fi

if PATH="${fake_bin_directory}:${PATH}" \
  CURL_RESPONSE=KO \
  CURL_ARGUMENTS_FILE="$curl_arguments_file" \
  CURL_CONFIG_FILE="$curl_config_file" \
  DUCKDNS_CREDENTIALS_FILE="$credentials_file" \
  DUCKDNS_LOCK_FILE="$lock_file" \
  DUCKDNS_DOMAIN=zombie-survival-3d-game \
  bash "${repository_directory}/deployment/duckdns/update.sh" >"$failure_output" 2>&1; then
  echo "DuckDNS의 OK 외 응답이 성공으로 처리되었습니다." >&2
  exit 1
fi
if grep -Fq "$test_token" "$failure_output"; then
  echo "DuckDNS 실패 응답 출력에 토큰이 노출되었습니다." >&2
  exit 1
fi

credentials_target="${temporary_directory}/credentials-target"
mv "$credentials_file" "$credentials_target"
ln -s "$credentials_target" "$credentials_file"
if PATH="${fake_bin_directory}:${PATH}" \
  CURL_ARGUMENTS_FILE="$curl_arguments_file" \
  CURL_CONFIG_FILE="$curl_config_file" \
  DUCKDNS_CREDENTIALS_FILE="$credentials_file" \
  DUCKDNS_LOCK_FILE="$lock_file" \
  DUCKDNS_DOMAIN=zombie-survival-3d-game \
  bash "${repository_directory}/deployment/duckdns/update.sh" >/dev/null 2>&1; then
  echo "심볼릭 링크 DuckDNS 자격 증명 파일이 허용되었습니다." >&2
  exit 1
fi
rm -f "$credentials_file"
mv "$credentials_target" "$credentials_file"

chmod 644 "$credentials_file"
if PATH="${fake_bin_directory}:${PATH}" \
  CURL_ARGUMENTS_FILE="$curl_arguments_file" \
  CURL_CONFIG_FILE="$curl_config_file" \
  DUCKDNS_CREDENTIALS_FILE="$credentials_file" \
  DUCKDNS_LOCK_FILE="$lock_file" \
  DUCKDNS_DOMAIN=zombie-survival-3d-game \
  bash "${repository_directory}/deployment/duckdns/update.sh" >/dev/null 2>&1; then
  echo "안전하지 않은 DuckDNS 자격 증명 파일 권한이 허용되었습니다." >&2
  exit 1
fi

echo "DuckDNS updater 자격 증명 보호 테스트가 통과했습니다."
