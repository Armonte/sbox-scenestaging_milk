using Sandbox;

/// <summary>
/// Player controller for bowling. Handles ball throwing mechanics with charge system.
/// Add this component to your player GameObject alongside PlayerController.
/// </summary>
public sealed class BowlingPlayerController : Component
{
	[Property] public GameObject BallPrefab { get; set; }
	[Property] public GameObject BallHoldPoint { get; set; } // Child GameObject where ball will be parented when held
	[Property] public string HandBoneName { get; set; } = "hand_r"; // Bone name to use if BallHoldPoint not set
	[Property] public Vector3 HoldOffset { get; set; } = new Vector3(10, 0, 0); // Local offset from hold point (forward, right, up)
	[Property] public Angles HoldRotation { get; set; } = new Angles(0, 0, 0); // Local rotation offset
	[Property] public float ThrowChargeTime { get; set; } = 1.5f; // Max charge time in seconds
	[Property] public float MinThrowForce { get; set; } = 400.0f;
	[Property] public float MaxThrowForce { get; set; } = 1800.0f;

	// Exposed state for UI and game manager
	[Property] public GameObject CurrentBall { get; private set; }
	[Property] public bool HasBall { get; private set; } = false;
	[Property] public bool IsCharging { get; private set; } = false;
	[Property] public float ChargePercent { get; private set; } = 0f;

	private PlayerController _playerController;
	private Rigidbody _ballRigidbody;
	private TimeSince _chargeStartTime;
	private bool _wasAttack1Down = false;
	private BowlingGameManager _gameManager;
	private SkinnedModelRenderer _bodyRenderer;
	private GameObject _handBone;

	protected override void OnStart()
	{
		base.OnStart();

		_playerController = Components.Get<PlayerController>(FindMode.InAncestors);
		_gameManager = Scene.GetAllComponents<BowlingGameManager>().FirstOrDefault();

		// Find the SkinnedModelRenderer to get bone objects
		_bodyRenderer = Components.Get<SkinnedModelRenderer>(FindMode.EverythingInSelfAndDescendants);
		
		// Tag the player for collision filtering (ball ignores player)
		Tags.Add("player");
		
		// If no explicit hold point, try to find the hand bone
		if (!BallHoldPoint.IsValid() && _bodyRenderer.IsValid())
		{
			FindHandBone();
		}

		// Spawn initial ball after a delay to ensure player controller is fully initialized
		Invoke(0.5f, SpawnBall);
	}

	private void FindHandBone()
	{
		// Find hand bone using GetBoneObject (requires CreateBoneObjects = true, NOT CreateAttachments)
		// Avoid IK and attachment bones - we want the actual skeletal hand bone
		GameObject bestMatch = null;
		
		for (int i = 0; i < _bodyRenderer.Model.BoneCount; i++)
		{
			var bone = _bodyRenderer.GetBoneObject(i);
			if (!bone.IsValid())
				continue;
				
			var boneName = bone.Name.ToLowerInvariant();
			
			// Skip IK bones and attachment bones
			if (boneName.Contains("ik") || boneName.Contains("attach") || boneName.Contains("target"))
				continue;
			
			if (boneName.Contains(HandBoneName.ToLowerInvariant()))
			{
				bestMatch = bone;
				// Prefer exact match
				if (boneName == HandBoneName.ToLowerInvariant() || boneName == "hand_r" || boneName == "hand_l")
				{
					_handBone = bone;
					Log.Info($"Found hand bone (exact): {bone.Name}");
					return;
				}
			}
		}
		
		// Use best partial match if no exact match
		if (bestMatch.IsValid())
		{
			_handBone = bestMatch;
			Log.Info($"Found hand bone (partial): {bestMatch.Name}");
			return;
		}
		
		// Log all bones to help debug
		Log.Warning($"Could not find bone containing '{HandBoneName}'. Available bones:");
		for (int i = 0; i < _bodyRenderer.Model.BoneCount; i++)
		{
			var bone = _bodyRenderer.GetBoneObject(i);
			if (bone.IsValid())
				Log.Info($"  Bone {i}: {bone.Name}");
		}
	}

	protected override void OnUpdate()
	{
		if (IsProxy)
			return;

		var attack1Down = Input.Down("attack1");

		// Handle ball throwing input
		if (attack1Down && !_wasAttack1Down && HasBall && CurrentBall.IsValid())
		{
			StartCharging();
		}

		if (attack1Down && IsCharging)
		{
			UpdateCharging();
		}

		// Detect button release
		if (!attack1Down && _wasAttack1Down && IsCharging)
		{
			ThrowBall();
		}

		_wasAttack1Down = attack1Down;

		// Restart game (for testing)
		if (Input.Pressed("reload") && _gameManager.IsValid())
		{
			if (_gameManager.CurrentState == BowlingGameManager.GameState.GameOver)
			{
				_gameManager.RestartGame();
			}
		}
	}

	/// <summary>
	/// Update ball position in PreRender for smooth following
	/// </summary>
	protected override void OnPreRender()
	{
		if (!HasBall || !CurrentBall.IsValid())
			return;

		// Get the hold position - prefer explicit hold point, fallback to hand bone
		var holdPoint = BallHoldPoint.IsValid() ? BallHoldPoint : _handBone;
		if (!holdPoint.IsValid())
			return;

		// Calculate final position with offset
		var finalPosition = holdPoint.WorldPosition + holdPoint.WorldRotation * HoldOffset;
		var finalRotation = holdPoint.WorldRotation * HoldRotation.ToRotation();

		CurrentBall.WorldPosition = finalPosition;
		CurrentBall.WorldRotation = finalRotation;
	}

	private void StartCharging()
	{
		IsCharging = true;
		_chargeStartTime = 0;
		ChargePercent = 0f;
	}

	private void UpdateCharging()
	{
		// Calculate charge (0 to 1)
		var chargeTime = (float)_chargeStartTime;
		ChargePercent = (chargeTime / ThrowChargeTime).Clamp(0f, 1f);
	}

	private void ThrowBall()
	{
		if (!HasBall || !CurrentBall.IsValid())
			return;

		IsCharging = false;

		// Store the world position before unparenting
		var throwPosition = CurrentBall.WorldPosition;
		var throwRotation = CurrentBall.WorldRotation;

		// Remove held tag
		CurrentBall.Tags.Remove("held_ball");

		// Unparent the ball (keep world transform)
		CurrentBall.SetParent(null, true);

		// Restore world position just to be safe
		CurrentBall.WorldPosition = throwPosition;
		CurrentBall.WorldRotation = throwRotation;

		// Get throw direction
		var throwDirection = GetThrowDirection();

		// Get the ball component and throw it
		var ball = CurrentBall.Components.Get<BowlingBall>();
		if (ball.IsValid())
		{
			// Calculate force based on charge
			var force = MinThrowForce + (MaxThrowForce - MinThrowForce) * ChargePercent;
			var forceMultiplier = force / ball.ThrowForce;

			// Re-enable ALL colliders first
			var colliders = CurrentBall.Components.GetAll<Collider>(FindMode.EverythingInSelfAndDescendants);
			foreach (var collider in colliders)
			{
				collider.Enabled = true;
			}

			// Re-fetch and re-enable rigidbody
			_ballRigidbody = CurrentBall.Components.Get<Rigidbody>(FindMode.EverythingInSelfAndDescendants);
			if (_ballRigidbody.IsValid())
			{
				_ballRigidbody.Enabled = true;
			}

			ball.Throw(throwDirection, forceMultiplier);
			HasBall = false;
			ChargePercent = 0f;

			// Notify game manager
			if (_gameManager.IsValid())
			{
				_gameManager.OnBallThrown(ball, throwPosition);
			}

			Log.Info($"Ball thrown! Dir: {throwDirection}, Charge: {ChargePercent:P0}, Force: {force:F0}");
		}
	}

	private Vector3 GetThrowDirection()
	{
		// Try to get direction from the main camera first
		var camera = Scene.Camera;
		if (camera.IsValid())
		{
			var forward = camera.WorldRotation.Forward;
			return forward.Normal;
		}

		// Fallback to player controller eye angles
		if (_playerController.IsValid())
		{
			var eyeAngles = _playerController.EyeAngles;
			var forward = eyeAngles.ToRotation().Forward;
			return forward.Normal;
		}

		Log.Warning("No valid camera or PlayerController, using fallback throw direction");
		return WorldRotation.Forward;
	}

	/// <summary>
	/// Give the player a new ball (called by game manager)
	/// </summary>
	public void GiveBall()
	{
		if (HasBall && CurrentBall.IsValid())
		{
			// Already have a ball
			return;
		}

		SpawnBall();
	}

	private void SpawnBall()
	{
		if (!BallPrefab.IsValid())
		{
			Log.Warning("BallPrefab not set in BowlingPlayerController!");
			return;
		}

		// Get hold point - prefer explicit, fallback to hand bone
		var holdPoint = BallHoldPoint.IsValid() ? BallHoldPoint : _handBone;
		if (!holdPoint.IsValid())
		{
			Log.Warning("No valid hold point! Set BallHoldPoint or ensure CreateBoneObjects is enabled on SkinnedModelRenderer.");
			return;
		}

		// Clean up old ball if exists
		if (CurrentBall.IsValid())
		{
			CurrentBall.Destroy();
		}

		// Spawn the ball at the hold point's position with offset
		var spawnPosition = holdPoint.WorldPosition + holdPoint.WorldRotation * HoldOffset;
		var spawnRotation = holdPoint.WorldRotation * HoldRotation.ToRotation();
		CurrentBall = BallPrefab.Clone(spawnPosition, spawnRotation);

		// Tag the ball as held
		CurrentBall.Tags.Add("held_ball");

		// IMPORTANT: Disable ALL physics components BEFORE parenting to prevent any collision
		// Disable Rigidbody
		_ballRigidbody = CurrentBall.Components.Get<Rigidbody>(FindMode.EverythingInSelfAndDescendants);
		if (_ballRigidbody.IsValid())
		{
			_ballRigidbody.Enabled = false;
		}
		else
		{
			Log.Warning("Ball prefab doesn't have a Rigidbody component!");
		}

		// Disable ALL colliders - CharacterController can still collide with enabled colliders
		var colliders = CurrentBall.Components.GetAll<Collider>(FindMode.EverythingInSelfAndDescendants);
		foreach (var collider in colliders)
		{
			collider.Enabled = false;
		}

		// Now parent it
		CurrentBall.SetParent(GameObject, true);

		CurrentBall.Enabled = true;
		HasBall = true;
		IsCharging = false;
		ChargePercent = 0f;

		// Verify ball component exists
		var ball = CurrentBall.Components.Get<BowlingBall>();
		if (!ball.IsValid())
		{
			Log.Warning("Ball prefab doesn't have a BowlingBall component!");
		}

		Log.Info("Ball spawned and ready!");
	}
}
