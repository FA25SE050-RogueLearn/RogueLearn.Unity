Headless Server (Relay) Docker Quickstart

Prerequisites
- Build a Linux headless (Server Build) from Unity 2022.3 LTS.
- Output path: Build/LinuxServer/ with BossFight2D.x86_64 and BossFight2D_Data/.
- Option A (Editor): Menu Build -> Headless -> Linux Server (Relay)
- Option B (CLI):
  "<UnityEditor>\Unity.exe" -batchmode -nographics -quit \
    -projectPath "D:\School\Capstone\roguelearn-unity-games" \
    -executeMethod HeadlessBuild.BuildLinuxServer \
    -logFile -

Build the image
1) From the project root (roguelearn-unity-games), run:
   docker build -f docker/HeadlessServer.Dockerfile -t roguelearn-server:latest .

Run the container
2) Start the container and capture the Relay join code from logs:
   docker run --rm \
     -e UNITY_SERVER_SCENE=HostUI \
     -e RELAY_REGION=us-central \
     -e RL_MAX_CONNECTIONS=20 \
     roguelearn-server:latest

3) Watch logs for lines like:
   [ServerBootstrap] Relay Join Code: ABCD1234
   {"event":"relay_join_code","joinCode":"ABCD1234","region":"us-central","max":20}

Notes
- The server uses StartHost() to bind to Relay. Clients should join using the displayed code.
- WebGL clients must be served over HTTPS and connect via wss; use the Join-by-code UI.
- You can override the server scene via UNITY_SERVER_SCENE (HostUI or Gameplay), region via RELAY_REGION.