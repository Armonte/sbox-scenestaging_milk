using Sandbox;

/// <summary>
/// Component for bowling pins. Handles pin state, collisions, and scoring.
/// Pins are made heavier and require more angle to be considered knocked over.
/// </summary>
public sealed class BowlingPin : Component, Component.ICollisionListener
{
	[Property] public bool IsKnockedOver { get; private set; } = false;
	[Property] public float KnockOverAngle { get; set; } = 60.0f; // Degrees from vertical (increased from 45)
	[Property] public int PinNumber { get; set; } = 0; // 1-10 for scoring
	[Property] public float PinMass { get; set; } = 1.5f; // Pin mass (regulation ~1.5kg)
	[Property] public float AngularDamping { get; set; } = 2.0f; // Higher = more stable

	private Rigidbody _rigidbody;
	private Vector3 _startPosition;
	private Rotation _startRotation;
	private TimeSince _timeSinceKnocked;
	private bool _wasHit = false;

	protected override void OnStart()
	{
		base.OnStart();

		_rigidbody = Components.Get<Rigidbody>();
		if (!_rigidbody.IsValid())
		{
			_rigidbody = Components.Create<Rigidbody>();
		}

		_startPosition = WorldPosition;
		_startRotation = WorldRotation;

		// Setup physics - make pins more stable
		_rigidbody.Gravity = true;
		_rigidbody.MotionEnabled = true;
		_rigidbody.MassOverride = PinMass;
		_rigidbody.LinearDamping = 0.5f;
		_rigidbody.AngularDamping = AngularDamping; // More angular damping = harder to tip

		// Tag the pin
		Tags.Add("bowling_pin");
	}

	protected override void OnFixedUpdate()
	{
		if (IsKnockedOver)
			return;

		// Only check for knockdown after being hit (gives pins time to settle initially)
		if (!_wasHit)
			return;

		// Check if pin is knocked over by checking its rotation
		var upVector = WorldRotation.Up;
		var dot = Vector3.Dot(upVector, Vector3.Up);
		var angleFromVertical = MathF.Acos(dot.Clamp(-1.0f, 1.0f)) * (180.0f / MathF.PI);

		if (angleFromVertical > KnockOverAngle)
		{
			KnockOver();
		}

		// Also check if pin fell below lane
		if (WorldPosition.z < _startPosition.z - 100f)
		{
			KnockOver();
		}
	}

	/// <summary>
	/// Called when the pin is hit by the ball
	/// </summary>
	public void OnHitByBall(Collision collision)
	{
		if (IsKnockedOver)
			return;

		_wasHit = true;

		// Apply additional impact force for more satisfying hits
		var impulse = collision.Contact.Impulse;
		if (impulse > 30.0f)
		{
			// Add some upward kick to make pins fly more dramatically
			var otherPos = collision.Other.GameObject.WorldPosition;
			var knockDirection = (WorldPosition - otherPos).Normal;
			knockDirection.z += 0.3f;
			knockDirection = knockDirection.Normal;

			_rigidbody.ApplyImpulse(knockDirection * impulse * 0.5f);
		}
	}

	/// <summary>
	/// Called when hit by another pin (chain reaction)
	/// </summary>
	public void OnHitByPin(BowlingPin otherPin, Collision collision)
	{
		if (IsKnockedOver)
			return;

		_wasHit = true;

		// Less force from pin-to-pin hits
		var impulse = collision.Contact.Impulse;
		if (impulse > 20.0f)
		{
			var knockDirection = (WorldPosition - otherPin.WorldPosition).Normal;
			_rigidbody.ApplyImpulse(knockDirection * impulse * 0.3f);
		}
	}

	/// <summary>
	/// Mark the pin as knocked over
	/// </summary>
	public void KnockOver()
	{
		if (IsKnockedOver)
			return;

		IsKnockedOver = true;
		_timeSinceKnocked = 0;

		Log.Info($"Pin {PinNumber} knocked over!");

		// Notify game manager if it exists
		var gameManager = Scene.GetAllComponents<BowlingGameManager>().FirstOrDefault();
		if (gameManager.IsValid())
		{
			gameManager.OnPinKnockedOver(this);
		}
	}

	/// <summary>
	/// Reset the pin to its starting position
	/// </summary>
	public new void Reset()
	{
		IsKnockedOver = false;
		_wasHit = false;
		_timeSinceKnocked = 0;

		WorldPosition = _startPosition;
		WorldRotation = _startRotation;

		if (_rigidbody.IsValid())
		{
			_rigidbody.Velocity = Vector3.Zero;
			_rigidbody.AngularVelocity = Vector3.Zero;
			_rigidbody.Sleeping = true;
		}
	}

	// Collision handling
	public void OnCollisionStart(Collision collision)
	{
		// Check if we collided with the ball
		if (collision.Other.GameObject.Tags.Has("bowling_ball"))
		{
			OnHitByBall(collision);
			return;
		}

		// Check if we collided with another pin
		if (collision.Other.GameObject.Tags.Has("bowling_pin"))
		{
			var otherPin = collision.Other.GameObject.Components.Get<BowlingPin>();
			if (otherPin.IsValid())
			{
				OnHitByPin(otherPin, collision);
			}
		}
	}

	public void OnCollisionUpdate(Collision collision)
	{
		// Additional collision handling if needed
	}

	public void OnCollisionStop(CollisionStop collision)
	{
		// Cleanup
	}
}
