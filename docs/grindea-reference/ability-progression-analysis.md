# Ability Progression Analysis -- Patterns from Grindea

## What Changes Per Spell Level in Grindea

Grindea uses a 4-tier system (Base / Bronze / Silver / Gold) across 10 levels, with mechanical breakpoints at Silver (Lv5) and Gold (Lv10). Between tiers, upgrades are numerical: more damage, higher freeze chance, extra bounces. At tier boundaries, the spell transforms: Fireball gets a phoenix trail at Gold, Chain Lightning jumps to 10 bounces (from 5), Earth Spike gains a 75% stun chance. The pattern is clear -- numbers climb steadily, but new behaviors unlock at thresholds.

EP costs also rise per tier, so leveling a spell is not pure power gain. You pay more to do more. This creates a decision weight that pure buffs lack.

## How Talents Scale

Talents are strictly linear: 3 levels, one flat multiplier per level. Quick Reflexes is +20%/40%/60%. Surgeon is +3%/6%/9% crit. No exceptions, no diminishing returns, no breakpoints. This works because talents are small passive bonuses -- they do not need to feel transformative individually. Their power comes from stacking and synergy (Combo Starter + Bloodthirst, Insult to Injury + Chill procs).

The interesting talents are not the ones with big numbers but the ones with conditional triggers: Last Stand (+60% DEF below 20% HP), Manaburn (+30 MATK above 50% EP), Sudden Strike (+60 ASPD/ATK after 2s idle). These create playstyle-defining moments even at level 1. Additional levels just widen the safety margin.

## What Makes Level-Ups Feel Meaningful

Three patterns stand out:

1. **Mechanic unlocks at specific levels.** Grindea gates new behaviors behind Silver/Gold. Static Touch at Gold drops cooldown to 0.3s and hits 260% MATK -- it plays like a different spell than the base version. The numbers matter less than the qualitative shift.

2. **Resource tradeoffs scale with power.** Every spell costs more EP at higher tiers. Berserk Mode increases your damage resistance debuff at max level (30% vs 15%). Power always has a price, so leveling feels like a commitment, not just inflation.

3. **Conditional talents create identity.** A level 1 Last Stand already changes how you play. Level 3 just makes it stronger. The behavior shift happens at acquisition; levels amplify it.

## Adapting This for a 1-2-3-4-5 Linear System

With only 5 levels and no tier gates, every single level must carry weight. Recommendations:

- **Level 1: Acquisition = identity.** The ability should already change how you play. Do not gate the interesting part behind level 3.
- **Level 2-3: Numerical scaling + one minor mechanic addition.** Follow Grindea's mid-tier model. More damage, longer duration, but also something like +1 bounce, +1 projectile, or a new proc chance appearing.
- **Level 4: The qualitative shift.** This is the Silver-tier equivalent. Add a new behavior: an AOE component, a status effect, a combo extension. This is the level where the ability feels upgraded, not just bigger.
- **Level 5: Capstone transformation.** Grindea's Gold tier. The spell should look and feel different. Phoenix trail on Fireball, 10-bounce Chain Lightning, 75% stun Earth Spike. This is the "I maxed this out" payoff.

The critical lesson: do not spread 5 levels of +8% damage. Front-load the playstyle impact at level 1, put the big mechanical shift at level 4, and make level 5 a visible transformation. Levels 2-3 can be numerical -- players accept gradual scaling between meaningful jumps, but they need those jumps to exist.

For talents/passives: keep them linear and simple. Three levels of flat scaling is fine for passives. Their depth comes from build synergy, not per-level complexity. Invest the design budget in making each passive's level-1 effect create a real behavioral hook.
