FROM ubuntu:22.04

RUN apt-get update && apt-get install -y libglib2.0-0 libxext6 libxrandr2 libxi6 libxcursor1 libxinerama1 libnss3 libasound2 ca-certificates python3 dos2unix && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy the built Linux headless player
COPY Build/LinuxServer/ /app/

# Copy the startup script (health server + unity)
COPY docker/start.sh /app/start.sh

# Normalize line endings and ensure executables are runnable
RUN dos2unix /app/start.sh && chmod +x /app/start.sh && chmod +x /app/BossFight2D.x86_64

# Cloud Run expects an HTTP server on $PORT
EXPOSE 8080

# Run via /bin/sh to avoid shebang/encoding issues
CMD ["/bin/sh","-c","/app/start.sh"]

Update docker/start.sh to include explicit binding on 0.0.0.0
Ensure the first line is exactly #!/bin/sh, with no BOM, and the file uses LF line endings. Use this content:

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