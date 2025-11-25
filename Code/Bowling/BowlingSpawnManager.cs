using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BowlingSpawnManager : Component, Component.INetworkListener
{
	[Property] public GameObject PlayerPrefab { get; set; }
	[Property] public List<GameObject> SpawnPoints { get; set; } = new();
	
	[Sync] public Dictionary<Guid, int> PlayerCharacters { get; set; } = new();
	
	private Dictionary<Connection, GameObject> connectionToPlayer = new();
	private int nextSpawnIndex = 0;
	
	protected override void OnStart()
	{
		base.OnStart();
		
		if (Networking.IsHost)
		{
			Log.Info("Bowling Spawn Manager initialized on host");
		}
	}
	
	public void OnActive(Connection channel)
	{
		Log.Info($"Player {channel.DisplayName} connected");
		
		// For bowling, spawn player directly without character selection
		SpawnPlayer(channel);
	}
	
	public void OnDisconnected(Connection channel)
	{
		Log.Info($"Player {channel.DisplayName} disconnected");
		
		if (connectionToPlayer.TryGetValue(channel, out var player))
		{
			if (player.IsValid())
			{
				PlayerCharacters.Remove(player.Id);
				player.Destroy();
			}
			
			connectionToPlayer.Remove(channel);
		}
	}
	
	void SpawnPlayer(Connection channel)
	{
		var spawnPoint = GetNextSpawnPoint();
		
		// Use default player prefab if not set
		var prefab = PlayerPrefab;
		if (prefab == null || !prefab.IsValid())
		{
			// Try to find player prefab in scene
			var playerObj = Scene.GetAllObjects(true).FirstOrDefault(go => go.Tags.Has("player"));
			if (playerObj != null)
			{
				prefab = playerObj;
			}
			else
			{
				Log.Warning("No player prefab found, creating basic player");
				prefab = new GameObject(true, "Player");
				prefab.Components.Create<CharacterController>();
			}
		}
		
		var player = prefab.Clone(spawnPoint);
		player.Name = $"Player_{channel.DisplayName}";
		
		player.NetworkSpawn(channel);
		connectionToPlayer[channel] = player;
		PlayerCharacters[player.Id] = 0; // Default character
		
		Log.Info($"Player spawned: {player.Name} at {player.WorldPosition}");
	}
	
	Transform GetNextSpawnPoint()
	{
		if (SpawnPoints == null || SpawnPoints.Count == 0)
		{
			return new Transform(Vector3.Zero, Rotation.Identity, 1f);
		}
		
		var spawnPoint = SpawnPoints[nextSpawnIndex % SpawnPoints.Count];
		nextSpawnIndex++;
		
		return spawnPoint?.WorldTransform ?? new Transform(Vector3.Zero, Rotation.Identity, 1f);
	}
	
	public int GetTotalPlayerCount()
	{
		return PlayerCharacters.Count;
	}
	
	/// <summary>
	/// RPC for character selection (kept for compatibility with CharacterSelectionUI)
	/// </summary>
	[Rpc.Broadcast]
	public void RequestCharacterSelection(int characterId)
	{
		if (!Networking.IsHost) return;
		
		var callerConnection = Rpc.Caller;
		Log.Info($"Character {characterId} selected by {callerConnection.DisplayName}");
		
		// For bowling, we just log this - players are already spawned
		PlayerCharacters[callerConnection.Id] = characterId;
	}
}
