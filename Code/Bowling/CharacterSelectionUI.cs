using Sandbox;
using System.Linq;

namespace Bowling
{
	/// <summary>
	/// Component that manages showing the character selection UI for a player
	/// </summary>
	public sealed class CharacterSelectionUI : Component
	{
		public BowlingGameFlowManager FlowManager { get; set; }
		public Connection PlayerConnection { get; set; }
		
		private GameObject uiObject;
		
		public void SetUIObject(GameObject uiObj)
		{
			uiObject = uiObj;
			Log.Info($"CharacterSelectionUI UI object reference set");
		}
		
		protected override void OnStart()
		{
			base.OnStart();
			
			Log.Info($"CharacterSelectionUI OnStart - IsProxy: {IsProxy}, PlayerConnection: {PlayerConnection?.DisplayName}");
			
			if (FlowManager != null && PlayerConnection != null)
			{
				Log.Info($"CharacterSelectionUI initialized for player: {PlayerConnection?.DisplayName}");
				DisablePlayerMovement();
			}
			else
			{
				Log.Info($"CharacterSelectionUI waiting for configuration - FlowManager: {FlowManager != null}, PlayerConnection: {PlayerConnection != null}");
			}
		}
		
		public void RequestCharacterSelection(int characterId)
		{
			Log.Info($"RequestCharacterSelection called with character {characterId}");
			
			// Find the spawn manager
			var spawnManager = Scene.GetAllComponents<BowlingSpawnManager>().FirstOrDefault();
			
			if (spawnManager != null && PlayerConnection != null)
			{
				Log.Info("Calling SpawnManager.RequestCharacterSelection RPC");
				
				// Remove the UI and re-enable movement
				CleanupUI();
				EnablePlayerMovement();
				
				// Request character selection via RPC (will be handled by host)
				spawnManager.RequestCharacterSelection(characterId);
				Log.Info($"Requested character via RPC: {characterId}");
			}
			else
			{
				Log.Error($"Cannot request character selection - SpawnManager: {spawnManager != null}, PlayerConnection: {PlayerConnection != null}");
			}
		}
		
		void DisablePlayerMovement()
		{
			// Disable CharacterController to prevent movement
			var cc = GameObject.Components.Get<CharacterController>();
			if (cc != null)
			{
				cc.Enabled = false;
				Log.Info("Player movement disabled");
			}
		}
		
		void EnablePlayerMovement()
		{
			// Re-enable CharacterController
			var cc = GameObject.Components.Get<CharacterController>();
			if (cc != null)
			{
				cc.Enabled = true;
				Log.Info("Player movement enabled");
			}
		}
		
		void CleanupUI()
		{
			if (uiObject != null && uiObject.IsValid())
			{
				uiObject.Destroy();
				Log.Info("Character selection UI cleaned up");
			}
		}
		
		protected override void OnDestroy()
		{
			CleanupUI();
			EnablePlayerMovement();
			base.OnDestroy();
		}
	}
}

