/// <summary>
/// Bow weapon with draw mechanic. Hold attack to draw, release to fire.
/// Damage and speed scale with draw time. Has a sweetspot window for max damage.
/// </summary>
[Title( "Bow" )]
[Icon( "sports_martial_arts" )]
public sealed class BowWeapon : WeaponBase
{
	[Property] public float MinDamageMultiplier { get; set; } = 0.3f;
	[Property] public float MaxDamageMultiplier { get; set; } = 1.2f;
	[Property] public float DrawTime { get; set; } = 1.2f;
	[Property] public float SweetspotStart { get; set; } = 0.85f;
	[Property] public float SweetspotEnd { get; set; } = 0.95f;
	[Property] public float SweetspotBonus { get; set; } = 1.5f;
	[Property] public float ArrowSpeed { get; set; } = 6000f;
	[Property] public float ArrowGravity { get; set; } = 200f;
	[Property] public float FireCooldown { get; set; } = 0.3f;

	/// <summary>
	/// Speed/knockback floor at 0% draw, as a fraction of full power.
	/// Quick shots travel at this fraction of ArrowSpeed with the same fraction of knockback.
	/// </summary>
	[Property] public float MinForceMultiplier { get; set; } = 0.4f;

	/// <summary>
	/// Where the arrow visually spawns, in eye-local space (X = right, Y = up, Z = forward).
	/// Used as a fallback when no BowViewmodel / NockPoint is found. Doesn't affect where
	/// the arrow lands — it always flies straight to the crosshair hit.
	/// </summary>
	[Property, Group( "Aim" )] public Vector3 SpawnOffset { get; set; } = new Vector3( 20f, -8f, 10f );

	// Scene-lookup reference — found via Scene.GetAllComponents<BowViewmodel>()
	// in OnEquip. BowWeapon is Components.Create<>()'d at runtime by the pedestal,
	// so [Property] GameObject slots would always be null. The viewmodel lives in
	// the scene and owns its own NockPoint / NockedArrowVisual refs (those slots
	// WORK in the editor because both sides are in the same scene).
	private BowViewmodel _viewmodel;

	/// <summary>
	/// Path to the arrow model (project-relative, i.e. under Assets/).
	/// </summary>
	/// <summary>
	/// Drag a .vmdl here. If null, projectiles fall back to the dev sphere.
	/// </summary>
	[Property, Group( "Visuals" )] public Model ArrowModel { get; set; }

	[Property, Group( "Visuals" )] public bool ArrowTrail { get; set; } = true;
	[Property, Group( "Visuals" )] public Color ArrowTrailColor { get; set; } = new Color( 1f, 0.92f, 0.75f );

	/// <summary>
	/// Visual rotation offset for the arrow mesh. Defaults to yaw 180° because the
	/// dungeon arrow FBX is modeled pointing back along the path of travel — tweak
	/// in the inspector if a different arrow model points along a different axis.
	/// </summary>
	[Property, Group( "Visuals" )] public Angles ArrowAngleOffset { get; set; } = new Angles( 0f, 180f, 0f );

	[Property, Group( "Visuals" )] public float ArrowScale { get; set; } = 1.3f;

	/// <summary>
	/// Arrows lodge into whatever they hit and linger for a few seconds.
	/// </summary>
	[Property, Group( "Stick" )] public bool ArrowSticks { get; set; } = true;

	/// <summary>
	/// Seconds a stuck arrow stays in the world before despawning.
	/// </summary>
	[Property, Group( "Stick" )] public float ArrowStickLifetime { get; set; } = 5f;

	/// <summary>
	/// Use hitbox traces so weak-point tags (e.g. "head", "weakpoint") apply
	/// damage multipliers when the arrow hits them.
	/// </summary>
	[Property, Group( "Combat" )] public bool EnableWeakpointHits { get; set; } = true;

	/// <summary>
	/// Log every arrow hit to the console (what was hit, whether a hitbox was
	/// involved, which weak-point tags matched). Turn on when tuning hurtboxes.
	/// </summary>
	[Property, Group( "Combat" )] public bool DebugHits { get; set; } = false;

	public float DrawProgress => _isDrawing ? Math.Clamp( _drawTimer / DrawTime, 0f, 1f ) : 0f;
	public bool IsDrawing => _isDrawing;
	public bool IsSweetspot => DrawProgress >= SweetspotStart && DrawProgress <= SweetspotEnd;

	private bool _isDrawing;
	private float _drawTimer;

	public BowWeapon()
	{
		BaseDamage = 25f;
		Category = WeaponCategory.Bow;
	}

	public override void OnEquip( RoguelitePlayer owner )
	{
		base.OnEquip( owner );

		var all = Scene.GetAllComponents<BowViewmodel>().ToList();
		_viewmodel = all.FirstOrDefault();

		if ( _viewmodel is null )
		{
			Log.Warning( "[BowWeapon] OnEquip — no BowViewmodel component found in the scene. " +
				"Add a BowViewmodel component to your bow_viewmodel GameObject (it must be on an " +
				"enabled GameObject so Scene.GetAllComponents can see it)." );
			return;
		}

		Log.Info( $"[BowWeapon] OnEquip — found {all.Count} BowViewmodel(s), using {_viewmodel.GameObject.Name}" );
		_viewmodel.SetVisible( true );
		_viewmodel.SetNockedArrowVisible( true );
	}

	public override void OnUnequip( RoguelitePlayer owner )
	{
		base.OnUnequip( owner );
		if ( _viewmodel is not null )
		{
			_viewmodel.SetVisible( false );
			_viewmodel = null;
		}
	}

	public override void PrimaryAttack()
	{
		// Bow uses hold/release — handled in OnWeaponTick
	}

	public override void SecondaryAttack()
	{
		// Quick shot — instant fire at minimum draw
		if ( !IsCooldownReady( "fire" ) ) return;

		FireArrow( MinDamageMultiplier, 0f );
		StartCooldown( "fire", FireCooldown );
	}

	protected override void OnWeaponTick()
	{
		if ( Owner is null || Owner.IsProxy ) return;

		// Push current draw progress into the viewmodel every frame so it can
		// drive the bone bend, string pull-back, and nocked-arrow slide. Zero
		// when not drawing, so the bow snaps back to rest pose on release/fire.
		if ( _viewmodel is not null )
			_viewmodel.SetDrawProgress( DrawProgress );

		// Start drawing on press
		if ( Input.Down( "attack1" ) && !_isDrawing && IsCooldownReady( "fire" ) )
		{
			_isDrawing = true;
			_drawTimer = 0f;
		}

		// Continue drawing
		if ( _isDrawing && Input.Down( "attack1" ) )
		{
			_drawTimer += Time.Delta;

			// Overdrawn — auto fire at slightly reduced damage, but still full physical force
			if ( _drawTimer > DrawTime * 1.3f )
			{
				FireArrow( MaxDamageMultiplier * 0.8f, 1f );
				StartCooldown( "fire", FireCooldown );
			}
		}

		// Release — fire based on draw progress
		if ( _isDrawing && !Input.Down( "attack1" ) )
		{
			var progress = Math.Clamp( _drawTimer / DrawTime, 0f, 1f );
			var dmgMult = MathX.Lerp( MinDamageMultiplier, MaxDamageMultiplier, progress );

			// Sweetspot bonus
			if ( progress >= SweetspotStart && progress <= SweetspotEnd )
				dmgMult = SweetspotBonus;

			FireArrow( dmgMult, progress );
			StartCooldown( "fire", FireCooldown );
		}
	}

	[Property] public float KnockbackForce { get; set; } = 600f;

	private void FireArrow( float damageMultiplier, float drawProgress )
	{
		_isDrawing = false;
		_drawTimer = 0;

		if ( Owner is null ) return;

		// Snap the viewmodel bones back to rest BEFORE reading NockPoint.WorldPosition.
		// Without this, the nock is still at the drawn-back position from last frame's
		// bone pose and the arrow spawns behind the bowstring's rest position.
		if ( _viewmodel is not null )
			_viewmodel.ForceRestPose();

		var eyePos = Owner.WorldPosition + Vector3.Up * Owner.EyeHeight;
		var lookRot = Owner.EyeAngles.ToRotation();

		// 1. Trace from the eye straight forward — this is where the crosshair actually points.
		var aimEnd = eyePos + lookRot.Forward * 10000f;
		var aimTrace = HitDetection.Ray( Scene, eyePos, aimEnd, 1f, Owner.GameObject );
		var aimPoint = aimTrace.Hit ? aimTrace.HitPosition : aimEnd;

		// 2. Spawn the arrow from the viewmodel's nock if one exists — its world
		//    position tracks the camera-parented bow mesh automatically. Otherwise
		//    fall back to the eye-local SpawnOffset so the bow still works without
		//    a viewmodel wired up (e.g. in tests or third-person scenarios).
		Vector3 spawnPos;
		var nock = _viewmodel?.NockPoint;
		if ( nock.IsValid() )
		{
			spawnPos = nock.WorldPosition;
		}
		else
		{
			spawnPos = eyePos
				+ lookRot.Right * SpawnOffset.x
				+ lookRot.Up * SpawnOffset.y
				+ lookRot.Forward * SpawnOffset.z;
		}

		// 3. Direction from the offset spawn point toward the crosshair hit.
		//    Arrow launches angled toward crosshair, visually comes from the bow hand,
		//    converges on exactly where the player was aiming.
		var shootDir = (aimPoint - spawnPos).Normal;
		var shootRot = Rotation.LookAt( shootDir );

		// Physical force scales with raw draw progress (decoupled from the sweetspot damage bonus).
		// A quick shot is a slow, floppy arrow; a full draw is fast and punchy.
		var forceScalar = MathX.Lerp( MinForceMultiplier, 1f, Math.Clamp( drawProgress, 0f, 1f ) );
		var arrowSpeed = ArrowSpeed * forceScalar;
		var kbForce = KnockbackForce * forceScalar;

		var attack = BuildAttack( damageMultiplier, DamageType.Pierce, canKnockback: true, knockbackForce: kbForce );

		var proj = ProjectileBase.Spawn(
			Scene,
			spawnPos,
			shootRot,
			attack,
			Owner,
			speed: arrowSpeed,
			gravity: ArrowGravity,
			model: ArrowModel,
			useTrail: ArrowTrail,
			trailColor: ArrowTrailColor,
			modelAngleOffset: ArrowAngleOffset,
			modelScale: ArrowScale,
			stickOnHit: ArrowSticks,
			useHitboxes: EnableWeakpointHits,
			debugHits: DebugHits
		);
		proj.StickLifetime = ArrowStickLifetime;

		// Hand off to the viewmodel's scale-in tween: the real world-space arrow
		// takes over flight, and the nocked-arrow visual pops back in over the
		// fire cooldown. All the timing lives on BowViewmodel now — we just kick
		// it off here with the cooldown duration.
		if ( _viewmodel is not null )
			_viewmodel.StartNockedArrowFade( FireCooldown );

		BroadcastFire();
	}

	[Rpc.Broadcast]
	private void BroadcastFire()
	{
		// All clients play fire animation/sound
	}
}
