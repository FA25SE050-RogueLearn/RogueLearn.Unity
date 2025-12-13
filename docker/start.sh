#!/bin/sh
# Use POSIX sh options; dash (the default /bin/sh on Ubuntu) does not support pipefail
set -eu

PORT="${PORT:-8080}"

# Default dedicated-server behavior for lobby-first flow: do NOT auto-load gameplay on first client connect.
# Clients remain in lobby until players press Ready and the server transitions everyone.
# To switch to auto-load on first client, set HEADLESS_AUTOLOAD_ON_FIRST_CLIENT=true at runtime.
export HEADLESS_AUTOLOAD_ON_FIRST_CLIENT="${HEADLESS_AUTOLOAD_ON_FIRST_CLIENT:-false}"

# For lobby-first, keep auto-ready disabled by default. Set to true to auto mark all connected players ready.
export HEADLESS_AUTO_READY_ALL="${HEADLESS_AUTO_READY_ALL:-false}"

# Start a minimal HTTP server exposing /healthz and /readyz
# Bind to 0.0.0.0 so external health checks can reach it
python3 - <<'PY' &
import os
from http.server import BaseHTTPRequestHandler, HTTPServer

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path.startswith('/healthz') or self.path.startswith('/readyz'):
            self.send_response(200)
            self.send_header('Content-Type','text/plain')
            self.end_headers()
            self.wfile.write(b'ok')
        else:
            self.send_response(404)
            self.end_headers()
    def log_message(self, format, *args):
        # Silence default logging
        return

port = int(os.environ.get('PORT', '8080'))
HTTPServer(('0.0.0.0', port), Handler).serve_forever()
PY
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