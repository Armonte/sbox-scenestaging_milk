/// <summary>
/// Scene-level manager that handles player spawning.
/// Put a NetworkSession component on the same or another GameObject to create the lobby.
/// This just handles INetworkListener for spawning players on connect.
/// </summary>
[Title( "Roguelite Game Manager" )]
[Icon( "sports_esports" )]
public sealed class RogueliteGameManager : Component, Component.INetworkListener
{
	[Property] public GameObject PlayerPrefab { get; set; }
	[Property] public GameObject SpawnPoint { get; set; }

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
