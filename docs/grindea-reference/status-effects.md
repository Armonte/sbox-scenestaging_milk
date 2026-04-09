# Status Effects -- Secrets of Grindea Reference

## Overview
Status effects are tracked via `BaseStats.StatusEffectSource` enum (101 entries) and stored in two dictionaries: `denxActiveEffects` (stat-modifying buffs/debuffs with duration) and `deniStatusChangeDuration` (non-stat status conditions like Freeze, Stun, Poison). Effects can conflict -- e.g., only one slow level can be active at a time (higher replaces lower).

## All Status Effects (StatusEffectSource enum)

### Slow Levels (0-4) -- Mutually Exclusive
| Name | ID | Notes |
|------|-----|-------|
| SlowLv1 | 0 | Lightest slow |
| SlowLv2 | 1 | |
| SlowLv3 | 2 | |
| SlowLv4 | 3 | |
| SlowLv5 | 4 | Heaviest slow |

Only one slow level can be active at a time. Higher level replaces lower. If a lower level is applied while higher is active, it is ignored.

### Protection & Ice
| Name | ID | Notes |
|------|-----|-------|
| ProtectLv1 | 5 | Light Protect spell |
| Freeze_PLAYERONLY | 6 | Freeze applied to players |
| Chilled | 7 | Reduces animation speed, tints blue |
| ChilledCritDebuff | 8 | Frozen enemies take +50% crit |

### Speed Buffs
| Name | ID | Notes |
|------|-----|-------|
| HasteASPD | 9 | Haste spell ASPD component |
| HasteCSPD | 10 | Haste spell CSPD component |

### Potion Buffs
| Name | ID | Notes |
|------|-----|-------|
| DamagePotionATK | 11 | Damage potion ATK boost (+15% ATK for 12s) |
| DamagePotionMATK | 12 | Damage potion MATK boost |
| SuperDamagePotionATK | 13 | Arcade super damage potion (+20% ATK) |
| SuperDamagePotionMATK | 14 | Arcade super damage potion MATK |
| SpeedPotionASPD | 15 | Speed potion ASPD (+15 ASPD for 18s) |
| SpeedPotionCSPD | 16 | Speed potion CSPD |
| SuperSpeedPotionASPD | 17 | Arcade super speed potion (+20) |
| SuperSpeedPotionCSPD | 18 | Arcade super speed potion CSPD |
| SuperEnergyPotionEPReg | 19 | Super energy potion (+300 EP regen for 30s) |
| ManaPotionA | 20 | Energy potion (50% EP restore) |
| ManaPotionB | 21 | Energy potion part B |
| DefensePotion | 22 | Defense potion |
| CritPotion | 23 | Crit potion (+20% crit for 15s) |
| WealthPotion | 24 | Gold drop +60% for 20s |
| LootPotion | 25 | Loot drop +20% for 20s |
| ChickenPotion | 26 | Chicken form (invulnerable 4.5s) |

### Berserk Mode Stacks
| Name | ID | Notes |
|------|-----|-------|
| BerserkModeASPD | 27 | Per-hit ASPD stacks (4/4/5 per hit, max 7/10/10) |
| BerserkModeCRIT | 28 | Per-hit crit stacks (0/2/3 per hit at Silver/Gold) |
| BerserkModeDMG | 29 | Base damage boost (25% Lv1, 35% Lv4) |
| BerserkModeDEF | 30 | Defense debuff (15/10/10/30% at L1-L4) |

### Miscellaneous Combat
| Name | ID | Notes |
|------|-----|-------|
| Slipping | 31 | Ice floor sliding |
| Turtle | 32 | Turtle talent DEF buff while channeling |
| Talent_LastStand | 33 | Last Stand DEF buff (below 20% HP) |
| Talent_LastBreath | 34 | Last Breath ATK buff (below 20% HP) |
| Talent_LastSpark | 35 | Last Spark MATK buff (below 20% HP) |

### Damage Over Time
| Name | ID | Notes |
|------|-----|-------|
| Burning | 36 | Fire damage over time |
| Poison | 64 | Standard poison |
| WeakPoison | 65 | Reduced poison |
| Aciding | 66 | Acid DOT |

### Talent Procs
| Name | ID | Notes |
|------|-----|-------|
| Talent_Manaburn | 37 | Manaburn MATK buff (EP > 50%) |
| Talent_SnapCast | 38 | Snap Cast MATK buff |
| Talent_Brawler | 39 | Brawler DEF buff on melee hit |
| Talent_BloodThirstASPDIncrease | 77 | Bloodthirst ASPD on kill |
| Talent_BloodThirstMoveSpeedIncrease | 78 | Bloodthirst movement speed on kill |
| Talent_ComboStarter | 79 | Combo Starter crit buff after skill |

### Marked / Special
| Name | ID | Notes |
|------|-----|-------|
| Marked | 40 | Death Mark target |
| Stunned | 63 | Standard stun (cannot act) |
| FrostyFriendProtect | 41 | Frosty Friend guard stance buff |
| FrostyFriendFrenzy | 42 | Frosty Friend frenzy mode buff |

### Card Effects
| Name | ID | Notes |
|------|-----|-------|
| LarvaCard | 67 | Larvacid card ASPD buff (+10 ASPD for 6s when below 20% HP) |
| LarvaCardCSPD | 68 | Larvacid card CSPD component |
| ShroomieCard | 69 | Shroomie card crit when blinded (+25%) |

### Buff Spell Effects
| Name | ID | Notes |
|------|-----|-------|
| BuffATK_ATK | 70 | Empower spell ATK (12.5% + 2.5%/lv for 15s) |
| BuffATK_MATK | 71 | Empower spell MATK component |
| BuffSPD_ASPD | 72 | Haste buff ASPD (7.5% + 2.5%/lv for 15s) |
| BuffSPD_CSPD | 73 | Haste buff CSPD |
| BuffDEF_DEF | 74 | Fortify buff DEF (16% + 4%/lv for 15s) |
| BuffDEF_ShieldReg | 75 | Fortify shield regen (25% + 25%/lv) |
| BuffDEF_EPReg | 76 | Fortify EP regen (7.5% + 2.5%/lv) |

### Potion Tracking
| Name | ID | Notes |
|------|-----|-------|
| NewPotion_HEALTH | 80 | Health potion cooldown tracker |
| NewPotion_DAMAGE | 81 | Damage potion cooldown tracker |
| NewPotion_ARROWS | 82 | Arrow potion cooldown tracker |

### Arcade Shrine Buffs (Duration: 6000s = 100 minutes)
| Name | ID | Stat Modified | Value |
|------|-----|---------------|-------|
| ShrineBuffATK_ATK | 83 | ATK | 0 (unused?) |
| ShrineBuffATK_MATK | 84 | MATK | 0 |
| ShrineBuffATK_CRIT | 85 | Crit | +100% |
| ShrineBuffASPD_ASPD | 86 | ASPD | +45 |
| ShrineBuffASPD_CSPD | 87 | CSPD | +45 |
| ShrineBuffMSPD | 88 | Move Speed | +1.0 |
| ShrineBuffEPReg | 89 | EP Regen | +175% |
| ShrineBuffKnockback | 90 | Knockback Res + DEF | +7 KB res, +50 DEF |
| ShrineBuffDEF | 91 | DEF | +50 |
| ShrineBuffShield | 92 | Shield | +300% regen, +4 PG frames, 25% dmg redux |
| ShrineBuffSRankHeal | 93 | Heal | 20% HP on S-rank |

### Equipment Procs
| Name | ID | Notes |
|------|-----|-------|
| Equipment_CameraLensCritIncrease | 94 | Camera Lens: +20% crit for 5s |
| Equipment_MagicBatteryEPRegIncrease | 95 | Magic Battery: +85 EP regen for 1.8s |
| Equipment_MushroomSlippersEPRegIncrease | 97 | Mushroom Slippers: +75 EP regen |

### Card Procs
| Name | ID | Notes |
|------|-----|-------|
| Card_OrangeSlimeASPDIncrease | 96 | Orange Slime card: +1 ASPD/stack, max 10, 5s |

### Badge (Pin) Effects
| Name | ID | Notes |
|------|-----|-------|
| Badge_SPDWhenCastingSpell_ASPD | 97 | Pin: ASPD on spell cast |
| Badge_SPDWhenCastingSpell_CSPD | 98 | Pin: CSPD on spell cast |
| Badge_CritDMGAfterBasicAttack | 99 | Pin: Crit DMG after basic attack |
| Badge_ASPDAfterBasicAttack | 100 | Pin: ASPD after basic attack |
| Badge_CSPDAfterBasicAttack | 101 | Pin: CSPD after basic attack |
| Badge_GoldToDamageATK | 102 | Pin: Gold converts to ATK |
| Badge_GoldToDamageMATK | 103 | Pin: Gold converts to MATK |
| Badge_GoldToSpeedASPD | 104 | Pin: Gold converts to ASPD |
| Badge_GoldToSpeedCSPD | 105 | Pin: Gold converts to CSPD |
| Badge_PGIncreasesATKForRestOfRoom | 106 | Pin: PG = permanent ATK for room |
| Badge_PGIncreasesMATKForRestOfRoom | 107 | Pin: PG = permanent MATK for room |
| Badge_StrongerForOneFloorATK | 108 | Pin: +100% ATK for one floor |
| Badge_StrongerForOneFloorMATK | 109 | Pin: +100% MATK for one floor |
| Badge_StrongerForOneFloorDEF | 110 | Pin: +50 DEF for one floor |
| Badge_MountingCircleDamage_ATK | 111 | Pin: +15% ATK per circle |
| Badge_MountingCircleDamage_MATK | 112 | Pin: +15% MATK per circle |
| Badge_StrongerWhileFocused_ATK | 113 | Pin: +20% ATK while focused |
| Badge_StrongerWhileFocused_MATK | 114 | Pin: +20% MATK while focused |
| Badge_FasterWhileFocused_ASPD | 115 | Pin: +25 ASPD while focused |
| Badge_FasterWhileFocused_CSPD | 116 | Pin: +25 CSPD while focused |
| TimeCrystalIndicator | 117 | Tai Ming time crystal active |

## Stat Enums Modified by Buffs
```
ATK, MATK, DEF, ASPD, CSPD, Crit, CritDMG, EPRegen,
CritVulnerabilityFlat, CritVulnerabilityMultiplier,
DamageResistance, ATKMultiplier, DEFMultiplier, MATKMultiplier,
ShldRegen, FlatMoveSpeed, KnockbackResistance
```

## Status Effect Conflict Resolution
- Slow levels are mutually exclusive (0-4). Higher level replaces lower. Lower is rejected if higher exists.
- Chill removes on status clear (resets animation speed and color overlay)
- Active buffs track duration; longest duration wins when re-applying same effect

## Ice Mechanic Details
```
Freeze crit increase: +50%
Freeze duration vs elites: 75% of normal
Freeze duration vs bosses: 50% of normal
Freeze probability when chilled: 2x
Consecutive freeze probability reduction: 0.8x per consecutive
Reduced probability after release: 14 seconds cooldown
```

## Key Code Locations
- StatusEffectSource enum: `/Stats n Attributes/BaseStats.cs` lines 1320-1421
- AddStatusEffect: `/Stats n Attributes/BaseStats.cs` line 269
- CalculateEffectChange: `/Stats n Attributes/BaseStats.cs` line 357
- Slow conflict resolution: `/Stats n Attributes/BaseStats.cs` line 462

## Design Patterns Worth Stealing
- Splitting compound buffs into separate status entries (ATK + MATK, ASPD + CSPD) allows partial dispel/override
- Shrine buffs lasting 6000 seconds effectively means "permanent for the run" without needing special-case code
- The freeze probability decay system (0.8x per consecutive + 14s cooldown) prevents perma-freeze while rewarding ice builds
- Badge/Pin effects stored as same-type status effects means they use identical buff infrastructure with no special code
- Conflict resolution for slows is simple: enum ordering + mutual exclusion check. No complex priority system needed.
