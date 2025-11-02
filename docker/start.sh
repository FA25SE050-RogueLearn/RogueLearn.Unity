#!/bin/sh
set -euo pipefail

PORT="${PORT:-8080}"

# Start a minimal HTTP server for Cloud Run health/readiness checks
# Bind to 0.0.0.0 so Cloud Run can reach it
python3 -m http.server "$PORT" --bind 0.0.0.0 --directory /tmp &
HTTP_PID=$!

cleanup() {
kill "$HTTP_PID" 2>/dev/null || true
}
trap cleanup TERM INT

# Locate the Unity binary (.x86_64) regardless of folder layout
UNITY_BIN=$(find /app -maxdepth 2 -type f -name "*.x86_64" | head -n 1)
if [ -z "$UNITY_BIN" ]; then
  echo "Error: Unity binary (*.x86_64) not found under /app" >&2
  echo "Contents of /app:" >&2
  ls -la /app >&2 || true
  exit 1
fi

# Ensure the Unity binary is executable
chmod +x "$UNITY_BIN"

# Run Unity headless; logs go to stdout (Cloud Run logs)
exec "$UNITY_BIN" -batchmode -nographics -logFile /dev/stdout