RogueLearn.Unity – Cloud Deployment Context

Purpose
- A single place to remember how the Unity headless server is deployed to Google Cloud, what runtime knobs exist, and how to verify and roll out updates.

Key components in this repo
- infra/compute/startup.sh
  - VM startup script. Installs Docker, logs into Artifact Registry using the VM’s service account, and runs the Unity headless server container.
  - Exposes port 8080 for health checks and mounts /var/log/unity for host-side logs.
- docker/HeadlessServer.Dockerfile
  - Dockerfile used to build the headless Unity server image.
- cloudbuild.yaml
  - Optional: Google Cloud Build config to build and push the headless server image to Artifact Registry.
- Assets/Scripts/Network/ServerBootstrap.cs
  - Headless-only boot logic. Initializes Unity Services, allocates Relay, and StartHost.
  - Instantiates ServerMatchRecorder and ServerAutoStartOnReady after the host is up.
  - HealthHttpServer may be instantiated so the VM/container exposes /healthz.
- Assets/Scripts/Network/HealthHttpServer.cs
  - Headless-only lightweight HTTP server on port 8080.
  - Responds “ok” to GET /healthz for orchestration/liveness checks.
- Assets/Scripts/Network/ServerAutoStartOnReady.cs
  - Ensures gameplay auto-starts in headless runs.
  - If QuizManager is present and idle: auto-ready players and begin quiz/game.
  - If QuizManager is absent: auto-loads the target gameplay scene once at least one client connects.
- Assets/Scripts/System/ServerMatchRecorder.cs
  - Records match summaries in JSON (file sink) under RESULTS_LOG_ROOT (default /var/log/unity/matches).
  - Includes match id, start/end times, result, scene, Relay region, player IDs, host client ID.

Runtime configuration (set via VM metadata attributes)
- image: Artifact Registry image to run (e.g., asia-southeast1-docker.pkg.dev/PROJECT/REPO/unity-server:latest)
- unity_server_scene: Initial Unity scene to load in headless (default: ServerHeadless)
- relay_region: Relay region for hosting (default: asia-southeast1)
- rl_max_connections: Max Relay connections (default: 4)
- results_sink: Where match summaries go; supported “file”, “http” (default: file)
- results_log_root: Directory for file sink (default: /var/log/unity/matches)
- results_http_url: HTTP sink endpoint (if using results_sink=http)
- results_http_token: Bearer token for HTTP sink (if required)

What startup.sh does on the VM
- Reads metadata attributes above via the GCE metadata server.
- Installs docker, jq, and optionally the Cloud Ops Agent for logs/metrics.
- Prepares /var/log/unity and ${RESULTS_LOG_ROOT}.
- Logs into Artifact Registry using the VM’s service account token and pulls the image.
- Runs the container detached (-d) as unity-server, exposing 8080 and passing all env vars from metadata.
- Prints a health URL (http://<VM_IP>:8080/healthz) and notes the match results directory.

How to roll out a new build
Option A: Build & push, update VM metadata, restart VM
- Build the image and push to Artifact Registry (Cloud Build or local docker push).
- Update the VM’s metadata “image” attribute to the new image tag.
- Restart the VM (or let startup.sh redeploy automatically on boot).

Option B: Redeploy the container on the running VM
- SSH into the VM.
- docker pull <new_image>
- docker rm -f unity-server
- Re-run startup.sh (or run docker run with the same env vars as in startup.sh).

Verification checklist
- Health endpoint: curl http://<VM_IP>:8080/healthz returns “ok”.
- Docker logs: look for StartHost succeeded, Relay allocation details, and “ServerMatchRecorder initialized”.
- Relay works: client can connect via join code; gameplay scene auto-loads after first client connects (if no QuizManager) or quiz auto-starts (if QuizManager present).
- Match results: JSON files appear under RESULTS_LOG_ROOT with expected content after a match concludes.

Troubleshooting quick tips
- Health not responding: ensure HealthHttpServer is instantiated in headless run and port 8080 is exposed in docker run.
- Relay allocation fails: verify RELAY_REGION is valid; confirm Unity Services/Auth initialization succeeds.
- No match JSON written: confirm RESULTS_SINK=file and RESULTS_LOG_ROOT is writable; check ServerMatchRecorder logs.
- StartHost errors: confirm NetworkManager and UnityTransport are configured; inspect logs for exception details.

Notes
- Keep VM metadata in sync with what your code reads. The startup.sh passes env vars into the container; the Unity code should read those env vars for scene selection, Relay region, and results sink.
- The health probe IP in startup.sh echo is illustrative; replace with your VM’s external IP or rely on the platform’s health check configuration.