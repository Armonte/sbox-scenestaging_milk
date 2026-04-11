using System;
using System.Collections.Generic;
using System.Text.Json;
using Sandbox;

/// <summary>
/// Typed event bus across the network. Send events to specific clients or broadcast to all.
/// Events are just structs — no base class, no registration, no boilerplate.
/// </summary>
/// <remarks>
/// <para><b>Host → one client:</b> <c>NetChannel.Send( conn, new MoneyChanged { Balance = 500 } );</c></para>
/// <para><b>Host → all:</b> <c>NetChannel.Broadcast( new ServerMsg { Text = "Hello" } );</c></para>
/// <para><b>Client subscribes:</b> <c>NetChannel.On&lt;MoneyChanged&gt;( e =&gt; UpdateHUD( e.Balance ) );</c></para>
/// </remarks>
[Title( "NetKit - Channel System" )]
public sealed class NetChannel : GameObjectSystem<NetChannel>
{
	private static readonly Dictionary<string, List<Action<string>>> _subscribers = new();

	public NetChannel( Scene scene ) : base( scene )
	{
		Log.Info( "[NetKit] Channel system online" );
	}

	/// <summary>Send a typed event to one client. Host-only.</summary>
	public static void Send<T>( Connection target, T payload )
	{
		if ( !Networking.IsHost || target == null ) return;
		var typeName = typeof( T ).FullName;
		var json = JsonSerializer.Serialize( payload );
		using ( Rpc.FilterInclude( target ) )
			RpcDeliver( typeName, json );
	}

	/// <summary>Broadcast a typed event to all clients. Host-only.</summary>
	public static void Broadcast<T>( T payload )
	{
		if ( !Networking.IsHost ) return;
		var typeName = typeof( T ).FullName;
		var json = JsonSerializer.Serialize( payload );
		RpcBroadcast( typeName, json );
	}

	/// <summary>Subscribe to events of type T. Call once.</summary>
	public static void On<T>( Action<T> handler )
	{
		var typeName = typeof( T ).FullName;
		if ( !_subscribers.TryGetValue( typeName, out var list ) )
		{
			list = new List<Action<string>>();
			_subscribers[typeName] = list;
		}
		list.Add( json =>
		{
			try { handler( JsonSerializer.Deserialize<T>( json ) ); }
			catch ( Exception ex ) { Log.Warning( $"[NetKit] Channel handler for {typeName} threw: {ex.Message}" ); }
		} );
	}

	/// <summary>Remove all subscribers for type T.</summary>
	public static void Off<T>() => _subscribers.Remove( typeof( T ).FullName );

	/// <summary>Remove all subscribers.</summary>
	public static void Clear() => _subscribers.Clear();

	[Rpc.Owner( NetFlags.Reliable )]
	private static void RpcDeliver( string typeName, string json ) => Dispatch( typeName, json );

	[Rpc.Broadcast( NetFlags.Reliable )]
	private static void RpcBroadcast( string typeName, string json )
	{
		if ( Networking.IsHost ) return;
		Dispatch( typeName, json );
	}

	private static void Dispatch( string typeName, string json )
	{
		if ( !_subscribers.TryGetValue( typeName, out var list ) ) return;
		foreach ( var handler in list )
		{
			try { handler( json ); }
			catch ( Exception ex ) { Log.Warning( $"[NetKit] Channel subscriber threw: {ex.Message}" ); }
		}
	}
}
