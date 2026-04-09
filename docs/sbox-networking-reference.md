# s&box Networking Reference

## Quick Reference

| Pattern | When |
|---------|------|
| `[Sync] public float HP { get; set; }` | State all clients need continuously (health, alive, stun) |
| `[Rpc.Broadcast] void PlayAnim() {}` | One-time events all clients see (animations, sounds, VFX) |
| `[Rpc.Broadcast(NetFlags.HostOnly)]` | Only host can call this broadcast |
| `if (IsProxy) return;` | Owner-only code (input, local UI) |
| `if (!Networking.IsHost) return;` | Host-only code (damage, AI, spawning) |
| `go.NetworkSpawn()` | Networked object, no specific owner |
| `go.NetworkSpawn(channel)` | Networked object owned by that player |
| `go.Network.TakeOwnership()` | Grab ownership of an object |
| `Rpc.Caller` | Inside RPC, who called it |

## Authority Model

- **Host** resolves all damage, runs all enemy AI, spawns all entities
- **Owner** (player) handles their own input, camera, UI
- **[Sync]** properties auto-replicate from owner to all clients
- **[Rpc.Broadcast]** runs a method on every client simultaneously

## Player Spawning Pattern

```csharp
public sealed class GameManager : Component, Component.INetworkListener
{
    [Property] public GameObject PlayerPrefab { get; set; }
    [Property] public GameObject SpawnPoint { get; set; }

    protected override void OnStart()
    {
        if (!Networking.IsActive)
            Networking.CreateLobby(new());
    }

    public void OnActive(Connection channel)
    {
        // HOST: called when a player connects
        var player = PlayerPrefab.Clone(SpawnPoint.WorldTransform);
        player.NetworkSpawn(channel); // This player belongs to that connection
    }

    public void OnDisconnected(Connection channel)
    {
        // Clean up player objects
    }
}
```

## Player Component Pattern

```csharp
public class Player : Component
{
    [Sync] public bool IsAlive { get; set; } = true;
    [Sync] public Angles EyeAngles { get; set; }

    protected override void OnUpdate()
    {
        if (IsProxy) return; // Only owner handles input
        HandleInput();
    }

    protected override void OnStart()
    {
        if (!IsProxy)
        {
            // Local player only: create HUD, camera, etc
        }
    }
}
```

## Enemy/NPC Pattern (host-controlled)

```csharp
public class Enemy : Component
{
    [Sync] public bool IsStunned { get; set; }

    protected override void OnUpdate()
    {
        if (!Networking.IsHost) return; // Only host runs AI
        Brain.Tick();
    }

    [Rpc.Broadcast]
    private void BroadcastDeath() { } // All clients see death
}

// Spawning enemies (host only):
var enemy = EnemyPrefab.Clone(position);
enemy.NetworkSpawn(); // No owner — host controls
```

## Damage Flow

1. Player attacks → host detects hit
2. Host calls `DamageResolver.Resolve()` 
3. Host modifies `[Sync] Health.Current` → auto-syncs to all clients
4. Host calls `[Rpc.Broadcast] BroadcastDeath()` if dead → all clients play death anim

## Projectile Pattern

```csharp
// Spawn on host
var obj = scene.CreateObject();
obj.Components.Create<ProjectileBase>();
obj.NetworkSpawn(); // All clients see it

// Movement runs on all clients (deterministic)
// Damage only on host:
protected virtual void OnHit(SceneTraceResult tr)
{
    if (!Networking.IsHost) return;
    DamageResolver.Resolve(...);
}
```

## Pickup/Ownership Transfer

```csharp
go.Network.TakeOwnership();  // I own this now
go.Network.DropOwnership();  // Release to host
```

## Prefab Network Settings (Inspector)

- **Network Mode**: `Network Snapshot` (default, auto-sync transforms)
- **Orphaned Mode**: `Destroy` (clean up when owner disconnects)
- **Owner Transfer**: `Takeover` (anyone can grab ownership)
- **Always Transmit**: Check for important objects (enemies, pickups)

## What Our Roguelite Needs

### Already Correct
- RogueliteGameManager implements INetworkListener, spawns players with channel
- Enemy AI gated by `Networking.IsHost`
- Health uses `[Sync]` for Current and IsDead
- Player input gated by `IsProxy`
- DamageResolver runs on host only

### Needs Testing
- Multiple players connecting and seeing each other
- Both players damaging same enemy → aggro switching
- Enemy death visible on all clients
- Projectiles visible and hitting on all clients
- Knockback visible on all clients (position sync during kb)

### Source Files
- `Code/ExampleComponents/GameNetworkManager.cs` — simple INetworkListener
- `Code/ExampleComponents/NetworkTest.cs` — ownership transfer example
- `Code/ExampleComponents/NetworkStress/SpawnNetworkedObjects.cs` — mass spawning
- `Code/Bowling/BowlingSpawnManager.cs` — multi-player spawn with character select
- `Assets/Scenes/Tests/Networking/networkstress.scene` — stress test scene
