using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bowling
{
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
			
			ShowCharacterSelectionUI(channel);
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
		
		void ShowCharacterSelectionUI(Connection channel)
		{
			SpawnTemporaryPlayer(channel);
		}
		
		void SpawnTemporaryPlayer(Connection channel)
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
			
			// Apply clothing/avatar
			var clothing = new ClothingContainer();
			clothing.Deserialize(channel.GetUserData("avatar"));
			
			if (player.Components.TryGet<SkinnedModelRenderer>(out var body, FindMode.EverythingInSelfAndDescendants))
			{
				clothing.Apply(body);
			}
			
			// Set name tag if available
			var nameTag = player.Components.Get<NameTagPanel>(FindMode.EverythingInSelfAndDescendants);
			if (nameTag != null && nameTag.IsValid())
			{
				nameTag.Name = channel.DisplayName;
			}
			
			player.NetworkSpawn(channel);
			connectionToPlayer[channel] = player;
			
			// Create character selection UI
			CreateCharacterSelectionUI(channel, player);
		}
		
		void CreateCharacterSelectionUI(Connection channel, GameObject player)
		{
			// Only create UI for the local connection
			if (channel != Connection.Local) return;
			
			// Create UI GameObject
			var uiObject = new GameObject(true, "CharacterSelectionUI");
			uiObject.Parent = player;
			
			// Add CharacterSelectionUI component
			var characterSelectionUI = uiObject.Components.Create<CharacterSelectionUI>();
			characterSelectionUI.FlowManager = Scene.GetAllComponents<BowlingGameFlowManager>().FirstOrDefault();
			characterSelectionUI.PlayerConnection = channel;
			
			// Create the UI panel
			var panel = uiObject.Components.Create<CharacterSelectPanel>();
			panel.SetSelector(characterSelectionUI);
			
			// Give the CharacterSelectionUI component a reference to its UI object for cleanup
			characterSelectionUI.SetUIObject(uiObject);
			
			Log.Info($"CharacterSelectionUI created for {channel.DisplayName}");
		}
		
		[Rpc.Broadcast]
		public void RequestCharacterSelection(int characterId)
		{
			if (!Networking.IsHost) return;
			
			// Find the connection that made this RPC call
			var callerConnection = Rpc.Caller;
			
			Log.Info($"RequestCharacterSelection RPC called - {callerConnection.DisplayName} wants character {characterId}");
			
			SelectCharacterForPlayer(callerConnection, characterId);
		}
		
		public void SelectCharacterForPlayer(Connection channel, int characterId)
		{
			if (!Networking.IsHost) return;
			
			Log.Info($"SelectCharacterForPlayer called - {channel.DisplayName} wants character {characterId}");
			
			if (connectionToPlayer.TryGetValue(channel, out var oldPlayer) && oldPlayer.IsValid())
			{
				Log.Info($"Destroying old player: {oldPlayer.Name}");
				oldPlayer.Destroy();
			}
			
			SpawnPlayerWithCharacter(channel, characterId);
			
			// Notify game flow manager
			var flowManager = Scene.GetAllComponents<BowlingGameFlowManager>().FirstOrDefault();
			if (flowManager != null)
			{
				flowManager.OnCharacterSelected(characterId);
			}
		}
		
		void SpawnPlayerWithCharacter(Connection channel, int characterId)
		{
			Log.Info($"SpawnPlayerWithCharacter called - Channel: {channel.DisplayName}, Character: {characterId}");
			
			var spawnPoint = GetNextSpawnPoint();
			GameObject playerPrefab = PlayerPrefab;
			
			if (playerPrefab == null || !playerPrefab.IsValid())
			{
				// Try to find player prefab in scene
				var playerObj = Scene.GetAllObjects(true).FirstOrDefault(go => go.Tags.Has("player"));
				if (playerObj != null)
				{
					playerPrefab = playerObj;
				}
				else
				{
					Log.Error($"No player prefab found for character {characterId}");
					return;
				}
			}
			
			var player = playerPrefab.Clone(spawnPoint);
			player.Name = $"Player_{channel.DisplayName}_Char{characterId}";
			
			Log.Info($"Player spawned: {player.Name} at {player.WorldPosition}");
			
			// Apply clothing/avatar
			var clothing = new ClothingContainer();
			clothing.Deserialize(channel.GetUserData("avatar"));
			
			if (player.Components.TryGet<SkinnedModelRenderer>(out var body, FindMode.EverythingInSelfAndDescendants))
			{
				clothing.Apply(body);
			}
			
			// Set name tag if available
			var nameTag = player.Components.Get<NameTagPanel>(FindMode.EverythingInSelfAndDescendants);
			if (nameTag != null && nameTag.IsValid())
			{
				nameTag.Name = channel.DisplayName;
			}
			
			player.NetworkSpawn(channel);
			
			connectionToPlayer[channel] = player;
			PlayerCharacters[player.Id] = characterId;
			
			Log.Info($"{channel.DisplayName} spawned with character {characterId}");
			
			// Notify game flow manager about character selection
			var flowManager = Scene.GetAllComponents<BowlingGameFlowManager>().FirstOrDefault();
			if (flowManager != null)
			{
				flowManager.OnCharacterSelected(characterId);
			}
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
	}
}

