using Sandbox;
using System;

/// <summary>
/// Manages the bowling game state, scoring, and frame/ball management.
/// Handles the complete game flow: throw -> wait for settle -> reset -> next throw.
/// </summary>
public sealed class BowlingGameManager : Component
{
	public enum GameState
	{
		WaitingForThrow,    // Player has ball, ready to throw
		BallInPlay,         // Ball has been thrown, waiting for it to settle
		Settling,           // Ball stopped, waiting for pins to settle
		ResettingPins,      // Pins are being reset (between rolls or frames)
		GameOver            // All frames complete
	}

	[Property] public int TotalFrames { get; set; } = 10;
	[Property] public float SettleTime { get; set; } = 2.5f; // Time to wait after ball stops for pins to settle
	[Property] public float MinSettleTime { get; set; } = 0.4f; // Minimum time before checking if pins stopped
	[Property] public float StrikeSettleTime { get; set; } = 0.6f; // Faster settle for strikes (all pins down)
	[Property] public float ResetDelay { get; set; } = 1.5f; // Time before resetting pins/ball
	[Property] public float BallDespawnZ { get; set; } = -500f; // Z position where ball despawns (fell off lane)
	[Property] public float BallMaxDistance { get; set; } = 3000f; // Max distance ball can travel before despawn
	[Property] public float BallPastPinsDistance { get; set; } = 1500f; // Distance past start where ball is considered past pins
	[Property] public float MinBallTravelTime { get; set; } = 1.5f; // Minimum time before checking if ball is past pins
	[Property] public float PinSettleVelocity { get; set; } = 3.0f; // Velocity threshold for pins to be considered settled

	[Property, Group("Prefabs")] public GameObject PinPrefab { get; set; }
	
	[Property, Group("Spawn Points")] 
	[Description("Empty GameObject marking where the front pin (pin 1) spawns. Pins spawn in a triangle going forward (+Y) from this point.")]
	public GameObject PinSpawnPoint { get; set; }
	
	[Property, Group("Spawn Points")] public float PinSpacing { get; set; } = 30.48f; // 12 inches = 30.48cm between pin centers
	[Property, Group("Spawn Points")] public float RowSpacing { get; set; } = 26.4f; // ~10.4 inches between rows
	[Property, Group("Spawn Points")] public bool SpawnPinsOnStart { get; set; } = true;

	// Game state
	[Sync] public GameState CurrentState { get; private set; } = GameState.WaitingForThrow;
	[Sync] public int CurrentFrame { get; private set; } = 1;
	[Sync] public int CurrentRoll { get; private set; } = 1; // 1 or 2 (or 3 in 10th frame)
	[Sync] public int TotalScore { get; private set; } = 0;
	[Sync] public int PinsKnockedThisFrame { get; private set; } = 0;
	[Sync] public int PinsKnockedThisRoll { get; private set; } = 0;
	[Sync] public int PinsRemaining { get; private set; } = 10;
	[Sync] public int StrikeStreak { get; private set; } = 0; // Consecutive strikes
	[Sync] public int SpareStreak { get; private set; } = 0; // Consecutive spares
	[Sync] public int GutterCount { get; private set; } = 0; // Total gutters this game

	// Frame scores for display
	[Sync] public NetList<int> FrameScores { get; private set; } = new();

	// Events for UI and other components
	public Action<int, int> OnScoreChanged; // (frame, score)
	public Action<GameState> OnStateChanged;
	public Action OnStrike;
	public Action OnSpare;
	public Action OnGutterBall;
	public Action OnGameOver;
	
	// Oddball special events
	public Action OnDouble;      // 2 strikes in a row
	public Action OnTurkey;      // 3 strikes in a row
	public Action OnHambone;     // 4 strikes in a row (aka Four-bagger)
	public Action OnYahtzee;     // 5 strikes in a row (aka Five-bagger)
	public Action OnSixPack;     // 6 strikes in a row
	public Action OnPerfectGame; // 12 strikes = 300 game
	public Action OnSplitPickup; // Picked up a split (spare after hitting 1 pin on first roll)
	public Action OnDutchman;    // Alternating strike-spare pattern
	public Action OnCleanGame;   // No open frames (all strikes or spares)

	private List<BowlingPin> _pins = new();
	private BowlingBall _currentBall;
	private BowlingPlayerController _playerController;
	private Vector3 _ballStartPosition;
	private TimeSince _settleTimer;
	private TimeSince _ballThrownTime;
	private int _pinsStandingAtRollStart = 10; // Track pins at start of roll for accurate counting

	protected override void OnStart()
	{
		base.OnStart();

		// Initialize frame scores
		for (int i = 0; i < TotalFrames; i++)
		{
			FrameScores.Add(0);
		}

		// Spawn pins if configured to do so
		if (SpawnPinsOnStart && PinPrefab.IsValid())
		{
			SpawnPins();
		}
		else
		{
			// Find existing pins in the scene
			RefreshPins();
		}

		// Find player controller
		_playerController = Scene.GetAllComponents<BowlingPlayerController>().FirstOrDefault();

		Log.Info($"BowlingGameManager started. Found {_pins.Count} pins.");
	}

	protected override void OnUpdate()
	{
		if (CurrentState == GameState.BallInPlay)
		{
			CheckBallStatus();
		}
		else if (CurrentState == GameState.Settling)
		{
			UpdateSettling();
		}
	}

	/// <summary>
	/// Refresh the list of pins in the scene
	/// </summary>
	public void RefreshPins()
	{
		_pins.Clear();
		var pinObjects = Scene.GetAllObjects(true).Where(go => go.Tags.Has("bowling_pin"));

		foreach (var pinObject in pinObjects)
		{
			var pin = pinObject.Components.Get<BowlingPin>();
			if (pin.IsValid())
			{
				_pins.Add(pin);
			}
		}

		PinsRemaining = _pins.Count(p => !p.IsKnockedOver);
	}

	/// <summary>
	/// Spawn all 10 pins in the standard triangle formation.
	/// Pin arrangement (looking down the lane):
	///     7  8  9  10   (row 4 - back)
	///       4  5  6     (row 3)
	///         2  3      (row 2)
	///           1       (row 1 - front, closest to bowler)
	/// </summary>
	public void SpawnPins()
	{
		if (!PinPrefab.IsValid())
		{
			Log.Warning("Cannot spawn pins - PinPrefab is not set!");
			return;
		}

		// Get the spawn origin - either from the spawn point GameObject or fall back to this object's position
		Vector3 origin;
		Rotation laneRotation;
		
		if (PinSpawnPoint.IsValid())
		{
			origin = PinSpawnPoint.WorldPosition;
			laneRotation = PinSpawnPoint.WorldRotation;
		}
		else
		{
			// Fall back to this GameObject's position if no spawn point set
			origin = WorldPosition;
			laneRotation = WorldRotation;
			Log.Warning("PinSpawnPoint not set - using BowlingGameManager position as origin");
		}

		// Destroy existing pins first
		foreach (var pin in _pins)
		{
			if (pin.IsValid())
			{
				pin.GameObject.Destroy();
			}
		}
		_pins.Clear();

		// Pin positions in the triangle (relative to origin)
		// Row 1 (front): 1 pin
		// Row 2: 2 pins
		// Row 3: 3 pins
		// Row 4 (back): 4 pins
		var pinPositions = new (int row, int col, int pinNumber)[]
		{
			(0, 0, 1),   // Front pin
			(1, -1, 2), (1, 1, 3),   // Row 2
			(2, -2, 4), (2, 0, 5), (2, 2, 6),   // Row 3
			(3, -3, 7), (3, -1, 8), (3, 1, 9), (3, 3, 10),   // Row 4 (back)
		};

		foreach (var (row, col, pinNumber) in pinPositions)
		{
			// Calculate local offset from origin
			// Forward (+Y in local space) goes down the lane, Right (+X) is left/right
			var localOffset = new Vector3(
				col * PinSpacing * 0.5f,  // Left/right offset
				row * RowSpacing,          // Forward offset (down the lane)
				0                          // Keep at ground level
			);

			// Transform local offset by lane rotation to get world position
			var worldOffset = laneRotation * localOffset;
			var position = origin + worldOffset;

			// Spawn the pin with the lane's rotation
			var pinObj = PinPrefab.Clone(position, laneRotation);
			pinObj.Name = $"Pin_{pinNumber}";

			// Get or add BowlingPin component
			var pinComponent = pinObj.Components.Get<BowlingPin>();
			if (!pinComponent.IsValid())
			{
				pinComponent = pinObj.Components.Create<BowlingPin>();
			}
			pinComponent.PinNumber = pinNumber;

			_pins.Add(pinComponent);

			Log.Info($"Spawned Pin {pinNumber} at {position}");
		}

		PinsRemaining = 10;
		Log.Info($"Spawned {_pins.Count} pins");
	}

	/// <summary>
	/// Called when a ball is thrown - start tracking it
	/// </summary>
	public void OnBallThrown(BowlingBall ball, Vector3 startPosition)
	{
		_currentBall = ball;
		_ballStartPosition = startPosition;
		_ballThrownTime = 0;
		_rollProcessed = false; // Reset for new roll
		
		// Capture how many pins are standing at the start of this roll
		_pinsStandingAtRollStart = _pins.Count(p => !p.IsKnockedOver);
		PinsKnockedThisRoll = 0;

		SetState(GameState.BallInPlay);
		Log.Info($"Ball thrown! Tracking ball at {startPosition}, Pins standing: {_pinsStandingAtRollStart}");
	}

	/// <summary>
	/// Called when a pin is knocked over - just log, don't count yet (wait for settling)
	/// </summary>
	public void OnPinKnockedOver(BowlingPin pin)
	{
		// Just log for now - actual counting happens after pins settle
		Log.Info($"Pin {pin.PinNumber} tipped!");
	}

	/// <summary>
	/// Called when the ball enters the gutter
	/// </summary>
	public void OnBallEnterGutter(BowlingBall ball, bool isLeftGutter)
	{
		Log.Info($"GUTTER BALL! ({(isLeftGutter ? "left" : "right")})");
		OnGutterBall?.Invoke();
	}

	private void CheckBallStatus()
	{
		if (!_currentBall.IsValid())
		{
			// Ball was destroyed, end the roll
			EndRoll();
			return;
		}

		var ballPos = _currentBall.WorldPosition;
		var distanceTraveled = Vector3.DistanceBetween(_ballStartPosition, ballPos);

		// Check if ball fell off the lane
		if (ballPos.z < BallDespawnZ)
		{
			Log.Info("Ball fell off lane!");
			EndRoll();
			return;
		}

		// Check if ball traveled too far
		if (distanceTraveled > BallMaxDistance)
		{
			Log.Info("Ball reached end of lane!");
			EndRoll();
			return;
		}
		
		// Check if ball is past the pins area - end roll faster (but only after minimum travel time)
		if (_ballThrownTime > MinBallTravelTime && distanceTraveled > BallPastPinsDistance)
		{
			Log.Info("Ball past pins area!");
			EndRoll();
			return;
		}

		// Check if ball stopped moving
		var rb = _currentBall.Components.Get<Rigidbody>(FindMode.EverythingInSelfAndDescendants);
		if (rb.IsValid() && rb.Velocity.Length < 5f && _currentBall.IsThrown)
		{
			Log.Info("Ball stopped moving!");
			EndRoll();
		}
	}

	private void EndRoll()
	{
		if (CurrentState != GameState.BallInPlay)
			return;

		Log.Info($"Roll ended. Pins knocked: {PinsKnockedThisRoll}");

		// Destroy the ball
		if (_currentBall.IsValid())
		{
			_currentBall.GameObject.Destroy();
			_currentBall = null;
		}

		// Start settling phase to let pins finish falling
		_settleTimer = 0;
		SetState(GameState.Settling);
	}

	private bool _rollProcessed = false; // Prevent multiple processing
	
	private void UpdateSettling()
	{
		// Prevent multiple processing
		if (_rollProcessed)
			return;
			
		// Wait minimum time before checking
		if (_settleTimer < MinSettleTime)
			return;
		
		// Quick check: are all pins knocked over? (potential strike/spare)
		int standingPins = _pins.Count(p => p.IsValid() && !p.IsKnockedOver);
		bool allPinsDown = standingPins == 0;
		
		// Use faster settle time for strikes/spares
		float requiredSettleTime = allPinsDown ? StrikeSettleTime : SettleTime;
		
		// Check if all pins have stopped moving
		bool allPinsSettled = ArePinsSettled();
		
		// Process when: pins settled, OR strike/spare time reached, OR max settle time reached
		if (allPinsSettled || _settleTimer > requiredSettleTime)
		{
			_rollProcessed = true; // Mark as processed to prevent re-entry
			
			// Now count knocked pins after they've settled
			CountKnockedPins();
			ProcessRollResult();
		}
	}
	
	/// <summary>
	/// Check if all pins have stopped moving
	/// </summary>
	private bool ArePinsSettled()
	{
		foreach (var pin in _pins)
		{
			if (!pin.IsValid()) continue;
			
			var rb = pin.Components.Get<Rigidbody>();
			if (rb.IsValid() && !rb.Sleeping)
			{
				// Check if pin is still moving significantly
				if (rb.Velocity.Length > PinSettleVelocity || rb.AngularVelocity.Length > PinSettleVelocity)
				{
					return false;
				}
			}
		}
		return true;
	}
	
	/// <summary>
	/// Count pins knocked this roll after they've settled
	/// </summary>
	private void CountKnockedPins()
	{
		int pinsStandingNow = _pins.Count(p => p.IsValid() && !p.IsKnockedOver);
		PinsKnockedThisRoll = _pinsStandingAtRollStart - pinsStandingNow;
		PinsKnockedThisFrame += PinsKnockedThisRoll;
		PinsRemaining = pinsStandingNow;
		
		Log.Info($"Pins settled! Knocked this roll: {PinsKnockedThisRoll}, This frame: {PinsKnockedThisFrame}, Remaining: {PinsRemaining}");
		
		// Handle strikes and spares with streak tracking
		if (PinsRemaining == 0)
		{
			// STRIKE = knocked all 10 pins on first ball OR knocked fresh 10 pins (after reset in 10th frame)
			// SPARE = knocked remaining pins (less than 10) on second ball
			bool isStrike = (_pinsStandingAtRollStart == 10);
			
			if (isStrike)
			{
				// STRIKE!
				StrikeStreak++;
				SpareStreak = 0; // Reset spare streak
				
				Log.Info($"STRIKE! Streak: {StrikeStreak}");
				OnStrike?.Invoke();
				
				// Check for special strike achievements
				CheckStrikeAchievements();
			}
			else
			{
				// SPARE! (picked up remaining pins)
				SpareStreak++;
				StrikeStreak = 0; // Reset strike streak
				
				Log.Info($"SPARE! Streak: {SpareStreak}");
				OnSpare?.Invoke();
				
				// Check for split pickup (spare after knocking only 1-2 pins on first roll)
				int firstRollPins = PinsKnockedThisFrame - PinsKnockedThisRoll;
				if (firstRollPins <= 2)
				{
					Log.Info("SPLIT PICKUP!");
					OnSplitPickup?.Invoke();
				}
			}
		}
		else
		{
			// Open frame (didn't get all pins) - reset streaks
			if (CurrentRoll >= 2)
			{
				StrikeStreak = 0;
				SpareStreak = 0;
			}
		}
	}
	
	/// <summary>
	/// Check and fire special strike achievements
	/// </summary>
	private void CheckStrikeAchievements()
	{
		switch (StrikeStreak)
		{
			case 2:
				Log.Info("DOUBLE!");
				OnDouble?.Invoke();
				break;
			case 3:
				Log.Info("TURKEY!");
				OnTurkey?.Invoke();
				break;
			case 4:
				Log.Info("HAMBONE! (Four-bagger)");
				OnHambone?.Invoke();
				break;
			case 5:
				Log.Info("YAHTZEE! (Five-bagger)");
				OnYahtzee?.Invoke();
				break;
			case 6:
				Log.Info("SIX-PACK!");
				OnSixPack?.Invoke();
				break;
			case 12:
				Log.Info("PERFECT GAME! 300!");
				OnPerfectGame?.Invoke();
				break;
		}
	}

	private void ProcessRollResult()
	{
		// Update score
		int rollScore = PinsKnockedThisRoll;
		TotalScore += rollScore;

		// Update frame score
		if (CurrentFrame <= TotalFrames && CurrentFrame > 0)
		{
			FrameScores[CurrentFrame - 1] += rollScore;
		}

		OnScoreChanged?.Invoke(CurrentFrame, TotalScore);

		// Determine next action (events already fired in CountKnockedPins)
		bool isStrike = (CurrentRoll == 1 && PinsRemaining == 0);
		bool isSpare = (CurrentRoll == 2 && PinsRemaining == 0);

		// Decide what happens next
		if (CurrentFrame < TotalFrames)
		{
			// Normal frames (1-9)
			if (isStrike || CurrentRoll >= 2)
			{
				// Move to next frame
				AdvanceFrame();
			}
			else
			{
				// Second roll of frame - clear knocked pins, keep standing ones
				CurrentRoll = 2;
				ClearKnockedPins();
				GivePlayerNewBall();
			}
		}
		else
		{
			// 10th frame special rules
			if (CurrentRoll == 1)
			{
				if (isStrike)
				{
					// Reset pins for 2nd roll
					ResetAllPins();
					CurrentRoll = 2;
					GivePlayerNewBall();
				}
				else
				{
					// Clear knocked pins for 2nd roll
					CurrentRoll = 2;
					ClearKnockedPins();
					GivePlayerNewBall();
				}
			}
			else if (CurrentRoll == 2)
			{
				if (isStrike || isSpare)
				{
					// Get a 3rd roll
					ResetAllPins();
					CurrentRoll = 3;
					GivePlayerNewBall();
				}
				else
				{
					// Game over
					EndGame();
				}
			}
			else
			{
				// 3rd roll complete
				EndGame();
			}
		}
	}

	private void AdvanceFrame()
	{
		CurrentFrame++;
		CurrentRoll = 1;
		PinsKnockedThisFrame = 0;

		if (CurrentFrame > TotalFrames)
		{
			EndGame();
			return;
		}

		Log.Info($"Frame {CurrentFrame} starting!");

		// Reset all pins and give new ball
		ResetAllPins();
		GivePlayerNewBall();
	}

	private void ResetAllPins()
	{
		SetState(GameState.ResettingPins);

		// Check if we need to respawn pins (if any were destroyed)
		int validPins = _pins.Count(p => p.IsValid());
		if (validPins < 10 && PinPrefab.IsValid())
		{
			// Respawn all pins
			SpawnPins();
		}
		else
		{
			// Just reset existing pins
			foreach (var pin in _pins)
			{
				if (pin.IsValid())
				{
					pin.Reset();
				}
			}
		}

		PinsRemaining = 10;
		PinsKnockedThisRoll = 0;

		Log.Info("Pins reset!");
	}
	
	/// <summary>
	/// Clear knocked pins from the lane (for second roll of frame)
	/// Standing pins remain in place
	/// </summary>
	private void ClearKnockedPins()
	{
		SetState(GameState.ResettingPins);
		
		int cleared = 0;
		for (int i = _pins.Count - 1; i >= 0; i--)
		{
			var pin = _pins[i];
			if (pin.IsValid() && pin.IsKnockedOver)
			{
				pin.GameObject.Destroy();
				_pins.RemoveAt(i);
				cleared++;
			}
		}
		
		PinsKnockedThisRoll = 0;
		
		Log.Info($"Cleared {cleared} knocked pins. {PinsRemaining} standing pins remain.");
	}

	private void GivePlayerNewBall()
	{
		// Find player controller and give them a new ball
		if (_playerController.IsValid())
		{
			_playerController.GiveBall();
			SetState(GameState.WaitingForThrow);
		}
		else
		{
			// Try to find it again
			_playerController = Scene.GetAllComponents<BowlingPlayerController>().FirstOrDefault();
			if (_playerController.IsValid())
			{
				_playerController.GiveBall();
				SetState(GameState.WaitingForThrow);
			}
			else
			{
				Log.Warning("No BowlingPlayerController found to give ball to!");
			}
		}
	}

	private bool _gameEnding = false;
	
	private void EndGame()
	{
		// Prevent multiple calls
		if (_gameEnding || CurrentState == GameState.GameOver)
			return;
			
		_gameEnding = true;
		
		// Delay game over to let final announcement be seen
		Invoke(2.5f, () => {
			SetState(GameState.GameOver);
			Log.Info($"GAME OVER! Final Score: {TotalScore}");
			OnGameOver?.Invoke();
		});
	}

	private void SetState(GameState newState)
	{
		if (CurrentState != newState)
		{
			CurrentState = newState;
			OnStateChanged?.Invoke(newState);
			Log.Info($"Game state changed to: {newState}");
		}
	}

	/// <summary>
	/// Restart the game
	/// </summary>
	public void RestartGame()
	{
		CurrentFrame = 1;
		CurrentRoll = 1;
		TotalScore = 0;
		PinsKnockedThisFrame = 0;
		PinsKnockedThisRoll = 0;
		StrikeStreak = 0;
		SpareStreak = 0;
		GutterCount = 0;
		_rollProcessed = false;
		_gameEnding = false;

		for (int i = 0; i < FrameScores.Count; i++)
		{
			FrameScores[i] = 0;
		}

		ResetAllPins();
		GivePlayerNewBall();

		Log.Info("Game restarted!");
	}

	/// <summary>
	/// Get current charge percentage from player controller (for UI)
	/// </summary>
	public float GetChargePercent()
	{
		if (_playerController.IsValid())
		{
			return _playerController.ChargePercent;
		}
		return 0f;
	}

	/// <summary>
	/// Check if player is currently charging a throw
	/// </summary>
	public bool IsCharging()
	{
		if (_playerController.IsValid())
		{
			return _playerController.IsCharging;
		}
		return false;
	}
}
