using Sandbox;

/// <summary>
/// Component for the bowling ball. Handles physics, throwing, and collision with pins.
/// </summary>
public sealed class BowlingBall : Component, Component.ICollisionListener
{
	[Property] public float ThrowForce { get; set; } = 1000.0f;
	[Property] public float MaxThrowForce { get; set; } = 2000.0f;
	[Property] public float Mass { get; set; } = 7.0f; // Standard bowling ball mass in kg
	[Property] public float Friction { get; set; } = 0.1f;
	[Property] public float RollingResistance { get; set; } = 0.05f;
	
	[Property] public bool IsThrown { get; private set; } = false;
	[Property] public bool IsInGutter { get; set; } = false;
	
	private Rigidbody _rigidbody;
	private SphereCollider _collider;
	private TimeSince _timeSinceThrown;
	
	protected override void OnStart()
	{
		base.OnStart();

		// Get existing components - search in children too since Rigidbody/Collider are on child GameObject
		_rigidbody = Components.Get<Rigidbody>(FindMode.EverythingInSelfAndDescendants);
		_collider = Components.Get<SphereCollider>(FindMode.EverythingInSelfAndDescendants);

		if (!_rigidbody.IsValid())
		{
			Log.Warning("BowlingBall: No Rigidbody found on prefab!");
			return;
		}

		if (!_collider.IsValid())
		{
			Log.Warning("BowlingBall: No SphereCollider found on prefab!");
		}
		else
		{
			// Ensure collider has valid radius (positive value)
			if (_collider.Radius <= 0)
			{
				Log.Warning($"BowlingBall: Invalid collider radius ({_collider.Radius}), setting to default 32");
				_collider.Radius = 32.0f;
			}
			
		}

		// Tag the ball for easy identification
		Tags.Add("bowling_ball");

		// Setup physics properties
		_rigidbody.MassOverride = Mass;
		_rigidbody.OverrideMassCenter = false;
		_rigidbody.Gravity = true;
		_rigidbody.LinearDamping = RollingResistance;
		_rigidbody.AngularDamping = 0.1f;
	}
	
	/// <summary>
	/// Throw the ball with a given direction and force
	/// </summary>
	public void Throw(Vector3 direction, float forceMultiplier = 1.0f)
	{
		if (IsThrown)
			return;

		IsThrown = true;
		_timeSinceThrown = 0;

		// Re-fetch components in case they weren't initialized yet or after unparenting
		if (!_rigidbody.IsValid())
		{
			_rigidbody = Components.Get<Rigidbody>(FindMode.EverythingInSelfAndDescendants);
		}
		
		if (!_collider.IsValid())
		{
			_collider = Components.Get<SphereCollider>(FindMode.EverythingInSelfAndDescendants);
		}

		// Enable physics when throwing
		if (_rigidbody.IsValid())
		{
			// Ensure collider is enabled for collisions
			if (_collider.IsValid())
			{
				_collider.Enabled = true;
				if (_collider.IsTrigger)
				{
					_collider.IsTrigger = false;
				}
			}

			// Enable motion and ensure gravity is on
			_rigidbody.MotionEnabled = true;
			_rigidbody.Gravity = true;
			_rigidbody.GravityScale = 1.0f;
			
			// Wake up the rigidbody to ensure physics simulation starts
			_rigidbody.Sleeping = false;
			
			// Ensure the rigidbody is active
			_rigidbody.Enabled = true;

			// Ensure mass is set correctly
			_rigidbody.MassOverride = Mass;
		}
		else
		{
			Log.Error("BowlingBall: Cannot throw - Rigidbody is not valid!");
			return;
		}

		var throwForce = (ThrowForce * forceMultiplier).Clamp(0, MaxThrowForce);
		var velocity = direction.Normal * throwForce;

		_rigidbody.Velocity = velocity;

		Log.Info($"Ball thrown with force: {throwForce}, velocity: {velocity}, MotionEnabled: {_rigidbody.MotionEnabled}, Gravity: {_rigidbody.Gravity}");
	}
	
	/// <summary>
	/// Reset the ball to its starting position
	/// </summary>
	public void Reset(Vector3 position, Rotation rotation)
	{
		IsThrown = false;
		IsInGutter = false;
		_timeSinceThrown = 0;
		
		WorldPosition = position;
		WorldRotation = rotation;
		
		if (_rigidbody.IsValid())
		{
			_rigidbody.Velocity = Vector3.Zero;
			_rigidbody.AngularVelocity = Vector3.Zero;
			_rigidbody.MotionEnabled = false; // Disable physics when reset (will be enabled when thrown)
			_rigidbody.Sleeping = true;
		}
	}
	
	protected override void OnFixedUpdate()
	{
		if (!IsThrown || IsInGutter)
			return;

		// Apply rolling resistance
		if (_rigidbody.Velocity.Length > 0.1f)
		{
			var resistance = _rigidbody.Velocity.Normal * RollingResistance * 100.0f;
			_rigidbody.Velocity -= resistance * Time.Delta;
		}

		// Stop the ball if it's moving very slowly and on ground
		if (_rigidbody.Velocity.Length < 10.0f && _rigidbody.Velocity.z < 1.0f)
		{
			_rigidbody.Velocity = Vector3.Zero;
			_rigidbody.AngularVelocity = Vector3.Zero;
		}
	}

	
	// Handle collisions with pins
	public void OnCollisionStart(Collision collision)
	{
		// Ignore collisions if ball hasn't been thrown yet
		if (!IsThrown)
			return;
			
		// Ignore player collisions
		if (collision.Other.GameObject.Tags.Has("player"))
			return;

		// Check if we hit a pin
		if (collision.Other.GameObject.Tags.Has("bowling_pin"))
		{
			Log.Info($"Ball collision with: {collision.Other.GameObject.Name}");
			var pin = collision.Other.GameObject.Components.Get<BowlingPin>();
			if (pin.IsValid())
			{
				pin.OnHitByBall(collision);
			}
		}
	}
	
	public void OnCollisionUpdate(Collision collision)
	{
		// Additional collision handling if needed
	}
	
	public void OnCollisionStop(CollisionStop collision)
	{
		// Cleanup if needed
	}
}
