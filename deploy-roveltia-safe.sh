#!/usr/bin/env bash
set -euo pipefail

# Safer production deploy for Roveltia:
# - Preserves the server-side .env instead of uploading local .env
# - Optionally enables Database__AutoMigrate on the server before restart
# - Uploads code and build metadata, then rebuilds/restarts the container
#
# Usage:
#   ./deploy-roveltia-safe.sh
#
# Optional environment variables:
#   SERVER_HOST=roveltia.com
#   SERVER_USER=root
#   DEPLOY_PATH=/opt/roveltia
#   COMPOSE_SERVICE=roveltia
#   ENABLE_AUTO_MIGRATE=true
#   AUTO_MIGRATE_VALUE=true
#   SSH_IDENTITY_FILE=~/.ssh/your_key

SERVER_HOST="${SERVER_HOST:-roveltia.com}"
SERVER_USER="${SERVER_USER:-root}"
DEPLOY_PATH="${DEPLOY_PATH:-/opt/roveltia}"
COMPOSE_SERVICE="${COMPOSE_SERVICE:-roveltia}"
ENABLE_AUTO_MIGRATE="${ENABLE_AUTO_MIGRATE:-true}"
AUTO_MIGRATE_VALUE="${AUTO_MIGRATE_VALUE:-true}"
SSH_IDENTITY_FILE="${SSH_IDENTITY_FILE:-}"

if [[ -z "${SSH_IDENTITY_FILE}" && -f "${HOME}/.ssh/printbusters_deploy" ]]; then
  SSH_IDENTITY_FILE="${HOME}/.ssh/printbusters_deploy"
fi

LOCAL_OVERRIDE_FILE="./docker-compose.override.yml"
LOCAL_BUILD_ENV_FILE="./.env.build"
SSH_CONTROL_PATH="${HOME}/.ssh/cm-%r@%h:%p"
SSH_OPTS=(
  -o ConnectTimeout=10
  -o ServerAliveInterval=15
  -o ServerAliveCountMax=3
  -o TCPKeepAlive=yes
  -o Compression=yes
  -o ControlMaster=auto
  -o ControlPersist=1800
  -o ControlPath="${SSH_CONTROL_PATH}"
)

if [[ -n "${SSH_IDENTITY_FILE}" ]]; then
  SSH_OPTS+=( -o "IdentityFile=${SSH_IDENTITY_FILE}" )
fi

mkdir -p "${HOME}/.ssh"
chmod 700 "${HOME}/.ssh" || true

retry() {
  local max=3
  local n=0
  local delay=2
  until "$@"; do
    n=$((n + 1))
    if [[ "${n}" -ge "${max}" ]]; then
      return 1
    fi
    echo "Command failed (attempt ${n}/${max}). Retrying in ${delay}s..."
    sleep "${delay}"
    delay=$((delay * 2))
  done
}

SSH_TARGET="${SERVER_USER}@${SERVER_HOST}"
RSYNC_SSH="ssh ${SSH_OPTS[*]}"

echo "Deploying Roveltia safely to ${SSH_TARGET} -> ${DEPLOY_PATH} ..."
echo "Remote .env will be preserved."

retry ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" "mkdir -p ${DEPLOY_PATH}"

GIT_SHA="$(git rev-parse --short HEAD 2>/dev/null || echo unknown)"
BUILD_UTC="$(date -u +%Y%m%d%H%M%S)"
cat > "${LOCAL_BUILD_ENV_FILE}" <<EOF
ROVELTIA_GIT_SHA=${GIT_SHA}
ROVELTIA_BUILD_UTC=${BUILD_UTC}
EOF

echo "Uploading build metadata (.env.build)..."
retry rsync -avz --progress -e "${RSYNC_SSH}" "${LOCAL_BUILD_ENV_FILE}" "${SSH_TARGET}:${DEPLOY_PATH}/.env.build"
retry ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" "chmod 644 ${DEPLOY_PATH}/.env.build || true"

if [[ -f "${LOCAL_OVERRIDE_FILE}" ]]; then
  echo "Uploading docker-compose.override.yml..."
  retry rsync -avz --progress -e "${RSYNC_SSH}" "${LOCAL_OVERRIDE_FILE}" "${SSH_TARGET}:${DEPLOY_PATH}/docker-compose.override.yml"
else
  echo "No ${LOCAL_OVERRIDE_FILE} (skipping override upload)."
fi

retry rsync -avz --delete --progress -e "${RSYNC_SSH}" \
  --exclude 'bin' \
  --exclude 'obj' \
  --exclude 'publish' \
  --exclude '.git' \
  --exclude 'node_modules' \
  --exclude '.env' \
  --exclude '.env.*' \
  --exclude '.env.build' \
  --exclude 'docker-compose.override.yml' \
  ./ "${SSH_TARGET}:${DEPLOY_PATH}/"

REMOTE_SCRIPT=$(cat <<EOF
set -euo pipefail
cd ${DEPLOY_PATH}

touch .env
chmod 600 .env || true

if [[ "${ENABLE_AUTO_MIGRATE}" == "true" ]]; then
  if grep -q '^Database__AutoMigrate=' .env; then
    sed -i.bak 's/^Database__AutoMigrate=.*/Database__AutoMigrate=${AUTO_MIGRATE_VALUE}/' .env
  else
    printf '\nDatabase__AutoMigrate=${AUTO_MIGRATE_VALUE}\n' >> .env
  fi
  rm -f .env.bak
  echo "Database__AutoMigrate set to ${AUTO_MIGRATE_VALUE} in remote .env"
else
  echo "Leaving Database__AutoMigrate unchanged in remote .env"
fi

docker compose build ${COMPOSE_SERVICE}
docker compose up -d --no-deps ${COMPOSE_SERVICE}

docker image prune -f
EOF
)

ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" "bash -s" <<< "${REMOTE_SCRIPT}"

echo "Deployment finished."
echo "Remote .env was preserved."
echo "This stack listens on 127.0.0.1:8081 by default (ROVELTIA_HOST_PORT in server .env overrides)."
echo "Public URL when proxied: https://roveltia.com"
