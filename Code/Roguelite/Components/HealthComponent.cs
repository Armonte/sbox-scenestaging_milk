/// <summary>
/// Composable health component. Attach to any GameObject that can take damage (players, enemies, destructibles).
/// Only DamageResolver should call ApplyDamage — everything else uses events.
/// </summary>
[Title( "Health" )]
[Icon( "favorite" )]
public sealed class RogueliteHealthComponent : Component
{
	[Property] public float MaxHealth { get; set; } = 100f;

	[Sync] public float Current { get; set; }
	[Sync] public bool IsDead { get; set; }

	public event Action OnDeath;
	public event Action OnRevive;
	public event Action<float, DamageType> OnDamageTaken;
	public event Action<float, DamageType, Component> OnDamageTakenFull;
	public event Action<float> OnHealed;

	protected override void OnStart()
	{
		Current = MaxHealth;
		IsDead = false;
	}

	public void Init( float maxHp )
	{
		MaxHealth = maxHp;
		Current = maxHp;
		IsDead = false;
	}

	/// <summary>
	/// Only called from DamageResolver. Do not call directly.
	/// </summary>
	internal void ApplyDamage( float amount, DamageType type, Component attacker = null )
	{
		if ( IsDead ) return;

		Current = MathF.Max( 0, Current - amount );
		OnDamageTaken?.Invoke( amount, type );
		OnDamageTakenFull?.Invoke( amount, type, attacker );

		if ( Current <= 0 )
		{
			IsDead = true;
			OnDeath?.Invoke();
		}
	}

	public void Heal( float amount )
	{
		if ( IsDead ) return;

		var prev = Current;
		Current = MathF.Min( MaxHealth, Current + amount );
		var healed = Current - prev;

		if ( healed > 0 )
			OnHealed?.Invoke( healed );
	}

	public void Revive( float hpPercent = 0.3f )
	{
		if ( !IsDead ) return;

		IsDead = false;
		Current = MaxHealth * hpPercent;
		OnRevive?.Invoke();
	}

	public void ForceKill() => ApplyDamage( Current, DamageType.True );
}
