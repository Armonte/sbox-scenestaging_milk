# Combat Mechanics -- Secrets of Grindea Reference

## Overview
Combat is real-time action with melee attacks, ranged spells, shield blocking, perfect guarding, and dodge mechanics. The system uses breaking power for stun thresholds, knockback physics, combo tracking, and directional shielding.

## Perfect Guard System

### Timing
- Perfect guard is a brief window at the start of a shield raise
- Quick Reflexes talent extends window by 20/40/60%
- Barrier spell creates a 12-frame perfect guard window on activation
- `bPerfectGuardBonusActivated` tracks if PG was triggered
- `iPerfectGuardBonus > 60` or `iBarrierPerfectGuardCountdown > 0` = deflection active

### Effects of Perfect Guard
1. **Knockback Redistribution**: Knockback is reversed onto attacker
   - Always redistribute: `vector = -attackerKnockback` (attacker gets pushed back, defender stays)
   - On PG only: Same reversal
   - Regular guard: `vector *= 0.125` (minimal push), attacker gets `2x reduced knockback`

2. **Damage Reduction**: Attacks are modified by `fStrengthVersusPerfectGuard` (usually very low)
3. **Barrier PG**: Damage set to 1 during barrier PG countdown
4. **Breaking Power After PG**: Minimum 9 (`Spells_MinimumBreakingPowerAfterPerfectGuard`)
5. **Riposte Talent**: Deals 25/50/75% of ATK as counter-damage
6. **Various Pin Effects**: PG can trigger ATK/MATK buffs, max HP increase (+3), stasis, etc.

### Shield Direction Detection
```csharp
// Shield faces one of 4 cardinal directions (0=Up, 1=Right, 2=Down, 3=Left)
// Hit registered if attack comes from shield-facing direction
// 25-pixel tolerance on perpendicular axis
// Pin: "ShieldBlocksInAllDirections" bypasses direction check
```

## Shield HP System
```csharp
int iShieldMaxHP;              // Total shield HP pool
float fShieldHPFraction;       // Current shield HP (as float)
int iShieldRecoveryCooldown;   // Frames until recovery starts
float fShieldHPRecoveryPerTick = 3/1000;     // Base recovery rate
float fShieldHPRecoveryFlatBonusPerTick = 0.02;
float fShieldHPRecoveryMultiplier = 1.0;
bool bShieldBreak;             // Shield is broken
int iShieldBreakRecoveryTimeToSet;   // Recovery time when broken
int iShieldHitRecoveryTimeToSet = 120; // Frames before regen after hit
```

Shield breaks when HP reaches 0, requiring `iShieldBreakRecoveryTimeToSet` frames to recover.

## Knockback System
```csharp
float fKnockBack;             // Knockback strength
int iBreakingPower;           // Required to stagger target
int iKnockbackResistance;     // Target's resistance to knockback

// Knockback only applies if:
// bAllowKnockback == true AND iKnockbackResistance <= iBreakingPower

// Default knockback resistance can be overridden per-entity
// Negative override means use default value
```

### Knockback Direction
```csharp
Vector2 knockback = -Normalize(attacker.pos - target.pos) * fKnockBack;

// Overrides:
v2KnockbackDirectionOverride  // Fixed direction
v2SideKnockbackOverride       // Side-push (perpendicular to attack dir)
```

### Breaking Power Levels
- 0: No stagger (target continues acting)
- Low (1-3): Brief hitstun
- Medium (4-6): Standard hitstun
- High (7+): Heavy hitstun, can interrupt charge attacks
- Shield Crush options: `None`, `AllButPerfectGuard`, `AllIncludingPerfectGuard`

## Combo System
The game tracks combo state through spell charges and attack chains:

### Spell Charge System
```csharp
public class SpellCharge  // Base class for charge attacks
{
    // Subclasses: FocusSpellCharge, HasteSpellCharge, WindSliceSpellCharge
    // Players hold button to charge, release to cast
    // Charge level determines spell tier (Base/Bronze/Silver/Gold)
}
```

### Attack Speed Factor
```csharp
float fAttackSPDFactor = iAttackSPD / 100f;  // 100 = 1.0x speed
float fCastSPDFactor = iCastSPD / 100f;
```

### Weapon Type Damage Ratios
```csharp
1H Melee ATK Ratio: 0.77  // 77% of ATK stat
2H Melee ATK Ratio: 1.00  // 100% of ATK stat
1H Wand MATK Ratio: 0.40  // 40% of MATK stat
2H Wand MATK Ratio: 0.40  // 40% of MATK stat
```

### Attack Cancel Cooldown
```csharp
Misc_AttackCooldownOnCancelExploitInFrames = 12;
// 12-frame cooldown when canceling an attack animation
```

## Invincibility System
```csharp
// Players gain invincibility frames after being hit
// bDontTriggerPlayerInvincibility: Some attacks skip this
// Chicken Potion: 4.5s invulnerability + 1s post-invulnerability
// bDeathImmune: Cannot die (used for specific boss mechanics)
// fHPCantGoBelow: HP floor (cannot go below this value, -1 = disabled)
```

## Dodge Mechanics
```csharp
// Lady Luck talent: 2/4/6% dodge chance
// Hauntie card: +2% dodge per card
// Dodge = attack misses entirely (no damage, no knockback, no effects)
// Dodge check runs AFTER shield check -- if shielding, dodge is suppressed
```

## Stun System
```csharp
// Stunned status prevents all actions
// bStunImmune: Entity cannot be stunned
// Stun reduced probability after release: 10 seconds cooldown
// Consecutive stuns have diminishing returns
// bForceShortStunTime: Override to brief stun
// bSuperShortStunTime: Override to very brief stun
```

## Freeze Mechanics
```csharp
Freeze crit increase: +50%
Freeze duration vs elites: 75%
Freeze duration vs bosses: 50%
Freeze probability when chilled: 2x
Consecutive freeze reduction: 0.8x
Cooldown after unfreeze: 14 seconds
bFreezeImmunity: Cannot be frozen
bChillImmunity: Cannot be chilled
```

## Stasis Mechanics
```csharp
// Target frozen in time (cannot act, cannot be damaged)
Base duration: 2s + 1s per level
Duration vs elites: 50%
Duration vs bosses: 20%
bStasisImmune: Cannot be stasised
fStasisModifier: Duration multiplier
bCanStasisDespiteUntargetable: Override for untargetable entities
```

## Body Size System
```csharp
public enum BodySize { Small, Medium, Large }
// Affects collision detection and some spell interactions
```

## Attack Phase System
```csharp
public class AttackPhase
{
    IEntity xOwner;              // Who owns this attack
    AttackStats xStats;          // Damage, knockback, element, etc.
    List<Collider> lxCurrentColliders;  // Active hitboxes
    TransformComponent xTransformOverride;  // Position override
    bool bTrustClient;           // Networked hit validation
}
```

### Collider Types
```csharp
SphereCollider  // Circular hitbox (radius + offset)
BoxCollider     // Rectangular hitbox
OvalCollider    // Elliptical hitbox
// All inherit from Collider base class
// bIsLarge flag for large hitbox optimization
// bCollideWithFlat for ground-level collision
```

## Attack Special Effects
```csharp
public enum EffectType
{
    Freeze,         // Apply freeze
    Chill,          // Apply chill
    Burn,           // Apply burn DOT
    BreakingPower,  // Dynamic breaking power
    BugCatch,       // Insect Swarm capture
}
```

## Damage Over Time Types
```csharp
public enum DOT
{
    Not,       // Instant damage
    SemiDot,   // Tick damage, still triggers hitstun
    FullDot,   // Tick damage, no hitstun
}
```

## Key Code Locations
- Attack collision: `/Game1.cs` line 94940
- AttackStats class: `/AttackPhases/AttackStats.cs`
- AttackPhase class: `/AttackPhases/` directory
- Collider types: `/SoG/Collider.cs`, `SphereCollider.cs`, `BoxCollider.cs`, `OvalCollider.cs`
- BaseStats (shield/knockback): `/Stats n Attributes/BaseStats.cs`
- SpellCharge: `/SoG/SpellCharge.cs`

## Design Patterns Worth Stealing
- Directional shielding with tolerance (25px) creates skill expression without pixel-perfect requirements
- Perfect guard as "first frames of shield" means it's a single input with timing, not a separate button
- Knockback redistribution on PG creates satisfying "reversal" feel
- Breaking power as a threshold (not percentage) creates clear "this attack can/cannot stagger" distinctions
- Shield HP as separate pool from character HP creates gear diversity and build choices
- The freeze/stun diminishing returns system (0.8x per consecutive, 10-14s cooldown) prevents stunlock while rewarding CC builds
- Attack cancel cooldown (12 frames) prevents animation cancel exploits while allowing responsive play
- DOT classification (Not/Semi/Full) elegantly handles whether damage ticks should interrupt
- Trust-client flag per attack phase allows selective lag compensation for different attack types
