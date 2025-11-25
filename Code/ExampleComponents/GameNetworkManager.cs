namespace Sandbox;

public sealed class GameNetworkManager : Component, Component.INetworkListener
{
	[Property] public GameObject PlayerPrefab { get; set; }
	[Property] public GameObject SpawnPoint { get; set; }

	public void OnActive( Connection channel )
	{
		var player = PlayerPrefab.Clone( SpawnPoint.WorldTransform );

		var nameTag = player.Components.Get<NameTagPanel>( FindMode.EverythingInSelfAndDescendants );
		if ( nameTag.IsValid() )
		{
			nameTag.Name = channel.DisplayName;
		}

		player.NetworkSpawn( channel );
	}
}
