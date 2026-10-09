#!/usr/bin/env bash
set -Eeuo pipefail

if [[ "${REDIS_FAILOVER_TEST:-}" != disposable ]]; then
  echo "이 시험은 disposable CI 환경에서만 실행합니다. REDIS_FAILOVER_TEST=disposable이 필요합니다." >&2
  exit 1
fi

compose=(docker compose --env-file "${APP_ENV_FILE:-app.env.example}")
key="redis-failover-smoke:$(cat /proc/sys/kernel/random/uuid)"

redis_command() {
  local service="$1"
  shift
  "${compose[@]}" exec -T "$service" sh -c 'export REDISCLI_AUTH="$REDIS_PASSWORD"; exec redis-cli --raw "$@"' sh "$@"
}

master_host() {
  redis_command redis-sentinel-1 -p 26379 SENTINEL get-master-addr-by-name game-primary | head -n 1 | tr -d '\r'
}

if [[ "$(master_host)" != redis-primary ]]; then
  echo "초기 primary가 redis-primary인 새 테스트 환경이 필요합니다." >&2
  exit 1
fi
trap '"${compose[@]}" start redis-primary >/dev/null' EXIT
[[ "$(redis_command redis-primary SET "$key" before EX 120)" == OK ]]

replicated=false
for ((attempt = 0; attempt < 30; attempt++)); do
  if [[ "$(redis_command redis-replica GET "$key")" == before ]]; then replicated=true; break; fi
  sleep 1
done
[[ "$replicated" == true ]]
"${compose[@]}" stop redis-primary

recovered=false
for ((attempt = 0; attempt < 60; attempt++)); do
  if [[ "$(master_host)" == redis-replica ]] && curl --fail --silent --max-time 5 http://127.0.0.1:5000/health >/dev/null; then
    recovered=true
    break
  fi
  sleep 1
done
if [[ "$recovered" != true ]]; then
  echo "Sentinel 승격 후 앱의 primary 연결이 복구되지 않았습니다." >&2
  exit 1
fi
[[ "$(redis_command redis-replica GET "$key")" == before ]]
[[ "$(redis_command redis-replica SET "$key" after EX 120)" == OK ]]
"${compose[@]}" start redis-primary

rejoined=false
for ((attempt = 0; attempt < 60; attempt++)); do
  role="$(redis_command redis-primary ROLE | head -n 1 | tr -d '\r')"
  if [[ "$role" == slave && "$(redis_command redis-primary GET "$key")" == after ]]; then rejoined=true; break; fi
  sleep 1
done
[[ "$rejoined" == true ]]
echo "Sentinel 승격, 앱 연결 복구, 진행 데이터 유지 및 이전 primary의 replica 재합류를 확인했습니다."
