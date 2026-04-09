/// <summary>
/// Persistent area damage. Place in the world to deal periodic damage to anything inside.
/// Use for: fire on ground, poison cloud, electric field, healing aura (negative damage),
/// lava, spike traps, boss arena hazards.
/// </summary>
[Title( "Damage Zone" )]
[Icon( "dangerous" )]
public class DamageZone : Component
{
	public enum ZoneShape
	{
		Sphere,
		Box
	}

	[Property] public ZoneShape Shape { get; set; } = ZoneShape.Sphere;
	[Property] public float Radius { get; set; } = 200f;
	[Property] public Vector3 BoxSize { get; set; } = new( 200, 200, 100 );
	[Property] public float DamagePerTick { get; set; } = 10f;
	[Property] public DamageType Type { get; set; } = DamageType.Fire;
	[Property] public float TickInterval { get; set; } = 0.5f;
	[Property] public float Lifetime { get; set; } = 0f;
	[Property] public bool AffectsPlayers { get; set; } = true;
	[Property] public bool AffectsEnemies { get; set; } = false;
	[Property] public float KnockbackForce { get; set; } = 0f;

	/// <summary>
	/// Optional — who created this zone. Used for faction checks and damage attribution.
	/// </summary>
	public Component Creator { get; set; }

	private float _tickTimer;
	private TimeSince _spawnTime;

	protected override void OnStart()
	{
		_spawnTime = 0;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;

		// Lifetime expiry
		if ( Lifetime > 0 && _spawnTime > Lifetime )
		{
			GameObject.Destroy();
			return;
		}

		// Runtime visual — draw circle every frame
		DrawZoneVisual();

		_tickTimer += Time.Delta;
		if ( _tickTimer < TickInterval ) return;
		_tickTimer -= TickInterval;

		DealDamage();
	}

	private void DealDamage()
	{
		List<GameObject> targets;

		if ( Shape == ZoneShape.Sphere )
		{
			targets = HitDetection.Sphere( Scene, WorldPosition, Radius );
		}
		else
		{
			var bounds = new BBox( WorldPosition - BoxSize * 0.5f, WorldPosition + BoxSize * 0.5f );
			targets = HitDetection.Box( Scene, bounds );
		}

		foreach ( var target in targets )
		{
			var entityRoot = FindEntityRoot( target );
			var damageable = entityRoot.Components.Get<global::IDamageable>( FindMode.EverythingInSelfAndDescendants );

			if ( damageable is null ) continue;

			if ( damageable.Faction == global::Faction.Player && !AffectsPlayers ) continue;
			if ( damageable.Faction == global::Faction.Enemy && !AffectsEnemies ) continue;

			var attack = new AttackData( DamagePerTick, Type, canCrit: false,
				canKnockback: KnockbackForce > 0, knockbackForce: KnockbackForce );

			var direction = (entityRoot.WorldPosition - WorldPosition);
			if ( direction.Length < 0.1f ) direction = Vector3.Up;

			var ctx = new HitContext( WorldPosition, direction.Normal, entityRoot.WorldPosition,
				Vector3.Up, direction.Length, false, false );

			DamageResolver.Resolve( attack, Creator, entityRoot, ctx );
		}
	}

	private static GameObject FindEntityRoot( GameObject obj )
	{
		var current = obj;
		while ( current is not null )
		{
			if ( current.Components.Get<global::IDamageable>( FindMode.EverythingInSelfAndDescendants ) is not null )
				return current;
			current = current.Parent;
		}
		return obj;
	}

	/// <summary>
	/// Spawn a damage zone at a position. Returns the DamageZone component.
	/// </summary>
	public static DamageZone Spawn( Scene scene, Vector3 position, float radius,
		float damagePerTick, DamageType type, float tickInterval, float lifetime,
		Component creator = null, bool affectsPlayers = true, bool affectsEnemies = false )
	{
		var obj = scene.CreateObject();
		obj.Name = $"DamageZone_{type}";
		obj.WorldPosition = position;
		obj.Tags.Add( "trigger" );

		var zone = obj.Components.Create<DamageZone>();
		zone.Radius = radius;
		zone.DamagePerTick = damagePerTick;
		zone.Type = type;
		zone.TickInterval = tickInterval;
		zone.Lifetime = lifetime;
		zone.Creator = creator;
		zone.AffectsPlayers = affectsPlayers;
		zone.AffectsEnemies = affectsEnemies;

		obj.NetworkSpawn();
		return zone;
	}

	private Color GetZoneColor()
	{
		return Type switch
		{
			DamageType.Fire => Color.Red,
			DamageType.Frost => Color.Cyan,
			DamageType.Poison => Color.Green,
			DamageType.Lightning => Color.Yellow,
			_ => Color.White
		};
	}

	private GameObject _zoneVisual;

	private void DrawZoneVisual()
	{
		if ( _zoneVisual is not null && _zoneVisual.IsValid() ) return;

		// Create a flat disc using a scaled sphere squashed on Z
		var color = GetZoneColor().WithAlpha( 0.25f );

		_zoneVisual = Scene.CreateObject();
		_zoneVisual.Name = "zone_visual";
		_zoneVisual.SetParent( GameObject );
		_zoneVisual.LocalPosition = Vector3.Up * 2f;

		var renderer = _zoneVisual.Components.Create<ModelRenderer>();
		renderer.Model = Model.Load( "models/dev/sphere.vmdl" );
		renderer.Tint = color;

		// Sphere is ~32 units diameter. Scale XY to radius, squash Z flat
		var scale = Radius / 16f;
		_zoneVisual.LocalScale = new Vector3( scale, scale, 0.02f );
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = GetZoneColor().WithAlpha( 0.2f );

		if ( Shape == ZoneShape.Sphere )
			Gizmo.Draw.LineSphere( Vector3.Zero, Radius );
		else
			Gizmo.Draw.LineBBox( new BBox( -BoxSize * 0.5f, BoxSize * 0.5f ) );
	}
}
