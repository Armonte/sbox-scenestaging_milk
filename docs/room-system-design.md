# Room System Design — Roguelite Run Progression

## Overview

Single scene with room prefabs placed far apart in the world. Players teleport between rooms. After clearing a room, pick 1 of 3 doors (Hades style) — each door shows what's in the next room (reward type, encounter type).

## Architecture: Single Scene, Offset Rooms

```
World Layout (top-down):

[Room 0]          [Room 1]          [Room 2]
 0,0,0          10000,0,0        20000,0,0

[Room 3]          [Room 4]          [Room 5]
 0,0,10000     10000,0,10000    20000,0,10000
```

- Each room is a prefab placed 10,000 units apart
- Only one room is "active" at a time — enemies only spawn in the current room
- Players teleport to the next room's spawn point
- Far-away rooms have everything disabled (no cost)
- NavMesh baked per room prefab

## Run Flow

```
[Lobby] → [Class Select] → [Weapon Select]
    ↓
[Room 1: Combat] → Clear → [Pick 1 of 3 Doors]
    ↓
[Room 2: Combat/Shop/Event] → Clear → [Pick 1 of 3 Doors]
    ↓
[Room 3: Combat] → Clear → [Pick 1 of 3 Doors]
    ↓
... repeat ...
    ↓
[Room N: Boss] → Clear → [Floor Complete / Next Floor]
```

## Room Types

| Type | Description | Frequency |
|------|-------------|-----------|
| **Combat** | Enemies spawn in waves. Kill all to clear. | Most common |
| **Elite Combat** | Combat with elite enemies (affixed). Better rewards. | Every 3-4 rooms |
| **Shop** | Spend gold on items/rerolls/heal | Every 4-5 rooms guaranteed |
| **Event** | Special encounter (NPC, shrine, gamble) | Random, 25-50% chance |
| **Treasure** | Free reward, no combat | Rare |
| **Boss** | Major fight, end of floor | Every 8-10 rooms |
| **Rest** | Heal, upgrade, prepare | Before boss |

## Door Selection (Hades Style)

After clearing a room, 3 doors appear. Each door shows:
- **Room type icon** (sword for combat, coin for shop, skull for elite, etc)
- **Reward type** (ability upgrade, gold, health, new ability)
- **Difficulty indicator** (easy/medium/hard for combat rooms)

Player walks to a door and interacts to go to that room. In multiplayer, majority vote or host decides.

### Door Reward Preview
```
[Door 1]              [Door 2]              [Door 3]
⚔️ Combat             🛒 Shop               ⚔️ Elite Combat
💎 Ability Upgrade     💰 Gold               🔥 Proc Card
⭐ Normal              —                     ⭐⭐ Hard
```

The reward is guaranteed if you clear the room. This lets players plan their route — need healing? Pick the shop door. Want to power up? Pick the ability upgrade combat room.

## Room Prefab Structure

Each room prefab contains:
```
Room Prefab
├── Geometry (walls, floor, props)
├── NavMesh Surface (baked)
├── SpawnPoints (enemy spawn locations)
├── PlayerSpawnPoint (where players arrive)
├── DoorSpawnPoints (3 positions for exit doors)
├── RoomController (component: manages waves, clear state)
└── Lighting
```

## RoomController Component

```
Properties:
- RoomType (Combat, Shop, Elite, Boss, etc)
- WaveDefinitions (list of waves)
- Reward (what the door promised)

State:
- [Sync] RoomState (Waiting, Active, Cleared)
- [Sync] CurrentWave
- [Sync] EnemiesRemaining

Flow:
1. Players teleport in → RoomState = Active
2. Spawn wave 1
3. When wave cleared → spawn next wave
4. All waves done → RoomState = Cleared
5. Spawn 3 doors with next room options
6. Player picks door → teleport all players → next room activates
```

## Wave Definition

```csharp
public class WaveDefinition
{
    public List<EnemySpawn> Enemies;
    public SpawnPattern Pattern; // AllAtOnce, Trickle, Surround
    public float TrickleInterval; // seconds between spawns for Trickle
}

public class EnemySpawn
{
    public GameObject Prefab;
    public int Count;
    public bool IsElite;
}

public enum SpawnPattern
{
    AllAtOnce,    // All enemies appear immediately
    Trickle,      // Spawn one at a time on a timer
    Surround,     // Spawn in a ring around the room edges
}
```

## Run Manager

Sits on a persistent GameObject, survives room transitions.

```
RunManager
├── CurrentFloor (int)
├── CurrentRoom (int)
├── RoomHistory (list of completed rooms)
├── PlayerLevel / XP / Gold
├── ActiveProcs (list)
├── AbilityLoadout
└── RunSeed (for deterministic room generation)

Methods:
- GenerateFloor() → creates list of room options per step
- GenerateDoorOptions() → pick 3 rooms from remaining pool
- TransitionToRoom(roomIndex) → teleport players, activate room
- OnRoomCleared() → award XP/gold, show doors
```

## Room Pool Per Floor

Each floor has a budget of room types:

**Floor 1** (5 rooms):
- 3 Combat (easy)
- 1 Shop OR Event
- 1 Boss

**Floor 2** (6 rooms):
- 3 Combat (medium)
- 1 Elite Combat
- 1 Shop
- 1 Boss

**Floor 3+** (7-9 rooms):
- 4-5 Combat (scaling)
- 1-2 Elite Combat
- 1 Shop (guaranteed)
- 0-1 Event
- 1 Boss

### Grindea-Style Guarantees
- Shop appears if `floorsWithoutShop >= 3` (prevent drought)
- Event appears if `floorsWithoutEvent >= 2`
- Elite appears every 3-4 rooms minimum
- Boss is always last room

## Door Selection Implementation

After room clear:
1. RunManager generates 3 options from remaining room pool
2. Spawn 3 door GameObjects at the DoorSpawnPoints
3. Each door has a DoorController with:
   - RoomType icon (rendered as world-space UI)
   - Reward type icon
   - Interact trigger (player walks in / presses E)
4. On interact → RunManager.TransitionToRoom()
5. All players teleport to next room

## Teleportation

```csharp
void TransitionToRoom(int roomIndex)
{
    // Deactivate current room (disable enemies, stop spawns)
    CurrentRoom.SetActive(false);
    
    // Activate next room
    var nextRoom = Rooms[roomIndex];
    nextRoom.SetActive(true);
    
    // Teleport all players
    foreach (var player in GetAllPlayers())
    {
        player.WorldPosition = nextRoom.PlayerSpawnPoint.WorldPosition;
    }
    
    // Start the encounter
    nextRoom.GetComponent<RoomController>().StartEncounter();
}
```

## XP/Gold Drops (Grindea-inspired)

- Enemies drop XP orbs + gold on death
- Orbs magnetically pulled toward nearest player after 0.5s delay
- XP fills a bar → level up → pick 3 upgrade cards
- Gold spent in shop rooms

## Implementation Priority

### Step 1: Room Prefabs
- Create 3-4 simple combat room prefabs (different layouts)
- Each has geometry, navmesh, spawn points, player spawn, door spawns
- Place them 10,000 units apart in the scene

### Step 2: RoomController
- Wave spawning (AllAtOnce first)
- Enemy count tracking
- Room clear detection
- [Sync] state for multiplayer

### Step 3: RunManager
- Room pool generation
- Door option generation
- Player teleportation
- Floor progression

### Step 4: Door UI
- World-space door objects with icons
- Interact to select
- Multiplayer voting (or host decides)

### Step 5: XP/Gold
- Drop on enemy death
- Magnetic pickup
- Level bar + level up event

### Step 6: Between-Room
- Pick 3 card UI on level up
- Shop room implementation
- Rest room (heal)

## Files to Create

```
Code/Roguelite/Rooms/
├── RoomController.cs      — Per-room wave spawning + clear detection
├── WaveDefinition.cs      — Wave data (enemies, pattern, timing)
├── DoorController.cs      — Exit door with room preview
├── RunManager.cs           — Run state, floor gen, transitions
└── RoomPool.cs             — Room type distribution per floor

Code/Roguelite/Pickups/
├── XPOrb.cs               — Magnetic XP pickup
└── GoldCoin.cs            — Magnetic gold pickup

Code/Roguelite/UI/
├── DoorPreviewPanel.razor — World-space door info display
├── XPBar.razor            — Level progress bar
└── GoldCounter.razor      — Gold display
```
