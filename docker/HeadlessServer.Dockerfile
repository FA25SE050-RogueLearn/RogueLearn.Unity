FROM ubuntu:22.04

RUN apt-get update && apt-get install -y libglib2.0-0 libxext6 libxrandr2 libxi6 libxcursor1 libxinerama1 libnss3 libasound2 ca-certificates python3 dos2unix && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy the built Linux player (Standalone)
COPY build/StandaloneLinux64/ /app/

# Copy the startup script (health server + unity)
COPY docker/start.sh /app/start.sh

# Normalize line endings and ensure executables are runnable
RUN dos2unix /app/start.sh && chmod +x /app/start.sh

# Cloud Run expects an HTTP server on $PORT
EXPOSE 8080

# Run via /bin/sh to avoid shebang/encoding issues
CMD ["/bin/sh","-c","/app/start.sh"]

