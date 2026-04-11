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
	[Property] public float ArrowSpeed { get; set; } = 3000f;
	[Property] public float ArrowGravity { get; set; } = 0f;
	[Property] public float FireCooldown { get; set; } = 0.3f;

	/// <summary>
	/// Speed/knockback floor at 0% draw, as a fraction of full power.
	/// Quick shots travel at this fraction of ArrowSpeed with the same fraction of knockback.
	/// </summary>
	[Property] public float MinForceMultiplier { get; set; } = 0.4f;

	/// <summary>
	/// Where the arrow visually spawns, in eye-local space (X = right, Y = up, Z = forward).
	/// Doesn't affect where the arrow lands — it always flies straight to the crosshair hit.
	/// This just moves the visible launch point so the arrow looks like it comes from the bow hand.
	/// </summary>
	[Property, Group( "Aim" )] public Vector3 SpawnOffset { get; set; } = new Vector3( 20f, -8f, 10f );

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

		var eyePos = Owner.WorldPosition + Vector3.Up * Owner.EyeHeight;
		var lookRot = Owner.EyeAngles.ToRotation();

		// 1. Trace from the eye straight forward — this is where the crosshair actually points.
		var aimEnd = eyePos + lookRot.Forward * 10000f;
		var aimTrace = HitDetection.Ray( Scene, eyePos, aimEnd, 1f, Owner.GameObject );
		var aimPoint = aimTrace.Hit ? aimTrace.HitPosition : aimEnd;

		// 2. Spawn the arrow at the bow-hand offset (eye-local, rotates with the camera).
		var spawnPos = eyePos
			+ lookRot.Right * SpawnOffset.x
			+ lookRot.Up * SpawnOffset.y
			+ lookRot.Forward * SpawnOffset.z;

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

		ProjectileBase.Spawn(
			Scene,
			spawnPos,
			shootRot,
			attack,
			Owner,
			speed: arrowSpeed,
			gravity: ArrowGravity
		);

		BroadcastFire();
	}

	[Rpc.Broadcast]
	private void BroadcastFire()
	{
		// All clients play fire animation/sound
	}
}
