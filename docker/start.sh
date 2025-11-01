Content:
#!/bin/sh
set -e

PORT="${PORT:-8080}"

#Start a minimal HTTP server for Cloud Run health/readiness checks
python3 -m http.server "$PORT" --directory /tmp &
HTTP_PID=$!

# Cleanup on exit
cleanup() {
kill "$HTTP_PID" 2>/dev/null || true
}
trap cleanup TERM INT

# Ensure the Unity binary is executable
chmod +x /app/BossFight2D.x86_64

# Run Unity headless; logs go to stdout (Cloud Run logs)
exec /app/BossFight2D.x86_64 -batchmode -nographics -logFile /dev/stdout