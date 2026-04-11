using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

/// <summary>
/// Shared per-player cooldown system with host-authoritative timing and client-side countdown.
/// No network round-trip needed to query remaining time on the client.
/// </summary>
/// <remarks>
/// <para><b>Host:</b> <c>NetCooldown.Start( conn, "dash", 5f );</c></para>
/// <para><b>Host check:</b> <c>if ( NetCooldown.IsActive( conn, "dash" ) ) return;</c></para>
/// <para><b>Client UI:</b> <c>float remaining = NetCooldown.Remaining( "dash" );</c></para>
/// </remarks>
[Title( "NetKit - Cooldown System" )]
public sealed class NetCooldown : GameObjectSystem<NetCooldown>
{
	private static readonly Dictionary<(ulong steamId, string key), (RealTimeSince started, float duration)> _host = new();
	private static readonly Dictionary<string, (RealTimeSince started, float duration)> _client = new();

	/// <summary>Fires on the client when a cooldown starts.</summary>
	public static event Action<string, float> OnStarted;

	/// <summary>Fires on the client when a cooldown expires.</summary>
	public static event Action<string> OnExpired;

	private RealTimeSince _lastCleanup = 0;

	public NetCooldown( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, Tick, "NetCooldown.Tick" );
	}

	/// <summary>Start a cooldown. Host-only. Automatically pushed to the owning client.</summary>
	public static void Start( Connection connection, string key, float duration )
	{
		if ( !Networking.IsHost || connection == null ) return;
		_host[(connection.SteamId, key)] = (0, duration);
		using ( Rpc.FilterInclude( connection ) )
			RpcReceive( key, duration );
	}

	/// <summary>Host: is this cooldown still active?</summary>
	public static bool IsActive( Connection connection, string key )
	{
		if ( connection == null ) return false;
		return _host.TryGetValue( (connection.SteamId, key), out var cd ) && cd.started < cd.duration;
	}

	/// <summary>Host: remaining seconds.</summary>
	public static float HostRemaining( Connection connection, string key )
	{
		if ( connection == null ) return 0f;
		return _host.TryGetValue( (connection.SteamId, key), out var cd ) ? MathF.Max( 0f, cd.duration - cd.started ) : 0f;
	}

	/// <summary>Client: remaining seconds (local countdown, no network round-trip).</summary>
	public static float Remaining( string key )
	{
		return _client.TryGetValue( key, out var cd ) ? MathF.Max( 0f, cd.duration - cd.started ) : 0f;
	}

	/// <summary>Client: true if cooldown expired or never started.</summary>
	public static bool IsReady( string key ) => Remaining( key ) <= 0f;

	private void Tick()
	{
		var expired = _client.Where( kv => kv.Value.started >= kv.Value.duration ).Select( kv => kv.Key ).ToList();
		foreach ( var key in expired )
		{
			_client.Remove( key );
			OnExpired?.Invoke( key );
		}

		if ( Networking.IsHost && _lastCleanup > 60f )
		{
			_lastCleanup = 0;
			var hostExpired = _host.Where( kv => kv.Value.started >= kv.Value.duration ).Select( kv => kv.Key ).ToList();
			foreach ( var key in hostExpired ) _host.Remove( key );
		}
	}

	[Rpc.Owner( NetFlags.Reliable )]
	private static void RpcReceive( string key, float duration )
	{
		_client[key] = (0, duration);
		OnStarted?.Invoke( key, duration );
	}
}
