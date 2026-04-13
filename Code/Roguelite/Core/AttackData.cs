/// <summary>
/// Immutable snapshot of everything about a single attack. Created by weapons/abilities,
/// consumed by DamageResolver. Never modified after construction.
///
/// Includes the attacker's hit sound so the feedback stays with the attack even if the
/// player swaps weapons while a projectile is in flight.
/// </summary>
public readonly struct AttackData
{
	public readonly float BaseDamage;
	public readonly DamageType Type;
	public readonly bool CanCrit;
	public readonly bool CanKnockback;
	public readonly float KnockbackForce;
	public readonly bool PierceTargets;

	// Attacker-side hit confirmation sound — played at the impact point with
	// pitch modulated by final damage. Each weapon stamps its own sound here
	// via WeaponBase.BuildAttack so bow sounds different from sword from beam.
	public readonly SoundEvent HitSound;
	public readonly float HitSoundPitchMin;
	public readonly float HitSoundPitchMax;
	public readonly float HitSoundReferenceDamage;

	public AttackData(
		float baseDamage,
		DamageType type,
		bool canCrit = true,
		bool canKnockback = false,
		float knockbackForce = 0f,
		bool pierceTargets = false,
		SoundEvent hitSound = null,
		float hitSoundPitchMin = 0.8f,
		float hitSoundPitchMax = 1.5f,
		float hitSoundReferenceDamage = 50f )
	{
		BaseDamage = baseDamage;
		Type = type;
		CanCrit = canCrit;
		CanKnockback = canKnockback;
		KnockbackForce = knockbackForce;
		PierceTargets = pierceTargets;
		HitSound = hitSound;
		HitSoundPitchMin = hitSoundPitchMin;
		HitSoundPitchMax = hitSoundPitchMax;
		HitSoundReferenceDamage = hitSoundReferenceDamage;
	}
}
