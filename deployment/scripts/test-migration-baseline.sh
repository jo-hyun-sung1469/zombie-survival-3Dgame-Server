#!/usr/bin/env bash
set -Eeuo pipefail

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
test_suffix="$$"
network_name="migration-baseline-test-${test_suffix}"
mysql_container_name="migration-baseline-mysql-${test_suffix}"
app_container_name="migration-baseline-app-${test_suffix}"
mysql_database="migration_baseline_test"
mysql_user="migration_test_user"
mysql_password="migration_test_password"
mysql_root_password="migration_test_root_password"
temporary_directory="$(mktemp -d)"
environment_file="${temporary_directory}/app.env"

cleanup() {
  docker container rm --force "$app_container_name" >/dev/null 2>&1 || true
  docker container rm --force "$mysql_container_name" >/dev/null 2>&1 || true
  docker network rm "$network_name" >/dev/null 2>&1 || true
  find "$temporary_directory" -type f -delete 2>/dev/null || true
  find "$temporary_directory" -depth -type d -empty -delete 2>/dev/null || true
}
trap cleanup EXIT

wait_for_mysql() {
  local deadline=$((SECONDS + 120))
  while (( SECONDS < deadline )); do
    if docker exec \
      -e "MYSQL_PWD=${mysql_root_password}" \
      "$mysql_container_name" \
      mysqladmin --protocol=tcp --host=127.0.0.1 --user=root ping --silent; then
      return 0
    fi
    sleep 2
  done
  return 1
}

wait_for_app() {
  local deadline=$((SECONDS + 120))
  while (( SECONDS < deadline )); do
    local status
    status="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$app_container_name" 2>/dev/null || true)"
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

mysql_exec() {
  docker exec \
    -e "MYSQL_PWD=${mysql_password}" \
    "$mysql_container_name" \
    mysql \
    --protocol=tcp \
    --host=127.0.0.1 \
    --user="$mysql_user" \
    --database="$mysql_database" \
    --batch \
    --skip-column-names \
    "$@"
}

printf '%s\n' \
  "MYSQL_DATABASE=${mysql_database}" \
  "MYSQL_USER=${mysql_user}" \
  "MYSQL_PASSWORD=${mysql_password}" \
  > "$environment_file"

docker network create "$network_name" >/dev/null
docker run --detach \
  --name "$mysql_container_name" \
  --network "$network_name" \
  --network-alias mysql \
  -e "MYSQL_DATABASE=${mysql_database}" \
  -e "MYSQL_USER=${mysql_user}" \
  -e "MYSQL_PASSWORD=${mysql_password}" \
  -e "MYSQL_ROOT_PASSWORD=${mysql_root_password}" \
  mysql:8.4.10@sha256:c592c15aaf4a1961e15d82eb31ea5987dda862d1c4b1e93424438c0e91dc1f8d >/dev/null

if ! wait_for_mysql; then
  docker logs "$mysql_container_name" >&2 || true
  echo "통합 테스트 MySQL이 준비되지 않았습니다." >&2
  exit 1
fi

docker run --detach \
  --name "$app_container_name" \
  --network "$network_name" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e Database__Host=mysql \
  -e Database__Port=3306 \
  -e "Database__Name=${mysql_database}" \
  -e "Database__User=${mysql_user}" \
  -e "Database__Credential=${mysql_password}" \
  -e Database__SslMode=Disabled \
  -e Jwt__SecretKey=migration_test_jwt_secret_key_0123456789abcdef \
  -e SmtpEmail__Host=smtp.example.invalid \
  -e SmtpEmail__Port=587 \
  -e SmtpEmail__EnableSsl=true \
  -e SmtpEmail__UserName=migration-test \
  -e SmtpEmail__Password=migration-test-password \
  -e SmtpEmail__FromAddress=migration-test@example.invalid \
  -e ReverseProxy__KnownNetworkCidr=172.29.0.0/24 \
  zombie-survival-server:ci >/dev/null

if ! wait_for_app; then
  docker logs "$app_container_name" >&2 || true
  echo "통합 테스트 앱이 최초 Migration을 적용하지 못했습니다." >&2
  exit 1
fi

docker container rm --force "$app_container_name" >/dev/null
mysql_exec --execute="DELETE FROM \`__EFMigrationsHistory\`;"
mysql_exec --execute="
  ALTER TABLE \`PlayerSaveData\` DROP COLUMN \`Version\`;
  ALTER TABLE \`AuthVerificationCodes\` DROP COLUMN \`Version\`;"

if MYSQL_CONTAINER_NAME="$mysql_container_name" \
  APP_ENV_FILE="$environment_file" \
  bash "${script_directory}/check-migration-readiness.sh"; then
  echo "Migration 이력이 없는 기존 DB가 허용되었습니다." >&2
  exit 1
fi

mysql_exec --execute="DROP INDEX \`IX_Users_UserName\` ON \`Users\`;"
if MYSQL_CONTAINER_NAME="$mysql_container_name" \
  APP_ENV_FILE="$environment_file" \
  BASELINE_BACKUP_DIRECTORY="${temporary_directory}/backups" \
  bash "${script_directory}/baseline-existing-database.sh" --confirm-initial-baseline; then
  echo "불일치 스키마가 baseline 검증을 통과했습니다." >&2
  exit 1
fi

if [[ "$(mysql_exec --execute="
  SELECT COUNT(*)
  FROM \`__EFMigrationsHistory\`;")" != "0" ]]; then
  echo "검증 실패 후 Migration 이력이 추가되었습니다." >&2
  exit 1
fi

mysql_exec --execute="CREATE UNIQUE INDEX \`IX_Users_UserName\` ON \`Users\` (\`UserName\`);"
MYSQL_CONTAINER_NAME="$mysql_container_name" \
APP_ENV_FILE="$environment_file" \
BASELINE_BACKUP_DIRECTORY="${temporary_directory}/backups" \
bash "${script_directory}/baseline-existing-database.sh" --confirm-initial-baseline

MYSQL_CONTAINER_NAME="$mysql_container_name" \
APP_ENV_FILE="$environment_file" \
bash "${script_directory}/check-migration-readiness.sh"

history_count="$(mysql_exec --execute="
  SELECT COUNT(*)
  FROM \`__EFMigrationsHistory\`
  WHERE \`MigrationId\` = '20260720014315_InitialCreate';")"
if [[ "$history_count" != "1" ]]; then
  echo "baseline 완료 후 최초 Migration 이력이 없습니다." >&2
  exit 1
fi

docker run --detach \
  --name "$app_container_name" \
  --network "$network_name" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e Database__Host=mysql \
  -e Database__Port=3306 \
  -e "Database__Name=${mysql_database}" \
  -e "Database__User=${mysql_user}" \
  -e "Database__Credential=${mysql_password}" \
  -e Database__SslMode=Disabled \
  -e Jwt__SecretKey=migration_test_jwt_secret_key_0123456789abcdef \
  -e SmtpEmail__Host=smtp.example.invalid \
  -e SmtpEmail__Port=587 \
  -e SmtpEmail__EnableSsl=true \
  -e SmtpEmail__UserName=migration-test \
  -e SmtpEmail__Password=migration-test-password \
  -e SmtpEmail__FromAddress=migration-test@example.invalid \
  -e ReverseProxy__KnownNetworkCidr=172.29.0.0/24 \
  zombie-survival-server:ci >/dev/null

if ! wait_for_app; then
  docker logs "$app_container_name" >&2 || true
  echo "baseline 이후 후속 Migration을 적용하지 못했습니다." >&2
  exit 1
fi

concurrency_history_count="$(mysql_exec --execute="
  SELECT COUNT(*)
  FROM \`__EFMigrationsHistory\`
  WHERE \`MigrationId\` LIKE '%_AddConcurrencyVersions';")"
if [[ "$concurrency_history_count" != "1" ]]; then
  echo "동시성 Migration 이력이 없습니다." >&2
  exit 1
fi

version_column_count="$(mysql_exec --execute="
  SELECT COUNT(*)
  FROM information_schema.columns
  WHERE table_schema = DATABASE()
    AND column_name = 'Version'
    AND table_name IN ('PlayerSaveData', 'AuthVerificationCodes');")"
if [[ "$version_column_count" != "2" ]]; then
  echo "동시성 Version 컬럼이 모두 생성되지 않았습니다." >&2
  exit 1
fi

backup_count="$(find "${temporary_directory}/backups" -maxdepth 1 -type f -name '*.sql.gz' | wc -l | tr -d ' ')"
if [[ "$backup_count" != "1" ]]; then
  echo "baseline 전에 생성된 dump 파일을 확인할 수 없습니다." >&2
  exit 1
fi

echo "Migration 배포 차단 및 baseline 통합 테스트가 통과했습니다."
