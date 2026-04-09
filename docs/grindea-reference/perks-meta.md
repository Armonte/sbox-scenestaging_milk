# Perks and Meta-Progression -- Secrets of Grindea Reference

## Overview
Perks are purchased with Essence (meta-currency) before starting an arcade run. Each perk costs essence and provides a starting bonus. Players start with 1 perk slot and can unlock up to 3. Some perks are gated behind progression flags. Essence converts at 100,000 gold per essence point.

## All Perks

### Starting Equipment (10 essence each)
| Perk | ID | Cost | Effect |
|------|-----|------|--------|
| Iron Sword | 1 | 10 | Start with Iron Sword (1H melee) |
| Claymore | 2 | 10 | Start with Claymore (2H melee) |
| Apprentice Rod | 13 | 10 | Start with Apprentice Rod (wand) |
| Iron Shield | 3 | 10 | Start with Iron Shield |

### HP Perks
| Perk | ID | Cost | Effect |
|------|-----|------|--------|
| Five More HP | 4 | 5 | +25 flat max HP (SpellVariable: 25) |
| Five Percent More HP | 5 | 25 | +5% max HP |

### Arrow Perks
| Perk | ID | Cost | Effect |
|------|-----|------|--------|
| More Max Arrows | 10 | 15 | +10 max arrows |
| More Arrow Drops | 9 | 20 | +30% arrow drop chance |
| More Arrow Damage | 16 | 25 | +15% arrow damage |

### Challenge/Timer Perks
| Perk | ID | Cost | Effect |
|------|-----|------|--------|
| Longer Challenge Timers | 8 | 50 | +10% time on challenge room timers (Normal mode) |
| Favor Unfinished Challenges | 27 | 50 | Prioritize challenge rooms not yet completed (Solo only) |

### Economy/Loot Perks
| Perk | ID | Cost | Effect |
|------|-----|------|--------|
| More Normal Items | 6 | ? | More non-rare items drop |
| Gold On Every Floor | 19 | 25 | Guaranteed gold reward each floor |
| Increased Card Drop Rate | 20 | 20 | +50% card drop chance |
| Extra Items In Shop | 21 | 40 | +1-2 extra items in shops |
| Start With Health Potion | 22 | 30 | Begin run with health potion |

### Patient Perks (Scaling Bonuses)
| Perk | ID | Cost | Effect |
|------|-----|------|--------|
| Patient Sharpening | 23 | 30 | ATK scaling bonus over time |
| Patient Brutality | 24 | 30 | Crit scaling bonus over time |
| Patient Wisdom | 25 | 30 | MATK scaling bonus over time |

### Healing/Recovery
| Perk | ID | Cost | Effect |
|------|-----|------|--------|
| Regen After Rooms | 11 | ? | HP regeneration after clearing rooms |
| More Regen After Floors | 12 | ? | Increased HP recovery between floors |

### Level/Progression
| Perk | ID | Cost | Effect |
|------|-----|------|--------|
| Start At Level Two | 7 | ? | Begin run at level 2 |

### Conditional Perks (Require Progression Flags)

| Perk | ID | Cost | Unlock Condition |
|------|-----|------|-----------------|
| More Fishing Rooms | 26 | 25 | Aquarium built in Arcadia |
| Pet Whisperer | 14 | 20 | Talked to Weiv NPC (slows pet speed by 15%) |
| Only Pins After Challenges | 15 | 30 | Pins unlocked |
| Chance At Pin After Battle Room | 18 | 30 | Pins unlocked (+5% pin drop after battle rooms) |
| More Loods | 17 | 25 | Has seen a Lood (+50/45/40/35% loot based on player count) |

### Perk Slot Unlocks
| Perk | ID | Cost | Effect |
|------|-----|------|--------|
| Perk Slot 2 | 1000 | 50 | Unlock 2nd perk slot |
| Perk Slot 3 | 1001 | 150 | Unlock 3rd perk slot |

## Essence Economy
| Action | Value |
|--------|-------|
| Essence Buy Price | 750 gold per essence |
| Essence Sell Price | 500 gold per essence |
| Gold Essence Upgrade Cost | 40 essence |
| Points per Essence | 100,000 |

## Arcadia Town Buildings (Meta-Progression)
Costs in gold to build town structures that provide permanent benefits:

| Building | Cost |
|----------|------|
| Player House | 45,000 |
| Tavern | 35,000 |
| Well | 20,000 |
| Cinema | 26,000 |
| Alchemist | 25,000 |
| Aquarium | 42,000 |
| Bank | 35,000 |
| Arena | 30,000 |
| Fae Tree | 50,000 |
| Park | 35,000 |
| Dojo | 30,000 |
| Statue | 40,000 |
| Clock Tower | 45,000 |
| Farm | 38,000 |
| Clear Trees (Right) | 25,000 |
| Clear Trees (Bottom) | 25,000 |
| Repair Bridge | 25,000 |

### Bank Insurance System
- Kept gold per upgrade level: 25%
- Insurance upgrade cost: 100,000 gold

### Pet System
| Setting | Value |
|---------|-------|
| Pet Lure Cost | 2,000 gold |
| Allowed Pets Per Level | 5 |
| Base Cost for Max Increase | 10,000 gold |
| Cost Increase Per Step | 2,500 gold |
| Max Cost | 25,000 gold |
| Max Allowed Pets | 100 |
| Egg Hatch Time | 60 minutes |
| Dragon Egg Cost | 25,000 gold |

### Quemi Upgrade
- Cost: 100 essence per upgrade

## Pin Unlock Costs
| Currency | Cost |
|----------|------|
| Silver Points | 10 |
| Talent Points | 10 |
| Gold Points | 2 |
| Money | 250,000 gold |
| Drop Chance in Story | 1% |

## Treat Achievement Threshold
- Achievement disabled when treat modifier exceeds 0.7 (70%)

## Key Code Locations
- PerkInfo.Init: `/States/RogueLike.cs` line 611
- Perk enum: `/States/RogueLike.cs` line 321
- Chaos upgrades: `/States/RogueLike.cs` line 355
- Town costs: `/Spells/SpellVariables.cs` lines 583-597
- Essence economy: `/Spells/SpellVariables.cs` lines 600-608

## Design Patterns Worth Stealing
- Perk slot unlocking creates two dimensions of progression: what perks you have AND how many slots
- Conditional perk unlocks tied to in-game discovery (see a Lood, meet an NPC) rewards exploration
- Patient perks that scale over time reward longevity in a run, counterbalancing "all-in early" strategies
- Town building as meta-progression gives tangible goals between runs
- Essence being both earnable and purchasable (with gold) gives two paths to progression
- The bank's "kept gold percentage" per upgrade creates a meaningful insurance system that reduces run-loss frustration
- More Loods perk scaling by player count (50/45/40/35%) maintains balance in multiplayer
- Pin drops from battle rooms (5%) add excitement to otherwise routine combat
