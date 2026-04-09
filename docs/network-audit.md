# Network Architecture Audit

**Date:** 2026-04-08
**Scope:** All 35 files in `Code/Roguelite/`
**Goal:** Identify every networking gap before building abilities and the proc system on top.

---

## Authority Model Summary

The codebase follows a **host-authoritative** model:
- **Host** runs all damage resolution, enemy AI, spawning, and state mutations
- **Owner** (each player's client) handles their own input, camera, and local UI
- `[Sync]` replicates state from owner/host to all clients
- `[Rpc.Broadcast]` fires one-shot events (animations, sounds, VFX) on all clients

---

## Per-File Analysis

---

### File: Core/DamageType.cs
**Type:** Plain enum (no networking concern)
**Current sync:** None needed
**Missing sync:** None
**Issues:** None -- pure data definition

---

### File: Core/AttackData.cs
**Type:** Readonly struct, passed by value (no networking concern)
**Current sync:** None needed
**Missing sync:** None
**Issues:** None -- immutable snapshot, never stored on networked objects

---

### File: Core/CooldownTracker.cs
**Type:** Plain class (Dictionary-backed timer)
**Current sync:** None -- local-only tracker
**Missing sync:** **CRITICAL** -- Cooldowns are completely invisible to other clients. When player A uses an ability with a 12s cooldown, player B's HUD (if showing party frames) has no idea. More importantly, CooldownTracker is used by WeaponBase and AbilityComponent, both of which run on the owning client only. If a future "inspect ally" or "party cooldown display" feature is added, there is no sync path.
**Issues:**
- Not a Component, so it cannot have [Sync] properties
- For party-visible cooldowns, either: (a) promote key cooldown timestamps to [Sync] on the owning Component, or (b) accept that cooldowns are local-only and only sync the "is ability ready" boolean

---

### File: Core/HitContext.cs
**Type:** Readonly struct, passed by value
**Current sync:** None needed
**Missing sync:** None
**Issues:** None -- ephemeral combat data, consumed within a single frame on host

---

### File: Core/DamageResolver.cs
**Type:** Static class -- central damage authority
**Current sync:** N/A (static)
**Current RPC:** None (correct -- runs on host, mutates [Sync]'d health)
**Host-only logic:** DamageResolver itself has no host gate. Callers are responsible for only invoking it on the host. This is **correctly** done by:
  - EnemyBase.PerformAttack: host-only (OnUpdate gates `!Networking.IsHost`)
  - ProjectileBase.OnHit: gates `!Networking.IsHost`
  - Beam.ApplyBeamDamage: gates `!Networking.IsHost`
  - DamageZone.OnUpdate: gates `!Networking.IsHost`
**Missing sync:** None
**Issues:**
- `MeleeTrace()` is called from `SwordWeapon.PrimaryAttack()` which runs in `RoguelitePlayer.OnUpdate()` gated by `IsProxy`. **BUG**: This means melee damage is applied by the owning client, NOT the host. If a non-host player swings a sword, damage executes on their machine but DamageResolver mutates HealthComponent.Current locally. Because Current is [Sync] and the HealthComponent lives on the enemy (owned by host/nobody), the non-host client's write may be ignored or cause a race.
- Same issue for `BowWeapon.FireArrow()` -- it calls `ProjectileBase.Spawn()` which is fine (projectile is NetworkSpawned), but the projectile's `OnHit()` correctly gates `!Networking.IsHost`. So bow is actually OK.
- `Hitscan.Fire/FireSpread` called from debug tool -- correctly only runs on non-proxy debug (host in most cases).
- `Explosion.At()` has no host gate -- relies on callers. Called from DamageZone (host-gated) and debug (host-only). OK for now, but any future client-side call would double-deal damage.

---

### File: Core/RogueliteDebug.cs
**Current sync:** None (debug tool)
**Current RPC:** None
**Host-only logic:** Line 35 `if (IsProxy) return;` -- only runs for the local player. Since debug spawns enemies via `NetworkSpawn()`, this works if the local player IS the host. **BUG if non-host**: A non-host player using debug will `Clone().NetworkSpawn()` enemies -- s&box may reject this since only the host should spawn authoritative objects, or the objects will spawn but have wrong ownership.
**Issues:**
- `SpawnEnemy` uses `go.NetworkSpawn()` -- needs `Networking.IsHost` check, not `IsProxy`
- All Explosion/Hitscan/DamageZone debug calls will execute damage on the calling client, not necessarily the host
- Low priority since it's a debug tool, but will mislead during multiplayer testing

---

### File: Core/RogueliteGameManager.cs
**Current sync:** None needed (host-only spawn manager)
**Current RPC:** None needed
**Host-only logic:** `OnActive(Connection)` is an INetworkListener callback, only fires on host. Correct.
**Issues:**
- `OnDisconnected` does not clean up the player's GameObject. The player object will persist as an orphan. Whether this is a problem depends on the prefab's Orphaned Mode setting -- if set to "Destroy", s&box handles it. But if not, it will remain.
- No spawn point randomization or multiple spawn points for co-op (minor, not a networking bug)

---

### File: Components/FactionComponent.cs
**Current sync:** None -- `Faction` property is NOT [Sync]
**Missing sync:** **MEDIUM** -- Faction is set in `OnStart()` and never changes at runtime for players/enemies. Since it's set identically on all clients (enemies set it in `EnemyBase.OnStart()`, players in `RoguelitePlayer.OnStart()`), it works via deterministic initialization. However, if any future mechanic changes faction at runtime (charm, mind control, PvP toggle), it will desync.
**Issues:**
- Consider adding `[Sync]` to Faction as defensive measure: `[Sync] public Faction Faction { get; set; }`
- Cost is negligible (single enum sync)

---

### File: Components/ArmorComponent.cs
**Current sync:** None -- `BaseArmor` is NOT [Sync]
**Missing sync:** **LOW** -- Armor is set once at spawn and queried by DamageResolver on the host. Clients never need to read enemy armor for gameplay. If a future "inspect enemy" tooltip or damage preview needs it, it will need sync.
**Issues:** None for current functionality

---

### File: Components/AbilityComponent.cs
**Current sync:** None -- ability slots are a plain C# array, cooldowns are a plain CooldownTracker
**Missing sync:** **HIGH**
  - `_slots` array (which abilities are equipped) is not synced. If player A equips CubeAbility, player B has no way to know what abilities A has. This matters for: party frames, inspect, buff/debuff display, spectating.
  - Cooldown state is not synced (same issue as CooldownTracker above)
  - `TryActivate()` runs on the owning client (called from `RoguelitePlayer.OnUpdate()` which gates `IsProxy`). The ability executes locally. If the ability has side effects (CubeAbility stuns an enemy), those effects happen on the client, not the host. **BUG**: CubeAbility calls `enemy.ApplyStun()` which gates `!Networking.IsHost` -- so a non-host player casting CubeAbility on an enemy will have the stun REJECTED.
**Current RPC:** None
**Missing RPC:** **CRITICAL** -- Ability activation needs an RPC flow:
  1. Owner presses ability key -> sends RPC to host
  2. Host validates (cooldown, range, alive) -> executes effect
  3. Host broadcasts visual/audio to all clients
**Host-only logic:** None currently. All logic runs on the owner. This is wrong for authoritative abilities.
**Issues:**
- Entire ability activation flow needs to be rearchitected for multiplayer
- Either: (a) `[Rpc.Host]` from owner to request activation, host executes, or (b) owner executes and host validates (optimistic), or (c) accept owner-authority for abilities (least secure, simplest)

---

### File: Components/AggroComponent.cs
**Current sync:** None -- threat table is a plain Dictionary
**Missing sync:** None needed -- aggro only matters on the host where enemy AI runs
**Host-only logic:** Aggro is used by EnemyBrain which runs host-only. Correct.
**Issues:** None -- host-only data, no need to replicate threat tables

---

### File: Components/HealthComponent.cs
**Current sync:** `[Sync] Current`, `[Sync] IsDead` -- CORRECT
**Missing sync:**
  - `MaxHealth` is NOT [Sync]. **MEDIUM** -- clients need MaxHealth to render health bars. Currently set in `Init()` which runs on host/owner in OnStart. If MaxHealth ever changes at runtime (level-up, buff), clients won't see the new max.
  - Events (`OnDeath`, `OnDamageTaken`, etc.) are C# events, NOT networked. They only fire on the machine that calls `ApplyDamage()`. Since damage is supposed to be host-only, only the host gets these events. **BUG**: `RoguelitePlayer.OnStart()` subscribes to `Health.OnDeath` to set `IsAlive = false`. But `ApplyDamage` runs on host (for enemies attacking player). The player's `Health.OnDeath` fires on the HOST, not on the OWNING CLIENT. The host will set `IsAlive = false` on the player object -- but since `IsAlive` is [Sync] on a player-owned object, only the OWNER can write to it. **This means the host writing IsAlive = false may not replicate to clients.**
**Current RPC:** None on HealthComponent itself (death broadcast is on EnemyBase)
**Missing RPC:** Consider broadcasting damage events (for floating damage numbers, hit flash on all clients)
**Issues:**
- **CRITICAL**: The death flow for players is broken. When host calls `ApplyDamage()` on a player's HealthComponent, `OnDeath` fires on the host, which calls `HandleDeath()` on RoguelitePlayer, which sets `IsAlive = false`. But the player object is owned by the player's Connection, so the host may not have write authority on [Sync] properties. Need to either: (a) make the host the authority for IsAlive via ownership model, or (b) use an RPC from host to owner to trigger death, or (c) have the owning client detect `IsDead == true` (which IS host-writable because HealthComponent lives on the same object) and set `IsAlive` locally.
- **MaxHealth** should be `[Sync]` for health bar rendering

---

### File: Player/PlayerClass.cs
**Type:** Enum + static data (no networking concern)
**Current sync:** None needed
**Issues:** None -- pure data definitions

---

### File: Player/PlayerMovement.cs
**Current sync:** None -- velocity, wish velocity, frozen state, dash cooldown are all local
**Missing sync:**
  - `IsFrozen` is NOT [Sync]. **HIGH** -- If CubeAbility freezes an ally, only the owning client knows they're frozen. Other clients see them still moving. The freeze visual (if any) won't show for others.
  - `WishVelocity` not synced -- acceptable, position sync via CharacterController handles visual position.
  - Dash has no broadcast. Other clients won't see/hear a dash effect.
**Host-only logic:** Line 29 `if (IsProxy) return;` -- correct, only owner drives movement
**Current RPC:** None
**Missing RPC:**
  - `BroadcastDash()` needed for dash VFX/SFX on all clients
  - IsFrozen should be [Sync] or freeze/unfreeze should broadcast
**Issues:**
- `DashForward()` does a trace and calls `onHit` callback which may call `DamageResolver.Resolve()` (used by SwordWeapon.PiercingDash). This runs on the OWNER, not the host. **BUG** for non-host players.

---

### File: Player/RoguelitePlayer.cs
**Current sync:** `[Sync] IsAlive`
**Missing sync:**
  - `Class` (PlayerClass) is NOT [Sync]. **MEDIUM** -- other clients can't display which class a player picked. Set once in inspector/startup but never replicated.
  - `ActiveWeapon` is NOT [Sync]. **HIGH** -- other clients don't know what weapon another player is using. No weapon model shown, no weapon-specific animations. For now weapons are components on the player GameObject (which is NetworkSpawned), so the component exists on all clients. But the `ActiveWeapon` reference is a local field -- if weapon swapping happens, other clients won't know which weapon is "active".
**Current RPC:** None
**Missing RPC:**
  - Weapon swap should broadcast (for weapon model visibility, animations)
  - Death/revive should broadcast for VFX/SFX
**Host-only logic:**
  - `OnStart()` line 27: `if (!IsProxy)` gates health init and movement init. This runs on the OWNER. But `Health.Init()` sets `Current = maxHp` which is [Sync] -- since the player object is owned by this connection, the owner CAN write [Sync] properties. This is correct.
  - `OnUpdate()` line 57: `if (IsProxy) return;` gates all input handling. Correct.
**Issues:**
- **CRITICAL**: `HandleWeaponInput()` calls `ActiveWeapon.PrimaryAttack()` / `SecondaryAttack()` which for SwordWeapon calls `DamageResolver.MeleeTrace()` directly. This runs on the OWNER, not the host. For the host player this works. For a non-host player, damage will be applied locally on their client but the health change may not replicate (enemy HealthComponent is not owned by that player).
- **CRITICAL**: `HandleAbilityInput()` same issue -- abilities execute on owner, not host.
- `EquipWeapon()` destroys/creates components locally. Other clients may not see this if the component lifecycle isn't properly network-replicated.
- HUD creation in OnStart is correctly gated by `!IsProxy` -- only local player gets HUD.

---

### File: Player/PlayerCamera.cs
**Current sync:** `[Sync] EyeAngles` -- CORRECT
**Missing sync:** None
**Current RPC:** None needed
**Host-only logic:** Input/camera positioning gated by `!IsProxy`. Correct.
**Issues:**
- Body rotation (line 63) runs on ALL clients using synced EyeAngles. Correct.
- Body visibility (line 72) correctly tags local player as "viewer". Correct.
- Well-implemented. No networking issues.

---

### File: Weapons/WeaponBase.cs
**Current sync:** None -- cooldowns are local CooldownTracker
**Missing sync:**
  - No weapon state is synced. Combo step, cooldown remaining, etc. are invisible to other clients.
**Current RPC:** None
**Missing RPC:** None on base class (subclasses handle broadcasts)
**Host-only logic:** `OnUpdate()` runs on all clients (no IsProxy gate) but only ticks cooldowns and calls `OnWeaponTick()`. This is fine -- weapon visuals should update on all clients.
**Issues:**
- `PrimaryAttack()` / `SecondaryAttack()` are called from `RoguelitePlayer.HandleWeaponInput()` which is owner-only. The attack logic (traces, damage) runs on the owner. For melee weapons, this means **non-host players apply damage locally without host authority**.
- Pattern needed: Owner presses attack -> Owner calls `[Rpc.Host]` to request attack -> Host validates -> Host calls DamageResolver -> Host broadcasts animation/VFX

---

### File: Weapons/Melee/SwordWeapon.cs
**Current sync:** None
**Missing sync:**
  - `_comboStep` NOT synced. **MEDIUM** -- other clients can't see which combo animation to play. The `BroadcastSwing(comboStep)` sends the step as a parameter, which is good for animation. But if any game logic depends on knowing another player's combo state, it's missing.
  - `_parryActive` NOT synced. **HIGH** -- if parry grants invulnerability or damage reduction, the host needs to know. Currently parry is TODO, so this is a future concern.
**Current RPC:** `[Rpc.Broadcast] BroadcastSwing(int comboStep)` -- CORRECT pattern
**Missing RPC:**
  - Parry start/end needs broadcast for visual shield effect
  - Blade wave needs broadcast for VFX
  - Piercing dash needs broadcast for trail VFX
**Host-only logic:** None -- all attack logic runs on owner. **BUG** (see WeaponBase issues)
**Issues:**
- **CRITICAL**: `PrimaryAttack()` calls `DamageResolver.MeleeTrace()` on the OWNER. For non-host players, this applies damage locally on their client to an enemy they don't own. The host never knows the attack happened.
- **CRITICAL**: `PiercingDash()` calls `movement.DashForward()` with a hit callback that calls `DamageResolver.Resolve()` -- same owner-authority problem.
- `FireBladeWave()` calls `DamageResolver.MeleeTrace()` -- same issue.
- `ApplyComboKnockback()` calls `enemy.ApplyKnockback()` which gates `!Networking.IsHost`. So knockback from a non-host player's combo is silently ignored. The damage still applies (incorrectly, on client), but knockback doesn't.

---

### File: Weapons/Ranged/BowWeapon.cs
**Current sync:** None
**Missing sync:**
  - `_isDrawing` / `DrawProgress` NOT synced. **MEDIUM** -- other clients can't see the draw animation or know when to play release animation.
**Current RPC:** `[Rpc.Broadcast] BroadcastFire()` -- CORRECT pattern for fire animation/sound
**Missing RPC:**
  - Draw start/cancel should broadcast for draw animation on other clients
**Host-only logic:** `OnWeaponTick()` line 48: `if (Owner.IsProxy) return;` -- correct, only owner handles draw input.
**Issues:**
- `FireArrow()` calls `ProjectileBase.Spawn()` which does `obj.NetworkSpawn()`. The projectile is visible to all clients. The projectile's `OnHit()` gates `!Networking.IsHost` -- so damage is correctly host-only. **Bow is the best-networked weapon currently.**
- However, `ProjectileBase.Spawn()` is called by the OWNER (not necessarily host). The `scene.CreateObject()` + `NetworkSpawn()` from a non-host client may work in s&box (clients can spawn network objects), but ownership may be unexpected. The projectile has no owner connection, so the host may not be able to write to it either. Need to verify s&box behavior for client-spawned NetworkSpawn objects.

---

### File: Abilities/IAbility.cs
**Type:** Interface (no networking concern)
**Issues:** The interface has no network awareness. `TryActivate` takes a RoguelitePlayer and returns bool. This is a local-only pattern. For networked abilities, the interface needs to either:
  - Be wrapped in an RPC flow on AbilityComponent, or
  - Include host-side validation in the implementation

---

### File: Abilities/CubeAbility.cs
**Current sync:** None (plain class, not a Component)
**Current RPC:** None
**Missing sync/RPC:** **CRITICAL**
  - `enemy.ApplyStun()` gates `!Networking.IsHost`. Non-host caster's stun is rejected.
  - `player.Movement.SetFrozen(true)` modifies the TARGET player's movement. If the caster is not the target's owner, this write happens on the wrong client.
  - `player.Abilities.AccelerateCooldowns()` modifies the TARGET player's cooldowns. Same issue.
  - `UnfreezeAfterDelay()` uses `GameTask.DelayRealtimeSeconds` -- this timer runs on the caster's client only. If the caster disconnects, the target stays frozen forever.
  - No visual/audio broadcast for the ice cube effect.
**Issues:**
- **CRITICAL**: Entire ability is broken for non-host players in multiplayer:
  1. Non-host casts on enemy -> `ApplyStun()` rejected by host gate -> stun doesn't happen
  2. Non-host casts on ally -> freeze/cooldown changes happen on the caster's machine, not the target's or the host's
- Pattern needed: CubeAbility.TryActivate should ONLY validate locally (range, target exists), then fire `[Rpc.Host]` to request the effect. Host applies stun/freeze/cooldown acceleration and broadcasts VFX.

---

### File: Enemies/EnemyBase.cs
**Current sync:** `[Sync] IsStunned`, `[Sync] IsMoving`, `[Sync] IsAttacking` -- GOOD
**Missing sync:**
  - `CurrentTarget` (RoguelitePlayer reference) NOT synced. **LOW** -- clients don't need to know who the enemy is targeting. If a future "aggro indicator" UI is added, this would need sync.
  - `_inKnockback` NOT synced. **MEDIUM** -- during knockback, NavAgent is disabled and Rigidbody drives position. Other clients see position via network transform sync, but don't know WHY the enemy is moving erratically. If knockback-specific animation is added, it needs sync.
  - `EnemyName` NOT synced. **LOW** -- set in OnStart deterministically from subclass. Works if prefab setup is identical.
**Current RPC:**
  - `[Rpc.Broadcast] BroadcastAttackAnim()` -- CORRECT, plays attack animation on all clients
  - `[Rpc.Broadcast] BroadcastDeath()` -- CORRECT pattern (currently empty, placeholder for death VFX/sound)
**Missing RPC:**
  - Knockback start/end could use broadcast for VFX (stagger animation, impact particles)
  - Stun visual effect needs broadcast (currently stun is [Sync]'d, so clients can detect it and play local VFX -- this works)
**Host-only logic:**
  - `OnUpdate()` line 77: `if (!Networking.IsHost) return;` after animation update -- CORRECT, AI only on host
  - `ApplyStun()` line 332: `if (!Networking.IsHost) return;` -- CORRECT
  - `ApplyKnockback()` line 239: `if (!Networking.IsHost) return;` -- CORRECT
  - `PerformAttack()` calls DamageResolver -- runs under host gate. CORRECT.
**Issues:**
- Animation update (line 72, `UpdateAnimation()`) runs BEFORE the host gate. This is intentional -- animation should run on all clients. CORRECT.
- `DelayedDamage()` uses `GameTask.DelaySeconds()` -- runs on host only (correct). Re-validates target alive and in range. Good defensive coding.
- `EndKnockback()` uses `await Task.Frame()` loop on host. Position syncs via network transform. Acceptable but jittery -- clients see delayed position updates during knockback physics.
- Enemy death: `OnDeath()` calls `BroadcastDeath()` then `GameObject.Destroy()`. The destroy happens on host. s&box should propagate the destroy to all clients via networking. Order concern: if BroadcastDeath fires async, destroy might happen before clients process the broadcast. Consider adding a short delay before destroy.

---

### File: Enemies/EnemyBrain.cs
**Type:** Plain class (not a Component, no networking)
**Current sync:** N/A
**Host-only logic:** Only instantiated and ticked by EnemyBase, which is host-only. CORRECT.
**Issues:**
- `SelectTarget()` calls `Scene.GetAllComponents<RoguelitePlayer>()` -- this works on host because all player objects exist there. CORRECT.
- `HasLineOfSight()` uses scene traces -- host-only. CORRECT.
- No networking concerns.

---

### File: Enemies/RusherEnemy.cs
**Current sync:** Inherits from EnemyBase (IsStunned, IsMoving, IsAttacking)
**Missing sync:**
  - `_isCharging` NOT synced. **HIGH** -- other clients don't know the rusher is charging. They see position changes (via network transform) but no charge animation/VFX. The rusher overrides `UpdateAnimation()` to show `run_N` during charge, but `_isCharging` is local to host. Clients will see the base class animation (walk/idle based on IsMoving) instead of the charge animation.
**Current RPC:** Inherits BroadcastAttackAnim, BroadcastDeath
**Missing RPC:**
  - `BroadcastChargeStart()` / `BroadcastChargeEnd()` needed for charge VFX, screenshake, audio
**Host-only logic:** `ChaseTarget()` and `UpdateCharge()` run under host gate (inherited from EnemyBase.OnUpdate). CORRECT.
**Issues:**
- **HIGH**: Charge animation is broken on clients. `UpdateAnimation()` checks `_isCharging` which is always false on non-host clients. They'll see idle/walk instead of the charge run.
- Fix: Add `[Sync] public bool IsCharging { get; set; }` and set it in StartCharge/EndCharge. UpdateAnimation already runs on all clients.
- `UpdateCharge()` directly sets `WorldPosition` -- this works because the host owns the enemy object and network transform syncs the position. But the interpolation may look jittery due to the high charge speed.

---

### File: Enemies/SummonerEnemy.cs
**Current sync:** Inherits from EnemyBase
**Missing sync:**
  - `_isCasting` NOT synced. **HIGH** -- clients don't see the cast animation. The point anim is played locally on host via direct sequence manipulation, but clients run `base.OnUpdate()` which uses the base animation system.
  - `_isRecovering` NOT synced. **MEDIUM** -- similar to _isCasting, affects animation state.
  - `_isCastingProjectile` NOT synced. **MEDIUM** -- attack_weapon animation not visible on clients.
  - `_summonIndicator` created via `Scene.CreateObject()` without `NetworkSpawn()`. **CRITICAL** -- the red ring and decal indicator are only visible on the HOST. Other players never see the summon telegraph.
**Current RPC:** Inherits BroadcastAttackAnim, BroadcastDeath
**Missing RPC:**
  - `BroadcastSummonCastStart(Vector3 position)` needed so all clients see the indicator
  - `BroadcastSummonComplete()` for spawn VFX on all clients
  - `BroadcastFallbackProjectile()` (or use ProjectileBase.Spawn which already NetworkSpawns -- check if this is correct)
**Host-only logic:** OnUpdate gates via `Networking.IsHost` (inherited). Cast/recovery logic runs host-only. CORRECT.
**Issues:**
- **CRITICAL**: Summon indicators (red ring, expanding decal) are host-only scene objects. Clients see enemies appear out of nowhere with no telegraph. Fix: Either NetworkSpawn the indicator objects, or use an RPC to broadcast the spawn position and have each client create their own local indicator.
- `CompleteSummon()` calls `MinionPrefab.Clone().NetworkSpawn()` -- CORRECT, minion is network-spawned and visible to all.
- `FadeMinionThenIndicator()` async task runs on host only. The fade-in effect (scaling, alpha) is only visible on host. Clients see the minion pop in at full size/alpha. Fix: Either accept the pop-in or broadcast the spawn with a client-side fade-in effect.
- `FireFallbackProjectile()` calls `ProjectileBase.Spawn()` which does `NetworkSpawn()`. The projectile is visible to all. The projectile's `OnHit()` gates host-only. CORRECT.
- `PlayPointAnim()` and `PlayCastAnim()` directly set Sequence on host. Clients don't see these animations. Needs [Rpc.Broadcast] wrappers.

---

### File: Enemies/FlyingEnemy.cs
**Current sync:** Inherits from EnemyBase
**Missing sync:**
  - `_isSwooping` NOT synced. **HIGH** -- clients don't see the swoop dive animation/trajectory. They see position changes via network transform, but the smooth arc is interpolated on host only. Clients see jittery position jumps.
  - `HoverHeight` / `_bobOffset` NOT synced. **LOW** -- bob is cosmetic and each client could compute it locally if they knew the timer. Position sync from host captures the result anyway.
**Current RPC:** Inherits BroadcastAttackAnim, BroadcastDeath
**Missing RPC:**
  - `BroadcastSwoopStart()` / `BroadcastSwoopEnd()` needed for dive VFX, screenshake, sound
  - `BroadcastFireProjectile()` not needed because ProjectileBase.Spawn does NetworkSpawn
**Host-only logic:** `OnUpdate()` gates most logic via `Networking.IsHost` (inherited). CORRECT.
**Issues:**
- **HIGH**: Swoop arc is only computed on host. Clients see position updates with network interpolation, which will smooth out the arc somewhat but lose the dramatic dive effect. For a better experience, broadcast swoop start position/direction/target and let clients compute the arc locally.
- `ChaseTarget()` directly sets `WorldPosition` -- host-only, position syncs via network transform. Acceptable.
- `FireProjectile()` uses `ProjectileBase.Spawn()` with `NetworkSpawn()`. CORRECT.
- `StartSwoop()` hit detection in `UpdateSwoop()` calls `DamageResolver.Resolve()` under host context. CORRECT.
- `Nav.Enabled = false` in OnStart -- runs on all clients. CORRECT (all clients need the same component state).

---

### File: Combat/HitDetection.cs
**Type:** Static utility class
**Current sync:** N/A
**Host-only logic:** Pure query functions, no state mutation. Callers determine authority.
**Issues:**
- `Sphere()` iterates `Scene.GetAllComponents<RogueliteHealthComponent>()` -- this works on host where all objects exist. If called on a client that's missing some objects (shouldn't happen with proper NetworkSpawn), results may differ.
- No networking concerns in the utility itself.

---

### File: Combat/AttackPattern.cs
**Type:** Static helpers for spawning projectile patterns
**Current sync:** N/A
**Host-only logic:** Depends on callers. All methods call `ProjectileBase.Spawn()` which does `NetworkSpawn()`.
**Issues:**
- `Burst()` and `Spiral()` use `GameTask.DelaySeconds()` for staggered spawning. If called from host, all spawns happen on host and NetworkSpawn replicates them. CORRECT.
- If called from a non-host client (e.g., debug tool), the spawns may have ownership issues.
- All patterns correctly use ProjectileBase.Spawn which handles networking. GOOD.

---

### File: Combat/CombatVfx.cs
**Type:** Static VFX utility
**Current sync:** None -- creates local scene objects without NetworkSpawn
**Missing sync:** **HIGH** -- ALL VFX from this class are LOCAL ONLY. They are not NetworkSpawned and have no RPC broadcast. This means:
  - Hitscan tracer lines: only visible on the client that fires
  - Impact markers: only visible locally
  - Explosion sphere visuals: only visible on the host (since Explosion.At is host-only)
  - DamageZone circle: only visible on host
**Issues:**
- **HIGH**: Every visual effect created by CombatVfx is invisible to other clients. This is the single biggest visual desync in the codebase. Hitscan shots, explosions, impact markers -- none are visible to other players.
- Fix options:
  1. NetworkSpawn each VFX object (expensive, adds network overhead for ephemeral visuals)
  2. Use `[Rpc.Broadcast]` at the call site to have each client create their own local VFX
  3. Use s&box's particle system which may have built-in network replication
- For now, the simplest fix is wrapping VFX calls in RPCs at the combat system level (Hitscan, Explosion, etc.)

---

### File: Combat/Explosion.cs
**Type:** Static one-shot area damage
**Current sync:** N/A (static)
**Host-only logic:** No internal gate -- relies on callers. Damage via DamageResolver. VFX via CombatVfx.
**Issues:**
- `CombatVfx.Sphere()` calls at lines 54-55 create LOCAL-ONLY visuals. Explosion effect only visible on the machine that calls `Explosion.At()`. **HIGH** -- if host calls it, only host sees the boom.
- Fix: Add an `[Rpc.Broadcast]` wrapper or have the calling site broadcast the explosion position for client-side VFX.

---

### File: Combat/DamageZone.cs
**Current sync:** None -- all properties are [Property] only
**Missing sync:**
  - Zone properties (Radius, DamagePerTick, Type, Lifetime) are NOT synced. Since the zone is created via `DamageZone.Spawn()` which sets properties BEFORE `NetworkSpawn()`, the initial values should replicate via s&box's property serialization. But if properties change after spawn, clients won't see updates.
  - Zone visual (`_zoneVisual`) is created as a local child object. **MEDIUM** -- since it's a child of the NetworkSpawned zone object, s&box may replicate it. But the ModelRenderer tint and scale are set locally. Need to verify if child objects of NetworkSpawned parents auto-replicate.
**Current RPC:** None
**Missing RPC:** None critical -- the visual is created on all clients if the parent replicates correctly.
**Host-only logic:** `OnUpdate()` line 42: `if (!Networking.IsHost) return;` -- CORRECT, only host ticks damage and lifetime.
**Issues:**
- `DrawZoneVisual()` is called from `OnUpdate()` which returns early on non-host clients. **BUG**: The zone visual is only created on the HOST. Clients see the zone GameObject (via NetworkSpawn) but no visual representation. Fix: Move `DrawZoneVisual()` to `OnStart()` or call it before the host gate.
- `Creator` property holds a Component reference. On clients, this reference may be null or invalid if the creator object's component isn't properly resolved across the network. DamageResolver uses Creator for faction checks and damage attribution -- since damage only runs on host, this is OK.
- `Spawn()` correctly calls `obj.NetworkSpawn()`. CORRECT.

---

### File: Combat/Hitscan.cs
**Type:** Static instant-hit traces
**Current sync:** N/A (static)
**Host-only logic:** No internal gate -- relies on callers.
**Issues:**
- All three methods (`Fire`, `FirePenetrating`, `FireSpread`) call `DamageResolver.Resolve()` directly. If called from a non-host client, damage is applied locally without authority.
- All three methods call `CombatVfx.Line()` and `CombatVfx.Impact()` which are LOCAL-ONLY. Tracer lines and impact markers are invisible to other clients.
- **HIGH**: Hitscan effects are completely invisible in multiplayer. If player A fires a shotgun, player B sees nothing -- no tracers, no impacts, no sound.
- Fix: The calling site (weapon, debug tool) should broadcast the fire event with origin/direction, and each client creates their own local tracers.

---

### File: Combat/Beam.cs
**Current sync:** None -- IsFiring, BeamEndPoint, BeamTarget are plain properties
**Missing sync:**
  - `IsFiring` NOT synced. **CRITICAL** -- the beam visual (LineRenderer) is only shown on the owning client. Other clients see nothing.
  - `BeamEndPoint` NOT synced. Clients can't draw the beam line.
  - `BeamTarget` NOT synced. Clients don't know what's being hit.
  - Chain arc visuals (`_activeChains`) NOT synced.
**Current RPC:** None
**Missing RPC:**
  - `BroadcastBeamState(bool firing, Vector3 endpoint)` needed every frame or on state change
  - Chain arc positions need broadcast for visual
**Host-only logic:** Line 133: damage tick gated by `!Networking.IsHost`. CORRECT.
**Issues:**
- **CRITICAL**: Beam is completely invisible to other clients. The LineRenderer exists on the beam's GameObject, but `_lineRenderer.Enabled` and `_lineRenderer.VectorPoints` are set locally. s&box does not auto-sync LineRenderer state.
- The beam is created via `Beam.Create()` / `Beam.CreateChain()` which add the component to an existing GameObject (not NetworkSpawned separately). If the parent is the player (which IS NetworkSpawned), the Beam component exists on all clients, but its state is all local.
- Fix: Sync IsFiring and BeamEndPoint via [Sync], and have client-side rendering use those synced values.
- Chain visuals: Create child LineRenderers on all clients, sync chain target positions.

---

### File: Projectiles/ProjectileBase.cs
**Current sync:** None on the component (position syncs via network transform on the GameObject)
**Missing sync:**
  - `_velocity` NOT synced. **MEDIUM** -- projectile movement is computed locally on each client from initial rotation and speed. Since all clients have the same initial state (position, rotation, speed set before NetworkSpawn), movement should be deterministic. Gravity and homing may cause drift over time.
  - `HomingTarget` NOT synced. **HIGH** -- homing projectiles: only the spawning client knows the target. Other clients compute straight-line movement. The projectile will appear to fly straight on their screen while curving on the spawner's screen.
**Current RPC:** None
**Missing RPC:**
  - Could broadcast hit VFX on impact
**Host-only logic:** `OnHit()` line 98: `if (!Networking.IsHost) return;` -- CORRECT
**Issues:**
- **HIGH**: Homing projectiles are broken for non-host clients. `HomingTarget` is set via `SpawnHoming()` before `NetworkSpawn()`, but it's a GameObject reference, not a [Sync] property. s&box may serialize it during NetworkSpawn, but this is unreliable for live references.
- `OnUpdate()` has no authority check -- movement runs on ALL clients. This is intentional for smooth visuals (client-side prediction). However, if movement diverges (homing, randomness), position will desync between clients until the next network transform update.
- `Spawn()` correctly calls `NetworkSpawn()`. CORRECT.
- `OnLifetimeExpired()` calls `Explosion.At()` which has no host gate -- but the explosion's DamageResolver calls would be redundant on non-host clients. The VFX would show on all clients (good) if the explosion is triggered on all clients. But `Explosion.At()` also calls DamageResolver, so it would double-deal damage. **BUG**: If multiple clients trigger `OnLifetimeExpired()` simultaneously, damage is applied by each. Since `OnHit` gates host-only but `OnLifetimeExpired` does NOT, the host's DamageResolver call is the correct one, but non-host clients also call it (doing nothing useful since their Resolve results are local). This is wasteful but not harmful because the health component is [Sync]'d from host.

---

### File: World/WeaponPedestal.cs
**Current sync:** None
**Missing sync:**
  - Pedestal availability (has the weapon been picked up?) NOT synced. **HIGH** -- if player A picks up the sword, player B still sees the pedestal as available and can also pick it up. Infinite weapon duplication.
  - Weapon type NOT synced (set via [Property], should be consistent if scene is identical).
**Current RPC:** None
**Missing RPC:**
  - `BroadcastPickup()` needed to: destroy/disable pedestal on all clients, play pickup VFX
  - Or: `[Rpc.Host] RequestPickup()` to validate on host, then host broadcasts result
**Host-only logic:** None -- `OnUpdate()` checks `IsProxy` for the nearby player, but `GiveWeapon()` runs on ANY client that presses E near the pedestal.
**Issues:**
- **CRITICAL**: `GiveWeapon()` runs on the local client. It destroys the existing weapon component and creates a new one. This only happens on that player's machine. Other clients and the host don't know the weapon changed.
- **CRITICAL**: No single-use protection. Multiple players can pick up from the same pedestal.
- **HIGH**: Component creation (`player.Components.Create<SwordWeapon>()`) on a NetworkSpawned player object from the owning client -- s&box may not replicate dynamically-added components to other clients.
- Fix: Gate GiveWeapon behind an `[Rpc.Host]` call. Host validates, creates the weapon component (or changes a synced WeaponType enum), marks pedestal as taken, broadcasts the change.

---

## Critical Bug Summary

| # | Bug | Severity | Files Affected |
|---|-----|----------|----------------|
| 1 | Melee damage runs on owner, not host | CRITICAL | SwordWeapon, WeaponBase, RoguelitePlayer |
| 2 | Ability activation runs on owner, not host | CRITICAL | AbilityComponent, CubeAbility, RoguelitePlayer |
| 3 | CubeAbility stun rejected for non-host caster | CRITICAL | CubeAbility, EnemyBase |
| 4 | Player death flow: host writes [Sync] on player-owned object | CRITICAL | HealthComponent, RoguelitePlayer |
| 5 | All CombatVfx are local-only (invisible to other clients) | HIGH | CombatVfx, Hitscan, Explosion, Beam |
| 6 | Beam is completely invisible to other clients | CRITICAL | Beam |
| 7 | Summoner indicators are host-only | CRITICAL | SummonerEnemy |
| 8 | WeaponPedestal has no network authority or single-use protection | CRITICAL | WeaponPedestal |
| 9 | Homing projectiles desync across clients | HIGH | ProjectileBase |
| 10 | Rusher charge animation not visible on clients | HIGH | RusherEnemy |
| 11 | Flying enemy swoop not visible on clients | HIGH | FlyingEnemy |
| 12 | Player IsFrozen not synced | HIGH | PlayerMovement |
| 13 | DamageZone visual only on host | HIGH | DamageZone |
| 14 | HealthComponent.MaxHealth not synced | MEDIUM | HealthComponent |
| 15 | PlayerClass not synced | MEDIUM | RoguelitePlayer |

---

## Architecture Recommendations

### 1. Damage Authority Pattern (Weapons + Abilities)

**Problem:** Melee weapons and abilities execute damage on the owning client, not the host.

**Recommended Pattern:**
```
Owner Input -> [Rpc.Host] RequestAttack(attackType, aimData) -> Host validates -> Host runs DamageResolver -> Host broadcasts VFX/anim
```

Concrete implementation:
```csharp
// On RoguelitePlayer (owner-side):
private void HandleWeaponInput()
{
    if (Input.Pressed("attack1"))
        RequestPrimaryAttack(Camera.EyeAngles); // Send aim data to host
}

[Rpc.Host]
private void RequestPrimaryAttack(Angles aimAngles)
{
    // Host executes the attack with the provided aim data
    ActiveWeapon?.HostPrimaryAttack(aimAngles);
}

// On SwordWeapon (host-side):
public void HostPrimaryAttack(Angles aimAngles)
{
    // ... melee trace using aimAngles ...
    DamageResolver.MeleeTrace(...);
    BroadcastSwing(_comboStep); // All clients see animation
}
```

**Alternative (simpler, less secure):** Keep owner-authority for attacks but route ALL damage through an `[Rpc.Host]` on a central combat manager. The owner detects the hit locally and sends `RequestDamage(targetId, attackData)` to host. Host validates range/line-of-sight and applies. This avoids restructuring every weapon.

### 2. Ability Activation Pattern

**Problem:** Abilities execute entirely on the owner with no host involvement.

**Recommended Pattern:**
```csharp
// AbilityComponent:
public bool TryActivate(int slot, RoguelitePlayer caster, GameObject target)
{
    // Local validation only (cooldown check, alive check)
    if (!CanActivate(slot, caster)) return false;
    
    // Send to host for execution
    RequestAbilityActivation(slot, target?.Id ?? Guid.Empty);
    return true;
}

[Rpc.Host]
private void RequestAbilityActivation(int slot, Guid targetId)
{
    // Host resolves target, validates, executes effect
    var ability = _slots[slot];
    var target = Scene.Directory.FindByGuid(targetId);
    ability.HostExecute(caster, target);
    StartCooldown(...); // Host manages cooldown
}
```

For abilities, add a `HostExecute` method to IAbility (or a parallel interface) that runs on the host.

### 3. Enemy Attack Phases (Cast Time / Startup / Active)

**Problem:** Enemy state machine phases (casting, charging, swooping) are not visible to clients.

**Recommended Pattern:** Use [Sync] enums for enemy phase state:
```csharp
public enum EnemyPhase { Idle, Windup, Active, Recovery }

[Sync] public EnemyPhase Phase { get; set; }
[Sync] public Vector3 PhaseTarget { get; set; } // For directional attacks

// Host sets phase:
Phase = EnemyPhase.Windup;
PhaseTarget = targetPosition;

// All clients read phase for animation:
protected override void UpdateAnimation()
{
    switch (Phase)
    {
        case EnemyPhase.Windup: PlayWindupAnim(); break;
        case EnemyPhase.Active: PlayAttackAnim(); break;
        // ...
    }
}
```

For the Summoner specifically:
```csharp
[Sync] public bool IsCasting { get; set; }
[Sync] public Vector3 SummonPosition { get; set; }

// When cast starts (host):
IsCasting = true;
SummonPosition = spawnPos;
BroadcastSummonStart(spawnPos); // RPC for indicator creation

// All clients create local indicator at SummonPosition
[Rpc.Broadcast]
private void BroadcastSummonStart(Vector3 pos)
{
    CreateLocalIndicator(pos);
}
```

### 4. [Sync] vs [Rpc.Broadcast] vs Host-Only Decision Guide

| Use Case | Mechanism | Why |
|----------|-----------|-----|
| Health, alive state, stun, phase | `[Sync]` | Continuous state that any client may read at any time |
| Combo step, charging, casting | `[Sync]` | Affects animation state which runs on all clients every frame |
| Attack animations, sounds | `[Rpc.Broadcast]` | One-shot events, fire-and-forget |
| Death, spawn, explosion VFX | `[Rpc.Broadcast]` | One-shot events with position data |
| Damage calculation, AI decisions | Host-only (no sync) | Authoritative game state, results synced via [Sync] health |
| Cooldown timers | Owner-only (no sync) | Only needed by owning client's UI; unless party frames needed |
| Aggro tables, threat values | Host-only (no sync) | Only host runs enemy AI |
| Beam endpoint, beam firing state | `[Sync]` | Continuous visual state updated every frame |
| Projectile velocity | Deterministic (no sync) | Set identically on all clients at spawn time |
| Homing target | `[Sync]` on ProjectileBase | Changes behavior, cannot be derived locally |

### 5. VFX Broadcasting Strategy

**Problem:** CombatVfx creates local-only objects.

**Recommended Pattern:** Do NOT NetworkSpawn VFX objects. Instead, broadcast the VFX parameters and let each client create their own local VFX:

```csharp
// On a networked component (e.g., a CombatManager singleton):
[Rpc.Broadcast]
public static void BroadcastExplosion(Vector3 position, float radius)
{
    CombatVfx.Sphere(Game.ActiveScene, position, radius, Color.Orange.WithAlpha(0.3f), 0.4f);
}

// Replace direct CombatVfx calls with broadcasts at the call site:
// In Explosion.At():
BroadcastExplosionVfx(center, radius); // Instead of CombatVfx.Sphere directly
```

This keeps VFX lightweight (no network objects) while ensuring all clients see them.

### 6. Weapon Pedestal / Pickup Authority

**Pattern:**
```csharp
// Owner presses E:
[Rpc.Host]
private void RequestPickup(RoguelitePlayer player)
{
    if (_taken) return;
    _taken = true;
    
    // Host creates weapon on player
    GiveWeapon(player);
    BroadcastPickedUp();
}

[Sync] public bool IsTaken { get; set; }

[Rpc.Broadcast]
private void BroadcastPickedUp()
{
    // All clients: hide pedestal, play pickup VFX
}
```

### 7. Player Death Authority Fix

**Problem:** Host writes `IsAlive` on player-owned object.

**Fix options (pick one):**
1. **Watch IsDead instead:** Since `HealthComponent.IsDead` is [Sync] and set by the host (who can write to it because damage happens on host), have the owning client detect `IsDead` changes and set `IsAlive` locally:
```csharp
protected override void OnUpdate()
{
    if (!IsProxy && Health.IsDead && IsAlive)
    {
        IsAlive = false;
        ActiveWeapon?.OnOwnerDied();
    }
}
```
2. **Remove IsAlive entirely:** Just use `!Health.IsDead` everywhere.
3. **Use Rpc.Owner:** Host sends death notification to the owning client.

Option 2 is simplest and eliminates the redundant state.

---

## Priority Fix Order

1. **Player death flow** (Bug #4) -- blocks all combat testing
2. **Melee damage authority** (Bug #1) -- sword is unplayable for non-host
3. **Ability activation authority** (Bug #2, #3) -- abilities broken for non-host
4. **CombatVfx broadcasting** (Bug #5) -- invisible combat feedback
5. **Beam sync** (Bug #6) -- entirely invisible weapon
6. **Enemy phase sync** (Bugs #7, #10, #11) -- enemy attacks invisible to clients
7. **DamageZone visual** (Bug #13) -- simple fix, move DrawZoneVisual before host gate
8. **WeaponPedestal authority** (Bug #8) -- item duplication
9. **Homing projectile sync** (Bug #9) -- visual desync
10. **Player movement sync** (Bug #12) -- IsFrozen not visible
11. **Health MaxHealth sync** (Bug #14) -- health bars broken
12. **PlayerClass sync** (Bug #15) -- cosmetic

---

## Files With No Networking Issues

These files are clean and need no changes:
- `Core/DamageType.cs` -- pure enum
- `Core/AttackData.cs` -- pure struct
- `Core/HitContext.cs` -- pure struct
- `Core/DamageResolver.cs` -- static, callers gate authority (except for the melee path)
- `Player/PlayerClass.cs` -- pure data
- `Player/PlayerCamera.cs` -- well-implemented [Sync] on EyeAngles
- `Enemies/EnemyBrain.cs` -- plain class, host-only usage
- `Components/AggroComponent.cs` -- host-only data
- `Components/ArmorComponent.cs` -- set-once data, host reads it
- `Combat/HitDetection.cs` -- pure utility
- `Combat/AttackPattern.cs` -- delegates to ProjectileBase.Spawn which handles networking
