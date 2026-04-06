/// <summary>
/// A pedestal that gives a weapon to the player on interact (E key).
/// Place in the world, set WeaponType, and the player walks up and presses E to pick up.
/// Displays the weapon name floating above it.
/// </summary>
[Title( "Weapon Pedestal" )]
[Icon( "pedestal" )]
public sealed class WeaponPedestal : Component, Component.ITriggerListener
{
	public enum PedestalWeapon
	{
		Sword,
		Bow
	}

	[Property] public PedestalWeapon WeaponType { get; set; } = PedestalWeapon.Sword;
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
		_label.Text = $"[E] {WeaponType}";
		_label.FontSize = 18f;
		_label.Color = Color.White;
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
		// Remove existing weapon if any
		var existing = player.Components.Get<WeaponBase>( FindMode.EverythingInSelfAndDescendants );
		if ( existing is not null )
			existing.Destroy();

		// Create the new weapon component on the player
		WeaponBase weapon = WeaponType switch
		{
			PedestalWeapon.Sword => player.Components.Create<SwordWeapon>(),
			PedestalWeapon.Bow => player.Components.Create<BowWeapon>(),
			_ => null
		};

		if ( weapon is not null )
		{
			player.EquipWeapon( weapon );
			Log.Info( $"[Pedestal] Gave {WeaponType} to {player.GameObject.Name}" );
		}
	}
}
