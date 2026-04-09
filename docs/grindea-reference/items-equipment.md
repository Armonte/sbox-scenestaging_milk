# Items and Equipment -- Secrets of Grindea Reference

## Overview
Items are managed through `ItemCodex` with a `ItemTypes` enum and `ItemDescription` objects. Equipment uses `EquipmentInfo` with stat changes and special effects. The system supports weapons (1H/2H melee, wands, bows, shields), armor (hats, facegear, accessories, shoes), and consumables.

## Item Description Structure
```csharp
public class ItemDescription
{
    ItemTypes enType;
    string sFullName;
    string sDescription;
    Texture2D txDisplayImage;
    List<ItemCategories> lenCategory;
    string sCategory;           // "Weapon", "Accessory", etc.
    ushort iInternalLevel;      // Level requirement / sorting
    byte byFancyness;           // Rarity tier (0-3+)
}
```

## Item Categories
```csharp
public enum ItemCategories
{
    SpecialEvent,
    Fish,
    KeyItem,
    GrantToServer,
    ItemGetWatcher90,
    // ... and more
}
```

## Equipment System

### Equipment Slots
```csharp
public enum EquipSlot
{
    Auto,           // Auto-assign
    // Weapon slots, shield, hat, facegear, accessory, shoes, etc.
}
```

### Stat Enum (What Equipment Can Modify)
```csharp
public enum StatEnum
{
    HP,             // Max HP
    EP,             // Max EP
    MaxEP,          // Same as EP
    ATK,            // Physical attack
    MATK,           // Magic attack
    DEF,            // Defense
    ASPD,           // Attack speed
    CSPD,           // Cast speed
    Crit,           // Critical hit chance
    CritDMG,        // Critical damage modifier
    EPRegen,        // EP regeneration rate
    ShldHP,         // Shield max HP
    ShldRegen,      // Shield recovery rate
    // Extended stats for buffs only:
    CritVulnerabilityFlat,
    CritVulnerabilityMultiplier,
    DamageResistance,
    ATKMultiplier,
    DEFMultiplier,
    MATKMultiplier,
    FlatMoveSpeed,
    KnockbackResistance,
}
```

### Equipment Stat Application
```csharp
// Adding equipment iterates stat changes:
foreach (var stat in equipment.deniStatChanges)
    AddStatBonus(stat.Key, stat.Value, equipment);

// HP changes maintain percentage:
float hpPct = iHP / iMaxHP;
iBaseMaxHP += hpValue;
iHP = (int)Math.Round(iMaxHP * hpPct);

// Shield regen stored as /100 (e.g., value 25 = +0.25 multiplier)
fShieldHPRecoveryMultiplier += value / 100f;
```

### Special Equipment Effects
```csharp
public enum SpecialEffect
{
    _Unique_ExtraDamagePerCard,      // Angel's Thirst: +3 ATK per card owned
    _Unique_LuckySeven_GuaranteedCrits, // Every 7th hit = guaranteed crit
    // Many more...
}
```

## Notable Equipment with SpellVariable Values

### Weapons
| Equipment | Effect | Value |
|-----------|--------|-------|
| Angel's Thirst | +ATK per card owned | 3 ATK per card |
| Blade of Echoes | Spawns echo clone | 1.5-3s spawn delay, 4s cooldown |
| Smash Light | Burn chance on hit | 50% |
| Plant Blade | Drop chance multiplier | 5x |

### Shields
| Equipment | Effect | Value |
|-----------|--------|-------|
| Wisp Shield | Projectile reflect damage | 2x modifier |
| Winter's Guard | Freeze on block | 40% chance, 80% slow |
| Camera Shield | Stun on perfect guard | 0.75s |
| Cog Shield | EP on guard | 10 regular, 20 perfect (1.5s diminish) |
| Shield of Dawn | Spin speed after guard | 1.5x regular, 2.5x PG (2s/5s duration) |

### Accessories
| Equipment | Effect | Value |
|-----------|--------|-------|
| Ice Pendant | Stronger ice spells | +10% |
| Giant Icicle | Slow on hit | 20% chance |
| Lightning Glove | Auto-lightning on hit | 60 frame cooldown, 20% MATK |
| Camera Lens | Crit after photo | +20% for 5s |
| Magic Battery | EP regen on cast | +85 regen for 1.8s (diminishes) |
| Golden Earrings | Gold drops | +30% |
| Empty Bottle | Potion recharge | -20% cooldown |
| Pan | Potion recharge | -10% cooldown |
| Roller Blades | Charge movement | +10% |
| Sailor Hat | Charge movement | +10% |
| Mushroom Slippers | EP regen | +75 boost |
| Thorn Mane | Spike distance | 14 pixels traveled per spike |
| Mystery Cube | Random proc | 15% per proc chance |
| Kobe's Tag | Summon block cost | -15% reduction |
| Fertilizer Hat | Insect Swarm duration | +15% |
| Triple A | EXP bonus | +2 levels |
| Bloodthirst Shoes | HP drain | 3s drain duration |
| Bones (Captain) | Damage | 50% of ATK+MATK |

### Bow System
| Setting | Value |
|---------|-------|
| Base Cooldown | 12 frames |
| Stretch Time | 15 frames |
| Base Quiver Size | 15 arrows |
| Upgrade Costs (Story) | 1500 / 5000 / 15000 / 50000 / 300000 |
| String Upgrade Cost | 100,000 |
| String Damage Bonus | +25% per upgrade |
| Arcade Upgrade Costs | 1000 / 3000 / 5000 |

## Rarity System
- `byFancyness` (byte): 0 = common, 1 = uncommon, 2 = rare, 3 = epic/unique
- Internal level (`iInternalLevel`) used for sorting and level gating
- Fanciness affects visual presentation (name color, drop sparkle)

## Crafting
Located in `/Items/Crafting.cs` - recipes combine materials into equipment.

## Card Album
Located in `/Items/Cards.cs` and related files:
- Cards are collected by defeating enemies
- Each enemy type has a card
- Multiple copies of same card stack bonuses
- `henCardAlbum.GetTotalCardAmount()` returns total cards for Angel's Thirst calculation

## Treasure Maps
Located in `/Items/TreasureMaps.cs` - special items that reveal hidden loot locations.

## Pre-Seeded Loot
Located in `/Items/PreSeededLoot.cs` - deterministic loot for specific encounters/chests.

## Key Code Locations
- ItemCodex: `/Items/ItemCodex.cs` (13163 lines)
- ItemDescription: `/Items/ItemDescriptions.cs`
- DropChance: `/Items/DropChance.cs`
- Equipment: `/Entities/Player/Equipment.cs`
- Inventory: `/Items/Inventory.cs`
- Crafting: `/Items/Crafting.cs`
- Cards: `/Items/Cards.cs`, `1Cards.cs`, `2Cards.cs`
- Badges/Pins: `/Items/1Badges.cs`, `Badges.cs`
- Equipment stat application: `/Stats n Attributes/BaseStats.cs` line 578
- Equipment special effects: `/Spells/SpellVariables.cs` lines 402-450

## Design Patterns Worth Stealing
- Equipment stat changes as dictionary of StatEnum->int makes adding new equipment trivial
- HP changes maintaining percentage prevents "equip full HP gear then unequip" exploits
- Special effects as an enum list on equipment allows stacking and easy checking
- Fancyness as a simple byte keeps rarity lightweight while being extensible
- Internal level for sorting creates a natural progression order in menus
- Shield recovery as a per-tick fraction with multiplier and flat bonus creates multiple ways to improve shield builds
- The "diminishing gain" mechanic on some equipment (Magic Battery, Cog Shield) prevents them from being broken while rewarding skill
- Bow having separate cooldown from melee creates a natural rhythm of alternating attacks
- Card album feeding into equipment effects (Angel's Thirst) creates cross-system synergies
