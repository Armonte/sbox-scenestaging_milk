using Sandbox;

/// <summary>
/// Component for bowling pins. Handles pin state, collisions, and scoring.
/// </summary>
public sealed class BowlingPin : Component, Component.ICollisionListener
{
	[Property] public bool IsKnockedOver { get; private set; } = false;
	[Property] public float KnockOverAngle { get; set; } = 45.0f; // Degrees from vertical
	[Property] public int PinNumber { get; set; } = 0; // 1-10 for scoring
	
	private Rigidbody _rigidbody;
	private Vector3 _startPosition;
	private Rotation _startRotation;
	private TimeSince _timeSinceKnocked;
	
	protected override void OnStart()
	{
		base.OnStart();
		
		_rigidbody = Components.Get<Rigidbody>();
		if ( !_rigidbody.IsValid() )
		{
			_rigidbody = Components.Create<Rigidbody>();
		}
		
		_startPosition = WorldPosition;
		_startRotation = WorldRotation;
		
		// Setup physics
		_rigidbody.Gravity = true;
		_rigidbody.MotionEnabled = true;
		_rigidbody.MassOverride = 1.6f; // Standard pin mass in kg
		_rigidbody.OverrideMassCenter = false;
		
		// Tag the pin
		Tags.Add( "bowling_pin" );
	}
	
	protected override void OnFixedUpdate()
	{
		if ( IsKnockedOver )
			return;
		
		// Check if pin is knocked over by checking its rotation
		var upVector = WorldRotation.Up;
		var dot = Vector3.Dot( upVector, Vector3.Up );
		var angleFromVertical = MathF.Acos( dot.Clamp( -1.0f, 1.0f ) ) * (180.0f / MathF.PI);
		
		if ( angleFromVertical > KnockOverAngle )
		{
			KnockOver();
		}
	}
	
	/// <summary>
	/// Called when the pin is hit by the ball
	/// </summary>
	public void OnHitByBall( Collision collision )
	{
		if ( IsKnockedOver )
			return;
		
		// Apply additional force from the collision
		var impulse = collision.Contact.Impulse;
		if ( impulse > 50.0f ) // Minimum impulse to register
		{
			// The physics system should handle most of this, but we can add extra force if needed
			_rigidbody.Velocity += collision.Contact.Normal * (impulse * 0.1f);
		}
	}
	
	/// <summary>
	/// Mark the pin as knocked over
	/// </summary>
	public void KnockOver()
	{
		if ( IsKnockedOver )
			return;
		
		IsKnockedOver = true;
		_timeSinceKnocked = 0;
		
		Log.Info( $"Pin {PinNumber} knocked over!" );
		
		// Notify game manager if it exists
		var gameManager = Scene.GetAllComponents<BowlingGameManager>().FirstOrDefault();
		if ( gameManager.IsValid() )
		{
			gameManager.OnPinKnockedOver( this );
		}
	}
	
	/// <summary>
	/// Reset the pin to its starting position
	/// </summary>
	public new void Reset()
	{
		IsKnockedOver = false;
		_timeSinceKnocked = 0;
		
		WorldPosition = _startPosition;
		WorldRotation = _startRotation;
		
		_rigidbody.Velocity = Vector3.Zero;
		_rigidbody.AngularVelocity = Vector3.Zero;
		_rigidbody.Sleeping = true;
	}
	
	// Collision handling
	public void OnCollisionStart( Collision collision )
	{
		// Check if we collided with another pin (pin-on-pin action)
		if ( collision.Other.GameObject.Tags.Has( "bowling_pin" ) )
		{
			var otherPin = collision.Other.GameObject.Components.Get<BowlingPin>();
			if ( otherPin.IsValid() && !otherPin.IsKnockedOver )
			{
				// Pin collision - physics will handle it, but we can add effects here
			}
		}
	}
	
	public void OnCollisionUpdate( Collision collision )
	{
		// Additional collision handling
	}
	
	public void OnCollisionStop( CollisionStop collision )
	{
		// Cleanup
	}
}

