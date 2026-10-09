#!/bin/sh
set -eu
: "${REDIS_PASSWORD:?REDIS_PASSWORD is required}"
case "$REDIS_PASSWORD" in *[!a-zA-Z0-9_-]*) echo 'Invalid Redis credential character.' >&2; exit 1;; esac
umask 077
config=/data/sentinel.conf
if [ ! -f "$config" ]; then
  {
    printf 'bind 0.0.0.0\nport 26379\ndir /data\nsentinel resolve-hostnames yes\nsentinel announce-hostnames yes\n'
    printf 'requirepass %s\nsentinel auth-pass game-primary %s\n' "$REDIS_PASSWORD" "$REDIS_PASSWORD"
    printf 'sentinel announce-ip %s\nsentinel announce-port 26379\n' "$SENTINEL_HOSTNAME"
    printf 'sentinel monitor game-primary redis-primary 6379 2\nsentinel down-after-milliseconds game-primary 5000\nsentinel failover-timeout game-primary 15000\nsentinel parallel-syncs game-primary 1\n'
  } > "$config"
fi
exec redis-server "$config" --sentinel
