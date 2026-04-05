/// <summary>
/// All damage types in the game. Used by DamageResolver for resistance/weakness calculations.
/// </summary>
public enum DamageType
{
	// Physical
	Slash,
	Pierce,
	Blunt,

	// Elemental
	Fire,
	Frost,
	Lightning,
	Poison,

	// Magical
	Arcane,
	Holy,
	Void,

	// Special — bypasses all resistances
	True
}
