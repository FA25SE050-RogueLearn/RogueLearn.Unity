# RogueLearn Project Plan

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

## Week 4: Deployment & Testing
- [ ] Implement a basic UI for starting/joining a game.
- [ ] Test the game with multiple clients.
- [ ] Build and deploy the game.