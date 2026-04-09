/// <summary>
/// Bow weapon with draw mechanic. Hold attack to draw, release to fire.
/// Damage and speed scale with draw time. Has a sweetspot window for max damage.
/// </summary>
[Title( "Bow" )]
[Icon( "sports_martial_arts" )]
public sealed class BowWeapon : WeaponBase
{
	[Property] public float MinDamageMultiplier { get; set; } = 0.3f;
	[Property] public float MaxDamageMultiplier { get; set; } = 1.5f;
	[Property] public float DrawTime { get; set; } = 1.2f;
	[Property] public float SweetspotStart { get; set; } = 0.85f;
	[Property] public float SweetspotEnd { get; set; } = 0.95f;
	[Property] public float SweetspotBonus { get; set; } = 2f;
	[Property] public float ArrowSpeed { get; set; } = 3000f;
	[Property] public float ArrowGravity { get; set; } = 200f;
	[Property] public float FireCooldown { get; set; } = 0.3f;

	public float DrawProgress => _isDrawing ? Math.Clamp( _drawTimer / DrawTime, 0f, 1f ) : 0f;
	public bool IsDrawing => _isDrawing;
	public bool IsSweetspot => DrawProgress >= SweetspotStart && DrawProgress <= SweetspotEnd;

	private bool _isDrawing;
	private float _drawTimer;

	public BowWeapon()
	{
		BaseDamage = 45f;
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

		FireArrow( MinDamageMultiplier );
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

			// Overdrawn — auto fire at slightly reduced damage
			if ( _drawTimer > DrawTime * 1.3f )
			{
				FireArrow( MaxDamageMultiplier * 0.8f );
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

			FireArrow( dmgMult );
			StartCooldown( "fire", FireCooldown );
		}
	}

	[Property] public float KnockbackForce { get; set; } = 150f;

	private void FireArrow( float damageMultiplier )
	{
		_isDrawing = false;
		_drawTimer = 0;

		var camera = Owner.Components.Get<PlayerCamera>();
		if ( camera is null ) return;

		var eyePos = Owner.WorldPosition + Vector3.Up * 64f;
		var lookRot = camera.EyeAngles.ToRotation();

		// Knockback scales with draw — quick shots barely push, full draw sends them flying
		var kbForce = KnockbackForce * damageMultiplier;
		var attack = BuildAttack( damageMultiplier, DamageType.Pierce, canKnockback: true, knockbackForce: kbForce );

		ProjectileBase.Spawn(
			Scene,
			eyePos + lookRot.Forward * 20f,
			lookRot,
			attack,
			Owner,
			speed: ArrowSpeed,
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
