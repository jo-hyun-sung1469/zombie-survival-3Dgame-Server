#!/usr/bin/env bash
set -Eeuo pipefail

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
temporary_directory="$(mktemp -d)"
environment_file="${temporary_directory}/app.env"

cleanup() {
  find "$temporary_directory" -depth -type f -delete 2>/dev/null || true
  find "$temporary_directory" -depth -type d -empty -delete 2>/dev/null || true
}
trap cleanup EXIT

write_environment() {
  local domain="$1"
  local scheme="$2"
  local subnet="${3:-172.29.0.0/24}"

  printf '%s\n' \
    "APP_DOMAIN=${domain}" \
    "APP_SCHEME=${scheme}" \
    "FRONTEND_SUBNET=${subnet}" \
    > "$environment_file"
  chmod 600 "$environment_file"
}

run_validation() {
  env \
    -u APP_DOMAIN \
    -u APP_SCHEME \
    -u FRONTEND_SUBNET \
    APP_ENV_FILE="$environment_file" \
    bash "${script_directory}/deploy.sh" validate
}

write_environment zombie-survival-3d-game.duckdns.org https

environment_mode="$(stat -c '%a' "$environment_file")"
if [[ "$environment_mode" != "600" ]]; then
  case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*)
      echo "Windows 파일 시스템에서는 배포 환경 파일의 POSIX 권한 검증 테스트를 건너뜁니다."
      exit 0
      ;;
    *)
      echo "테스트 배포 환경 파일에 0600 권한을 적용하지 못했습니다." >&2
      exit 1
      ;;
  esac
fi

run_validation >/dev/null

chmod 644 "$environment_file"
if run_validation >/dev/null 2>&1; then
  echo "안전하지 않은 권한의 배포 환경 파일이 허용되었습니다." >&2
  exit 1
fi
chmod 600 "$environment_file"

environment_target="${temporary_directory}/app.env.target"
mv "$environment_file" "$environment_target"
ln -s "$environment_target" "$environment_file"
if run_validation >/dev/null 2>&1; then
  echo "심볼릭 링크인 배포 환경 파일이 허용되었습니다." >&2
  exit 1
fi
rm "$environment_file"
mv "$environment_target" "$environment_file"

if APP_DOMAIN=other.example.org \
  APP_ENV_FILE="$environment_file" \
  bash "${script_directory}/deploy.sh" validate >/dev/null 2>&1; then
  echo "GitHub와 app.env의 APP_DOMAIN 불일치가 허용되었습니다." >&2
  exit 1
fi

write_environment https://zombie-survival-3d-game.duckdns.org https
if run_validation >/dev/null 2>&1; then
  echo "스킴이 포함된 APP_DOMAIN이 허용되었습니다." >&2
  exit 1
fi

write_environment zombie-survival-3d-game.duckdns.org http
if run_validation >/dev/null 2>&1; then
  echo "운영 APP_SCHEME=http가 허용되었습니다." >&2
  exit 1
fi

write_environment localhost https
if run_validation >/dev/null 2>&1; then
  echo "공개 FQDN이 아닌 APP_DOMAIN이 허용되었습니다." >&2
  exit 1
fi

write_environment zombie-survival-3d-game.duckdns.org https invalid-subnet
if run_validation >/dev/null 2>&1; then
  echo "잘못된 FRONTEND_SUBNET이 허용되었습니다." >&2
  exit 1
fi

write_environment zombie-survival-3d-game.duckdns.org https 0.0.0.0/0
if run_validation >/dev/null 2>&1; then
  echo "공개 전체 대역 FRONTEND_SUBNET이 허용되었습니다." >&2
  exit 1
fi

write_environment zombie-survival-3d-game.duckdns.org https 172.29.0.0/29
run_validation >/dev/null

write_environment zombie-survival-3d-game.duckdns.org https 172.29.0.0/30
if run_validation >/dev/null 2>&1; then
  echo "컨테이너 주소가 부족한 /30 FRONTEND_SUBNET이 허용되었습니다." >&2
  exit 1
fi

write_environment zombie-survival-3d-game.duckdns.org https 172.29.0.1/29
if run_validation >/dev/null 2>&1; then
  echo "네트워크 주소로 정렬되지 않은 FRONTEND_SUBNET이 허용되었습니다." >&2
  exit 1
fi

echo "운영 배포 도메인과 프록시 설정 검증 테스트가 통과했습니다."
