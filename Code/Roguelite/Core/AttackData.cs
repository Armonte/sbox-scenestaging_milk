/// <summary>
/// Immutable snapshot of everything about a single attack. Created by weapons/abilities,
/// consumed by DamageResolver. Never modified after construction.
/// </summary>
public readonly struct AttackData
{
	public readonly float BaseDamage;
	public readonly DamageType Type;
	public readonly bool CanCrit;
	public readonly bool CanKnockback;
	public readonly float KnockbackForce;
	public readonly bool PierceTargets;

	public AttackData(
		float baseDamage,
		DamageType type,
		bool canCrit = true,
		bool canKnockback = false,
		float knockbackForce = 0f,
		bool pierceTargets = false )
	{
		BaseDamage = baseDamage;
		Type = type;
		CanCrit = canCrit;
		CanKnockback = canKnockback;
		KnockbackForce = knockbackForce;
		PierceTargets = pierceTargets;
	}
}
