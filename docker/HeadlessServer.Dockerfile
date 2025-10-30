# Runtime image for the Unity headless (Server Build) with Relay host.
# Assumes you have built a Linux headless player into Build/LinuxServer/.

FROM ubuntu:22.04

RUN apt-get update && apt-get install -y \
    libglib2.0-0 \
    libxext6 \
    libxrandr2 \
    libxi6 \
    libxcursor1 \
    libxinerama1 \
    libnss3 \
    libasound2 \
    ca-certificates \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy the built Linux headless player (adjust paths to your actual build output)
COPY Build/LinuxServer/ /app/

# Optional environment variables to control server bootstrap
ENV UNITY_SERVER_SCENE=HostUI \
    RELAY_REGION=us-central \
    RL_MAX_CONNECTIONS=20

# Stream logs to stdout; Unity supports "-logfile -" to pipe logs out
CMD ["/app/BossFight2D.x86_64", "-batchmode", "-nographics", "-logfile", "-"]