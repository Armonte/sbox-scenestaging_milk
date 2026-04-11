using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Sandbox;

/// <summary>
/// Async request/response over s&amp;box RPCs. RPCs are fire-and-forget with no return values.
/// NetRequest adds typed, awaitable request/response with automatic correlation, timeouts,
/// and error handling. The #1 gap in s&amp;box networking.
/// </summary>
/// <remarks>
/// <para><b>Client:</b></para>
/// <code>var response = await NetRequest.Ask&lt;BuyReq, BuyRes&gt;( new BuyReq { ItemId = "sword" } );</code>
/// <para><b>Host (register once):</b></para>
/// <code>NetRequest.Handle&lt;BuyReq, BuyRes&gt;( (caller, req) =&gt; new BuyRes { Success = true } );</code>
/// </remarks>
[Title( "NetKit - Request System" )]
public sealed class NetRequest : GameObjectSystem<NetRequest>
{
	private static readonly Dictionary<string, TaskCompletionSource<string>> _pending = new();
	private static readonly Dictionary<string, Func<Connection, string, string>> _handlers = new();

	/// <summary>Default timeout in seconds. Requests that exceed this resolve with default(TResponse).</summary>
	public static float TimeoutSeconds { get; set; } = 10f;

	public NetRequest( Scene scene ) : base( scene )
	{
		Log.Info( "[NetKit] Request system online" );
	}

	/// <summary>
	/// Send a typed request to the host and await a typed response.
	/// </summary>
	public static async Task<TResponse> Ask<TRequest, TResponse>( TRequest request )
	{
		var requestId = Guid.NewGuid().ToString( "N" );
		var typeName = typeof( TRequest ).FullName;
		var json = JsonSerializer.Serialize( request );

		var tcs = new TaskCompletionSource<string>();
		_pending[requestId] = tcs;

		RpcSendRequest( typeName, requestId, json );

		// Poll for completion — Task.WhenAny is blocked by s&box's security whitelist.
		var elapsed = 0f;
		while ( !tcs.Task.IsCompleted && elapsed < TimeoutSeconds )
		{
			await GameTask.Delay( 50 );
			elapsed += 0.05f;
		}

		_pending.Remove( requestId );

		if ( tcs.Task.IsCompleted && tcs.Task.Result != null )
		{
			try
			{
				return JsonSerializer.Deserialize<TResponse>( tcs.Task.Result );
			}
			catch ( Exception ex )
			{
				Log.Warning( $"[NetKit] Failed to deserialize response for {typeName}: {ex.Message}" );
				return default;
			}
		}

		Log.Warning( $"[NetKit] Request {typeName} timed out after {TimeoutSeconds}s" );
		return default;
	}

	/// <summary>
	/// Register a handler for a request type. The handler runs on the host and returns
	/// a response sent back to the calling client.
	/// </summary>
	public static void Handle<TRequest, TResponse>( Func<Connection, TRequest, TResponse> handler )
	{
		var typeName = typeof( TRequest ).FullName;
		_handlers[typeName] = ( caller, json ) =>
		{
			var request = JsonSerializer.Deserialize<TRequest>( json );
			var response = handler( caller, request );
			return JsonSerializer.Serialize( response );
		};
	}

	[Rpc.Host( NetFlags.Reliable )]
	private static void RpcSendRequest( string typeName, string requestId, string json )
	{
		var caller = Rpc.Caller;
		if ( caller == null ) return;

		if ( !_handlers.TryGetValue( typeName, out var handler ) )
		{
			Log.Warning( $"[NetKit] No handler for {typeName}" );
			using ( Rpc.FilterInclude( caller ) )
				RpcSendResponse( requestId, "{}" );
			return;
		}

		string responseJson;
		try
		{
			responseJson = handler( caller, json );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[NetKit] Handler for {typeName} threw: {ex.Message}" );
			responseJson = "{}";
		}

		using ( Rpc.FilterInclude( caller ) )
			RpcSendResponse( requestId, responseJson );
	}

	[Rpc.Owner( NetFlags.Reliable )]
	private static void RpcSendResponse( string requestId, string json )
	{
		if ( _pending.TryGetValue( requestId, out var tcs ) )
			tcs.TrySetResult( json );
	}
}
