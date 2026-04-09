# Damage Formulas -- Secrets of Grindea Reference

## Overview
Damage calculation flows through `_CollisionMaster_ResolveAttackCollision` in Game1.cs (~line 94940). The pipeline handles elemental resistance, elemental bonus damage, armor penetration, defense reduction, flat armor reduction, crit stacking, shield interactions, and PvP scaling.

## Complete Damage Pipeline

### Step 1: Base Damage
```csharp
int iInDamage = (int)(xStats.iBaseDamage * xStats.fDamageModifier);
```
- `iBaseDamage` is set by the attack/spell
- `fDamageModifier` defaults to 1.0, can be modified

### Step 2: Target Damage Multiplier
```csharp
if (xBaseStats.fMultiplyDamageOfAttackOnHit != 1.0)
    xStats.iBaseDamage = (int)Math.Max(1f, xStats.iBaseDamage * xBaseStats.fMultiplyDamageOfAttackOnHit);
```
Allows certain targets to take modified damage from specific attacks.

### Step 3: Elemental Resistance
```csharp
if (xBaseStats.denfResistances.ContainsKey(xStats.enAttackElement))
    iInDamage = (int)(iInDamage * xBaseStats.denfResistances[xStats.enAttackElement]);
```
Elements: `Neutral, Ice, Fire, Wind, Earth`

### Step 4: Card Armor Penetration (Player Attacker Only)
```csharp
// Echo card: +15% armor pen per card owned
num1 += SpellVariable.Get(Card_Echo_ArPen) * cardCount; // 0.15 per card
```

### Step 5: Elemental Bonus Damage (Player Attacker Only)
Physical attacks:
- **Insult to Injury**: +10% per level vs debuffed enemies (chilled/burning/acid/stunned)

Elemental spell attacks (Fire/Ice/Wind/Earth):
- **Specialist talent**: `damage * (1 + (totalSpellPointsInElement * specialistLevel * 0.15) / 100)`
- Counts ALL spell points across all 3 spells in that element

### Step 6: Lucky Seven (Equipment Effect)
```csharp
// Every 7th hit is a guaranteed crit
if (owner.xEquipment.lenActiveEquipmentEffects.Contains(LuckySeven_GuaranteedCrits))
{
    ++owner.xEntity.iLuckyNumberSeventhCounter;
    if (counter >= 7) { counter = 0; flag1 = true; } // guaranteed crit
}
```

### Step 7: Shield Interaction
If target is shielding in the correct direction:
```csharp
// Projectiles deal 70% damage to shields
if (xStats.bIsProjectile)
    iInDamage = (int)(iInDamage * 0.7f); // Misc_ProjectileShieldDamageMultiplier

iInDamage = (int)(iInDamage * xStats.fStrengthVersusShields);

// Perfect guard further reduces
if (xBaseStats.bPerfectGuard)
    iInDamage = (int)(iInDamage * xStats.fStrengthVersusPerfectGuard);

// Shrine buff: additional 25% damage reduction while shielding
if (ShrineBuffShield active)
    iInDamage = (int)(iInDamage * 0.25f);
```

Shield direction check uses 4 cardinal directions (0=up, 1=right, 2=down, 3=left) with 25-pixel tolerance for side hits.

### Step 8: Defense Reduction (THE CORE FORMULA)
```csharp
public int _AttackStats_CalculateDefenseReduction(
    int iDamageToApply, BaseStats xBaseStats,
    float fArmorPenetration = 0.0f,
    bool bIgnoreFlatArmor = false,
    bool bIgnoreDamageResistance = false)
{
    if (xBaseStats.iDefense >= 0)
    {
        // Percentage-based reduction: DEF / (DEF + 140)
        int effectiveDef = Math.Max(0, (int)(xBaseStats.iDefense * (1.0 - fArmorPenetration)));
        iDamageToApply = (int)(iDamageToApply / (1.0 + effectiveDef / 140.0));
    }
    else
    {
        // Negative DEF = damage amplification
        iDamageToApply = (int)(iDamageToApply * (1.0 + xBaseStats.iDefense / -140.0));
    }

    // Damage resistance multiplier
    if (!bIgnoreDamageResistance)
        iDamageToApply = (int)(iDamageToApply * xBaseStats.fDamageResistance);

    // Flat armor reduction: min(25% of damage, DEF/6)
    if (!bIgnoreFlatArmor)
    {
        float maxFlatReduction = (float)(xBaseStats.iDefense * (1.0 - fArmorPenetration) / 6.0);
        int flatReduction = (int)Math.Min(Math.Ceiling(iDamageToApply * 0.25), maxFlatReduction);
        iDamageToApply -= flatReduction;
    }

    if (iDamageToApply < 1) iDamageToApply = 1;
    return iDamageToApply;
}
```

**Defense Formula Breakdown:**
- **Percentage reduction**: `damage / (1 + effectiveDEF / 140)`
  - 70 DEF = 33% reduction
  - 140 DEF = 50% reduction
  - 280 DEF = 67% reduction
- **Flat reduction**: `min(ceil(damage * 0.25), effectiveDEF / 6)`
  - Caps at 25% of post-percentage damage OR DEF/6, whichever is lower
- **Armor Penetration**: reduces effective DEF by percentage (0.0 to 1.0)
  - Standard ArPen Level 2: 5%
  - Standard ArPen Level 3: 20%
  - Standard ArPen Level 4: 30%
- **Negative DEF**: damage is AMPLIFIED by `DEF / -140`
- **Minimum damage**: always at least 1

### Step 9: Damage Modifier vs Specific Targets
```csharp
// Modified resistance vs specific attacker entity
if (xDef.dxfModifiedResistanceVsTarget.ContainsKey(attacker))
    damageModifier *= resistance;
```

### Step 10: PvP Scaling
```csharp
if (bPvPEnabled && attacker is Player && defender is Player)
    num4 = (int)(num4 * 0.1f); // Spells_DamageMultiplierForPvP = 0.1
```
PvP damage is reduced to **10%** of normal.

### Step 11: Critical Hit Stacking
```csharp
int critChance = baseCritBonus; // from BaseStats.iCritChanceBonus

// Crit vulnerability on target
if (!xStats.bIgnoreCritVulnerability)
    critChance = (int)(critChance * xBaseStats.fCritVulnerabilityMultiplier)
                 + xBaseStats.iCritVulnerabilityFlat;

critChance += xStats.iBonusCritFlat;

// Lucky Seven guaranteed crit adds 1
if (luckySevenProc) ++numCrits;

// CRIT STACKING: every 100% crit = 1 guaranteed crit
// Remainder is rolled as percentage chance
for (totalCrit = critChance * (1 + fBonusCritMultiplier); totalCrit >= 100; totalCrit -= 100)
    ++numCrits;
if (random.Next(100) < totalCrit)
    ++numCrits;

// Each crit multiplies damage by CritDMG/100 (default 50% = 1.5x)
if (numCrits > 0)
{
    float critMult = CritDamageModifier / 100f; // default 0.5
    for (int i = 0; i < numCrits; i++)
        damage += (int)(damage * critMult);
}
```

**Critical stacking means:**
- 150% crit = 1 guaranteed crit + 50% chance for 2nd
- 250% crit = 2 guaranteed crits + 50% chance for 3rd
- Each crit applies CritDMG modifier multiplicatively
- Default CritDMG = 50% (so each crit = 1.5x)

### Step 12: Damage Variance
```csharp
if (damage > -2)
    damage += random.Next(2 + (int)(damage * 0.05));
```
Adds random 0 to `2 + 5% of damage` as variance.

### Step 13: Barrier Check
```csharp
if (xBaseStats.iBarrierHP > 0 && barrierPerfectGuardCountdown > 0)
{
    damage = 1; // Barrier + PG window = 1 damage
}
```

## Stat Formulas

### ATK Calculation
```csharp
iATK = (int)(iBaseATK * fBaseATKMultiplier + iMATK * (fMATKToATKConversionInPCT / 100.0));
```

### MATK Calculation
```csharp
iMATK = (int)(iBaseMATK * fBaseMATKMultiplier);
```

### Defense Calculation
```csharp
// Positive DEF: multiplied
iDefense = (int)(iBaseDEF * fBaseDEFMultiplier);
// Negative DEF: divided (or 0.1 if multiplier is 0)
iDefense = (int)(iBaseDEF / (fBaseDEFMultiplier != 0 ? fBaseDEFMultiplier : 0.1));
```

### Max HP Calculation
```csharp
iMaxHP = (int)Math.Round(iBaseMaxHP * fMaxHPMultiplier * fConfigSetting_MaxHPMultiplier);
```

### Movement Speed
```csharp
fMovementSpeed = (fBaseMoveSpeed + fCurrentMoveSpeedFlatAdd) * fCurrentMoveSpeedMod * fCurrentMoveSpeedDebuff;
```

### Weapon Damage Ratios
```
1H Melee ATK ratio: 0.77 (77% of ATK stat)
2H Melee ATK ratio: 1.00 (100% of ATK stat)
1H Wand MATK ratio: 0.40 (40% of MATK stat)
2H Wand MATK ratio: 0.40 (40% of MATK stat)
```

## Key Code Locations
- Main collision resolver: `Game1.cs` line 94940 (`_CollisionMaster_ResolveAttackCollision`)
- Defense reduction formula: `Game1.cs` line 94913 (`_AttackStats_CalculateDefenseReduction`)
- Damage modifier: `Game1.cs` line 94905 (`_AttackStats_GetDamageModifier`)
- AttackStats class: `/AttackPhases/AttackStats.cs`
- Elemental bonus damage: `/AttackPhases/AttackStats.cs` lines 154-216
- BaseStats properties: `/Stats n Attributes/BaseStats.cs` lines 227-267

## Design Patterns Worth Stealing
- The DEF/(DEF+140) formula creates diminishing returns that never reaches 100% -- prevents invincibility
- Dual defense system (percentage + flat) gives DEF double value -- both scale meaningfully
- Crit stacking beyond 100% is brilliant: rewards heavy crit investment with guaranteed multi-crits
- PvP multiplier as a simple 0.1x is elegant -- no separate PvP stat system needed
- Negative DEF amplifying damage creates meaningful debuff design space
- The 5% damage variance prevents "solved" damage breakpoints while keeping fights deterministic
- Shield direction checking with tolerance creates skill-based blocking without pixel-perfect requirements
