# Drops and Economy -- Secrets of Grindea Reference

## Overview
The drop system uses `DropChance` objects with integer probabilities out of 100,000. Each enemy has a `lxLootTable` (normal drops) and `lxHiddenLootTable` (special drops). The economy includes gold coins, potions, health orbs, card drops, and an essence meta-currency.

## DropChance Structure
```csharp
public class DropChance
{
    public const int GuaranteedDrop = 100000;
    public int iChance;              // out of 100,000
    public ItemCodex.ItemTypes enItemToDrop;
    public int iRolls = 1;           // number of times to roll this drop
}
```

### Drop Probability Examples
| Value | Percentage | Usage |
|-------|-----------|-------|
| 100000 | 100% | Guaranteed drops (quest items, boss loot) |
| 80000 | 80% | Very common drops |
| 60000 | 60% | Common drops (Apple from slimes) |
| 50000 | 50% | Common material drops |
| 25000 | 25% | Uncommon material drops |
| 20000 | 20% | Standard material drops |
| 15000 | 15% | Uncommon drops |
| 10000 | 10% | Semi-rare drops |
| 1000 | 1% | Rare drops (cosmetic hats) |
| 400 | 0.4% | Very rare drops (Slime Hat) |

## Coin Values
| Coin Type | Gold Value |
|-----------|-----------|
| Small Silver | 1 |
| Big Silver | 5 |
| Small Gold | 10 |
| Big Gold | 15 |

## Health Orb System
| Setting | Value |
|---------|-------|
| Story Mode Healing | 20% of max HP |
| Roguelike Mode Healing | 30% of max HP |
| Low Catalyst Bonus | +30% healing |
| Treat Bonus | +30% healing |
| Health Insurance Talent | +10/20/30% per level |
| Critical HP Threshold | 33.3% of max HP |

## Potion System

### Health Potion
| Setting | Value |
|---------|-------|
| Story HP Gained | 15% of max HP |
| Expert Mode HP Gained | 10% of max HP |
| Arcade + Catalyst | 20% of max HP |
| Arcade No Catalyst | 35% of max HP |
| Recharge Time | 26 seconds |

### Damage Potion
| Setting | Value |
|---------|-------|
| Duration | 12 seconds |
| DMG Increase | 15% (ATK and MATK) |
| Recharge Time | 10 seconds |

### Arrow Potion
| Setting | Value |
|---------|-------|
| Arrows Gained | 4 |
| Recharge Time | 10 seconds |

### Wealth Potion
| Setting | Value |
|---------|-------|
| Gold Drop Increase | +60% |
| Duration | 20 seconds |
| Recharge Time | 15 seconds |

### Loot Potion
| Setting | Value |
|---------|-------|
| Drop Chance Increase | +20% |
| Duration | 20 seconds |
| Recharge Time | 15 seconds |

### Chicken Potion
| Setting | Value |
|---------|-------|
| Duration | 4.5 seconds |
| Post-Invulnerability | 1 second |
| Recharge Time | 26 seconds |
| Movement Speed Bonus | 1x (base), 1.25x (fleeing) |

### Crit Potion
| Setting | Value |
|---------|-------|
| Crit Increase | +20% |
| Duration | 15 seconds |
| Recharge Time | 10 seconds |

### Speed Potion
| Setting | Value |
|---------|-------|
| ASPD/CSPD Increase | +15 |
| Duration | 18 seconds |
| Recharge Time | 10 seconds |

### Energy Potion
| Setting | Value |
|---------|-------|
| EP Gained | 50% of max EP |
| Recharge Time | 15s (Story), 25s (Arcade) |

### Lightning Potion
| Setting | Value |
|---------|-------|
| Duration | 15 seconds |
| Recharge Time | 15 seconds |
| Sparks Spawned | 4 (main), 2 (on other potion use) |

### Potion Enhancement (Stacking)
| Setting | Per Copy |
|---------|----------|
| Duration Increase | +10% |
| Cooldown Reduction | +10% |
| Effect Increase | +10% |

### General Potion Settings
| Setting | Value |
|---------|-------|
| Recovery Outside Combat | 20% |
| Cost to Buy Second Potion | 2,000 gold |
| Gilded Flasks Recovery Bonus | +25% |
| Alchemist Talent Bonus | +5/10/15% recharge speed |

## Arcade Super Potions
| Potion | Effect | Duration |
|--------|--------|----------|
| Super Damage | +20% ATK/MATK | -- |
| Super Speed | +20 ASPD/CSPD | -- |
| Super Energy | +300 EP regen | 30 seconds |
| Super Lightning | 4 conduits | 90 seconds |

## Card Drop System
- Each enemy has `iCardDropChance` (out of 100,000)
- Example rates: Green Slime 500 (0.5%), Rabby 300 (0.3%), Bee 100 (0.1%)
- Perk "Increased Card Drop Rate" adds +50%
- Cards provide passive stat bonuses when equipped

### Example Card Effects
| Card | Effect |
|------|--------|
| Yeti | 3% chill chance |
| Scoundrel | -5% projectile damage taken |
| Rogue | 5% extra coin drop |
| Blue Slime | +15% freeze/chill duration |
| Present | 1% gift box drop |
| Gift | 5% store discount |
| Season Knights | +3 ASPD, +5 ATK, +4 DEF |
| Season Mages | +5 CSPD, +5 EP, +5 MATK |
| Boar | +15% charge movement |
| Spinsect | +5 DEF |
| Toxic Tulip | -15% debuff duration |
| Shroomie | +25% crit when blinded |
| Larvacid | +10 ASPD for 6s at <20% HP |
| Thorn Worm | 40% proc chance, 20% ATK+MATK damage |
| Echo | +15% armor penetration |
| Moss | +5 max EP |
| Statue | +6 DEF |
| Monkey | +50% food drop rate |
| Orange Slime | +1 ASPD/stack, max 10, 5s |
| Sand Raven | -15% slow reduction |
| Crabby | +10 DEF |
| Skeleton Mage | +20% EP regen |
| Skeleton Warrior | +5% basic attack damage |
| Hauntie | +2% dodge chance |
| Red Slime | +30% slow resistance |

## Equipment Special Effects
| Equipment | Effect |
|-----------|--------|
| Golden Earrings | +30% gold drops |
| Plant Blade | 5x drop chance multiplier |
| Triple A | +2 EXP level |
| Empty Bottle | -20% potion recharge |
| Pan | -10% potion recharge |

## Arcade Shop Economics
| Setting | Value |
|---------|-------|
| Shadier Merchant Discount | 50% |
| Treat: Shop Cost Reduction | -30% |
| Ice Cream Price | 1,000 gold |

## Lood Drop Modifiers (Arcade)
| Setting | Value |
|---------|-------|
| More Loods perk (1P) | +50% spawn chance |
| More Loods perk (2P) | +45% spawn chance |
| More Loods perk (3P) | +40% spawn chance |
| More Loods perk (4P) | +35% spawn chance |
| Treat: More Loods | +30% spawn chance |
| Pin: More Loods HP Increase | +70% |
| Pin: More Loods Chance | +100% |

## Key Code Locations
- DropChance class: `/Items/DropChance.cs`
- Enemy loot tables: `/Entities/Enemy/EnemyCodex.cs` (throughout Init())
- Potion values: `/Spells/SpellVariables.cs` lines 535-571
- Card values: `/Spells/SpellVariables.cs` lines 451-486
- Coin values: `/Spells/SpellVariables.cs` lines 523-526
- Health orb values: `/Spells/SpellVariables.cs` lines 531-534
- Equipment effects: `/Spells/SpellVariables.cs` lines 402-450
- Lood modifiers: `/Spells/SpellVariables.cs` lines 510-513, 645-646

## Design Patterns Worth Stealing
- Integer probabilities (/100000) avoid floating point comparison issues and allow very granular drop rates
- Potion recharge system instead of consumable inventory prevents hoarding while maintaining strategic use
- Potion enhancement stacking (+10% per copy) makes duplicate potions meaningful instead of wasted
- Health orb healing as % of max HP scales naturally with progression
- Card drop chances varying widely (0.1% to 0.5%) creates natural rarity tiers without an explicit rarity system
- Lood scaling by player count prevents multiplayer from being strictly better for farming
- Hidden loot tables allow surprise discovery drops that don't clutter the main drop display
- "Recovery outside combat" mechanic (20%) ensures potions are always somewhat available after fights
