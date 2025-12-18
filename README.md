# RogueLearn - Boss Fight Module (Unity)

## Overview
This is the **Boss Fight** gameplay module for the RogueLearn platform. It is a Unity-based interactive quiz battle where players demonstrate their learning by fighting a boss. Correct answers deal damage, while incorrect answers or slow responses result in penalties.

This module is designed to be embedded within the RogueLearn web platform (via WebGL) or run as a standalone client for testing.

## Key Features
- **Quiz-Based Combat**: Players answer questions to attack the boss.
- **Multiplayer Support**: Uses Unity Relay for synchronized game sessions.
- **Safe Zone Mechanic**: Players must stay in the safe zone to answer questions; incorrect answers eject players.
- **Backend Integration**: Automatically reports match results, player stats, and win/lose conditions to the RogueLearn API.

## Technical Requirements
- **Unity Version**: `2022.3.55f1` (LTS)
- **Render Pipeline**: Universal Render Pipeline (URP)
- **Input System**: New Unity Input System package
- **Build Target**: WebGL (primary), Windows/Mac (dev/testing)

## Setup & Installation
1. **Prerequisites**:
   - Install [Unity Hub](https://unity.com/download).
   - Install Unity Editor version **2022.3.55f1**.
   - Ensure the **WebGL Build Support** module is installed.

2. **Open Project**:
   - Open Unity Hub.
   - Click **Add** and select the `RogueLearn.Unity` folder.
   - Open the project.

## Configuration
The game client communicates with the RogueLearn backend. In a development environment, you may need to configure the API endpoints.

### Environment Variables / Config
The `ResultsLogger.cs` script handles API communication. It looks for the following configuration (typically injected during WebGL build or set in editor):

- `QUESTS_API_BASE`: Base URL for the backend Quests API (e.g., `https://api.roguelearn.local/quests`).
- `RESULTS_API_KEY`: API Key for secure communication.

## Game Loop
1. **Lobby/Start**: Player enters a join code (if multiplayer) or starts a solo session.
2. **Phase 1 - Preparation**: Brief countdown and instruction.
3. **Phase 2 - Combat**:
   - **Quiz Cycle**: Question appears -> Player answers -> Result applied.
   - **Win Condition**: Boss Health = 0.
   - **Lose Condition**: Player Health = 0.
4. **Phase 3 - Results**: Match summary is displayed and sent to the backend.

## Development Notes
- **Scenes**:
  - `Scenes/MainMenu`: Entry point for the game.
  - `Scenes/BossFight`: The main gameplay arena.
- **Scripts**:
  - `Scripts/Network`: Handles Unity Relay and multiplayer synchronization.
  - `Scripts/Gameplay`: Core game logic (Health, Quiz, Movement).
  - `Scripts/API`: HTTP communication with the backend.

## Build Instructions
1. Go to **File > Build Settings**.
2. Select **WebGL**.
3. Click **Switch Platform** if not already selected.
4. Click **Build** and choose an output directory (e.g., `../RogueLearn.Frontend/public/game-build`).
