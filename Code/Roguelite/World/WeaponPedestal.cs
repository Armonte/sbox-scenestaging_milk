/// <summary>
/// A pedestal that gives a weapon to the player on interact (E key).
/// Drag any weapon prefab into <see cref="WeaponPrefab"/>. On pickup the prefab
/// is cloned, reparented under the player, and its <see cref="WeaponBase"/>
/// component is handed to <see cref="RoguelitePlayer.EquipWeapon"/>.
///
/// Prefabs let designers tune every weapon stat in the inspector — previously
/// the pedestal did Components.Create&lt;T&gt;() which meant all [Property]
/// values fell back to C# defaults.
/// </summary>
[Title( "Weapon Pedestal" )]
[Icon( "pedestal" )]
public sealed class WeaponPedestal : Component, Component.ITriggerListener
{
	/// <summary>
	/// The weapon prefab this pedestal hands out. Must contain a GameObject with
	/// a <see cref="WeaponBase"/> component somewhere in its hierarchy.
	/// </summary>
	[Property] public GameObject WeaponPrefab { get; set; }

	/// <summary>
	/// Optional label override. If left blank, the label reads the weapon prefab's
	/// GameObject name so renaming a prefab automatically updates the label.
	/// </summary>
	[Property] public string LabelOverride { get; set; }

	[Property] public float InteractRange { get; set; } = 100f;

	private RoguelitePlayer _nearbyPlayer;
	private TextRenderer _label;

	protected override void OnStart()
	{
		// Create floating label
		var labelObj = Scene.CreateObject();
		labelObj.Name = "PedestalLabel";
		labelObj.SetParent( GameObject );
		labelObj.LocalPosition = Vector3.Up * 80f;

		_label = labelObj.Components.Create<TextRenderer>();
		_label.Text = $"[E] {GetWeaponLabel()}";
		_label.FontSize = 18f;
		_label.Color = Color.White;
	}

	private string GetWeaponLabel()
	{
		if ( !string.IsNullOrEmpty( LabelOverride ) )
			return LabelOverride;
		if ( WeaponPrefab.IsValid() )
			return WeaponPrefab.Name;
		return "Empty Pedestal";
	}

	protected override void OnUpdate()
	{
		// Billboard the label toward the camera
		if ( _label is not null && Scene.Camera is not null )
		{
			var dir = (Scene.Camera.WorldPosition - _label.WorldPosition).Normal;
			_label.WorldRotation = Rotation.LookAt( -dir, Vector3.Up );
		}

		// Check for interact
		if ( _nearbyPlayer is not null && Input.Pressed( "use" ) )
		{
			GiveWeapon( _nearbyPlayer );
		}

		// Find nearby player manually (fallback if trigger doesn't work)
		_nearbyPlayer = null;
		var players = Scene.GetAllComponents<RoguelitePlayer>();
		foreach ( var p in players )
		{
			if ( p.IsProxy ) continue;
			if ( p.WorldPosition.Distance( WorldPosition ) <= InteractRange )
			{
				_nearbyPlayer = p;
				break;
			}
		}

		// Update label visibility
		if ( _label is not null )
		{
			_label.Color = _nearbyPlayer is not null ? Color.Yellow : Color.White;
		}
	}

	private void GiveWeapon( RoguelitePlayer player )
	{
		if ( !WeaponPrefab.IsValid() )
		{
			Log.Warning( $"[Pedestal {GameObject.Name}] WeaponPrefab is not assigned — nothing to give." );
			return;
		}

		// Unequip + destroy the existing weapon (if any). We deliberately call
		// OnUnequip first so the old weapon can clean up its viewmodel visibility
		// state — otherwise the bow viewmodel would linger after swapping to a sword.
		var existing = player.Components.Get<WeaponBase>( FindMode.EverythingInSelfAndDescendants );
		if ( existing is not null )
		{
			existing.OnUnequip( player );
			existing.GameObject.Destroy();
		}

		// Clone the prefab and reparent it under the player so the weapon component
		// lives in the player's hierarchy — that's where RoguelitePlayer.OnStart
		// looks for it via Components.Get<WeaponBase>(..InSelfAndDescendants).
		var weaponObj = WeaponPrefab.Clone();
		weaponObj.SetParent( player.GameObject );
		weaponObj.LocalPosition = Vector3.Zero;
		weaponObj.LocalRotation = Rotation.Identity;

		var weapon = weaponObj.Components.Get<WeaponBase>( FindMode.EverythingInSelfAndDescendants );
		if ( weapon is null )
		{
			Log.Warning( $"[Pedestal {GameObject.Name}] Cloned prefab has no WeaponBase component — destroying." );
			weaponObj.Destroy();
			return;
		}

		player.EquipWeapon( weapon );
		Log.Info( $"[Pedestal] Gave {WeaponPrefab.Name} to {player.GameObject.Name}" );
	}
}
