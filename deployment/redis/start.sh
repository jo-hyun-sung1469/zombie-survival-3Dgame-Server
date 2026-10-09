#!/bin/sh
set -eu
: "${REDIS_PASSWORD:?REDIS_PASSWORD is required}"
case "$REDIS_PASSWORD" in *[!a-zA-Z0-9_-]*) echo 'Invalid Redis credential character.' >&2; exit 1;; esac
umask 077
config=/data/redis.conf
if [ ! -f "$config" ]; then
  {
    printf 'bind 0.0.0.0\nprotected-mode yes\nport 6379\ndir /data\nappendonly yes\nappendfsync everysec\nmaxmemory 256mb\nmaxmemory-policy noeviction\n'
    printf 'requirepass %s\nmasterauth %s\n' "$REDIS_PASSWORD" "$REDIS_PASSWORD"
    printf 'replica-announce-ip %s\nreplica-announce-port 6379\n' "$REDIS_HOSTNAME"
    if [ "${REDIS_ROLE:-primary}" = replica ]; then printf 'replicaof redis-primary 6379\n'; fi
  } > "$config"
fi
exec redis-server "$config"
