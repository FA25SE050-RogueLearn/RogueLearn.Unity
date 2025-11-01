#!/bin/sh
set -e

PORT="${PORT:-8080}"

# Start a minimal HTTP server for Cloud Run health/readiness checks
# Bind to 0.0.0.0 so Cloud Run can reach it
python3 -m http.server "$PORT" --bind 0.0.0.0 --directory /tmp &
HTTP_PID=$!

cleanup() {
kill "$HTTP_PID" 2>/dev/null || true
}
trap cleanup TERM INT

# Ensure the Unity binary is executable (extra safety)
chmod +x /app/BossFight2D.x86_64

# Run Unity headless; logs go to stdout (Cloud Run logs)
exec /app/BossFight2D.x86_64 -batchmode -nographics -logFile /dev/stdout