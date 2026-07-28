#!/usr/bin/env bash

readonly INITIAL_MIGRATION_ID="20260720014315_InitialCreate"
readonly EF_PRODUCT_VERSION="9.0.18"
readonly MYSQL_CONTAINER_NAME="${MYSQL_CONTAINER_NAME:-game-mysql}"
readonly APP_ENV_FILE="${APP_ENV_FILE:-app.env}"

read_app_env_value() {
  local key="$1"
  local line

  line="$(grep -E "^${key}=" "$APP_ENV_FILE" | tail -n 1 || true)"
  printf '%s' "${line#*=}" | tr -d '\r'
}

load_database_settings() {
  if [[ ! -f "$APP_ENV_FILE" ]]; then
    echo "환경 파일을 찾을 수 없습니다: ${APP_ENV_FILE}" >&2
    return 1
  fi

  MYSQL_DATABASE_VALUE="$(read_app_env_value "MYSQL_DATABASE")"
  MYSQL_USER_VALUE="$(read_app_env_value "MYSQL_USER")"
  MYSQL_PASSWORD_VALUE="$(read_app_env_value "MYSQL_PASSWORD")"

  if [[ -z "$MYSQL_DATABASE_VALUE" || -z "$MYSQL_USER_VALUE" || -z "$MYSQL_PASSWORD_VALUE" ]]; then
    echo "MYSQL_DATABASE, MYSQL_USER, MYSQL_PASSWORD가 모두 필요합니다." >&2
    return 1
  fi

  if [[ ! "$MYSQL_DATABASE_VALUE" =~ ^[A-Za-z0-9_]+$ ]]; then
    echo "MYSQL_DATABASE는 영문, 숫자, 밑줄만 사용할 수 있습니다." >&2
    return 1
  fi

  if ! docker container inspect "$MYSQL_CONTAINER_NAME" >/dev/null 2>&1; then
    echo "MySQL 컨테이너를 찾을 수 없습니다: ${MYSQL_CONTAINER_NAME}" >&2
    return 1
  fi
}

mysql_exec() {
  docker exec \
    -e "MYSQL_PWD=${MYSQL_PASSWORD_VALUE}" \
    "$MYSQL_CONTAINER_NAME" \
    mysql \
    --protocol=tcp \
    --host=127.0.0.1 \
    --user="$MYSQL_USER_VALUE" \
    --database="$MYSQL_DATABASE_VALUE" \
    --batch \
    --skip-column-names \
    "$@"
}

mysql_exec_stdin() {
  docker exec \
    --interactive \
    -e "MYSQL_PWD=${MYSQL_PASSWORD_VALUE}" \
    "$MYSQL_CONTAINER_NAME" \
    mysql \
    --protocol=tcp \
    --host=127.0.0.1 \
    --user="$MYSQL_USER_VALUE" \
    --database="$MYSQL_DATABASE_VALUE" \
    --batch \
    --skip-column-names
}

count_application_tables() {
  mysql_exec --execute="
    SELECT COUNT(*)
    FROM information_schema.tables
    WHERE table_schema = DATABASE()
      AND table_type = 'BASE TABLE'
      AND table_name <> '__EFMigrationsHistory';"
}

has_initial_migration_history() {
  local history_table_count

  history_table_count="$(mysql_exec --execute="
    SELECT COUNT(*)
    FROM information_schema.tables
    WHERE table_schema = DATABASE()
      AND table_name = '__EFMigrationsHistory';")"

  if [[ "$history_table_count" != "1" ]]; then
    printf '0'
    return
  fi

  mysql_exec --execute="
    SELECT COUNT(*)
    FROM \`__EFMigrationsHistory\`
    WHERE \`MigrationId\` = '${INITIAL_MIGRATION_ID}';"
}
