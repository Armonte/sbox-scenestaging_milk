/// <summary>
/// Interface for class abilities. Concrete abilities implement this.
/// Weapon-specific moves (blade wave, piercing dash) stay on WeaponBase — this is for class abilities only.
/// </summary>
public interface IAbility
{
	string Id { get; }
	float Cooldown { get; }

	/// <summary>
	/// Attempt to activate the ability. Returns true if successful (cooldown will be consumed).
	/// Target may be null for self-cast abilities.
	/// </summary>
	bool TryActivate( RoguelitePlayer caster, GameObject target );
}
