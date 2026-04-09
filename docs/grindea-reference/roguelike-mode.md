# Roguelike / Arcade Mode Systems -- Secrets of Grindea Reference

## Overview
The roguelike mode (`RogueLikeMode` class, 2884 lines) is the game's arcade/endless mode. Players progress through procedurally generated floors of connected rooms, fighting enemies, completing challenges, finding shops, and fighting bosses. The system includes treats/curses, perks, chaos mode upgrades, floor bets, and a score/reward system.

## Floor Generation

### Room Count Formula
```csharp
int roomCount = 2 + currentFloor;
if (currentFloor > 2)
    roomCount = 4 + (currentFloor - 2) / 2;
if (roomCount > 8 + (currentFloor + 1) % 2)
    roomCount = 8 + (currentFloor + 1) % 2;
roomCount = Math.Max(5, roomCount);

// Region adjustments:
if (TimeTemple || GhostShip) --roomCount;
```

**Room count by floor:**
| Floor | Base Count |
|-------|-----------|
| 1 | 5 |
| 2 | 5 |
| 3 | 5 |
| 4 | 5 |
| 5 | 5 |
| 6 | 6 |
| 7 | 6 |
| 8+ | 7-9 (alternates) |

### Floor Layout
- Grid-based: `Room[width, height]` 2D array
- Start room placed at center-bottom (or center for GhostShip/TimeTemple)
- Rooms connected by random walk with direction changes
- If walk hits edge or 65% of rooms placed, teleport to random existing room and restart
- Max 100 retries before regenerating entire floor

### Gauntlet Rooms (Double Encounters)
```csharp
// Gauntlet chance: 1 in (3 - floorsWithoutDoubleGauntlets)
// Disabled on Easy difficulty
// Types: GauntletContinuous or GauntletWaves (50/50 random)
// Max 2 gauntlets per floor
```

### Difficult Enemy Allowance Per Region
| Region | Difficult Enemy Budget |
|--------|----------------------|
| Pillar Mountains | 1 + playerCount/2 |
| Evergrind East | 2 + playerCount - 1 |
| Halloween Forest | 2 + playerCount - 1 |
| Flying Fortress | 2 + playerCount - 1 |
| Winterland | 2 + playerCount - 1 |
| Season Temple | 2 + playerCount - 1 |
| Time Temple | 1 + (playerCount-1)/2 |
| Desert | 2 + playerCount - 1 |
| Ghost Ship | 1 + (playerCount-1)/2 |

### Shop Generation
```csharp
// Shop appears if: floorsWithoutShop >= 3 OR random(3 - floorsWithoutShop) == 0
// First floor never has shop
// Floor 11 never has shop
```

### Event Room Generation
```csharp
// Quest-triggered events at specific floors
// Random events: 25% base, 66% after 1 floor without, 100% after 2 floors without
// Floor 11 has no events
```

## Room Types
| Type | ID | Description |
|------|-----|-------------|
| Normal | 0 | Combat encounter room |
| Boss | 1 | Boss fight |
| Challenge | 2 | Mini-game / challenge room |
| TreasureRoom | 3 | Loot room |
| Shop | 4 | Buy items/equipment |
| StartingRoom | 5 | Floor entry point |
| Nurse | 6 | Healing station |
| EventRoom | 7 | Special event (fishing, NPC rescue, etc) |
| Archie | 8 | Special NPC room |

### Room Dimensions
```csharp
int roomWidth  = 300 + random(100);  // 300-399
int roomHeight = 140 + random(65);   // 140-204
// Max: 400 wide, 220 tall
// Boss rooms have custom sizes (e.g. GigaSlime: 400x200)
```

## Event Types (30 total)
```
None, Fishing, FindCandy, AlchemistTransmute, ShadierMerchant,
BuffShrine, SaveNPC_Tannie, SaveNPC_Pott, SaveNPC_LittleJ,
SaveNPC_Winato, SaveNPC_Mesido, SaveNPC_Archie, SaveNPC_Shinsai,
SaveNPC_8, Astrid_RedApple, Chix, BlessingShrine, FindBiline,
PinMerchant, BossChanger, Casino_SelectionEntry, CasinoRoulette,
CasinoChests, LoodGod, Spa, BootlegPotions, Maracas, IceCreamBar,
BishopRewardRoom, AevumTimeCrystal
```

## Boss Encounters (36 total)
```
VilyaElite, RegularGigaSlime, WhiteRabbyAndFriends, VilyaSolo,
Halloweed, BeeHive, TerrorWeed, PumpKing, CrystalChallenge,
Gund4m, Phaseman, Marino, FrostlingYeti, ToyMachine,
SummerAndAutumn, SeasonHydras, WinterElder, BlackFerrets,
QueenBee, PowerFlower, CursedPriestess, GiantThornWorm,
AncientMimic, RedGigaSlime, MarinoV2, SolGem, Remedi,
CptBones, EvilEye, Luke, Bishop, GrindeaP1, GrindeaP2,
GrindeaP3, GrindeaFull
```

Boss placement rule: Boss room must be at least `min(1 + currentFloor, 5)` rooms from start.

## Challenge Types (100+ varieties)
Categories:
- **Chicken Chase**: Herd chickens (region-specific variants)
- **Block Puzzles**: Push-block puzzles
- **Kill In Order**: Numbered enemies, kill sequentially
- **Survival/Dodge**: Avoid hazards (boars, bullets, thorns, ice)
- **Perfect Guard**: Precision blocking challenges
- **Destroy Rock**: Break objects
- **Open Chest**: Reach chest amid hazards
- **Kill At Same Time**: Synchronized kills
- **Sound Game**: Audio-based challenge
- **Question Game**: Trivia
- **Simon Says**: Memory pattern
- **Archery Game**: Target shooting (region variants)
- **Light Torches**: Puzzle

## Treats and Curses System

### Treats (Floor Modifiers - Positive)
| ID | Name | Effect |
|----|------|--------|
| 100 | More Treasure Rooms | Extra treasure rooms |
| 101 | No Elites | Elites disabled |
| 102 | Better Healing | +30% health orb, +30% treat healing |
| 103 | Free Time Crystal | Tai Ming time crystal event |
| 104 | Easy | Overall difficulty reduction |
| 105 | More Loods | +30% lood spawn chance |
| 106 | Cheaper Stores | -30% shop prices |

### Curses (Floor Modifiers - Negative)
| ID | Name | Effect |
|----|------|--------|
| 200 | More Elites | Increased elite spawn rate |
| 201 | No Health Orbs | Health orbs disabled |
| 202 | Icey Floors | Slippery ground |
| 203 | Enemy Corpse Hazards | Defeated enemies leave damaging zones |
| 204 | Fire At Edges | Fire at room borders |
| 205 | Start Room Blind | Blinded at room start |
| 206 | Random Mushroom Patches | Poison mushroom areas |
| 207 | Enemies From Higher Floors | Tougher enemy pool |
| 208 | Take Double Damage | 2x damage received |
| 209 | Hard | Overall difficulty increase |

## Room Bets (Bishop System)
| Bet | Effect |
|-----|--------|
| ThirtySecClear | Clear room in 30 seconds |
| NoDamageClear | Clear without taking damage |
| SlowPlayers | Players are slowed |
| DoubleMonsters | 2x enemy count |
| DoubleDamage | Enemies deal 2x damage |
| MonstersRegenerate | Enemies regenerate HP |

### Floor Bets
| Bet | Effect |
|-----|--------|
| ExtraEnemiesFromAboveFloor | Higher-tier enemies mixed in |
| OneExtraEliteEveryRoom | Guaranteed elite per room |
| TemporaryCatalystIncrease | Temporary catalyst boost |
| EnemiesHaveMoreLife | Enemy HP increase |

Floor bet trigger: `iCurrentFloor > 1 && random(11) == 0` (after Bishop encounter)

## Chaos Mode Upgrades

### Upgrade Types
| Type | Per-Upgrade Value |
|------|------------------|
| HPUp | +50 Max HP (and heals +50) |
| DamageUp | +10 ATK, +10 MATK |
| SpeedUp | +8 ASPD, +8 CSPD |
| MaxEPUp | +15 Max EP |
| EPRegUp | +22% EP Regen |
| TalentPoints | +2 Talent Points |
| Spell | Level up a spell (0->1, 1->5, 5->10) |

### Spell Level-Up in Chaos Mode
- Level 0 -> 1: Single level up, auto-equip to empty slot
- Level 1 -> 5: 4 level ups (offensive) or 2 level ups (utility)
- Level 5 -> 10: 5 level ups

## Score Rewards
| Score Threshold | Reward |
|----------------|--------|
| 70,000 | Paper Bag Hat |
| 200,000 | NPC: Robin Hood |
| 400,000 | Angry Eyebrows |
| 800,000 | NPC: Papa Guard |
| 2,000,000 | Cat Ears Hat |
| 3,500,000 | Fancy Beard |
| 5,000,000 | Turban Hat |

## Room Properties (Flags)
```
None = 0
AllowDifficultEnemy = 1
RoomVarianceA-L = 2-4096 (visual/layout variants)
GauntletWaves = 8192
GauntletContinuous = 16384
DontShowChallengeTimer = 32768
```

## Scoring System
- Each room has `enRoomGrade` (C through S)
- Arena damage grade and total grade tracked
- S-Rank rooms heal 20% HP (with shrine buff) or 6% (with pin)
- Score used for total score rewards and Bishop run challenges
- `SRankOrDie` challenge: S-rank every room or fail

## Enemy Threat System
```csharp
public class EnemyThreatAndMax
{
    int iThreat;           // Threat budget cost for this enemy type
    int iMax;              // Maximum of this enemy on screen
    int iEliteLimit;       // Max elites of this type (default 1)
    float fMaxBreakChance; // Chance to exceed max limit (default 5%)
    EnemyTypes enAcceptableReplacement; // Fallback if can't spawn
    List<EnemyTypes> lenSharesEliteLimitWith; // Shared elite caps
    List<EnemyTypes> lenEliteSuppressesSpawnOfAndSpawnSuppressesElite;
}
```

### Elite Spawning
- Base elite chance: 5% (`Enemy_BaseEliteChance`)
- Elite names use suffix system (12 general suffixes, 4 green-slime-specific)
- Prefix vs suffix depends on language setting

## Key Code Locations
- RogueLikeMode class: `/States/RogueLike.cs` (2884 lines)
- Floor generation: `/States/RogueLike.cs` line 725 (`TestGeneration`)
- PerkInfo.Init: `/States/RogueLike.cs` line 611
- Chaos upgrades: `/States/RogueLike.cs` line 366
- Room class: `/States/RogueLike.cs` line 2538
- Boss encounter mapping: `/States/RogueLike.cs` line 173

## Design Patterns Worth Stealing
- Floor generation uses constrained random walk with backtracking -- simple but produces varied layouts
- Threat budget system for enemies prevents "unfair" rooms while allowing difficulty variation
- Gauntlet rooms (waves vs continuous) add encounter variety without new room types
- Floor bet system creates risk/reward player agency mid-run
- Treat/Curse system acts as run modifiers without needing complex meta-systems
- Chaos mode's exponential spell leveling (0->1->5->10) makes each spell pickup feel transformative
- Room size randomization (300-400 x 140-205) prevents muscle memory while keeping combat readable
- The "floors without X" counter system ensures shops and events appear regularly without being predictable
- Score reward thresholds create long-term goals that persist across runs
