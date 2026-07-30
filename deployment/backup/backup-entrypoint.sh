#!/usr/bin/env bash
set -Eeuo pipefail

required_variables=(
  MYSQL_HOST
  MYSQL_PORT
  MYSQL_DATABASE
  MYSQL_USER
  MYSQL_PASSWORD
  MYSQL_BACKUP_S3_URI
  AWS_REGION
)

for variable_name in "${required_variables[@]}"; do
  if [[ -z "${!variable_name:-}" ]]; then
    echo "필수 환경 변수가 비어 있습니다: ${variable_name}" >&2
    exit 1
  fi
done

backup_interval="${BACKUP_INTERVAL_SECONDS:-86400}"
if ! [[ "$backup_interval" =~ ^[1-9][0-9]*$ ]]; then
  echo "BACKUP_INTERVAL_SECONDS는 양의 정수여야 합니다." >&2
  exit 1
fi

umask 077
mkdir -p /backups

while true; do
  find /backups -type f -name '*.sql.gz' -mmin +2880 -delete

  timestamp="$(date -u +'%Y%m%dT%H%M%SZ')"
  backup_name="${MYSQL_DATABASE}-${timestamp}.sql.gz"
  backup_path="/backups/${backup_name}"
  destination="${MYSQL_BACKUP_S3_URI%/}/${MYSQL_DATABASE}/${backup_name}"

  echo "MySQL 백업을 생성합니다: ${backup_name}"
  MYSQL_PWD="$MYSQL_PASSWORD" mysqldump \
    --host="$MYSQL_HOST" \
    --port="$MYSQL_PORT" \
    --user="$MYSQL_USER" \
    --single-transaction \
    --quick \
    --routines \
    --triggers \
    --events \
    --set-gtid-purged=OFF \
    --no-tablespaces \
    "$MYSQL_DATABASE" \
    | gzip -9 > "$backup_path"

  aws s3 cp "$backup_path" "$destination" --only-show-errors
  echo "S3 업로드가 완료되었습니다: ${destination}"
  sleep "$backup_interval"
done
