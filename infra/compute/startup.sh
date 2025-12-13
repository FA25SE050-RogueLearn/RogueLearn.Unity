#!/bin/bash
set -euxo pipefail

# Helper to read instance metadata attributes with optional default
get_md() {
local key="$1"; local default_val="${2:-}"
local url="http://metadata.google.internal/computeMetadata/v1/instance/attributes/${key}"
if val=$(curl -sf -H "Metadata-Flavor: Google" "$url"); then
echo "$val"
else
echo "$default_val"
fi
}

# Read configuration from metadata (set these when creating the VM)
IMAGE="$(get_md image)"
UNITY_SERVER_SCENE="$(get_md unity_server_scene ServerHeadless)"
RELAY_REGION="$(get_md relay_region asia-southeast1)"
RL_MAX_CONNECTIONS="$(get_md rl_max_connections 4)"
RESULTS_SINK="$(get_md results_sink file)"
RESULTS_LOG_ROOT="$(get_md results_log_root /var/log/unity/matches)"
RESULTS_HTTP_URL="$(get_md results_http_url)"
RESULTS_HTTP_TOKEN="$(get_md results_http_token)"

echo "[startup] IMAGE=${IMAGE}"
echo "[startup] UNITY_SERVER_SCENE=${UNITY_SERVER_SCENE}"
echo "[startup] RELAY_REGION=${RELAY_REGION}"
echo "[startup] RL_MAX_CONNECTIONS=${RL_MAX_CONNECTIONS}"
echo "[startup] RESULTS_SINK=${RESULTS_SINK}"
echo "[startup] RESULTS_LOG_ROOT=${RESULTS_LOG_ROOT}"

# Install Docker + jq
apt-get update -y
apt-get install -y docker.io ca-certificates curl gnupg jq
systemctl enable --now docker

# Optional: Cloud Ops Agent for logs/metrics
curl -sSL https://dl.google.com/cloud-ops-agent/install.sh | bash || true

# Prepare log directories
mkdir -p /var/log/unity
mkdir -p "${RESULTS_LOG_ROOT}"

# Clean previous container if exists
if docker ps -a --format '{{.Names}}' | grep -q '^unity-server$'; then
docker rm -f unity-server || true
fi

# Login to Artifact Registry using VMs service account token (no gcloud needed)
REG_HOST="$(echo "$IMAGE" | cut -d/ -f1)"  # e.g., asia-southeast1-docker.pkg.dev
TOKEN="$(curl -s -H "Metadata-Flavor: Google" http://metadata.google.internal/computeMetadata/v1/instance/service-accounts/default/token | jq -r '.access_token')"
docker login -u oauth2accesstoken -p "$TOKEN" "https://${REG_HOST}"

# Optional: pre-pull to verify auth
docker pull "${IMAGE}" || true

# Run the container
docker run --name unity-server --restart=always -d -p 8080:8080 -e UNITY_SERVER_SCENE="${UNITY_SERVER_SCENE}" -e RELAY_REGION="${RELAY_REGION}" -e RL_MAX_CONNECTIONS="${RL_MAX_CONNECTIONS}" -e RESULTS_SINK="${RESULTS_SINK}" -e RESULTS_LOG_ROOT="${RESULTS_LOG_ROOT}" -e RESULTS_HTTP_URL="${RESULTS_HTTP_URL}" -e RESULTS_HTTP_TOKEN="${RESULTS_HTTP_TOKEN}" -v /var/log/unity:/var/log/unity "${IMAGE}"

echo "[startup] Container launched: unity-server"
echo "[startup] Health probe: http://34.143.247.143:8080/healthz"
echo "[startup] Match results will be written under ${RESULTS_LOG_ROOT} (file sink)"

