#!/usr/bin/env sh
# Dokploy / produktion: undgå "container name already in use" efter afbrudt deploy.
set -eu

COMPOSE_FILE="${COMPOSE_FILE:-docker/docker-compose.yml}"
PROJECT="${COMPOSE_PROJECT_NAME:-auth-authprod-zuyvik}"

docker rm -f "${PROJECT}-api-1" "${PROJECT}-spa-1" 2>/dev/null || true

exec docker compose -p "$PROJECT" -f "$COMPOSE_FILE" up -d --build --remove-orphans --force-recreate "$@"
