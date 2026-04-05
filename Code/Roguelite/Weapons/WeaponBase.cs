public enum WeaponCategory
{
	Sword, Dagger, Axe, Mace, Spear, Scythe, Flail, Whip, Fists, Bow, Staff, Wand
}

/// <summary>
/// Abstract base for all weapons. Handles combo tracking, cooldowns, and provides
/// BuildAttack() + PerformMeleeTrace() helpers. Subclasses implement PrimaryAttack/SecondaryAttack.
/// </summary>
[Title( "Weapon" )]
[Icon( "sports_martial_arts" )]
public abstract class WeaponBase : Component
{
	[Property] public float BaseDamage { get; set; } = 35f;
	[Property] public WeaponCategory Category { get; set; } = WeaponCategory.Sword;

	protected RoguelitePlayer Owner;

	// Inline cooldown tracker — extracted to CooldownTracker class in Phase 3
	private readonly Dictionary<string, float> _cooldowns = new();

	public virtual void OnEquip( RoguelitePlayer owner ) => Owner = owner;
	public virtual void OnUnequip( RoguelitePlayer owner ) => Owner = null;
	public virtual void OnOwnerDied() => ResetAllCooldowns();

	protected override void OnUpdate()
	{
		if ( Owner is null || !Owner.IsAlive ) return;

		// Tick cooldowns
		foreach ( var key in _cooldowns.Keys.ToList() )
			_cooldowns[key] = MathF.Max( 0, _cooldowns[key] - Time.Delta );

		OnWeaponTick();
	}

	public abstract void PrimaryAttack();
	public virtual void SecondaryAttack() { }
	public virtual void WeaponAbility( int index ) { }
	protected virtual void OnWeaponTick() { }

	// --- Cooldown helpers ---

	protected bool IsCooldownReady( string id ) =>
		!_cooldowns.ContainsKey( id ) || _cooldowns[id] <= 0;

	protected void StartCooldown( string id, float duration ) =>
		_cooldowns[id] = duration;

	protected void ResetAllCooldowns() => _cooldowns.Clear();

	// --- Attack helpers ---

	/// <summary>
	/// Build an AttackData struct from weapon stats.
	/// </summary>
	protected AttackData BuildAttack( float damageMultiplier, DamageType type,
		bool canCrit = true, bool canKnockback = false, float knockbackForce = 0f )
	{
		var dmg = BaseDamage * damageMultiplier;
		// RunModifiers will be applied here in Phase 5
		return new AttackData( dmg, type, canCrit, canKnockback, knockbackForce );
	}

	/// <summary>
	/// Perform a melee trace from the player's eye and resolve damage via DamageResolver.
	/// </summary>
	protected List<DamageResult> PerformMeleeTrace( float range = 120f, float traceRadius = 16f )
	{
		if ( Owner is null ) return new List<DamageResult>();

		var attack = BuildAttack( 1f, DamageType.Slash );
		return DamageResolver.MeleeTrace( Owner, attack, range, traceRadius );
	}
}
