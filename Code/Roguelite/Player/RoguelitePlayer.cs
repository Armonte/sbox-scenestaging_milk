/// <summary>
/// Root player entity. Implements IDamageable with inlined health/faction.
/// </summary>
[Title( "Roguelite Player" )]
[Icon( "person" )]
public sealed class RoguelitePlayer : Component, IDamageable
{
	[RequireComponent] public PlayerMovement Movement { get; set; }
	[RequireComponent] public PlayerCamera Camera { get; set; }
	[RequireComponent] public AbilityComponent Abilities { get; set; }

	[Property] public PlayerClass Class { get; set; } = PlayerClass.Warrior;

	// Inlined health
	[Property] public float HealthMax { get; set; } = 200f;
	[Sync] public float HealthCurrent { get; set; }
	[Sync] public bool IsDead { get; set; }

	// Inlined faction — always Player
	public Faction Faction => global::Faction.Player;

	[Sync] public bool IsAlive { get; set; } = true;

	public WeaponBase ActiveWeapon { get; private set; }

	protected override void OnStart()
	{
		var stats = ClassStats.Get( Class );
		Tags.Add( "player" );

		var cc = Components.Get<CharacterController>();
		if ( cc.IsValid() )
			cc.IgnoreLayers.Add( "enemy" );

		if ( !IsProxy )
		{
			HealthMax = stats.MaxHp;
			HealthCurrent = stats.MaxHp;
			IsDead = false;
			Movement.InitFromClass( stats );
		}

		ActiveWeapon = Components.Get<WeaponBase>( FindMode.EverythingInSelfAndDescendants );
		if ( ActiveWeapon is not null )
			ActiveWeapon.OnEquip( this );

		Log.Info( $"[RoguelitePlayer] Started as {Class}. HP: {HealthCurrent}/{HealthMax}. Weapon: {ActiveWeapon?.GetType().Name ?? "NONE"}" );

		if ( !IsProxy )
		{
			var hudObj = Scene.CreateObject();
			hudObj.Name = "RogueliteHUD";
			hudObj.SetParent( GameObject );
			hudObj.Components.Create<ScreenPanel>();
			hudObj.Components.Create<RogueliteHUD>();
		}
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		if ( !IsAlive ) return;

		HandleWeaponInput();
		HandleAbilityInput();
	}

	public void ApplyDamage( float amount, DamageType type, Component attacker )
	{
		if ( IsDead ) return;

		HealthCurrent = MathF.Max( 0, HealthCurrent - amount );

		if ( HealthCurrent <= 0 )
		{
			IsDead = true;
			IsAlive = false;
			ActiveWeapon?.OnOwnerDied();
		}
	}

	public void ApplyKnockback( Vector3 direction, float force )
	{
		// Player knockback via CharacterController
		var cc = Components.Get<CharacterController>();
		if ( cc.IsValid() )
			cc.Punch( direction.WithZ( 0 ).Normal * force );
	}

	public void Heal( float amount )
	{
		if ( IsDead ) return;
		HealthCurrent = MathF.Min( HealthMax, HealthCurrent + amount );
	}

	public void Revive( float hpPercent = 0.3f )
	{
		if ( !IsDead ) return;
		IsDead = false;
		IsAlive = true;
		HealthCurrent = HealthMax * hpPercent;
	}

	private void HandleAbilityInput()
	{
		if ( Input.Pressed( "Slot1" ) ) Abilities.TryActivate( 0, this, GetAimTarget() );
		if ( Input.Pressed( "Slot2" ) ) Abilities.TryActivate( 1, this, GetAimTarget() );
		if ( Input.Pressed( "Slot3" ) ) Abilities.TryActivate( 2, this, GetAimTarget() );
		if ( Input.Pressed( "Slot4" ) ) Abilities.TryActivate( 3, this, GetAimTarget() );
	}

	private GameObject GetAimTarget()
	{
		var eyePos = WorldPosition + Vector3.Up * Camera.EyeHeight;
		var forward = Camera.EyeAngles.ToRotation().Forward;

		var tr = Scene.Trace
			.Ray( eyePos, eyePos + forward * 1500f )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		return tr.Hit ? tr.GameObject : null;
	}

	private void HandleWeaponInput()
	{
		if ( ActiveWeapon is null ) return;

		if ( Input.Pressed( "attack1" ) )
			ActiveWeapon.PrimaryAttack();

		if ( Input.Pressed( "attack2" ) )
			ActiveWeapon.SecondaryAttack();
	}

	public void EquipWeapon( WeaponBase weapon )
	{
		ActiveWeapon?.OnUnequip( this );
		ActiveWeapon = weapon;
		weapon.OnEquip( this );
	}
}
