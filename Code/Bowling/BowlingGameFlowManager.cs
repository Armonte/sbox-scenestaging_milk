using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

public enum BowlingGameFlowState
{
	TitleScreen,
	CharacterSelection,
	Lobby,
	InGame
}

public sealed class BowlingGameFlowManager : Component
{
	[Property] public SceneFile GameScene { get; set; }
	[Property] public SceneFile TitleScene { get; set; }
	[Property] public SceneFile CharacterSelectScene { get; set; }
	
	[Sync] public BowlingGameFlowState CurrentState { get; set; } = BowlingGameFlowState.TitleScreen;
	[Sync] public bool IsSoloMode { get; set; } = false;
	[Sync] public bool IsMultiplayerMode { get; set; } = false;
	
	protected override void OnStart()
	{
		base.OnStart();
		
		if (Networking.IsHost)
		{
			Log.Info("Bowling Game Flow Manager initialized");
			CurrentState = BowlingGameFlowState.TitleScreen;
		}
	}
	
	public void StartSoloGame()
	{
		Log.Info("Starting solo game...");
		IsSoloMode = true;
		IsMultiplayerMode = false;
		CurrentState = BowlingGameFlowState.CharacterSelection;
		
		// Transition to character selection
		TransitionToCharacterSelection();
		
		// Broadcast to all clients
		StartSoloGameRPC();
	}
	
	[Rpc.Broadcast]
	void StartSoloGameRPC()
	{
		if (Networking.IsHost) return;
		
		IsSoloMode = true;
		IsMultiplayerMode = false;
		CurrentState = BowlingGameFlowState.CharacterSelection;
	}
	
	public void StartMultiplayerGame()
	{
		Log.Info("Starting multiplayer game...");
		IsSoloMode = false;
		IsMultiplayerMode = true;
		CurrentState = BowlingGameFlowState.Lobby;
		
		// For now, go straight to character selection
		// TODO: Implement lobby system
		TransitionToCharacterSelection();
		
		// Broadcast to all clients
		StartMultiplayerGameRPC();
	}
	
	[Rpc.Broadcast]
	void StartMultiplayerGameRPC()
	{
		if (Networking.IsHost) return;
		
		IsSoloMode = false;
		IsMultiplayerMode = true;
		CurrentState = BowlingGameFlowState.Lobby;
	}
	
	[Rpc.Broadcast]
	public void OnCharacterSelected(int characterId)
	{
		if (!Networking.IsHost) return;
		
		Log.Info($"Character {characterId} selected");
		
		// In solo mode, immediately start game
		if (IsSoloMode)
		{
			TransitionToGame();
		}
		// In multiplayer, wait for all players to select
		else if (IsMultiplayerMode)
		{
			// TODO: Check if all players have selected
			// For now, start after a short delay or when all ready
			TransitionToGame();
		}
	}
	
	void TransitionToCharacterSelection()
	{
		CurrentState = BowlingGameFlowState.CharacterSelection;
		Log.Info("Transitioning to character selection");
		
		// Character selection UI is handled by BowlingSpawnManager when players connect
		// No need to manually show UI here
	}
	
	void TransitionToGame()
	{
		CurrentState = BowlingGameFlowState.InGame;
		Log.Info("Transitioning to game scene");
		
		// Load the game scene
		if (GameScene != null)
		{
			Game.ActiveScene.Load(GameScene);
		}
		else
		{
			Log.Warning("GameScene not set! Loading default bowling scene");
			Game.ActiveScene.LoadFromFile("scenes/bowling.scene");
		}
	}
}
