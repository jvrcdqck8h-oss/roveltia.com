#!/usr/bin/env bash
# Run on the server inside the repo clone (after .env exists).
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ ! -f .env ]]; then
  echo "Create .env from .env.example and set POSTGRES_PASSWORD." >&2
  exit 1
fi
docker compose build
docker compose up -d
echo "OK — listen on 127.0.0.1 port from ROVELTIA_HOST_PORT in .env (default 8081); point your reverse proxy there."
