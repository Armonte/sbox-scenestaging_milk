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

	private readonly CooldownTracker _cooldowns = new();

	public virtual void OnEquip( RoguelitePlayer owner ) => Owner = owner;
	public virtual void OnUnequip( RoguelitePlayer owner ) => Owner = null;
	public virtual void OnOwnerDied() => ResetAllCooldowns();

	protected override void OnUpdate()
	{
		if ( Owner is null || !Owner.IsAlive ) return;

		_cooldowns.Tick( Time.Delta );

		OnWeaponTick();
	}

	public abstract void PrimaryAttack();
	public virtual void SecondaryAttack() { }
	public virtual void WeaponAbility( int index ) { }
	protected virtual void OnWeaponTick() { }

	// --- Cooldown helpers ---

	protected bool IsCooldownReady( string id ) => _cooldowns.IsReady( id );
	protected void StartCooldown( string id, float duration ) => _cooldowns.Start( id, duration );
	protected void ResetAllCooldowns() => _cooldowns.Reset();

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
