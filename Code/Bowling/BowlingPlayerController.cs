using Sandbox;

/// <summary>
/// Player controller for bowling. Handles ball throwing mechanics.
/// Add this component to your player GameObject alongside PlayerController.
/// </summary>
public sealed class BowlingPlayerController : Component
{
	[Property] public GameObject BallPrefab { get; set; }
	[Property] public GameObject BallHoldPoint { get; set; } // Child GameObject where ball will be parented when held
	[Property] public float ThrowChargeTime { get; set; } = 2.0f; // Max charge time in seconds
	[Property] public float MinThrowForce { get; set; } = 500.0f;
	[Property] public float MaxThrowForce { get; set; } = 2000.0f;
	
	[Property] public GameObject CurrentBall { get; private set; }
	[Property] public bool HasBall { get; private set; } = false;
	
	private PlayerController _playerController;
	private Rigidbody _ballRigidbody;
	private TimeSince _chargeStartTime;
	private bool _isCharging = false;
	private bool _wasAttack1Down = false;
	
	protected override void OnStart()
	{
		base.OnStart();

		_playerController = Components.Get<PlayerController>( FindMode.InAncestors );

		// Spawn initial ball after a delay to ensure player controller is fully initialized
		Invoke( 0.5f, SpawnBall );
	}
	
	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;
		
		var attack1Down = Input.Down( "attack1" );
		
		// Handle ball throwing input
		if ( attack1Down && !_wasAttack1Down && HasBall && CurrentBall.IsValid() )
		{
			StartCharging();
		}
		
		if ( attack1Down && _isCharging )
		{
			UpdateCharging();
		}
		
		// Detect button release
		if ( !attack1Down && _wasAttack1Down && _isCharging )
		{
			ThrowBall();
		}
		
		_wasAttack1Down = attack1Down;
		
		// Reset ball (for testing - remove in production or bind to different key)
		if ( Input.Pressed( "reload" ) )
		{
			ResetBall();
		}
	}
	
	
	private void StartCharging()
	{
		_isCharging = true;
		_chargeStartTime = 0;
	}
	
	private void UpdateCharging()
	{
		// Visual feedback for charging could go here
		// For now, we'll just track the charge time
	}
	
	private void ThrowBall()
	{
		if ( !HasBall || !CurrentBall.IsValid() )
			return;

		_isCharging = false;

		// Store the world position before unparenting
		var throwPosition = CurrentBall.WorldPosition;
		var throwRotation = CurrentBall.WorldRotation;

		// Unparent the ball from the hold point
		CurrentBall.SetParent( null );

		// Restore world position (unparenting can sometimes mess with position)
		CurrentBall.WorldPosition = throwPosition;
		CurrentBall.WorldRotation = throwRotation;

		// Calculate charge (0 to 1)
		var chargeTime = _chargeStartTime;
		var charge = (chargeTime / ThrowChargeTime).Clamp( 0, 1 );

		// Get throw direction
		var throwDirection = GetThrowDirection();

		// Get the ball component and throw it
		var ball = CurrentBall.Components.Get<BowlingBall>();
		if ( ball.IsValid() )
		{
			// Calculate force multiplier based on charge
			var forceMultiplier = MinThrowForce + (MaxThrowForce - MinThrowForce) * charge;
			forceMultiplier /= ball.ThrowForce; // Normalize to ball's throw force

		// Re-fetch rigidbody reference after unparenting (in case it changed)
		_ballRigidbody = CurrentBall.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );

		// Re-enable colliders for physics interaction
		var colliders = CurrentBall.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants );
		foreach ( var collider in colliders )
		{
			collider.Enabled = true;
			// Ensure collider is not a trigger
			collider.IsTrigger = false;
		}

		// Enable physics and throw (ball.Throw will handle the rest)
		if ( _ballRigidbody.IsValid() )
		{
			_ballRigidbody.MotionEnabled = true;
			_ballRigidbody.Gravity = true;
			_ballRigidbody.Velocity = Vector3.Zero;
			_ballRigidbody.AngularVelocity = Vector3.Zero;
			_ballRigidbody.Sleeping = false;
		}

		ball.Throw( throwDirection, forceMultiplier );
			HasBall = false;

			Log.Info( $"Ball thrown from {throwPosition} dir {throwDirection} charge: {charge:F2}" );
		}
	}
	
	private Vector3 GetThrowDirection()
	{
		// Try to get direction from the main camera first
		var camera = Scene.Camera;
		if ( camera.IsValid() )
		{
			var forward = camera.WorldRotation.Forward;
			Log.Info( $"Throw direction from Camera: {forward}" );
			return forward.Normal;
		}

		// Fallback to player controller eye angles
		if ( _playerController.IsValid() )
		{
			var eyeAngles = _playerController.EyeAngles;
			var forward = eyeAngles.ToRotation().Forward;
			Log.Info( $"Throw direction from EyeAngles: {eyeAngles}, Forward: {forward}" );
			return forward.Normal;
		}

		Log.Warning( "No valid camera or PlayerController, using fallback throw direction" );
		return WorldRotation.Forward;
	}
	
	private void SpawnBall()
	{
		if ( !BallPrefab.IsValid() )
		{
			Log.Warning( "BallPrefab not set in BowlingPlayerController!" );
			return;
		}

		if ( !BallHoldPoint.IsValid() )
		{
			Log.Warning( "BallHoldPoint not set in BowlingPlayerController! Create a child GameObject on your player to hold the ball." );
			return;
		}

		// Spawn the ball at the hold point's position
		CurrentBall = BallPrefab.Clone( BallHoldPoint.WorldPosition, BallHoldPoint.WorldRotation );

		// Disable colliders BEFORE enabling the ball to prevent initial collision
		var colliders = CurrentBall.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants );
		foreach ( var collider in colliders )
		{
			collider.Enabled = false;
		}

		// Get the rigidbody and disable physics while held (search in children too)
		_ballRigidbody = CurrentBall.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
		if ( _ballRigidbody.IsValid() )
		{
			_ballRigidbody.MotionEnabled = false; // Disable physics while parented
		}
		else
		{
			Log.Warning( "Ball prefab doesn't have a Rigidbody component!" );
		}

		// Now enable the ball and parent it
		CurrentBall.Enabled = true;
		HasBall = true;

		// Parent the ball to the hold point so it moves with the player
		CurrentBall.SetParent( BallHoldPoint );
		CurrentBall.LocalPosition = Vector3.Zero;
		CurrentBall.LocalRotation = Rotation.Identity;

		// Verify ball component exists
		var ball = CurrentBall.Components.Get<BowlingBall>();
		if ( !ball.IsValid() )
		{
			Log.Warning( "Ball prefab doesn't have a BowlingBall component!" );
		}

		Log.Info( "Ball spawned and parented to hold point" );
	}
	
	private void ResetBall()
	{
		if ( CurrentBall.IsValid() )
		{
			var ball = CurrentBall.Components.Get<BowlingBall>();
			if ( ball.IsValid() && BallHoldPoint.IsValid() )
			{
				// Reset ball state
				ball.Reset( BallHoldPoint.WorldPosition, BallHoldPoint.WorldRotation );
				HasBall = true;

				// Parent back to hold point
				CurrentBall.SetParent( BallHoldPoint );
				CurrentBall.LocalPosition = Vector3.Zero;
				CurrentBall.LocalRotation = Rotation.Identity;

				// Disable physics while held
				if ( _ballRigidbody.IsValid() )
				{
					_ballRigidbody.MotionEnabled = false;
					_ballRigidbody.Velocity = Vector3.Zero;
					_ballRigidbody.AngularVelocity = Vector3.Zero;
				}

				// Disable colliders while held to prevent collision with player
				var colliders = CurrentBall.Components.GetAll<Collider>( FindMode.EnabledInSelfAndDescendants );
				foreach ( var collider in colliders )
				{
					collider.Enabled = false;
				}
			}
		}
		else
		{
			SpawnBall();
		}
	}
}
