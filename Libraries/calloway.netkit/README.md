# NetKit — Networking Primitives for s&box

The missing layer between s&box's raw networking API and your gameplay code. Every common networking task in one line.

**GitHub:** [github.com/CallowayisWeird/NetKit](https://github.com/CallowayisWeird/NetKit)
**Support & Questions:** Discord — `callowayisweird`

## Components

| Component | What | One-liner |
|---|---|---|
| **NetRequest** | Async request/response | `var r = await NetRequest.Ask<Req, Res>(req);` |
| **NetChannel** | Typed event bus | `NetChannel.Send(conn, new Event { ... });` |
| **NetCooldown** | Shared cooldowns | `NetCooldown.Start(conn, "dash", 5f);` |
| **NetTransaction** | Client prediction | `await NetTransaction.Execute(predict, req, rollback);` |
| **NetState\<T\>** | Replicated state machines | `State.TransitionTo(DoorState.Opening);` |
| **NetVisibility** | Declarative visibility | Drop component, set `Mode = Distance` |

## Install

Add NetKit as a library dependency in your s&box project.

## Quick Start

### Request/Response (replaces fire-and-forget RPCs)

```csharp
// Host: register handler once
NetRequest.Handle<BuyItemRequest, BuyItemResponse>( (caller, req) =>
{
    // validate, execute, return
    return new BuyItemResponse { Success = true, NewBalance = 400 };
});

// Client: send and await
var response = await NetRequest.Ask<BuyItemRequest, BuyItemResponse>(
    new BuyItemRequest { ItemId = "sword", Price = 100 }
);
if ( response.Success )
    Log.Info( $"Balance: {response.NewBalance}" );
```

### Event Bus (replaces Rpc.FilterInclude + JSON ceremony)

```csharp
// Host pushes to one client:
NetChannel.Send( connection, new MoneyChanged { Balance = 500 } );

// Host broadcasts to all:
NetChannel.Broadcast( new ServerAnnouncement { Message = "Restarting in 5m" } );

// Client subscribes:
NetChannel.On<MoneyChanged>( e => UpdateHUD( e.Balance ) );
```

### Cooldowns (replaces per-feature TimeSince fields)

```csharp
// Host:
if ( NetCooldown.IsActive( conn, "ability.dash" ) )
    return; // on cooldown
NetCooldown.Start( conn, "ability.dash", 5f );

// Client (for UI, no network round-trip):
float remaining = NetCooldown.Remaining( "ability.dash" );
```

### Client Prediction (zero perceived latency)

```csharp
await NetTransaction.Execute(
    predict:  () => { money -= 100; UpdateHUD(); },
    request:  new BuyRequest { ItemId = "sword", Price = 100 },
    rollback: () => { money += 100; ShowError("Failed"); }
);
```

### Replicated State Machines

```csharp
// On a Component:
[Sync] public int _doorState { get; set; }
public NetState<DoorState> Door;

protected override void OnStart()
{
    Door = new NetState<DoorState>( () => _doorState, v => _doorState = v );
    Door.OnTransition += ( from, to ) => PlayAnim( to );
}

// Host:
Door.TransitionTo( DoorState.Opening );

// Client (call in OnUpdate):
Door.Poll();
```

### Declarative Visibility

```csharp
// Drop NetVisibility component on any networked GameObject via editor or code:
var vis = go.GetOrAddComponent<NetVisibility>();
vis.Mode = NetVisibilityMode.Distance;
vis.MaxDistance = 500f;

// Or owner-only:
vis.Mode = NetVisibilityMode.OwnerOnly;

// Or custom filter:
vis.Mode = NetVisibilityMode.Custom;
vis.Filter = conn => MyCustomCheck( conn );
```

## License

MIT — see [licence.md](licence.md)
