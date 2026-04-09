/// <summary>
/// Scene-level manager that handles player spawning, networking, and enemy placement.
/// Handles lobby creation and player spawning for both host and clients.
/// </summary>
[Title( "Roguelite Game Manager" )]
[Icon( "sports_esports" )]
public sealed class RogueliteGameManager : Component, Component.INetworkListener
{
	[Property] public GameObject PlayerPrefab { get; set; }
	[Property] public GameObject SpawnPoint { get; set; }
	[Property] public bool AutoCreateLobby { get; set; } = true;

	protected override async Task OnLoad()
	{
		if ( AutoCreateLobby && !Networking.IsActive )
		{
			// Small delay to let scene finish loading
			await Task.DelayRealtimeSeconds( 0.1f );
			Networking.CreateLobby( new() );
		}
	}

	/// <summary>
	/// Called when a player connects (including the host).
	/// </summary>
	public void OnActive( Connection channel )
	{
		if ( PlayerPrefab is null || SpawnPoint is null )
		{
			Log.Warning( "RogueliteGameManager: PlayerPrefab or SpawnPoint not set!" );
			return;
		}

		Log.Info( $"[GameManager] Spawning player for {channel.DisplayName}" );

		var player = PlayerPrefab.Clone( SpawnPoint.WorldTransform );
		player.Name = $"Player ({channel.DisplayName})";
		player.NetworkSpawn( channel );
	}

	public void OnDisconnected( Connection channel )
	{
		Log.Info( $"[GameManager] Player disconnected: {channel.DisplayName}" );
	}
}
