#!/usr/bin/env sh
set -eu

COMPOSE_FILE="${COMPOSE_FILE:-compose.yaml}"
BASE_URL="${BASE_URL:-http://localhost:5292}"
COOKIE_JAR="$(mktemp)"
cleanup() {
  status=$?
  rm -f "$COOKIE_JAR"
  if [ "${KEEP_RUNNING:-0}" != "1" ]; then
    docker compose -f "$COMPOSE_FILE" down --remove-orphans >/dev/null 2>&1 || true
  fi
  return "$status"
}
trap cleanup EXIT

docker compose -f "$COMPOSE_FILE" up --build -d lotr-web

for attempt in $(seq 1 60); do
  if curl -fsS "$BASE_URL/Auth/Login" >/dev/null; then
    break
  fi

  if [ "$attempt" -eq 60 ]; then
    echo "web container did not serve /Auth/Login within 60 seconds" >&2
    docker compose -f "$COMPOSE_FILE" logs --tail=100 lotr-web lotr-api >&2
    exit 1
  fi

  sleep 2
done

curl -fsS "$BASE_URL/" >/dev/null
curl -fsS "$BASE_URL/css/site.css" >/dev/null
curl -fsS \
  -c "$COOKIE_JAR" \
  -H 'Content-Type: application/json' \
  -d '{"username":"admin","password":"password"}' \
  "$BASE_URL/api/auth/login" >/dev/null
curl -fsS -b "$COOKIE_JAR" "$BASE_URL/premade" >/dev/null
curl -fsS -b "$COOKIE_JAR" "$BASE_URL/character/create" >/dev/null

echo "web container smoke test passed for $BASE_URL"
