/// <summary>
/// Damage resistance component. Provides flat armor and per-DamageType resistance multipliers.
/// Used by DamageResolver during damage calculation.
/// </summary>
[Title( "Armor" )]
[Icon( "shield" )]
public sealed class RogueliteArmorComponent : Component
{
	[Property] public float BaseArmor { get; set; } = 0f;

	/// <summary>
	/// Returns a damage multiplier for the given type. Less than 1 = resistant, greater than 1 = vulnerable.
	/// Flat armor is applied as a percentage reduction: 100 armor = 50% reduction at 100 damage.
	/// </summary>
	public float GetResistance( DamageType type )
	{
		if ( type == DamageType.True )
			return 1f;

		// Flat armor uses diminishing returns formula: reduction = armor / (armor + 100)
		var armorReduction = 1f - (BaseArmor / (BaseArmor + 100f));

		return armorReduction;
	}
}
