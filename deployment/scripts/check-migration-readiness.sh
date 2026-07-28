#!/usr/bin/env bash
set -Eeuo pipefail

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${script_directory}/migration-common.sh"

load_database_settings

application_table_count="$(count_application_tables)"
if [[ "$application_table_count" == "0" ]]; then
  echo "빈 데이터베이스입니다. 앱 시작 시 최초 Migration을 적용합니다."
  exit 0
fi

initial_migration_applied="$(has_initial_migration_history)"
if [[ "$initial_migration_applied" == "1" ]]; then
  echo "최초 Migration 이력이 확인되었습니다."
  exit 0
fi

cat >&2 <<EOF
기존 테이블 ${application_table_count}개가 있지만 ${INITIAL_MIGRATION_ID} 이력이 없습니다.
EnsureCreated()로 생성된 DB일 수 있으므로 새 앱 배포를 중단합니다.
스키마 검증과 백업 후 다음 명령으로 baseline을 수행하세요.

  bash deployment/scripts/baseline-existing-database.sh --confirm-initial-baseline
EOF
exit 1
