public enum Faction
{
	Player,
	Enemy,
	Neutral
}

public enum TargetStrategy
{
	HighestThreat,
	LowestHP,
	LowestArmor,
	Nearest,
	ClassPriority,
	DensestCluster
}

/// <summary>
/// Interface for anything that can take damage. DamageResolver uses this
/// instead of separate Health/Faction components.
/// </summary>
public interface IDamageable
{
	float HealthCurrent { get; set; }
	float HealthMax { get; set; }
	bool IsDead { get; set; }
	Faction Faction { get; }

	void ApplyDamage( float amount, DamageType type, Component attacker );
	void ApplyKnockback( Vector3 direction, float force );
}
