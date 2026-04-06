/// <summary>
/// Manages class ability slots (separate from weapon abilities).
/// Each player has 4 slots mapped to keybinds.
/// </summary>
[Title( "Abilities" )]
[Icon( "auto_fix_high" )]
public sealed class AbilityComponent : Component
{
	public const int MaxSlots = 4;

	private readonly IAbility[] _slots = new IAbility[MaxSlots];
	private readonly CooldownTracker _cooldowns = new();

	public void Equip( int slot, IAbility ability )
	{
		if ( slot < 0 || slot >= MaxSlots ) return;
		_slots[slot] = ability;
	}

	public void Unequip( int slot )
	{
		if ( slot < 0 || slot >= MaxSlots ) return;
		_slots[slot] = null;
	}

	public IAbility GetAbility( int slot )
	{
		if ( slot < 0 || slot >= MaxSlots ) return null;
		return _slots[slot];
	}

	public bool TryActivate( int slot, RoguelitePlayer caster, GameObject target = null )
	{
		if ( slot < 0 || slot >= MaxSlots ) return false;

		var ability = _slots[slot];
		if ( ability is null ) return false;
		if ( !_cooldowns.IsReady( ability.Id ) ) return false;
		if ( !caster.IsAlive ) return false;

		if ( ability.TryActivate( caster, target ) )
		{
			_cooldowns.Start( ability.Id, ability.Cooldown );
			return true;
		}

		return false;
	}

	public float GetCooldownRemaining( int slot )
	{
		if ( slot < 0 || slot >= MaxSlots ) return 0;
		var ability = _slots[slot];
		return ability is null ? 0 : _cooldowns.Remaining( ability.Id );
	}

	public bool IsReady( int slot )
	{
		if ( slot < 0 || slot >= MaxSlots ) return false;
		var ability = _slots[slot];
		return ability is not null && _cooldowns.IsReady( ability.Id );
	}

	/// <summary>
	/// Accelerate all ability cooldowns by burning seconds off. Used by CubeAbility.
	/// </summary>
	public void AccelerateCooldowns( float seconds ) => _cooldowns.Accelerate( seconds );

	protected override void OnUpdate()
	{
		_cooldowns.Tick( Time.Delta );
	}
}
