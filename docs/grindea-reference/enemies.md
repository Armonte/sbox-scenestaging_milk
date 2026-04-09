# Enemies -- Secrets of Grindea Reference

## Overview
Enemies are defined in `EnemyCodex.cs` via `EnemyDescription` objects. Each enemy has a level, base HP, threat/tankiness/attack ratings, card drop chance, loot table, and AI behavior. The codex has 100+ enemy types across 9 world regions. Enemy instances are created through a massive factory method (`GetEnemyInstance`, 162K instructions).

## Enemy Description Structure
```csharp
new EnemyDescription(EnemyTypes type, string nameHandle, int level, int baseHP)
{
    sOnHitSound, sOnDeathSound     // Audio
    iCardDropChance                 // out of 100,000 (100000 = guaranteed)
    fHowAnnoyingItIs               // AI targeting priority weight
    fTankiNessGrade                 // Tankiness rating for threat system
    fAttackNessGrade                // Attack power rating
    v2ApproximateOffsetToMid        // Hitbox center offset
    v2ApproximateSize               // Approximate collision size
    bFilthyRich                     // Drops extra gold
    bDropsSeed                      // Can drop seeds
    bCanHavePlusters                // Can have pluster modifiers attached
    lxLootTable                     // List<DropChance> regular drops
    lxHiddenLootTable               // List<DropChance> special/hidden drops
}
```

## Enemy Data Table (All Documented Enemies)

### Starting Area (Pillar Mountains / Evergrind)

| Enemy | Level | HP | Card% | Tank | ATK | Annoy | Loot |
|-------|-------|-----|-------|------|-----|-------|------|
| Green Slime | 2 | 60 | 0.5% | 0.5 | 1.0 | 0.35 | Sticky Mucus (20%), Apple (60%), Slime Hat (0.4%) |
| Red Slime | 3 | 100 | 0.4% | 1.5 | 2.0 | -- | Red Goo (20%), Apple (60%), Red Slime Hat (1%) |
| Rabby | 3 | 110 | 0.3% | 1.0 | 2.0 | 0.35 | Carrot (50%), Fur (20%), Rabbit's Foot (1%) |
| Mrs. Bee | 5 | 50 | 0.1% | 1.0 | 2.0 | 2.0 | Stinger (50%), Honey (60%) |
| Bloomo | 4 | 220 | 0.125% | 2.5 | 2.0 | 0.35 | Blue Petal (15%), Root (25%), Bloomo Seed (hidden 1%) |
| Boar | 5 | 500 | 0.2% | 3.5 | 3.0 | 2.0 | Tusk (15%), Tough Skin (25%) |

### Special / Boss Enemies

| Enemy | Level | HP | Category | Notes |
|-------|-------|-----|----------|-------|
| Elder Rabby (White) | 10 | 1,500 | Boss | FilthyRich, drops Carrot Sword (100%) |
| Elder Boar | 20 | 10,000 | Boss | FilthyRich |
| Bee Hive | 5 | 180 | Boss | Drops 3x Honey (100%, 60%, 30%) |
| Queen Bee | 15 | 7,500 | Miniboss | FilthyRich, 5x Honey drops |
| Bee Guard | 13 | 600 | Regular | No plusters allowed |

### Lood Types (Breakable Reward Entities)

| Type | Level | HP | Card% | Purpose |
|------|-------|-----|-------|---------|
| Gold Lood | 1 | 1,000 | 0 | Gold reward |
| Health Lood | 1 | 1,000 | 0 | Health orb reward |
| Item Lood | 1 | 1,000 | 0 | Item drop reward |
| Talent Lood | 1 | 1,000 | 0 | Talent point reward |
| Pin Lood | 1 | 1,000 | 0 | Pin/badge reward |

## Enemy Categories
```csharp
public enum Category
{
    Regular,    // Standard enemies
    Miniboss,   // Mini-boss encounters
    Boss        // Full boss fights (implied by bFilthyRich)
}
```

## Enemy Types Enum (Selection of Key Entries)
The full `EnemyTypes` enum contains 200+ entries. Key groupings:
- **Slimes**: GreenSlime, RedSlime, OrangeSlime, BlueSlime, ShadowSlime, PapaSlime, RedPapaSlime
- **Wildlife**: Rabbi, Boar, TwilightBoar, Bee, BeeHive, QueenBee, Blomma
- **Halloween**: JackOLantern, Halloweed, TerrorWeed, Pumpking, Ghosty
- **Flying Fortress**: PhaseMan, GundamMain, Crystal enemies
- **Winter**: Frostling, Yeti (SmashieBashie), ScoundrellKid
- **Season Temple**: AutumnFae, WinterFae, SeasonHydra variants, Season Knights/Mages
- **Mt. Bloom**: Mushroom enemies, Larvacid, Spinsect, MossClump, QueenBee
- **Time Temple**: Statues, Mimic, TempleGuardian, Echo, GiantWorm
- **Desert**: Cacute, Solem, SolGem, BossTroll
- **Ghost Ship**: Skeleton types, Hauntie, CaptainBones, EvilEye, Luke
- **Final**: Bishop, Grindea, Zhamla, Dad (final boss)

## Enemy Threat System (Roguelike)
```csharp
public class EnemyThreatAndMax
{
    int iThreat;        // How much "room budget" this enemy costs
    int iMax;           // Maximum concurrent spawns
    int iEliteLimit;    // Max elite versions (default 1)
    float fMaxBreakChance;  // 5% chance to exceed max limit
    EnemyTypes enAcceptableReplacement;  // Fallback spawn type
}
```

## Elite Enemy System
- Base elite chance: **5%** per enemy spawn
- Elites get name suffixes (or prefixes in some languages)
- 12 general elite suffixes, 4 green-slime-specific suffixes
- Elite suppression: some enemy types prevent elite spawns of related types
- Shared elite limits: some enemy groups share a combined elite cap

## AI Behavior Files
Located in `/AI/Bossar/` for bosses:
- `DadBoss.cs` - Final boss (Dad) with elemental phases
- `Bishop.cs` - Bishop boss with bet mechanics
- `CaptainBones.cs` - Ghost Ship captain
- `EnragedToyMachine.cs` - Toy Machine boss
- `EvilEye.cs` - Ghost Ship evil eye
- `FinalSlime.cs` - Papa Slime boss
- `FreddyTeddy.cs` - Freddy/Teddy bear boss

Located in `/Behaviours/`:
- `BlommaAI.cs` - Flower enemy
- `CacuteAI.cs` - Cactus enemy (Desert)
- `GrindeaHandAI.cs` - Final boss hand phase
- `ZhamlaBraazletAI.cs` - Zhamla boss AI
- Various NPC behaviors

## Enemy Spell System
Enemy-specific spells (IDs 40000-40120):
- Jack O'Lantern Flame, Halloweed Root
- Ball Spark Homing, Crystal Shield Projectile
- Phaseman bullets (Red, Blue, Special, Mega)
- Gundam Rockets and Mega Bullets
- Tornado, Linear Shockwave
- Season Mage/Hydra projectiles
- Temple Guardian fan attacks (Main, Side, Lightspeed, Homing, Bending)
- Zhamla attacks: Meteor Rain, Clone Dash, Bug Swarm, Giga Earth Spike
- Dad attacks: Lightning, Vine Slam, Fireball, Ice Spiral, Root
- Pluster system (10 types: Laser, ATK Up, Wisp, Thorns, Firebomb, Enemy Spawn, Echo, Mage Projectile, Healing)

## Key Code Locations
- EnemyCodex.Init: `/Entities/Enemy/EnemyCodex.cs` line 30
- EnemyDescription class: `/Entities/Enemy/EnemyDescription.cs`
- Enemy class: `/Entities/Enemy/Enemy.cs` (651 lines)
- Boss class: `/Entities/Enemy/Boss.cs` (33 lines)
- Enemy spawner: `/Entities/Enemy/EnemySpawner.cs`
- AI behaviors: `/AI/Bossar/*.cs` and `/Behaviours/*.cs`
- Enemy spells: `/Spells/SpellInstanceCodex.cs` lines 7820-7940

## Design Patterns Worth Stealing
- `fHowAnnoyingItIs` as a spawning weight prevents rooms full of annoying enemies -- elegant annoyance budget
- `fTankiNessGrade` and `fAttackNessGrade` create a 2D difficulty profile per enemy, useful for encounter building
- Threat budget system with max-break-chance (5%) adds controlled randomness -- usually balanced, occasionally wild
- Card drop chances as /100000 integers avoid floating point issues while allowing very rare drops (0.1%)
- Loot table with hidden loot table separation keeps special drops discoverable
- Acceptable replacement system prevents spawn failures -- always has a fallback
- Pluster system (10 modifier types attachable to enemies) adds variety without needing new enemy types
- Elite suffix/prefix system with language awareness is a nice touch for procedural naming
