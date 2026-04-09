# Talents -- Secrets of Grindea Reference

## Overview
Talents are passive abilities that provide stat bonuses or trigger effects. There are 53 talent slots in the SpellTypes enum (IDs 2000-2052), with 1 marked OBSOLETE. Each talent has 3 levels (tier span = 3), and scales linearly per level using a per-level (PL) multiplier from SpellVariables.

Talents are divided into four categories: General, Melee, Magic, and Shield/Utility.

## All Talents with Per-Level Values

### Shield / Defensive Talents (IDs 2000-2008)

| # | Name | ID | Per-Level Value | Effect |
|---|------|-----|----------------|--------|
| 1 | Quick Reflexes | 2000 | +20% per level | Prolongs perfect guard window by 20/40/60% |
| 2 | Shield Bearer | 2001 | +10% per level | Reduces shield movement speed penalty by 10/20/30% |
| 3 | Multitasking | 2002 | +8% per level | Reduces charge movement speed penalty by 8/16/24% |
| 4 | Adaptable | 2003 | +1 per level | Increases both ATK and MATK by 1/2/3 |
| 5 | Tenacious | 2004 | +2% per level | Increases max HP by 2/4/6% |
| 6 | Last Stand | 2005 | +20% DEF per level | When HP below 20%, DEF increases by 20/40/60% |
| 7 | Surgeon | 2006 | +3% per level | Increases crit chance by 3/6/9% |
| 8 | Brutality | 2007 | +10% per level | Increases crit damage by 10/20/30% |
| 9 | Endurance | 2008 | +3 per level | Increases max EP by 3/6/9 |

### General Stat Talents (IDs 2009-2013)

| # | Name | ID | Per-Level Value | Effect |
|---|------|-----|----------------|--------|
| 10 | Fine Taste | 2009 | +12% per level | Increases potion/consumable effect by 12/24/36% |
| 11 | Strength | 2010 | +2 ATK per level | Flat ATK increase: 2/4/6 |
| 12 | Brawler | 2011 | +5 DEF for 3s | On melee hit, gain 5/10/15 DEF for 3 seconds |
| 13 | Second Wind | 2012 | 1 EP per 2 hits | Every 2 hits, recover 1/2/3 EP |
| 14 | Wit | 2013 | +20% per level | Increases charge speed bonus by 20/40/60% |

### Elemental Melee Talents (IDs 2014-2018)

| # | Name | ID | Per-Level Value | Effect |
|---|------|-----|----------------|--------|
| 15 | Burning Weapon | 2014 | +5% chance, 100% ATK burn | Melee attacks have 5/10/15% chance to burn for 100% ATK damage |
| 16 | Chilly Touch | 2015 | +5% chance, 0.2 slow, 3s | Melee attacks have 5/10/15% chance to chill (20% slow for 3s) |
| 17 | Static Field | 2016 | +2% chance, 3s stun | Melee attacks have 2/4/6% chance to stun for 3 seconds |
| 18 | Fencer | 2017 | +2 ASPD per level | Flat ASPD increase: 2/4/6 |
| 19 | Last Breath | 2018 | +10 ATK per level | When HP below 20%, ATK increases by 10/20/30 |

### Magic Talents (IDs 2020-2036)

| # | Name | ID | Per-Level Value | Effect |
|---|------|-----|----------------|--------|
| 20 | Intelligence | 2020 | +2 MATK per level | Flat MATK increase: 2/4/6 |
| 21 | Arcane Charge | 2021 | +15 ATK per charge | After casting a spell, next melee attack gets +15/30/45 ATK |
| 22 | Prismatic | 2022 | -7% EP cost per level | Reduces EP cost of off-element spells by 7/14/21% |
| 23 | Battlemage | 2023 | -10% EP cost per level | Reduces EP cost of melee skills by 10/20/30% |
| 24 | Turtle | 2024 | +10 DEF per level | While channeling/charging, gain 10/20/30 DEF |
| 25 | Last Spark | 2025 | +10 MATK per level | When HP below 20%, MATK increases by 10/20/30 |
| 26 | Arcane Collar | 2026 | -4 EP per level | Reduces EP cost of spells by flat 4/8/12 |
| 27 | Backhander | 2027 | +10 ATK per level | After casting a spell, melee ATK increases by 10/20/30 for next hit |
| 28 | Insult to Injury | 2028 | +10% per level | Physical attacks deal 10/20/30% more damage to debuffed enemies (chilled/burning/acid/stunned) |
| 29 | Manaburn | 2029 | +10 MATK per level | When EP above 50%, MATK increases by 10/20/30 |
| 30 | Snap Cast | 2030 | +10 MATK per level | Instant-cast spells gain 10/20/30 bonus MATK |
| 31 | Crippling Blast | 2031 | +8% per level | Spells have 8/16/24% increased chance to apply debuffs |
| 32 | Fast Talker | 2032 | +2 CSPD per level | Flat CSPD increase: 2/4/6 |
| 33 | Soul Eater | 2033 | +1 EP per hit | Offensive spells restore 1/2/3 EP per enemy hit |
| 34 | Concentration | 2034 | +1 per level | Increases spell level bonus by 1/2/3 (overleveling effect) |
| 35 | Specialist | 2035 | +0.15% per spell point | Spells deal 0.15% more damage per invested spell point in that element (stacks with all 3 spells in element) |
| 36 | Wand Master | 2036 | +15% per level | Wand projectile damage increases by 15/30/45% |

### Melee Combat Talents (IDs 2037-2041)

| # | Name | ID | Per-Level Value | Effect |
|---|------|-----|----------------|--------|
| 37 | Bloodthirst | 2037 | +10 ASPD, +0.33 movespd, 3s | On killing an enemy, gain +10/20/30 ASPD and +0.33/0.66/0.99 move speed for 3 seconds |
| 38 | Riposte | 2038 | 25% of ATK per level | Perfect guard deals 25/50/75% of ATK as counter-damage |
| 39 | Combo Starter | 2039 | +10% crit for 2s | After using a skill, gain 10/20/30% crit chance for 2 seconds |
| 40 | Knowledge is Power | 2040 | 5% MATK to ATK per level | Converts 5/10/15% of MATK into bonus ATK |
| 41 | Sudden Strike | 2041 | +20 ASPD+ATK | After 2 seconds of not attacking, next attack gets +20/40/60 ASPD and ATK |

### General / Utility Talents (IDs 2042-2052)

| # | Name | ID | Per-Level Value | Effect |
|---|------|-----|----------------|--------|
| 42 | Got You Covered | 2042 | +1s per level | Extends buff duration on allies by 1/2/3 seconds |
| 43 | Metabolism | 2043 | +4% EP regen per level | EP regeneration increased by 4/8/12% |
| 44 | Health Insurance | 2044 | +10% per level | Health orb healing increased by 10/20/30% |
| 45 | Lady Luck | 2045 | +2% per level | Dodge chance: 2/4/6% |
| 46 | Utility Flow | 2046 | -5% per cast, max 3 stacks | Utility spell cost reduced by 5% per utility spell cast, max 3 stacks (15% reduction) |
| 47 | Kinetic Energy | 2047 | +3 EP per shield hit | Gain 3/6/9 EP regen on shield block |
| 48 | Efficient Counter | 2048 | -15% per level | Counter-attack EP cost reduced by 15/30/45% |
| 49 | Ammo Scavenger | 2049 | +10% per level | Arrow drop chance increased by 10/20/30% |
| 50 | Quick Shot | 2050 | +20% per level | Bow firing speed increased by 20/40/60% |
| 51 | Alchemist | 2051 | +5% per level | Potion recharge speed increased by 5/10/15% |
| 52 | Steady Defense | 2052 | DEF threshold scaling | At 50 DEF (reduced by 10 per level), gain damage resistance. Thresholds: 50/40/30 DEF |

### OBSOLETE

| # | Name | ID | Notes |
|---|------|-----|-------|
| -- | OBSOLETE1 | 2019 | Removed from game |

## Key Code Locations
- Talent enum: `/Secrets Of Grindea/Spells/SpellInstanceCodex.cs` lines 7717-7770
- SpellVariable values: `/Secrets Of Grindea/Spells/SpellVariables.cs` lines 28-95
- SpellVariable Handle enum: `/Secrets Of Grindea/Spells/SpellVariables.cs` lines 688-756
- Talent description classes: `/Secrets Of Grindea/Spells/!Descriptions/47SpellDescriptions.cs` through `99SpellDescriptions.cs`
- Insult to Injury application: `/Secrets Of Grindea/AttackPhases/AttackStats.cs` line 207
- Specialist application: `/Secrets Of Grindea/AttackPhases/AttackStats.cs` lines 162-199
- Lady Luck dodge: `/Secrets Of Grindea/Game1.cs` line 94997

## Design Patterns Worth Stealing
- Per-level linear scaling with a single multiplier constant makes balancing trivial -- change one number to rebalance
- Threshold-based talents (Last Stand/Last Breath/Last Spark at 20% HP, Manaburn at 50% EP) create exciting low-resource gameplay
- Elemental weapon proc talents (Burn/Chill/Stun) share identical structure but different effects -- great for a template system
- Combo Starter and Bloodthirst create "momentum" gameplay that rewards aggressive play
- Specialist talent rewards deep investment in one element over spreading points -- natural build diversity
- Steady Defense's threshold-lowering-per-level design is clever: makes it accessible at lower levels while powerful at higher
- Tier span of 3 levels per talent keeps each talent meaningful without too many micro-decisions
