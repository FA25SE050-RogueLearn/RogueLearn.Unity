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
- [x] Confirm Relay allocation & max connections via `ServerBootstrap.cs` (no code changes needed).
- [x] Validate existing UI flows (StartNetworkedGameButton, HostWithRelayButton, JoinAsClientButton) in `HostUI.unity` and `ClientUI.unity` – no new UI required.
- [x] Multi-client test: WebGL client connects to local Docker host; gameplay auto-start via readiness.
- [x] Logging path validated for file sink (`/var/log/unity/matches` and local `tmp/match-results`); HTTP sink ready in backend.

### Week 4 Hotfix: Headless Host Readiness UI (Completed)
- [x] Exclude headless host from player counts in `QuizManager` (TotalPlayers, ReadyCount).
- [x] Skip auto-readying the headless host in `ServerAutoStartOnReady`.
- [x] Result: ReadyStation label shows correct counts (e.g., 1/1 with a single client), and readiness gating behaves correctly in cloud-hosted matches.

## Week 5: Website Orchestration & Room Lifecycle
- [x] Backend: proxy host endpoint implemented (`/api/game/host`) returning join code; local Docker orchestration in dev.
- [x] Handshake: join code passed to WebGL via page → RelayConnector; `GameSessionClient` resolves and fetches pack.
- [x] WebGL launch flow: website opens WebGL page with join code; client auto-joins on load.
- [x] Session leadership: first client invites; server remains Netcode host.
- [x] Room lifecycle: auto-start when ready; quiz auto-advances; cleanup on match end.
- [ ] Observability: logs/metrics for server health and allocation failures.

## Week 6: Match Results API & Persistence
- [x] Local HTTP sink implemented: Unity posts completion to `/api/quests/game/sessions/{id}/complete`; backend writes result files.
- [ ] Implement ingestion service and database schema; secure endpoint with bearer token.
- [x] Handle partial failures (TLS CN mismatch) with opt-in insecure TLS for dev; retries supported by client.
- [ ] End-to-end test to DB/dashboard.

## Week 7: AI Question Pack Generation & Ingestion (New)
- [x] JSON schemas present in `BMAD_Rogue_Learn/docs/schemas/`.
- [x] AI prompt template authored and used server-side for structured output.
- [x] CLI skeleton exists for local generation and Ajv validation (`RogueLearn.Frontend/scripts/question-packs/generate.ts`).
- [ ] Static packs under `extracted-data/question-packs/` (optional; using server-generated packs).
- [x] Unity ingestion via web API implemented; `GameSessionClient` injects backend pack into `QuizManager`.
- [x] Verification: WebGL match uses backend AI pack; Power Play and logging validated.

## Checklists

### A. End-to-End Multiplayer Flow
- [x] Web “Start Boss Exam” UI flow (Host page) ready.
- [x] Local Docker server container running; host endpoint operational.
- [x] Relay allocation succeeds; join code delivered to the website.
- [x] WebGL client loads and auto-joins using join code.
- [x] Session lead shares join code; other clients join.
- [x] Gameplay auto-starts; match proceeds.
- [x] Results posted via HTTP sink; stored to disk; website visualization pending.
- [x] Failure handling: TLS CN mismatch handled; host unreachable path returns stub; client disconnects handled.

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
 - For question packs, prefer static ingestion via StreamingAssets for demos; switch to HTTP ingestion when backend endpoints are ready.

## References
- RogueLearn.Unity/Context/cloud-deployment-context.md
- BMAD_Rogue_Learn/docs/unity-content/headless-server-bootstrap-relay.md
- BMAD_Rogue_Learn/docs/unity-content/match-results-logging.md
- BMAD_Rogue_Learn/docs/stories/story-4/4.11.webgl-clients-headless-server-relay.md
- BMAD_Rogue_Learn/docs/stories/story-4/4.12.match-results-api.md
 - BMAD_Rogue_Learn/docs/schemas/question-pack.schema.json
 - BMAD_Rogue_Learn/docs/ai/question-pack-generation.md

# Next Steps: Adaptive Packs & Per‑Player Summaries

## Objectives
- Keep multiplayer rooms fair with one shared pack per room.
- Capture per‑player analytics and write a per‑player summary on completion.
- Use the latest summary to bias the next solo practice pack for that player.

## Backend (User API)
- Add endpoints to persist and fetch per‑player summaries and detailed events.
- `POST /api/quests/game/sessions/{id}/events` → accept batched per‑player events.
- `POST /api/quests/game/sessions/{id}/complete` → include per‑player summary payload; persist by `user_id`.
- Optional: `GET /api/player/{userId}/last-summary` for generator to consume.
- Add `RESULTS_DIR` env to configure sink path (e.g., RogueLearn.Unity/Context) for demo runs.

## Frontend (Next.js)
- Generator route (done): `POST /api/ai/question-packs/generate` validates JSON and returns `{ pack, packId, packUrl }`.
- Retrieval route (done): `GET /api/question-packs/[id]` serves stored packs.
- Host flow: for solo practice, call generator with `{ userId, priorSummary }` and pass its `pack_url` to session creation.
- Mock exam flow: continue using a neutral shared pack.

## Unity (Server‑authoritative)
- Instrument per‑question logging in `QuizManager` to record: `user_id, question_id, choice, correct, time_ms, topic, difficulty`.
- On completion, bundle a per‑player summary (topic accuracy, difficulty curve, timing) and POST to User API.
- Ensure win/lose replication is immediate via `NetworkGameState` and panels show without extra hits.

## Data Schema (Supabase)
- `question_packs(id text pk, subject text, topic text, difficulty text, content jsonb, created_at timestamptz)`.
- `player_session_summaries(user_id uuid, session_id uuid, summary jsonb, created_at timestamptz, primary key(user_id, session_id))`.
- `match_events(session_id uuid, user_id uuid, question_id text, choice int, correct boolean, time_ms int, topic text, difficulty text, created_at timestamptz)`.

## Milestones
1. Per‑player event logging in Unity and POST to `/events`.
2. Completion payload: compute and persist per‑player summary on API.
3. Solo practice flow: generator consumes `priorSummary` and returns personalized pack.
4. Observability: basic dashboard listing recent matches and summaries per user.
5. Demo polish: set `RESULTS_DIR` to Context path; limit boss HP for quick runs.

## Notes
- Multiplayer fairness: never adapt items within a shared room; adapt only across sessions.
- Validation: keep Ajv schemas aligned with Unity `BackendPack/BackendQuestion` expectations.