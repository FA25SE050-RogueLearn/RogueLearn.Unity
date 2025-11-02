# RogueLearn.Unity CI/CD Setup Plan (Unity Headless + Artifact Registry + Watchtower)

Repo: RogueLearn.Unity
Branch: multiplayer
Unity: 2022.3.55f1 (LTS)
Build Target: Linux Headless (LinuxServer)
Registry: Artifact Registry (asia-southeast1) repo: roguelearn-unity
CD: Watchtower on VM auto-pulls server:latest and restarts unity-server

## 1) GitHub Actions CI (build Unity headless and push Docker)

Create file: `.github/workflows/unity-headless-docker.yml` with this content:

```yaml
name: Unity Headless CI to Artifact Registry

on:
  push:
    branches: [ multiplayer ]
  workflow_dispatch:

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    permissions:
      contents: read
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Cache Unity Library
        uses: actions/cache@v4
        with:
          path: Library
          key: Library-${{ runner.os }}-Unity-2022.3.55f1-${{ github.ref }}-${{ hashFiles('**/*.meta') }}
          restore-keys: |
            Library-${{ runner.os }}-Unity-2022.3.55f1-

      - name: Unity - Build Linux Headless
        uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
        with:
          unityVersion: 2022.3.55f1
          targetPlatform: LinuxServer
          projectPath: .
          buildName: BossFight2D
          buildPath: Build

      - name: Authenticate to Google Cloud
        uses: google-github-actions/auth@v2
        with:
          credentials_json: ${{ secrets.GCP_SA_KEY }}

      - name: Setup gcloud
        uses: google-github-actions/setup-gcloud@v2

      - name: Configure Docker for Artifact Registry
        run: gcloud auth configure-docker asia-southeast1-docker.pkg.dev -q

      - name: Build & Push Docker image
        uses: docker/build-push-action@v6
        with:
          context: .
          file: docker/HeadlessServer.Dockerfile
          push: true
          tags: |
            asia-southeast1-docker.pkg.dev/${{ secrets.GCP_PROJECT_ID }}/${{ secrets.GCP_ARTIFACT_REPO }}/server:latest
            asia-southeast1-docker.pkg.dev/${{ secrets.GCP_PROJECT_ID }}/${{ secrets.GCP_ARTIFACT_REPO }}/server:${{ github.sha }}
```

Notes:
- The Dockerfile expects the Unity build at `Build/LinuxServer/` and binary name `BossFight2D.x86_64`. The action sets `buildPath: Build` and `buildName: BossFight2D` to match.
- If your binary name differs, update `buildName` or the Dockerfile `chmod` line.

## 2) GitHub Secrets (required)
- `UNITY_LICENSE`: Paste the full contents of your Unity `.ulf` license file.
- `GCP_SA_KEY`: JSON key for a GCP service account with Artifact Registry writer.
- `GCP_PROJECT_ID`: `rougelearn-bossfight-476714`
- `GCP_ARTIFACT_REPO`: `roguelearn-unity`

Optional (for future OIDC setup): avoid storing keys by using Workload Identity Federation with `id-token: write` permissions.

## 3) How to get the Unity license (ULF)
Unity Personal is free (no cost) if your revenue in the last 12 months is under the Unity Personal threshold.
Steps to generate an offline license file for CI:
1. Install Unity 2022.3.55f1 via Unity Hub on any dev machine.
2. Run the Editor once to ensure its initialized.
3. Generate the manual activation file:
   - Windows (example):
     ```powershell
     & "C:\Program Files\Unity\Hub\Editor\2022.3.55f1\Editor\Unity.exe" -quit -batchmode -nographics -logFile - -createManualActivationFile
     ```
   - This produces a `.alf` file, typically under `%ProgramData%/Unity` or `%LocalAppData%/Unity`.
4. Go to https://license.unity3d.com/manual, sign in with your Unity ID, choose Unity Personal, upload the `.alf`, and download the `.ulf` license file.
5. Open the `.ulf` in a text editor, copy all contents, and paste into the GitHub secret `UNITY_LICENSE`.

## 4) Google Artifact Registry setup
Run these once with `gcloud`:
```bash
PROJECT_ID=rougelearn-bossfight-476714
REGION=asia-southeast1
REPO=roguelearn-unity

# Create repository (if not exists)
gcloud artifacts repositories create $REPO \
  --repository-format=docker \
  --location=$REGION \
  --description="Unity headless images"

# Create CI service account
gcloud iam service-accounts create github-actions-ci \
  --display-name="GitHub Actions CI"

# Grant writer to CI SA
gcloud projects add-iam-policy-binding $PROJECT_ID \
  --member="serviceAccount:github-actions-ci@${PROJECT_ID}.iam.gserviceaccount.com" \
  --role="roles/artifactregistry.writer"

# Create key for CI SA (save JSON and add to GitHub secret GCP_SA_KEY)
gcloud iam service-accounts keys create ci-key.json \
  --iam-account="github-actions-ci@${PROJECT_ID}.iam.gserviceaccount.com"

# Grant reader to VM SA (replace VM_SA_EMAIL with your VM service account email)
gcloud projects add-iam-policy-binding $PROJECT_ID \
  --member="serviceAccount:VM_SA_EMAIL" \
  --role="roles/artifactregistry.reader"

# Configure docker auth on the VM
gcloud auth configure-docker asia-southeast1-docker.pkg.dev -q
```

## 5) Watchtower on the VM (auto-update unity-server)
```bash
# Stop any existing unity-server if needed (fixes name conflict)
docker rm -f unity-server || true

# Run unity-server with latest tag
PROJECT_ID=rougelearn-bossfight-476714
REPO=roguelearn-unity
REGION=asia-southeast1
IMAGE="${REGION}-docker.pkg.dev/${PROJECT_ID}/${REPO}/server:latest"

docker run -d --name unity-server --restart=always \
  -p 8080:8080 \
  -e UNITY_SERVER_SCENE="ServerHeadless" \
  -e RELAY_REGION="asia-southeast1" \
  -e RL_MAX_CONNECTIONS="4" \
  -e RESULTS_SINK="file" \
  -e RESULTS_LOG_ROOT="/var/log/unity/matches" \
  "$IMAGE"

# Install Watchtower (auto-pull & restart unity-server on image updates)
docker run -d --name watchtower --restart=always \
  -v /var/run/docker.sock:/var/run/docker.sock \
  containrrr/watchtower unity-server \
  --interval 60 \
  --cleanup \
  --trace \
  --registry-auth
```

## 6) Test checklist
- Push to branch `multiplayer`.
- GitHub Action completes Unity headless build and pushes Docker image to Artifact Registry.
- Watchtower pulls the new `server:latest` and restarts `unity-server`.
- Health check: `curl http://<VM_IP>:8080/healthz` returns `ok`.
- Relay join code appears in logs; WebGL clients connect and play.
- Match JSON written under `/var/log/unity/matches` at match end.

## 7) Troubleshooting
- Docker name conflict: `docker rm -f unity-server` before redeploy.
- Build folder mismatch: Ensure GitHub Action uses `buildPath: Build` so Dockerfile `COPY Build/LinuxServer/ /app/` works.
- Artifact Registry auth: run `gcloud auth configure-docker asia-southeast1-docker.pkg.dev` on the VM.
- Unity license: ensure `UNITY_LICENSE` secret contains full `.ulf` contents. Unity Personal is free.
- Port 8080 conflicts: if using an external health server, avoid binding 8080 twice; prefer HealthHttpServer inside Unity.

## 8) Future improvements
- Use OIDC (Workload Identity Federation) in GitHub Actions to remove JSON keys.
- Add semantic versioning tags to images (e.g., `unity-2022.3.55f1-game-1.0.0`).
- Add a staging environment and smoke tests post-deploy.

-- End of plan --
