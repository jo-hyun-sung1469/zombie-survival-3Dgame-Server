#!/usr/bin/env bash
set -Eeuo pipefail

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_directory="$(cd "${script_directory}/../.." && pwd)"
validation_sql="${repository_directory}/deployment/migrations/validate-initial-schema.sql"
source "${script_directory}/migration-common.sh"

if [[ "${1:-}" != "--confirm-initial-baseline" || "$#" -ne 1 ]]; then
  cat >&2 <<EOF
이 명령은 기존 DB를 최초 Migration의 baseline으로 등록합니다.
실행하려면 다음 확인 인자를 정확히 지정하세요.

  bash deployment/scripts/baseline-existing-database.sh --confirm-initial-baseline
EOF
  exit 2
fi

load_database_settings

application_table_count="$(count_application_tables)"
if [[ "$application_table_count" == "0" ]]; then
  echo "빈 데이터베이스에는 baseline이 필요하지 않습니다." >&2
  exit 1
fi

if [[ "$(has_initial_migration_history)" == "1" ]]; then
  echo "최초 Migration이 이미 적용되어 있습니다."
  exit 0
fi

if [[ ! -f "$validation_sql" ]]; then
  echo "스키마 검증 SQL을 찾을 수 없습니다: ${validation_sql}" >&2
  exit 1
fi

schema_mismatches="$(mysql_exec_stdin < "$validation_sql")"
if [[ -n "$schema_mismatches" ]]; then
  echo "현재 DB 스키마가 최초 Migration과 일치하지 않아 baseline을 중단합니다." >&2
  printf '%s\n' "$schema_mismatches" >&2
  exit 1
fi

backup_directory="${BASELINE_BACKUP_DIRECTORY:-${repository_directory}/deployment/backups}"
backup_timestamp="$(date -u +'%Y%m%dT%H%M%SZ')"
backup_path="${backup_directory}/${MYSQL_DATABASE_VALUE}-before-baseline-${backup_timestamp}.sql.gz"
mkdir -p "$backup_directory"
umask 077

echo "baseline 전 전체 dump를 생성합니다: ${backup_path}"
docker exec \
  -e "MYSQL_PWD=${MYSQL_PASSWORD_VALUE}" \
  "$MYSQL_CONTAINER_NAME" \
  mysqldump \
  --protocol=tcp \
  --host=127.0.0.1 \
  --user="$MYSQL_USER_VALUE" \
  --single-transaction \
  --quick \
  --skip-lock-tables \
  --no-tablespaces \
  "$MYSQL_DATABASE_VALUE" \
  | gzip -9 > "$backup_path"

if [[ ! -s "$backup_path" ]]; then
  echo "baseline 전 dump 생성에 실패했습니다." >&2
  exit 1
fi

mysql_exec --execute="
  CREATE TABLE IF NOT EXISTS \`__EFMigrationsHistory\` (
    \`MigrationId\` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    \`ProductVersion\` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT \`PK___EFMigrationsHistory\` PRIMARY KEY (\`MigrationId\`)
  ) CHARACTER SET=utf8mb4;

  INSERT INTO \`__EFMigrationsHistory\` (\`MigrationId\`, \`ProductVersion\`)
  VALUES ('${INITIAL_MIGRATION_ID}', '${EF_PRODUCT_VERSION}');"

bash "${script_directory}/check-migration-readiness.sh"
echo "기존 DB baseline 등록이 완료되었습니다."
