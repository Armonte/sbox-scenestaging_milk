/// <summary>
/// Quake/Source-style FPS movement with air strafing.
/// No sprint — base speed only, modified by class/items.
/// Shift is reserved for movement abilities.
/// </summary>
[Title( "Roguelite Movement" )]
[Icon( "directions_run" )]
public sealed class PlayerMovement : Component
{
	[Property] public float BaseSpeed { get; set; } = 280f;
	[Property] public float JumpForce { get; set; } = 280f;
	[Property] public Vector3 Gravity { get; set; } = new Vector3( 0, 0, 800 );
	[Property] public float GroundAcceleration { get; set; } = 10f;
	[Property] public float AirAcceleration { get; set; } = 50f;
	[Property] public float AirMaxWishSpeed { get; set; } = 30f;
	[Property] public float GroundFriction { get; set; } = 6f;
	[Property] public float DashSpeed { get; set; } = 600f;
	[Property] public float DashCooldown { get; set; } = 1.5f;

	public Vector3 WishVelocity { get; private set; }
	public bool IsFrozen { get; private set; }
	public bool CanDash => _timeSinceDash >= DashCooldown;

	private TimeSince _timeSinceDash = 10f;
	private TimeSince _dashFrictionProtection = 10f;

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;
		if ( IsFrozen ) return;

		var cc = Components.Get<CharacterController>();
		if ( !cc.IsValid() ) return;

		BuildWishVelocity();

		// Auto bhop — jump as soon as we touch ground while holding jump
		if ( cc.IsOnGround && Input.Down( "Jump" ) )
		{
			cc.Punch( Vector3.Up * JumpForce );
		}

		// Dash — shift key, goes in movement direction (or forward if standing still)
		if ( Input.Pressed( "Run" ) && CanDash )
		{
			Dash( cc );
		}

		if ( cc.IsOnGround )
		{
			GroundMove( cc );
		}
		else
		{
			AirMove( cc );
		}

		cc.Move();

		// Second half of gravity
		if ( !cc.IsOnGround )
		{
			cc.Velocity -= Gravity * Time.Delta * 0.5f;
		}
		else
		{
			cc.Velocity = cc.Velocity.WithZ( 0 );
		}
	}

	/// <summary>
	/// Standard ground movement — full acceleration and friction.
	/// </summary>
	private void GroundMove( CharacterController cc )
	{
		cc.Velocity = cc.Velocity.WithZ( 0 );
		cc.Accelerate( WishVelocity );

		// Reduced friction right after a dash so momentum carries
		var friction = _dashFrictionProtection < 0.3f ? 0.5f : GroundFriction;
		cc.ApplyFriction( friction );
	}

	/// <summary>
	/// Quake-style air movement. The key to strafe jumping:
	/// - Only accelerate in the wish direction up to AirMaxWishSpeed
	/// - Don't clamp total speed — this lets you gain speed by turning + strafing
	/// - Low air friction so speed is preserved
	/// </summary>
	private void AirMove( CharacterController cc )
	{
		// First half of gravity
		cc.Velocity -= Gravity * Time.Delta * 0.5f;

		// Air strafe acceleration
		if ( WishVelocity.Length > 0 )
		{
			var wishDir = WishVelocity.Normal;
			var currentSpeed = Vector3.Dot( cc.Velocity, wishDir );
			var addSpeed = AirMaxWishSpeed - currentSpeed;

			if ( addSpeed > 0 )
			{
				var accelSpeed = AirAcceleration * AirMaxWishSpeed * Time.Delta;
				accelSpeed = MathF.Min( accelSpeed, addSpeed );
				cc.Velocity += wishDir * accelSpeed;
			}
		}

		// Minimal air friction
		cc.ApplyFriction( 0.1f );
	}

	private void BuildWishVelocity()
	{
		var camera = Components.Get<PlayerCamera>();
		var rot = camera is not null
			? camera.EyeAngles.ToRotation()
			: WorldRotation;

		WishVelocity = rot * Input.AnalogMove;
		WishVelocity = WishVelocity.WithZ( 0 );

		if ( !WishVelocity.IsNearZeroLength )
			WishVelocity = WishVelocity.Normal;

		WishVelocity *= BaseSpeed;
	}

	/// <summary>
	/// Dash in the direction the player is holding. If no input, dash forward.
	/// </summary>
	private void Dash( CharacterController cc )
	{
		var camera = Components.Get<PlayerCamera>();
		var rot = camera is not null
			? camera.EyeAngles.ToRotation()
			: WorldRotation;

		var moveInput = Input.AnalogMove;
		Vector3 dashDir;

		if ( moveInput.Length > 0.1f )
		{
			dashDir = (rot * moveInput).WithZ( 0 ).Normal;
		}
		else
		{
			dashDir = rot.Forward.WithZ( 0 ).Normal;
		}

		// Add dash impulse to current velocity — momentum carries through
		var currentHorizontal = cc.Velocity.WithZ( 0 );
		cc.Velocity = (currentHorizontal + dashDir * DashSpeed).WithZ( cc.Velocity.z );
		_timeSinceDash = 0;
		_dashFrictionProtection = 0;
	}

	/// <summary>
	/// Dash with hit detection. Used by weapon abilities (e.g. SwordWeapon piercing dash).
	/// </summary>
	public void DashForward( float distance, Action<SceneTraceResult> onHit = null )
	{
		var cc = Components.Get<CharacterController>();
		if ( !cc.IsValid() ) return;

		var camera = Components.Get<PlayerCamera>();
		var forward = camera is not null
			? camera.EyeAngles.ToRotation().Forward.WithZ( 0 ).Normal
			: WorldRotation.Forward;

		var currentHorizontal = cc.Velocity.WithZ( 0 );
		cc.Velocity = (currentHorizontal + forward * DashSpeed).WithZ( cc.Velocity.z );
		_timeSinceDash = 0;
		_dashFrictionProtection = 0;

		if ( onHit is not null )
		{
			var tr = Scene.Trace
				.Ray( WorldPosition + Vector3.Up * 40f, WorldPosition + Vector3.Up * 40f + forward * distance )
				.Size( 20f )
				.IgnoreGameObjectHierarchy( GameObject )
				.Run();

			if ( tr.Hit )
				onHit( tr );
		}
	}

	public void SetFrozen( bool frozen )
	{
		IsFrozen = frozen;

		if ( frozen )
		{
			var cc = Components.Get<CharacterController>();
			if ( cc.IsValid() )
				cc.Velocity = Vector3.Zero;
		}
	}

	public void InitFromClass( ClassProfile stats )
	{
		BaseSpeed = stats.Speed;
	}
}
