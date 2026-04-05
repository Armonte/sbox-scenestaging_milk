/// <summary>
/// Root player entity for the roguelite. Composes Health, Faction, Movement, Camera,
/// and routes input to the active weapon. Attach to the player prefab root.
/// </summary>
[Title( "Roguelite Player" )]
[Icon( "person" )]
public sealed class RoguelitePlayer : Component
{
	[RequireComponent] public RogueliteHealthComponent Health { get; set; }
	[RequireComponent] public FactionComponent Faction { get; set; }
	[RequireComponent] public PlayerMovement Movement { get; set; }
	[RequireComponent] public PlayerCamera Camera { get; set; }

	[Property] public PlayerClass Class { get; set; } = PlayerClass.Warrior;

	[Sync] public bool IsAlive { get; set; } = true;

	public WeaponBase ActiveWeapon { get; private set; }

	protected override void OnStart()
	{
		// Init from class stats
		var stats = ClassStats.Get( Class );
		Health.Init( stats.MaxHp );
		Movement.InitFromClass( stats );
		Faction.Faction = global::Faction.Player;

		// Find weapon on this GameObject or children
		ActiveWeapon = Components.Get<WeaponBase>( FindMode.EverythingInSelfAndDescendants );
		if ( ActiveWeapon is not null )
			ActiveWeapon.OnEquip( this );

		Log.Info( $"[RoguelitePlayer] Started as {Class}. HP: {Health.Current}/{Health.MaxHealth}. Weapon: {ActiveWeapon?.GetType().Name ?? "NONE"}" );

		// Subscribe to death
		Health.OnDeath += HandleDeath;
		Health.OnRevive += HandleRevive;

		// Create HUD for local player
		if ( !IsProxy )
		{
			var hudObj = Scene.CreateObject();
			hudObj.Name = "RogueliteHUD";
			hudObj.SetParent( GameObject );
			var screenPanel = hudObj.Components.Create<ScreenPanel>();
			hudObj.Components.Create<RogueliteHUD>();
		}
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		if ( !IsAlive ) return;

		HandleWeaponInput();
	}

	private void HandleWeaponInput()
	{
		if ( ActiveWeapon is null ) return;

		if ( Input.Pressed( "attack1" ) )
			ActiveWeapon.PrimaryAttack();

		if ( Input.Pressed( "attack2" ) )
			ActiveWeapon.SecondaryAttack();
	}

	private void HandleDeath()
	{
		IsAlive = false;
		ActiveWeapon?.OnOwnerDied();
	}

	private void HandleRevive()
	{
		IsAlive = true;
	}

	/// <summary>
	/// Equip a weapon, replacing the current one.
	/// </summary>
	public void EquipWeapon( WeaponBase weapon )
	{
		ActiveWeapon?.OnUnequip( this );
		ActiveWeapon = weapon;
		weapon.OnEquip( this );
	}
}
