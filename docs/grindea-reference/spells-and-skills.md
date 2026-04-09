# Spells and Skills -- Secrets of Grindea Reference

## Overview
All spells/skills are identified by `SpellCodex.SpellTypes` enum. Player spells range from ID 7 (Bow) through ID 1302 (Combat Passives). Each spell has 4 tiers: Base, Bronze (Lv2-4), Silver (Lv5-9), Gold (Lv10). The tier upgrade at Lv5 and Lv10 adds new mechanics. EP costs scale per tier. Armor penetration scales: Lv2=5%, Lv3=20%, Lv4=30%.

## Spell Categories

### Classification Functions
```csharp
IsUtilitySkill: SpellTypes >= _Magic_Light_Heal && < _Magic_OneHandProjectile_Basic  (500-515)
IsMagicSkill:   SpellTypes >= _Magic_Ice_IceSpikes && < _Skill_TwoHanded_Overhead     (100-999)
IsMeleeSkill:   SpellTypes >= _Skill_TwoHanded_Overhead && <= _Skill_OneHanded_ShadowClone (1000-1204)
IsTalent:       SpellTypes >= _Talent_QuickReflexes && < _Talent_Last                  (2000-2999)
IsEPBlocking:   Cloud, Shadow Clone, Frosty Friend (reserves EP while active)
```

## Fire Spells (IDs 200-205)

### Fireball (200)
- **EP Cost**: 15 / 20 / 25 / 30
- **Tags**: Long Range, Fast Charge, Ease of Use
- **Tiers**: Bronze = more damage, Silver = bigger fireball + silver charge, Gold = phoenix trail variant

### Meteor (201)
- **EP Cost**: 30 / 35 / 45 / 55
- **Tags**: High DMG, Long Range, Targeted, High Skill
- **Gold special**: Molten ground for 5 seconds

### Flamethrower (202)
- **EP Cost**: 25 / 30 / 40 / 50
- **Base Duration**: 0.8 / 1.1 / 1.5 / 2.0 seconds
- **Additional Duration per charge**: +0.06s
- **Continuous Cost**: 20 EP per 1 second of sustained fire
- **Perfect Guard Blast**: 3.5x MATK ratio

## Ice Spells (IDs 100-103)

### Ice Spikes (100)
- **EP Cost**: 20 / 25 / 35 / 45
- **Freeze Chance**: 15% / 20% / 35% / 50%
- **Freeze Duration**: 3s / 3s / 4s / 5s

### Ice Nova (101)
- **EP Cost**: 25 / 30 / 40 / 50
- **Freeze Chance**: 15% / 20% / 35% / 40%
- **Freeze Duration**: 3s / 3s / 4s / 5s

### Frosty Friend (102) -- EP Blocking Summon
- **EP Cost (blocked)**: 35 / 40 / 50 / 60
- **Frenzy ASPD**: 200, Duration: 4s, Cooldown: 8s
- **Guard**: Max 90% damage reduction, 3s duration, 10s cooldown
- **Smash Cooldown**: 10s
- **Death Recovery**: 12s automatic, 2.5s if player assists
- **Defensive Stance Heal**: 2.5s full, Out of Combat: 3.5s full
- **CSPD to ASPD conversion**: 25%

## Wind/Lightning Spells (IDs 400-404)

### Cloud Strike (400)
- **EP Cost**: 15 / 20 / 35 / 50
- **CSPD to ASPD conversion**: 25%

### Chain Lightning (401)
- **EP Cost**: 30 / 35 / 40 / 50
- **Bounces**: 3 / 4 / 5 / 10
- **Gold max instances**: 1

### Static Touch (402)
- **EP Cost**: 30 / 35 / 40 / 50
- **Summoned Orbs**: 4 / 6 / 8 / 10
- **Max Orbs**: 4 / 6 / 8 / 10
- **MATK Base%**: 130 / 140 / 200 / 230
- **MATK Max%**: 150 / 160 / 230 / 260
- **Base Cooldown**: 1.2 / 1.0 / 0.8 / 0.7 seconds
- **Min Cooldown**: 1.0 / 0.8 / 0.45 / 0.3 seconds
- **Charge-up**: 2 hits per charge, max 3 charge-ups
- **Reset timer**: 4s without hits, power down after 7s, then every 3s
- **Stun**: Lv3 0%, Lv4 0% (stun chance disabled but infrastructure exists)
- **Stun Duration**: 1.5s / 2.0s
- **Balls per Perfect Guard**: 2

## Earth Spells (IDs 300-303)

### Earth Spike (300)
- **EP Cost**: 20 / 25 / 30 / 40
- **Gold Stun Chance**: 75%
- **Gold Stun Duration**: 4 seconds

### Summon Plant (301)
- **EP Cost**: 25 / 30 / 35 / 50 (Recast at Gold: 20)
- **Leader buff**: +10% lifespan, +20 ASPD, 60 range
- **CSPD to ASPD conversion**: 25%
- **Silver Charge Range**: 110

### Insect Swarm (302)
- **EP Cost**: 30 / 35 / 40 / 50
- **Base Duration**: 4 / 5 / 8 / 9 seconds
- **Additional Duration per charge**: +0.2s

## Two-Handed Melee Skills (IDs 1000-1005)

### Overhead / Heroic Slam (1000)
- **EP Cost**: 25 / 30 / 35 / 40

### Spin / Whirlslash (1001)
- **EP Cost**: 30 / 35 / 40 / 45
- **Gold Charge Extra Spins**: 3

### Throw / Titan's Throw (1002)
- **EP Cost**: 30 / 35 / 40 / 55
- **EP Return**: 0 (was planned)
- **Bronze Return Time**: 0.5s
- **Silver Return Time**: 0.15s
- **EP Refund at Pickup**: 50%

### Smash (1003)
- **EP Cost**: 20 / 22 / 25 / 30

### Berserk Mode (1004) -- No EP Cost, EP Drain
- **EP Depletion**: 10 per second
- **Damage Boost**: 25% (L1), 35% (L4)
- **Damage Resistance Debuff**: 15/10/10/30% (L1-L4)
- **EP Recovery Per Hit**: 10/12/--/14 (L1/L2/L4)
- **Bonus ASPD Per Hit**: 4/--/4/5 (L1/L3/L4)
- **Bonus Max Stacks**: 7/--/10/10 (L1/L3/L4)
- **Bonus Crit Per Hit**: 0/--/2/3 (L1/L3/L4)
- **Stack Duration**: 3s, Decay Rate: 1s, Gain Cooldown: 0s

## One-Handed Melee Skills (IDs 1200-1205)

### Stinger / Piercing Dash (1200)
- **EP Cost**: 15 / 20 / 30 / 40
- **Post Bonus Charge Duration**: 0.5s

### Million Stabs / Blade Flurry (1201)
- **EP Cost**: 20 / 25 / 35 / 45

### Spirit Slash (1202)
- **EP Cost**: 20 / 25 / 30 / 40

### Quick Counter / Dodging Strike (1203)
- **EP Cost**: 15 / 5 / 20 / 30
- **Counter EP Cost**: 0 / 0 / 5 / 10
- **Mark Duration**: 4s
- **Marked Damage Addition**: +10%
- **Stun Chance**: 60% default, 100% on marked target
- **Stun Duration**: 3s default, 4s on marked target
- **Charge Inheritance Duration**: 1s

### Shadow Clone (1204) -- EP Blocking Summon
- **EP Cost (blocked)**: 15 / 20 / 25 / 30
- **Clone 1 Damage**: 5% base, +2% at bronze
- **Clone 2 Damage**: 15% base
- **Gold Damage Boost**: +10%
- **Damage Per Skill Level**: +0.5%
- **Proc Apply Chance**: 40% base, +1% per level, +20% at gold

## Utility Spells (IDs 500-515)

### Heal (500)
- Restores HP

### Protect (501)
- Damage reduction buff

### Blink (510)
- **Base EP Cost**: 30, -10 per level
- **EP Increase Per Cast**: +20 (debuff lasts 3s)
- **Range**: 75 (start) to 120 (max)
- **Cooldown**: 0s base, -0s per level

### Focus (511) -- No EP Cost
- **Base EP Regen Multiplier**: 0.6 + 0.1 per level
- **Full Channel Multiplier**: 2.0x at full channel
- **Full Channel Time**: 2 seconds
- **Sigil EP Reduction**: 50%

### Barrier (512)
- **EP Cost**: 30
- **Absorb**: 2.5% + 7.5% per level of max HP (Expert: 5% + 2.5%/lv)
- **Duration**: 20s (Expert: 60s)
- **Cooldown**: 15s (Expert: 50s)
- **Max Hits Before Break**: 5 (Expert: 2)
- **Max Breaking Power**: 2
- **Perfect Guard Window**: 12 frames

### Death Mark (513)
- **EP Cost**: 35
- **Duration**: 3.5 seconds
- **Damage Absorbed**: 35% + 5% per level
- **Execution Modifier**: 150% (200% vs bosses)
- **Max Damage Cap**: 300% of (ATK+MATK) base
- **Max Range**: 200
- **Max Time Before Count**: 2s

### Stasis (514)
- **EP Cost**: 40
- **Base Duration**: 2s + 1s per level
- **Duration vs Elites**: 50%
- **Duration vs Bosses**: 20%

### Taunt (515)
- **EP Cost**: 25
- **Base Duration**: 4s + 1s per level
- **Range**: 120 effect, 150 leash
- **Max Targets**: 1 + 1 per level
- **Enemy Damage Increase**: +25%
- **Player Damage Increase to taunted**: +50%

### Buff ATK / Empower (507)
- **EP Cost**: 30, **Duration**: 15s
- **ATK Buff**: 12.5% + 2.5% per level

### Buff DEF / Fortify (509)
- **EP Cost**: 30, **Duration**: 15s
- **DEF Buff**: 16% + 4% per level
- **Shield Regen**: 25% + 25% per level
- **EP Regen**: 7.5% + 2.5% per level

### Buff SPD / Haste (508)
- **EP Cost**: 20, **Duration**: 15s
- **ASPD/CSPD Buff**: 7.5% + 2.5% per level

## Bow Skills (IDs 7-9)

### Shoot Arrow (7)
- **Base Cooldown**: 12 frames
- **Stretch Time**: 15 frames

### Splitting Arrow (8)

### Machine Bow (9)

**Bow Upgrade Costs** (Story): 1500 / 5000 / 15000 / 50000 / 300000
**String Upgrade Cost**: 100000
**String Damage Increase**: +25% per upgrade
**Arcade Bow Upgrade Costs**: 1000 / 3000 / 5000

## Combat Passives (IDs 1300-1302)
| ID | Name | Notes |
|-----|------|-------|
| 1300 | CombatPassive1 | Passive combat ability 1 |
| 1301 | CombatPassive2 | Passive combat ability 2 |
| 1302 | CombatPassive3 | Passive combat ability 3 |

## Overleveling System (Arcade/Roguelike)
```
Max per loop (normal spells): 5
Max per loop (utility spells): 3
Base damage increase per level: +7.5%
Effect chance increase per level: +20%
```

## Key Code Locations
- SpellTypes enum: `/Spells/SpellInstanceCodex.cs` lines 7653-7942
- SpellVariable Init: `/Spells/SpellVariables.cs` lines 27-685
- Spell descriptions: `/Spells/!Descriptions/*.cs`
- Spell instance creation: `/Spells/SpellInstanceCodex.cs` line 125
- IsUtilitySkill/IsMagicSkill/IsMeleeSkill: `/Spells/SpellInstanceCodex.cs` lines 80-103

## Design Patterns Worth Stealing
- EP Blocking (Cloud/Shadow Clone/Frosty Friend) reserves max EP, creating resource commitment
- 4-tier system (Base/Bronze/Silver/Gold) with major upgrades at Silver and Gold creates meaningful progression milestones
- Utility spells having lower overlap caps (max 3 per loop vs 5 for offense) prevents utility stacking from trivializing combat
- Death Mark's "absorb then release" mechanic creates setup/execution gameplay
- Berserk Mode's EP drain + EP recovery per hit creates a "sustain through aggression" loop
- Focus channeling with ramping EP regen rewards strategic downtime
- Blink's escalating EP cost per cast (debuff for 3s) prevents spam while allowing emergency double-blinks
