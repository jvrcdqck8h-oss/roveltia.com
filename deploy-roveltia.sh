#!/usr/bin/env bash
set -e

# Roveltia (roveltia.com): Docker deploy, modeled after Printbusters publish flow.
#
# Side-by-side safety (does not replace other stacks):
# - Uses Compose project name "roveltia" (see docker-compose.yml) and DEPLOY_PATH (/opt/roveltia by default).
# - Default published host port is 8081 so an existing app can keep 8080 or another port.
#   Set ROVELTIA_HOST_PORT in the server’s ${DEPLOY_PATH}/.env to change the host port.
# - Only builds/starts the "roveltia" service; does not run docker compose down globally.
#
# Usage: from repo root
#   ./deploy-roveltia.sh
#
# SSH passwords: this script opens several SSH/rsync connections. They reuse one
# connection (ControlMaster) so you should only type your password once, unless
# the shared socket expires mid-deploy (see ControlPersist below).
#
# To stop password prompts entirely, install your public key once:
#   ssh-copy-id -i ~/.ssh/id_ed25519.pub "${SERVER_USER:-root}@${SERVER_HOST:-roveltia.com}"
# (Use your real key path.) After that, deploy uses the key only.

# Configuration: adjust to your server
SERVER_HOST="${SERVER_HOST:-roveltia.com}"
SERVER_USER="${SERVER_USER:-root}"
DEPLOY_PATH="${DEPLOY_PATH:-/opt/roveltia}"
COMPOSE_SERVICE="${COMPOSE_SERVICE:-roveltia}"
# Optional: SSH private key (defaults to project deploy key if present)
SSH_IDENTITY_FILE="${SSH_IDENTITY_FILE:-}"
if [[ -z "${SSH_IDENTITY_FILE}" && -f "${HOME}/.ssh/printbusters_deploy" ]]; then
  SSH_IDENTITY_FILE="${HOME}/.ssh/printbusters_deploy"
fi

LOCAL_ENV_FILE="./.env"
LOCAL_OVERRIDE_FILE="./docker-compose.override.yml"
LOCAL_BUILD_ENV_FILE="./.env.build"
EXPORT_DIR="$(mktemp -d /tmp/roveltia-deploy.XXXXXX)"

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

cleanup() {
  rm -rf "${EXPORT_DIR}"
}

trap cleanup EXIT

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

echo "Deploying Roveltia to ${SSH_TARGET} → ${DEPLOY_PATH} …"

if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "Working tree has uncommitted changes. Deploy will use committed HEAD only."
fi

git archive --format=tar HEAD | tar -xf - -C "${EXPORT_DIR}"

retry ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" "mkdir -p ${DEPLOY_PATH}"

GIT_SHA="$(git rev-parse --short HEAD 2>/dev/null || echo unknown)"
BUILD_UTC="$(date -u +%Y%m%d%H%M%S)"
cat > "${LOCAL_BUILD_ENV_FILE}" <<EOF
ROVELTIA_GIT_SHA=${GIT_SHA}
ROVELTIA_BUILD_UTC=${BUILD_UTC}
EOF

if [[ -f "${LOCAL_ENV_FILE}" ]]; then
  echo "Uploading local .env to ${DEPLOY_PATH}/.env on server…"
  retry rsync -avz --progress -e "${RSYNC_SSH}" "${LOCAL_ENV_FILE}" "${SSH_TARGET}:${DEPLOY_PATH}/.env"
  retry ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" "chmod 600 ${DEPLOY_PATH}/.env || true"
else
  echo "No local .env at ${LOCAL_ENV_FILE} (skipping secrets upload)."
fi

echo "Uploading build metadata (.env.build)…"
retry rsync -avz --progress -e "${RSYNC_SSH}" "${LOCAL_BUILD_ENV_FILE}" "${SSH_TARGET}:${DEPLOY_PATH}/.env.build"
retry ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" "chmod 644 ${DEPLOY_PATH}/.env.build || true"

if [[ -f "${LOCAL_OVERRIDE_FILE}" ]]; then
  echo "Uploading docker-compose.override.yml…"
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
  "${EXPORT_DIR}/" "${SSH_TARGET}:${DEPLOY_PATH}/"

REMOTE_SCRIPT=$(cat <<EOF
set -e
cd ${DEPLOY_PATH}

touch .env
chmod 600 .env || true

docker compose build ${COMPOSE_SERVICE}
docker compose up -d --no-deps ${COMPOSE_SERVICE}

docker image prune -f
EOF
)

ssh "${SSH_OPTS[@]}" "${SSH_TARGET}" "bash -s" <<< "${REMOTE_SCRIPT}"

echo "Deployment finished."
echo "This stack listens on 127.0.0.1:8081 by default (ROVELTIA_HOST_PORT in server .env overrides)."
echo "Point nginx/Caddy at that upstream when you are ready; other sites on the host are unchanged."
echo "Public URL when proxied: https://roveltia.com"
