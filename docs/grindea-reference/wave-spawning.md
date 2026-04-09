# Wave / Encounter Spawning System -- Secrets of Grindea Reference

## Overview
Room encounters are managed by the `OpenGatesAtEnemyClear` bagman class (inherits `OpenGatesAtRoomClear`). This system handles enemy spawning, wave timing, gauntlet modes, room clear detection, grading, and score rewards. The "Bagman" pattern is the game's term for room-level controllers that manage encounter state.

## OpenGatesAtEnemyClear Structure
```csharp
public class OpenGatesAtEnemyClear : OpenGatesAtRoomClear
{
    int iScoreToReward;
    int iHyperElitesCanSpawn;
    GauntletType enGauntletType;
    List<List<GauntletSpawn>> llenGauntletWaves;  // Discrete wave definitions
    List<EnemyTypes> lenGauntletUnblockEnemies;
    int iArbitraryEnemySpawnerBlock;              // Extra "phantom" enemies blocking clear
    int iGauntletCounter;                         // Current wave/timer
    int iContinuousGauntletSpawnStartAt = 240;    // 4 seconds before first continuous spawn
    int iContinuousGauntletSpawnInterval = 80;    // ~1.3 seconds between spawns
    bool bQuickenContinuous;                      // Speed up continuous spawns
    int iRoomStartingEnemyAmount;
    int iGauntletAddsSpawned;
    List<GauntletSpawn> lxGauntletEnemiesSpawning;
    int iDontCheckClearIn;                        // Frames to delay clear check
    int iParTime;                                 // Time for S-rank (in frames)
    bool bOpened;                                 // Room cleared flag
}
```

## Gauntlet Types
```csharp
public enum GauntletType
{
    // (inferred from room properties)
    GauntletWaves,      // Discrete waves with clear conditions
    GauntletContinuous  // Enemies spawn continuously on timer
}
```

### Discrete Waves
- `llenGauntletWaves` contains lists of `GauntletSpawn` entries
- Each wave is a `List<GauntletSpawn>`
- Waves progress when all enemies from current wave are defeated
- Each wave can have different enemy compositions

### Continuous Spawning
- First spawn after `iContinuousGauntletSpawnStartAt` frames (default 240 = 4 seconds)
- Subsequent spawns every `iContinuousGauntletSpawnInterval` frames (default 80 = 1.33 seconds)
- `bQuickenContinuous` flag can accelerate spawn rate
- Spawns continue until the gauntlet's total enemy count is reached

## GauntletSpawn Structure
```csharp
// Each spawn entry defines:
// - Enemy type to spawn
// - Position/spawn point within room
// - Whether it's an elite variant
// - Delay before spawning
```

## Active Enemy Count
```csharp
public int ActiveEnemies
{
    get
    {
        int count = 0;
        foreach (Enemy e in lxEnemies)
        {
            // Skip specific boss types that shouldn't block clear
            // (Grindea, Bishop, Echo, Mimic stages)
            
            // Auto-skip enemies that are out of bounds or NaN position
            if (outOfBounds) e.bAllowSkipInRoguelike = true;
            
            if (!e.bDefeated && !e.bAllowSkipInRoguelike)
                ++count;
        }
        return count + iArbitraryEnemySpawnerBlock;
    }
}
```

Key: `iArbitraryEnemySpawnerBlock` allows adding "phantom" enemies that delay clear without actual entities (used for scripted sequences).

## Room Clear Grading
```csharp
public void RoomClear()
{
    int time = currentRoom.iActiveTimeInRoom;
    ArcadeGrade grade;
    
    if (time < iParTime)
        grade = S;                          // Under par time
    else if (time < iParTime * 1.5)
        grade = A;                          // Up to 1.5x par
    else if (time < iParTime * 2.0)
        grade = B;                          // Up to 2x par
    else
        grade = C;                          // Over 2x par
}
```

### Grade Thresholds
| Grade | Time Requirement |
|-------|-----------------|
| S | < par time |
| A | < 1.5x par time |
| B | < 2x par time |
| C | >= 2x par time |

### Arena Damage Grading
```csharp
float damageScore = (1.0 - damageTaken / baseMaxHP) * 4.0;
// Clamp to 0 minimum
ArcadeGrade damageGrade = (ArcadeGrade)(int)Math.Round(damageScore);
// 0 damage = 4 (S rank), full HP damage = 0 (C rank)
```

## Room Properties Affecting Spawns

### Double Gauntlet Logic
```csharp
// Chance for 2 gauntlets on a floor:
if (floorsWithoutDoubleGauntlets >= 0 &&
    random(3 - floorsWithoutDoubleGauntlets) == 0)
{
    numGauntlets = 2;
}
// Disabled on Easy difficulty
// 50% chance: GauntletContinuous vs GauntletWaves
```

### Difficult Enemy Flag
```csharp
// RoomProperties.AllowDifficultEnemy flag
// Budget per region (1-3 difficult enemies per floor)
// 25% chance per room to spend a difficult enemy slot
```

## Elite Spawning in Encounters
- Base elite chance: 5% per enemy
- `iHyperElitesCanSpawn` tracks available hyper-elite slots
- Elite enemies have enhanced stats and elite name suffixes
- Some enemies suppress elite spawns of related types
- Shared elite limits across enemy type groups

## Lood Spawning
```csharp
// After room clear, Loods can spawn:
// - Gold Lood, Health Lood, Item Lood, Talent Lood, Pin Lood
// bLoodSpawned flag prevents double-spawning
// Lood HP: 1000 (needs to be hit to release reward)
// Lood movement: Flies away using LoodAI.MyPeopleNeedMe()
```

## Score System
- `iScoreToReward` per room
- Room grade affects final score multiplier
- Bishop challenge `SRankOrDie` requires S-rank on every room
- Normal mode challenge timer has +10% leniency (`Misc_Arcade_NormalModeChallengeTimerLeniencyInPCT`)

## Archie Spawn System
```csharp
byte bySpawnArchieQueued = 4; // 4 = don't spawn
// After room clear, Archie NPC can be queued to spawn
// SpawnArchie(direction) places NPC in room for interaction
```

## Bagman Pattern (General Architecture)
The game uses "Bagmen" as room-level state controllers:
- `OpenGatesAtEnemyClear` - Main combat room controller
- `OpenGatesAtRoomClear` - Base class for any room-clearing condition
- Region-specific bagmen: `ArcadiaDojoBagman`, `DesertBotMidBagman`, etc.
- Housing: `HousingBagman`
- Arcade-specific: 58+ `ArcadeBagmen` files, 28+ `GhostShipBagmen`, 22+ `EndGameBagmen`

Each bagman handles:
1. Level loading (spawning enemies, placing objects)
2. Encounter management (wave tracking, clear detection)
3. Reward distribution (score, grade, loot)
4. Gate control (blocking/unblocking exits)

## Key Code Locations
- OpenGatesAtEnemyClear: `/LevelLoading/1ArcadeBagmen.cs`
- ArcadeBagmen (58 files): `/LevelLoading/*ArcadeBagmen.cs`
- GhostShipBagmen: `/LevelLoading/*GhostShipBagmen.cs`
- EndGameBagmen: `/LevelLoading/*EndGameBagmen.cs`
- EnemySpawner: `/Entities/Enemy/EnemySpawner.cs`
- Bagman base: `/src/Bagmen/Bagman.cs`

## Design Patterns Worth Stealing
- "Bagman" pattern: Each room type has its own state controller instance, keeping room logic modular
- Phantom enemy count (`iArbitraryEnemySpawnerBlock`) allows scripted sequences without fake entities
- Two gauntlet modes (discrete waves vs continuous) from the same system creates variety without code duplication
- S/A/B/C grading with simple time multipliers (1x/1.5x/2x of par) is intuitive and easy to tune
- `bAllowSkipInRoguelike` auto-skipping out-of-bounds enemies prevents softlocks
- The "floors without double gauntlets" counter creates escalating probability, preventing long streaks of same encounter type
- Damage-based grading as separate from time grading rewards both aggressive and defensive play
- Lood system (breakable reward entities) creates a gameplay moment from rewards instead of instant pickup
