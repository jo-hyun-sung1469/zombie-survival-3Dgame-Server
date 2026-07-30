#!/usr/bin/env bash
set -Eeuo pipefail

if [[ -z "${MYSQL_BACKUP_S3_URI:-}" || -z "${AWS_REGION:-}" ]]; then
  echo "MYSQL_BACKUP_S3_URI와 AWS_REGION이 필요합니다." >&2
  exit 1
fi

check_key="${MYSQL_BACKUP_S3_URI%/}/deployment-checks/$(date -u +'%Y%m%dT%H%M%SZ')-$$.txt"
printf 'zombie survival deployment write check\n' | aws s3 cp - "$check_key" --only-show-errors
aws s3 rm "$check_key" --only-show-errors
echo "S3 쓰기 권한 검증이 완료되었습니다."
