/// <summary>
/// Scene-level manager that handles player spawning and enemy placement.
/// Add this to a GameObject in your roguelite scene alongside a NetworkHelper or use INetworkListener.
/// </summary>
[Title( "Roguelite Game Manager" )]
[Icon( "sports_esports" )]
public sealed class RogueliteGameManager : Component, Component.INetworkListener
{
	[Property] public GameObject PlayerPrefab { get; set; }
	[Property] public GameObject SpawnPoint { get; set; }

	/// <summary>
	/// Called when a player connects. Spawns their player prefab.
	/// </summary>
	public void OnActive( Connection channel )
	{
		if ( PlayerPrefab is null || SpawnPoint is null )
		{
			Log.Warning( "RogueliteGameManager: PlayerPrefab or SpawnPoint not set!" );
			return;
		}

		var player = PlayerPrefab.Clone( SpawnPoint.WorldTransform );
		player.NetworkSpawn( channel );
	}
}
