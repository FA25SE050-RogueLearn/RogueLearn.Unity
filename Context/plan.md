# RogueLearn Project Plan

Project path: D:\School\Capstone\RogueLearn.Unity

Source of truth for cross-system requirements: D:\School\Capstone\BMAD_Rogue_Learn\docs

End-to-end multiplayer mock exam flow (approved)
1. User on the website selects “Fight Boss” (mock exam).
2. Website signals to start the Unity WebGL client and requests a room.
3. Cloud headless server allocates a Relay and hosts the match room.
4. The initiating user joins the room via join code and acts as the session lead (server is the Netcode host).
5. The session lead shares the join code; other users join as clients.
6. They play the game; on match end, results are sent back to the website and saved to the database.

## Week 1: Project Setup & Initial Design (Completed)
- [x] Set up Unity project with Netcode for GameObjects.
- [x] Initial design for the quiz game mechanics.

## Week 2: Server Authority & Core Game Logic (Completed)
- [x] Implement `QuestionManager` for server-side question handling.
- [x] Implement server-authoritative answer validation.
- [x] Synchronize question and answer state to clients.
- [x] Provide visual feedback for correct/incorrect answers on the client.

## Week 3: Player Interaction & State Sync (Completed)
- [x] Create a `Player` prefab and script for player-specific logic.
- [x] Configure `NetworkManager` to spawn players for connected clients.
- [x] Implement server-authoritative player movement and dashing.
- [x] Synchronize player position, rotation, and animations.
- [x] Implement a multiplayer-compatible camera system (e.g., Cinemachine) that follows the local player.
- [x] Implement Player Health & State Sync.
- [x] Implement player interaction with the quiz elements (e.g., answer stations).
- [x] Integrate quiz and combat mechanics (Power Play).

## Week 4: Cloud Hosting & WebGL Setup (Planning – no gameplay changes)
- [ ] Prepare WebGL build profile (compression, streaming, caching) and verify scenes (MainMenu, ClientUI).
- [ ] Build headless server image using `docker/HeadlessServer.Dockerfile`; push to Artifact Registry.
- [ ] Provision VM startup via `infra/compute/startup.sh` passing env: `unity_server_scene`, `relay_region`, `results_sink`, `results_http_url`, `results_http_token`.
- [ ] Verify health endpoint `/healthz` from `HealthHttpServer.cs` in headless container.
- [ ] Confirm Relay allocation & max connections via `ServerBootstrap.cs` (no code changes needed).
- [ ] Validate existing UI flows (StartNetworkedGameButton, HostWithRelayButton, JoinAsClientButton) in `HostUI.unity` and `ClientUI.unity` – no new UI required.
- [ ] Multi-client test: two WebGL clients connect to cloud host; gameplay auto-start via `ServerAutoStartOnReady.cs`.
- [ ] Logging path `/var/log/unity/matches` validated for file sink; plan to switch to HTTP sink when web endpoint is ready.

### Week 4 Hotfix: Headless Host Readiness UI (Completed)
- [x] Exclude headless host from player counts in `QuizManager` (TotalPlayers, ReadyCount).
- [x] Skip auto-readying the headless host in `ServerAutoStartOnReady`.
- [x] Result: ReadyStation label shows correct counts (e.g., 1/1 with a single client), and readiness gating behaves correctly in cloud-hosted matches.

## Week 5: Website Orchestration & Room Lifecycle
- [ ] Backend: implement “Start Boss Exam” endpoint to request a room (Relay allocation orchestrator or proxy).
- [ ] Define handshake: website returns join code and config to the WebGL client (query params or `WebBridge` payload).
- [ ] WebGL launch flow: website opens WebGL page with join code; client auto-joins on load.
- [ ] Session leadership: first client acts as “session lead” for inviting; server remains the Netcode host.
- [ ] Room lifecycle: auto-start when ready (`ServerAutoStartOnReady`); auto-cleanup on match end.
- [ ] Observability: logs, metrics, and alerts for server health and room allocation failures.

## Week 6: Match Results API & Persistence
- [ ] Finalize match-results HTTP API (see BMAD_Rogue_Learn/docs/unity-content/match-results-logging.md and stories/story-4/4.12.match-results-api.md).
- [ ] Implement ingestion service and database schema per BMAD docs; secure endpoint with bearer token.
- [ ] Ensure idempotency and retries for results submission; handle partial failures.
- [ ] End-to-end test: Unity (HTTP sink) → Web API → Database; confirm dashboards update.

## Checklists

### A. End-to-End Multiplayer Flow
- [ ] Web “Start Boss Exam” routes and UI ready.
- [ ] Cloud server container running; `/healthz` returns “ok”.
- [ ] Relay allocation succeeds; join code delivered to the website.
- [ ] WebGL client loads and auto-joins using join code.
- [ ] Session lead shares join code; other clients join.
- [ ] Gameplay auto-starts; match proceeds without gameplay code changes.
- [ ] Results posted via HTTP sink; stored in DB; visible on website.
- [ ] Failure handling: host unreachable, relay allocation errors, results post failures, client disconnects.

### B. Cloud Deployment
- [ ] VM metadata configured (image, `unity_server_scene`, `relay_region`, `results_sink`, `results_http_url`, `results_http_token`).
- [ ] `startup.sh` installs Docker and runs container with env vars.
- [ ] Logs captured from `/var/log/unity`; Cloud Ops Agent optional.
- [ ] Security: service account scopes, firewall rules, Artifact Registry permissions.

## Notes
- No gameplay modifications required at this stage. Utilize existing scripts:
  - Assets/Scripts/Network: `ServerBootstrap.cs`, `HealthHttpServer.cs`, `ServerAutoStartOnReady.cs`
  - Assets/Scripts/System: `ServerMatchRecorder.cs`, `WebBridge.cs`
  - Assets/Scripts/UI: `StartNetworkedGameButton.cs`, `HostWithRelayButton.cs`, `JoinAsClientButton.cs`
- Align API and data contracts with BMAD_Rogue_Learn/docs before any Unity changes.

## References
- RogueLearn.Unity/Context/cloud-deployment-context.md
- BMAD_Rogue_Learn/docs/unity-content/headless-server-bootstrap-relay.md
- BMAD_Rogue_Learn/docs/unity-content/match-results-logging.md
- BMAD_Rogue_Learn/docs/stories/story-4/4.11.webgl-clients-headless-server-relay.md
- BMAD_Rogue_Learn/docs/stories/story-4/4.12.match-results-api.md