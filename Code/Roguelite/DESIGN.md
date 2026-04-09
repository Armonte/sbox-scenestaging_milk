# Roguelite Design Document — Systems & Game Loop

## Context

The roguelite has a solid combat framework (damage pipeline, 2 weapons, 3 enemy types, AI state machine, networking scaffolding) but no game loop, progression, or upgrade system. This document defines the full game design: how a run works, how abilities evolve, how spawning/encounters work, and what needs to be built.

## Core Loop: Room-to-Room Progression

```
[Class Select] → [Weapon Select] → [Room 1] → [Upgrade Pick 3] → [Room 2] → ... → [Boss] → [Run End]
                                        ↓              ↓
                                   Kill enemies    Level up from XP
                                   Collect XP      Pick 1 of 3 cards
                                   Collect Gold     (ability/proc/new skill)
                                        ↓
                                   [Shop between rooms]
                                   Spend gold on items/rerolls
```

### Run Structure
1. **Class Select** — Pick class (affects base stats + signature starter ability). Use reclaimer-style 3-card UI adapted for 6 classes.
2. **Weapon Select** — Pick weapon (Sword, Bow, future: Staff, Wand, etc). Independent of class.
3. **Combat Rooms** — Clear enemies to progress. Enemies drop XP orbs and gold.
4. **Level Up** — XP fills a bar. On level up: pick 1 of 3 upgrade cards.
5. **Shop** — Between rooms, spend gold. Buy items, reroll upgrades, heal.
6. **Boss Room** — Every N rooms, face a boss encounter.
7. **Run End** — Death or victory. Meta-progression TBD (not this doc).

### Economy
- **XP** — Dropped by enemies (scales with enemy tier). Fills level bar. On level up → pick 3 cards.
- **Gold** — Dropped by enemies (random amounts). Spent in shop between rooms.

## Class System

Pick class → get base stats + signature starter ability. All other upgrades from universal pool.

| Class | HP | Armor | Speed | Ability Slots | Signature Ability |
|-------|-----|-------|-------|---------------|-------------------|
| Warrior | High | High | Normal | 3 | Shield Bash (stun + knockback) |
| Archer | Normal | Low | Fast | 3 | Evasion Shot (backflip + shot) |
| Wizard | Low | Low | Normal | 4 | Arcane Pulse (AoE burst) |
| Necromancer | Normal | Normal | Slow | 4 | Raise Skeleton (summon) |
| Summoner | Low | Normal | Normal | 4 | Spirit Familiar (passive orb) |
| Bard | Normal | Normal | Fast | 3 | War Cry (buff allies) |

## Movement Utility (Shift)

Everyone starts with a **basic dash** (ability slot 0, always available, separate from the 3-4 slots).

Dash is an ability and can be upgraded through the card system:
- **Base**: Short dash, brief invulnerability
- **Upgrade**: Longer range
- **Upgrade**: Leaves damaging trail
- **Upgrade**: Resets on kill
- **Upgrade**: Dashes through enemies dealing damage
- **Upgrade**: Blink (instant teleport variant)

## Ability Evolution Trees

Everything is an ability. Abilities start simple and evolve through upgrade cards. The pool is universal (any class can get any tree), but your class signature ability gives you a head start in one tree.

### Lightning Tree
```
[Spark Bolt] → +Multi-Strike → [Lightning Beam] → +Chain → [Arc Storm]
  Single cast    2-3 bolts       Held beam         Chains to    Full AoE
  click-fire     per cast        continuous dmg     nearby       lightning storm
```

### Ballistic Tree
```
[Single Shot] → +Spread → [Shotgun Blast] → +Explosive Rounds → [Artillery]
  One projectile   3-5 spread   Cone burst        Rounds explode      Arcing grenade
                   pattern      on impact          on impact           to target area
```

### AoE/Fire Tree
```
[Ember] → +Radius → [Flame Circle] → +Grenade Arc → [Napalm Strike]
  Small dmg zone   Bigger area    Persistent AoE    Throw projectile    Projectile arcs to
  at cursor        burning        ground fire        to your placed      placed circle,
                                                     circle              massive explosion
```

### Homing Tree
```
[Seeking Orb] → +Count → [Orb Swarm] → +Proc Chance → [Chaos Spheres]
  1 homing ball   3 orbs    5+ orbs      Each orb can     Orbs carry your
  seeks nearest   seek      constant     proc on-hit      procs, multiply
                  targets   barrage      effects           effect application
```

### Summoning Tree
```
[Minion] → +Durability → [Pack] → +AI Upgrade → [Army]
  1 weak summon   Longer lived   3 summons   Summons use     5+ summons
  basic attack    more HP        at once     your abilities  share your procs
```

### Utility/Dash Tree (see Movement above)

## Upgrade Cards (Pick 3 System)

On level up, draw 3 cards from the available pool. Cards can be:

### Card Types
1. **Ability Evolution** — Advance an ability tree one step. "Lightning Beam → Chain Lightning"
2. **New Ability** — Unlock a new ability tree at tier 1. "Unlock: Seeking Orb"
3. **Proc/Affix** — On-hit effect. "Attacks have 15% chance to chain lightning to 2 nearby enemies"
4. **Synergy** — Combines two systems. "Summons inherit your burn effect" / "Dash cooldown reduced per active orb"
5. **Transformative** — Changes how something fundamentally works. "Shotgun now fires behind you too" / "AoE zone follows you instead of staying placed"

### Anti-Boring Design Rules
- No "+5% damage" cards. Minimum tier is "+1 projectile" or "attacks pierce 1 enemy".
- Every card should change HOW you play, not just make numbers bigger.
- Proc effects should be visible and satisfying (particles, screen effects).
- Synergy cards reference your current build — only offered if you have the prerequisites.

### Card Weighting
- **Common**: New tier-1 abilities, basic procs
- **Uncommon**: Ability evolutions, stronger procs
- **Rare**: Synergy cards, transformative effects
- **Legendary**: Build-defining (offered rarely, maybe 1 per run)

## Proc/Affix System

Procs are on-hit (or on-kill, on-dash, on-cast) effects that stack and interact.

### Example Procs
- **Ignite**: 15% chance to burn (DoT fire damage, 3s)
- **Chain**: 10% chance to arc to 1 nearby enemy
- **Leech**: Heal 3% of damage dealt
- **Shatter**: Frozen enemies explode on death dealing AoE
- **Echo**: 8% chance to repeat the attack instantly
- **Rupture**: Crits cause bleed (stacking DoT)
- **Magnetize**: Killed enemies pull nearby enemies toward death location
- **Overcharge**: Lightning damage has 20% chance to stun for 0.5s
- **Soul Harvest**: On kill, gain a soul. At 10 souls, next ability is free + empowered

### Proc Architecture
- Each proc is a component/modifier attached to the player
- DamageResolver checks active procs after resolving base damage
- Procs can trigger other procs (chain reactions) with a recursion depth limit
- Visual feedback per proc (particles on the target, HUD icon stack)

## Spawning / Encounter System

### Room Structure
- Each room is a self-contained arena with spawn points
- Room has an `EncounterDefinition`: list of waves
- Wave defines: enemy types, counts, spawn pattern (all-at-once, trickle, surround)
- Room is "cleared" when all waves complete and all enemies dead
- On clear: portal/door opens, gold/XP burst, shop access

### Enemy Tiers (scale with room number)
- **Fodder**: Low HP, low damage, drops little XP. Swarm enemies.
- **Standard**: Current enemies (Rusher, Flyer). Normal drops.
- **Elite**: Enhanced enemies with affixes (burning aura, shield, teleport). Good drops.
- **Boss**: Unique encounter every N rooms. Special mechanics. Big reward.

### Enemy Drops
- XP orbs (pulled toward player magnetically after short delay)
- Gold coins (same magnetic pickup)
- Rare: Health orb

### Passive Test Enemy
- Change FactionComponent to Neutral on an enemy instance
- Or add a `Passive` flag to EnemyBase that skips the brain update
- Useful for testing procs, damage numbers, ability targeting

## Shop System

Between rooms, a shop area with:
- **Reroll**: Spend gold to redraw upgrade cards
- **Heal**: Buy HP restoration
- **Items**: Passive stat items (but interesting ones — "attacks have +1 pierce" not "+5% damage")
- **Ability Reset**: Expensive, lets you refund one ability tree

## UI Needed

1. **Class Select Screen** — 6 cards, reclaimer style (already have reference in `Code/Reclaimer/UI/`)
2. **Weapon Select Screen** — Smaller card row after class pick
3. **Pick 3 Upgrade Panel** — 3 cards with rarity glow, ability icon, description, "choose" button
4. **XP Bar** — Bottom of HUD, fills up, pops on level up
5. **Gold Counter** — Top-right with coin icon
6. **Ability Hotbar** — Show ability icons with cooldown overlays (shift=dash, 1-4=abilities)
7. **Proc Icons** — Small icon stack showing active procs
8. **Shop UI** — Grid of buyable items with gold costs
9. **Room Cleared** — Overlay with rewards summary
10. **Enemy Health Bars** — Floating bars above enemies

## Implementation Priority

### Phase 1: Foundation (test what works)
- Add passive enemy flag for testing
- Fix backstab detection or remove it
- Test bow weapon + knockback
- Validate aggro system with multiple enemies
- Test basic multiplayer (2 players, enemy targeting)

### Phase 2: Game Loop
- XP/Gold drop system (magnetic pickup)
- Level bar + level up trigger
- Room/encounter definition system
- Wave spawner
- Room clear detection + portal

### Phase 3: Ability System Overhaul
- Refactor abilities: everything is an ability (dash included)
- Ability evolution tree data structure
- Implement Lightning tree tier 1-2 as proof of concept
- Implement Dash upgrade path as proof of concept

### Phase 4: Upgrade Cards
- Pick 3 card UI (adapt reclaimer cards)
- Card pool + weighting system
- Card types: ability evolution, new ability, proc, synergy
- Wire up: level up → show cards → apply selection

### Phase 5: Proc System
- Proc component architecture
- Implement 3-4 starter procs (Ignite, Chain, Leech, Echo)
- Hook into DamageResolver pipeline
- Visual feedback per proc
- Recursion depth limiting

### Phase 6: Content & Polish
- More enemy types + elite affixes
- Boss encounter(s)
- Shop system
- Remaining ability trees
- Audio + particles
- Balance pass

## Files to Modify/Create

### Existing (modify)
- `Code/Roguelite/Core/DamageResolver.cs` — Add proc hook point
- `Code/Roguelite/Enemies/EnemyBase.cs` — Add passive flag, XP/gold drop on death
- `Code/Roguelite/Components/AbilityComponent.cs` — Refactor for evolution trees
- `Code/Roguelite/Player/RoguelitePlayer.cs` — Add XP/gold/level tracking, dash as ability slot 0
- `Code/Roguelite/Core/RogueliteGameManager.cs` — Room/encounter management

### New files
- `Code/Roguelite/Core/EncounterDefinition.cs` — Room wave definitions
- `Code/Roguelite/Core/WaveSpawner.cs` — Spawns enemies per wave
- `Code/Roguelite/Core/XPSystem.cs` — XP tracking, level up events
- `Code/Roguelite/Core/GoldSystem.cs` — Gold tracking
- `Code/Roguelite/Abilities/AbilityTree.cs` — Evolution tree data
- `Code/Roguelite/Abilities/DashAbility.cs` — Base dash + upgrades
- `Code/Roguelite/Abilities/LightningBolt.cs` — Lightning tree starter
- `Code/Roguelite/Procs/ProcSystem.cs` — Proc manager
- `Code/Roguelite/Procs/IgniteProc.cs` — Example proc
- `Code/Roguelite/Pickups/XPOrb.cs` — Magnetic XP pickup
- `Code/Roguelite/Pickups/GoldCoin.cs` — Magnetic gold pickup
- `Code/Roguelite/UI/ClassSelectPanel.razor` — 6-class card select
- `Code/Roguelite/UI/UpgradeCardPanel.razor` — Pick 3 cards
- `Code/Roguelite/UI/ShopPanel.razor` — Between-room shop
- `Code/Roguelite/UI/AbilityHotbar.razor` — Ability icons + cooldowns

## Verification

### Phase 1 Tests
- Spawn passive enemy → attack it → verify damage numbers, no aggro response
- Bow: shoot enemy → verify knockback
- Spawn 3+ enemies → verify aggro switches correctly
- 2 players: both attack same enemy → verify damage authority stays on host
- Backstab: approach from behind → verify consistent detection or confirm removed

---

## Reference: Secrets of Grindea Roguelite Analysis

Source at `/mnt/c/dev/grindea/`. Key files: `States/RogueLike.cs` (13k lines), `Spells/SpellVariables.cs`, `Game1.cs` (173k lines).

### Talent System (53 Talents) — What We Should Steal

Grindea's talents are the "interesting upgrades" we want. They fall into categories that map to our card types:

**Combat Feel Changers** (these change HOW you play):
- **QuickReflexes** — Increase perfect guard window (+1 frame/level). Makes parry more forgiving.
- **BloodThirst** — On hit: +10 ASPD, +33% move speed for 3s. Rewards aggression.
- **SuddenStrike** — +20% ATK/ASPD after 2s without attacking. Rewards patience/timing.
- **Riposte** — Bonus damage on perfect guard counter. Rewards skill.
- **ComboStarter** — First hit in combo does bonus damage. Rewards hit-and-run.
- **LastBreath** — +10% ATK when below 20% HP. Risk/reward.
- **LastStand** — +20% DEF when below 20% HP. Survival clutch.
- **SecondWind** — HP regen boost at low HP. Comeback mechanic.

**Elemental Procs** (on-hit chance effects):
- **BurningWeapon** — 5% burn chance/level (100% ATK as DoT over 3s)
- **ChillyTouch** — 5% chill chance/level (20% slow for 3s)
- **StaticField** — 2% stun chance/level (3s stun)
- **InsultToInjury** — Bonus damage vs status-affected enemies. Synergy reward.

**Resource/Sustain:**
- **FineTaste** — +12%/level potion effectiveness. Potions heal more.
- **HealthInsurance** — +10%/level health orb value. Pickups heal more.
- **Metabolism** — +4%/level EP regen. More ability spam.
- **Prismatic** — -7%/level spell cost. Cheaper abilities.
- **SoulEater** — Gain EP on kill. Sustain through murder.

**Stat Conversions** (build-defining):
- **KnowledgeIsPower** — Convert 5% MATK to ATK. Hybrid builds.
- **Battlemage** — -10%/level spell cost when using melee. Hybrid sustain.
- **ArcaneCharge** — +15% ATK per spell charge. Spell-melee synergy.

### What Grindea Does RIGHT for Roguelite Upgrades
1. **Talents change playstyle, not just numbers** — BloodThirst makes you aggressive, SuddenStrike makes you patient
2. **Synergy between systems** — InsultToInjury rewards having burn/chill procs
3. **Low HP mechanics** — LastBreath/LastStand create tension without being boring
4. **Guard/parry upgrades** — Perfect guard window increase rewards skill
5. **Resource conversion** — MATK→ATK conversion enables hybrid builds
6. **Scaling per level** — Each talent level is meaningful (5% burn → 25% burn at level 5)

### Their Damage Formula (simplified)
```
FinalDamage = BaseDamage × DamageModifier × DefenseReduction × CritMultiplier + Random(0-2 + 5%)
```
- Armor penetration: flat OR percentage (flag toggles mode)
- Crits stack: 200% crit chance = guaranteed double-crit
- Each crit multiplies by CritDamageModifier (default 1.5x)

### Their Wave/Encounter System
- **Discrete waves** (enemies spawn in groups) OR **Continuous** (trickle spawning on timer)
- Continuous spawn starts after 4s delay, then every 1.33s
- `bQuickenContinuous` flag accelerates spawn rate over time
- Room cleared when all waves done + all enemies dead
- Elites spawn based on `iHyperElitesCanSpawn` frequency

### Their Drop System
- Drops use 0-100000 scale (100000 = guaranteed, 20000 = 20%)
- Each enemy has `lxLootTable: List<DropChance>`
- Drop rolls: `iRolls` allows multiple attempts per drop entry
- Separate card drop system with its own chance table
- Health orbs, gold, XP all separate drop mechanics

### Their Meta-Progression (Perks bought with Essence)
- **Essence** earned per roguelite run
- Buy permanent perks: +5 HP, start with better weapon, +health potion
- Perk slots: start with 1, buy 2nd (50 essence), 3rd (150 essence)
- Content unlocks: more shop items, more fishing rooms, longer challenge timers
- **Key insight**: Meta-progression makes early runs feel like progress even on death

### Upgrades We Should Adapt for Our Game

**Direct ports (rename + rebalance):**
| Grindea Talent | Our Version | Card Type |
|---------------|-------------|-----------|
| QuickReflexes | Perfect Guard Window+ | Synergy |
| BloodThirst | Bloodrage (ASPD+speed on hit) | Proc |
| SuddenStrike | Ambush (bonus after pause) | Proc |
| BurningWeapon | Ignite (burn proc) | Proc |
| ChillyTouch | Frostbite (slow proc) | Proc |
| StaticField | Overcharge (stun proc) | Proc |
| InsultToInjury | Exploitation (bonus vs debuffed) | Synergy |
| LastBreath | Desperation (ATK at low HP) | Transformative |
| LastStand | Iron Will (DEF at low HP) | Transformative |
| FineTaste | Alchemist (better potions) | Utility |
| HealthInsurance | Vitality Siphon (better orbs) | Utility |
| Metabolism | Flow State (faster regen) | Utility |
| SoulEater | Soul Reap (resource on kill) | Proc |
| KnowledgeIsPower | Arcane Infusion (MATK→ATK) | Transformative |

**New ideas inspired by Grindea patterns:**
- **Echo Strike** — 8% chance to repeat attack (their system supports multi-crit, we can do multi-hit)
- **Magnetize** — Killed enemies pull nearby enemies (crowd control on kill)
- **Shatter** — Frozen enemies explode on death (synergy: need freeze first)
- **Chain Reaction** — Procs have 15% chance to trigger another proc (recursion with depth limit)
- **Lucky Seven** — Every 7th hit is guaranteed crit (predictable power spike)
- **Berserker's Rage** — Below 30% HP: +50% damage, -30% defense (high risk/reward)
