/// <summary>
/// The whole player. Owns movement, camera, health, weapons, and ability input.
/// Stats are configured on the prefab and can be optionally overridden by class presets
/// (only if HealthMax is left at 0 — otherwise the prefab is the source of truth).
/// </summary>
[Title( "Roguelite Player" )]
[Icon( "person" )]
public sealed class RoguelitePlayer : Component, global::IDamageable
{
	[RequireComponent] public CharacterController CharController { get; set; }
	[RequireComponent] public AbilityComponent Abilities { get; set; }

	// =====================================================================
	// CLASS / IDENTITY
	// =====================================================================

	[Property, Group( "Class" )]
	public PlayerClass Class { get; set; } = PlayerClass.Warrior;

	// =====================================================================
	// HEALTH
	// =====================================================================

	[Property, Group( "Health" )] public float HealthMax { get; set; } = 0f;
	[Sync] public float HealthCurrent { get; set; }
	[Sync] public bool IsDead { get; set; }
	[Sync] public bool IsAlive { get; set; } = true;

	public Faction Faction => global::Faction.Player;

	// =====================================================================
	// MOVEMENT
	// =====================================================================

	[Property, Group( "Movement" )] public float BaseSpeed { get; set; } = 280f;
	[Property, Group( "Movement" )] public float JumpForce { get; set; } = 280f;
	[Property, Group( "Movement" )] public Vector3 Gravity { get; set; } = new Vector3( 0, 0, 800 );
	[Property, Group( "Movement" )] public float GroundAcceleration { get; set; } = 10f;
	[Property, Group( "Movement" )] public float AirAcceleration { get; set; } = 50f;
	[Property, Group( "Movement" )] public float AirMaxWishSpeed { get; set; } = 30f;
	[Property, Group( "Movement" )] public float GroundFriction { get; set; } = 6f;
	[Property, Group( "Movement" )] public float DashSpeed { get; set; } = 600f;

	public Vector3 WishVelocity { get; private set; }
	public bool IsFrozen { get; private set; }

	private TimeSince _dashFrictionProtection = 10f;

	// =====================================================================
	// CAMERA
	// =====================================================================

	[Property, Group( "Camera" )] public float EyeHeight { get; set; } = 64f;
	[Property, Group( "Camera" )] public bool FirstPerson { get; set; } = false;
	[Property, Group( "Camera" )] public float ThirdPersonDistance { get; set; } = 300f;

	[Sync] public Angles EyeAngles { get; set; }

	private CameraComponent _cachedCam;

	// =====================================================================
	// WEAPONS
	// =====================================================================

	public WeaponBase ActiveWeapon { get; private set; }

	// =====================================================================
	// LIFECYCLE
	// =====================================================================

	protected override void OnEnabled()
	{
		if ( IsProxy ) return;

		_cachedCam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
		if ( _cachedCam.IsValid() )
		{
			var angles = _cachedCam.WorldRotation.Angles();
			angles.roll = 0;
			EyeAngles = angles;
		}
	}

	protected override void OnStart()
	{
		Tags.Add( "player" );

		if ( CharController.IsValid() )
			CharController.IgnoreLayers.Add( "enemy" );

		// Stat defaults: ClassStats only fills in if the prefab left HealthMax at 0
		var stats = ClassStats.Get( Class );

		if ( !IsProxy )
		{
			if ( HealthMax <= 0f ) HealthMax = stats.MaxHp;
			HealthCurrent = HealthMax;
			IsDead = false;
		}

		ActiveWeapon = Components.Get<WeaponBase>( FindMode.EverythingInSelfAndDescendants );
		ActiveWeapon?.OnEquip( this );

		Log.Info( $"[RoguelitePlayer] Started as {Class}. HP: {HealthCurrent}/{HealthMax}. Weapon: {ActiveWeapon?.GetType().Name ?? "NONE"}" );

		if ( !IsProxy )
		{
			var hudObj = Scene.CreateObject();
			hudObj.Name = "RogueliteHUD";
			hudObj.SetParent( GameObject );
			hudObj.Components.Create<ScreenPanel>();
			hudObj.Components.Create<RogueliteHUD>();
		}
	}

	// =====================================================================
	// UPDATE LOOPS
	// =====================================================================

	protected override void OnUpdate()
	{
		// Camera and body rotation runs for everyone (proxies see other players turning)
		UpdateCamera();
		UpdateBodyRotation();
		UpdateBodyVisibility();

		if ( IsProxy ) return;
		if ( !IsAlive ) return;

		HandleWeaponInput();
		HandleAbilityInput();
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;
		if ( IsFrozen ) return;
		if ( !CharController.IsValid() ) return;

		BuildWishVelocity();

		// Auto-bhop: jump as soon as we touch ground while holding jump
		if ( CharController.IsOnGround && Input.Down( "Jump" ) )
		{
			CharController.Punch( Vector3.Up * JumpForce );
		}

		if ( CharController.IsOnGround )
			GroundMove();
		else
			AirMove();

		CharController.Move();

		// Second half of gravity / kill vertical velocity on ground
		if ( !CharController.IsOnGround )
			CharController.Velocity -= Gravity * Time.Delta * 0.5f;
		else
			CharController.Velocity = CharController.Velocity.WithZ( 0 );
	}

	// =====================================================================
	// MOVEMENT IMPL
	// =====================================================================

	private void BuildWishVelocity()
	{
		var rot = EyeAngles.ToRotation();
		WishVelocity = rot * Input.AnalogMove;
		WishVelocity = WishVelocity.WithZ( 0 );

		if ( !WishVelocity.IsNearZeroLength )
			WishVelocity = WishVelocity.Normal;

		WishVelocity *= BaseSpeed;
	}

	private void GroundMove()
	{
		CharController.Velocity = CharController.Velocity.WithZ( 0 );
		CharController.Accelerate( WishVelocity );

		// Reduced friction right after a dash so momentum carries
		var friction = _dashFrictionProtection < 0.3f ? 0.5f : GroundFriction;
		CharController.ApplyFriction( friction );
	}

	private void AirMove()
	{
		// First half of gravity
		CharController.Velocity -= Gravity * Time.Delta * 0.5f;

		// Quake-style air strafe acceleration
		if ( WishVelocity.Length > 0 )
		{
			var wishDir = WishVelocity.Normal;
			var currentSpeed = Vector3.Dot( CharController.Velocity, wishDir );
			var addSpeed = AirMaxWishSpeed - currentSpeed;

			if ( addSpeed > 0 )
			{
				var accelSpeed = AirAcceleration * AirMaxWishSpeed * Time.Delta;
				accelSpeed = MathF.Min( accelSpeed, addSpeed );
				CharController.Velocity += wishDir * accelSpeed;
			}
		}

		CharController.ApplyFriction( 0.1f );
	}

	/// <summary>
	/// Public dash method. Called by weapons (e.g. SwordWeapon piercing dash) and
	/// will be called by the future Dash ability slot.
	/// </summary>
	public void DashForward( float distance, Action<SceneTraceResult> onHit = null )
	{
		if ( !CharController.IsValid() ) return;

		var forward = EyeAngles.ToRotation().Forward.WithZ( 0 ).Normal;
		var currentHorizontal = CharController.Velocity.WithZ( 0 );
		CharController.Velocity = (currentHorizontal + forward * DashSpeed).WithZ( CharController.Velocity.z );
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
		if ( frozen && CharController.IsValid() )
			CharController.Velocity = Vector3.Zero;
	}

	// =====================================================================
	// CAMERA IMPL
	// =====================================================================

	private void UpdateCamera()
	{
		if ( IsProxy ) return;

		var ee = EyeAngles;
		ee += Input.AnalogLook * 0.5f;
		ee.pitch = ee.pitch.Clamp( -89f, 89f );
		ee.roll = 0;
		EyeAngles = ee;

		if ( !_cachedCam.IsValid() )
			_cachedCam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();

		if ( !_cachedCam.IsValid() ) return;

		var lookDir = EyeAngles.ToRotation();
		var eyePos = WorldPosition + Vector3.Up * EyeHeight;

		if ( FirstPerson )
		{
			_cachedCam.WorldPosition = eyePos;
			_cachedCam.WorldRotation = lookDir;
		}
		else
		{
			_cachedCam.WorldPosition = eyePos + lookDir.Backward * ThirdPersonDistance + Vector3.Up * 40f;
			_cachedCam.WorldRotation = lookDir;
		}

		_cachedCam.RenderExcludeTags.Set( "viewer", true );
	}

	private void UpdateBodyRotation()
	{
		// Runs for ALL players (including proxies) so other clients see correct facing.
		var targetYaw = new Angles( 0, EyeAngles.yaw, 0 ).ToRotation();
		var body = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		if ( body.IsValid() )
			body.WorldRotation = Rotation.Lerp( body.WorldRotation, targetYaw, Time.Delta * 10f );
	}

	private void UpdateBodyVisibility()
	{
		// "viewer" tag pattern: hide own body in first person, never hide other players' bodies.
		bool viewer = FirstPerson && !IsProxy;
		var renderer = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		var bodyGo = renderer?.GameObject ?? GameObject;
		if ( bodyGo.IsValid() )
			bodyGo.Tags.Set( "viewer", viewer );
	}

	// =====================================================================
	// COMBAT / DAMAGE
	// =====================================================================

	public void ApplyDamage( float amount, DamageType type, Component attacker )
	{
		if ( IsDead ) return;

		HealthCurrent = MathF.Max( 0, HealthCurrent - amount );

		if ( HealthCurrent <= 0 )
		{
			IsDead = true;
			IsAlive = false;
			ActiveWeapon?.OnOwnerDied();
		}
	}

	public void ApplyKnockback( Vector3 direction, float force )
	{
		if ( CharController.IsValid() )
			CharController.Punch( direction.WithZ( 0 ).Normal * force );
	}

	public void Heal( float amount )
	{
		if ( IsDead ) return;
		HealthCurrent = MathF.Min( HealthMax, HealthCurrent + amount );
	}

	public void Revive( float hpPercent = 0.3f )
	{
		if ( !IsDead ) return;
		IsDead = false;
		IsAlive = true;
		HealthCurrent = HealthMax * hpPercent;
	}

	// =====================================================================
	// WEAPON / ABILITY INPUT
	// =====================================================================

	private void HandleWeaponInput()
	{
		if ( ActiveWeapon is null ) return;
		if ( Input.Pressed( "attack1" ) ) ActiveWeapon.PrimaryAttack();
		if ( Input.Pressed( "attack2" ) ) ActiveWeapon.SecondaryAttack();
	}

	private void HandleAbilityInput()
	{
		if ( Input.Pressed( "Slot1" ) ) Abilities.TryActivate( 0, this, GetAimTarget() );
		if ( Input.Pressed( "Slot2" ) ) Abilities.TryActivate( 1, this, GetAimTarget() );
		if ( Input.Pressed( "Slot3" ) ) Abilities.TryActivate( 2, this, GetAimTarget() );
		if ( Input.Pressed( "Slot4" ) ) Abilities.TryActivate( 3, this, GetAimTarget() );
	}

	private GameObject GetAimTarget()
	{
		var eyePos = WorldPosition + Vector3.Up * EyeHeight;
		var forward = EyeAngles.ToRotation().Forward;

		var tr = Scene.Trace
			.Ray( eyePos, eyePos + forward * 1500f )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		return tr.Hit ? tr.GameObject : null;
	}

	public void EquipWeapon( WeaponBase weapon )
	{
		ActiveWeapon?.OnUnequip( this );
		ActiveWeapon = weapon;
		weapon.OnEquip( this );
	}
}
