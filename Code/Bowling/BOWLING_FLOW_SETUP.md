# Bowling Game Flow Setup Guide

This document explains how to set up the title screen → character select → game flow for the bowling prototype.

## Overview

The flow works as follows:
1. **Title Screen** → Player selects Solo or Multiplayer
2. **Character Selection** → Player picks a character
3. **Game Scene** → Player spawns into the bowling game

## Components Created

### UI Components
- `TitleScreenPanel.razor` - Main menu with Solo/Multi/Options/Exit
- `CharacterSelectPanel.razor` - Character selection screen

### Manager Components
- `BowlingGameFlowManager.cs` - Handles game state transitions
- `BowlingSpawnManager.cs` - Manages player spawning and character selection
- `CharacterSelectionUI.cs` - Manages character selection UI for individual players

## Scene Setup

### 1. Title Screen Scene

Create a new scene (e.g., `Assets/Scenes/title.scene`) with:

**Required GameObjects:**
- **Camera** - Main camera for the scene
- **Screen UI** - GameObject with `ScreenPanel` component
  - **Title Screen Panel** - Child GameObject with `TitleScreenPanel` component
- **Game Flow Manager** - GameObject with `BowlingGameFlowManager` component
  - Set `TitleScene` property to the title scene file
  - Set `GameScene` property to your bowling game scene (e.g., `scenes/bowling.scene`)

**Example Structure:**
```
Title Scene
├── Camera
├── Screen UI (ScreenPanel)
│   └── Title Screen Panel (TitleScreenPanel)
└── Game Flow Manager (BowlingGameFlowManager)
    - TitleScene: scenes/title.scene
    - GameScene: scenes/bowling.scene
```

### 2. Character Selection Scene (Optional)

You can either:
- **Option A**: Use the same scene as title screen, just show different UI
- **Option B**: Create a separate character selection scene

If using Option A, the flow manager will handle UI transitions automatically.

If using Option B, create `Assets/Scenes/character_select.scene` with:
- **Camera**
- **Screen UI** with `CharacterSelectPanel`
- **Spawn Manager** with `BowlingSpawnManager` component
  - Set `PlayerPrefab` to your player prefab
  - Add spawn points to `SpawnPoints` list
- **Game Flow Manager** (same as title screen)

### 3. Game Scene Setup

Your existing bowling scene (`BowlingScene.scene` or `bowling.scene`) needs:

- **Spawn Manager** - GameObject with `BowlingSpawnManager` component
  - Set `PlayerPrefab` to your player prefab
  - Add spawn points to `SpawnPoints` list
- **Game Flow Manager** - GameObject with `BowlingGameFlowManager` component
  - Set `GameScene` to the scene file itself

## Configuration

### Update Startup Scene

In `bowl.sbproj`, update the `StartupScene`:
```json
"StartupScene": "scenes/title.scene"
```

### Flow Manager Properties

The `BowlingGameFlowManager` needs these scene references:
- `TitleScene` - The title screen scene file
- `CharacterSelectScene` - (Optional) Character selection scene
- `GameScene` - The main bowling game scene

## How It Works

1. **Title Screen**: 
   - Shows `TitleScreenPanel` with menu options
   - Player clicks "Solo" or "Multiplayer"
   - Flow manager transitions to character selection

2. **Character Selection**:
   - `BowlingSpawnManager` spawns a temporary player when connection is active
   - Creates `CharacterSelectionUI` and `CharacterSelectPanel` for the player
   - Player selects a character
   - Flow manager transitions to game scene

3. **Game Scene**:
   - Player spawns with selected character
   - Game begins

## Notes

- The spawn manager uses `INetworkListener` to detect when players connect
- Character selection UI is created per-player and cleaned up after selection
- Scene transitions use `Game.ActiveScene.Load()` or `Game.ActiveScene.LoadFromFile()`
- All UI components use Razor syntax and SCSS for styling

## Next Steps

1. Create the title screen scene in the editor
2. Add the required components to each scene
3. Set up spawn points in your game scene
4. Test the flow: Title → Character Select → Game

