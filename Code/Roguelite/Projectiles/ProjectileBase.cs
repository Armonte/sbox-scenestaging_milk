/// <summary>
/// Base projectile. Moves forward, traces for hits, calls DamageResolver.
/// Configure via properties or subclass for special behavior (piercing, exploding, homing).
/// </summary>
[Title( "Projectile" )]
[Icon( "arrow_forward" )]
public class ProjectileBase : Component
{
	[Property] public float Speed { get; set; } = 2000f;
	[Property] public float Lifetime { get; set; } = 5f;
	[Property] public float TraceRadius { get; set; } = 4f;
	[Property] public float Gravity { get; set; } = 0f;
	[Property] public bool Pierce { get; set; } = false;
	[Property] public int MaxPierceTargets { get; set; } = 3;
	[Property] public float ExplosionRadius { get; set; } = 0f;
	[Property] public float ExplosionFalloff { get; set; } = 0.5f;

	// Homing
	[Property] public float HomingStrength { get; set; } = 0f;
	public GameObject HomingTarget { get; set; }

	// Trail
	[Property] public bool UseTrail { get; set; } = false;
	[Property] public Color TrailColor { get; set; } = Color.White;

	public AttackData Attack { get; set; }
	public Component Attacker { get; set; }

	private Vector3 _velocity;
	private TimeSince _spawnTime;
	private readonly HashSet<GameObject> _alreadyHit = new();

	protected override void OnStart()
	{
		_velocity = WorldRotation.Forward * Speed;
		_spawnTime = 0;

		if ( UseTrail )
		{
			var trail = Components.Create<TrailRenderer>();
			trail.Color = new Gradient( new Gradient.ColorFrame( 0, TrailColor ), new Gradient.ColorFrame( 1, TrailColor.WithAlpha( 0 ) ) );
			trail.LifeTime = 0.3f;
		}
	}

	protected override void OnUpdate()
	{
		if ( !Attacker.IsValid() )
		{
			GameObject.Destroy();
			return;
		}

		if ( _spawnTime > Lifetime )
		{
			OnLifetimeExpired();
			return;
		}

		if ( Gravity > 0 )
			_velocity -= Vector3.Up * Gravity * Time.Delta;

		// Homing — steer toward target (ramps up over time for a curving arc)
		if ( HomingStrength > 0 && HomingTarget.IsValid() )
		{
			// Ramp: homing gets stronger the longer the projectile has been alive
			var ramp = MathF.Min( 1f, _spawnTime / 0.5f ); // reaches full strength at 0.5s
			var strength = HomingStrength * ramp;

			var toTarget = (HomingTarget.WorldPosition - WorldPosition).Normal;
			_velocity = Vector3.Lerp( _velocity.Normal, toTarget, strength * Time.Delta ).Normal * Speed;
		}

		var from = WorldPosition;
		var to = from + _velocity * Time.Delta;

		var tr = HitDetection.Ray( Scene, from, to, TraceRadius, Attacker?.GameObject );

		if ( tr.Hit && tr.GameObject is not null && !_alreadyHit.Contains( tr.GameObject ) )
		{
			_alreadyHit.Add( tr.GameObject );
			OnHit( tr );

			if ( !Pierce || _alreadyHit.Count >= MaxPierceTargets )
			{
				GameObject.Destroy();
				return;
			}
		}

		WorldPosition = to;
		if ( _velocity.Length > 0.1f )
			WorldRotation = Rotation.LookAt( _velocity.Normal );
	}

	protected virtual void OnHit( SceneTraceResult tr )
	{
		if ( !Networking.IsHost ) return;

		if ( ExplosionRadius > 0 )
		{
			Explosion.At( Scene, tr.HitPosition, ExplosionRadius, Attack, Attacker, ExplosionFalloff );
		}
		else
		{
			var ctx = HitContext.FromTrace( tr, Attacker );
			DamageResolver.Resolve( Attack, Attacker, tr.GameObject, ctx );
		}
	}

	protected virtual void OnLifetimeExpired()
	{
		// If it has an explosion radius and didn't hit anything, detonate where it is (timed grenade)
		if ( ExplosionRadius > 0 && _alreadyHit.Count == 0 )
			Explosion.At( Scene, WorldPosition, ExplosionRadius, Attack, Attacker, ExplosionFalloff );

		GameObject.Destroy();
	}

	/// <summary>
	/// Spawn a simple projectile.
	/// </summary>
	public static ProjectileBase Spawn( Scene scene, Vector3 position, Rotation rotation,
		AttackData attack, Component attacker, float speed = 2000f, float gravity = 0f )
	{
		var obj = scene.CreateObject();
		obj.Name = "Projectile";
		obj.WorldPosition = position;
		obj.WorldRotation = rotation;
		obj.Tags.Add( "projectile" );

		var proj = obj.Components.Create<ProjectileBase>();
		proj.Attack = attack;
		proj.Attacker = attacker;
		proj.Speed = speed;
		proj.Gravity = gravity;

		var renderer = obj.Components.Create<ModelRenderer>();
		renderer.Model = Model.Load( "models/dev/sphere.vmdl" );
		obj.LocalScale = Vector3.One * 0.1f;

		obj.NetworkSpawn();

		return proj;
	}

	/// <summary>
	/// Spawn a projectile that explodes on impact or at end of life.
	/// </summary>
	public static ProjectileBase SpawnExplosive( Scene scene, Vector3 position, Rotation rotation,
		AttackData attack, Component attacker, float speed, float gravity,
		float explosionRadius, float explosionFalloff = 0.5f )
	{
		var proj = Spawn( scene, position, rotation, attack, attacker, speed, gravity );
		proj.ExplosionRadius = explosionRadius;
		proj.ExplosionFalloff = explosionFalloff;
		return proj;
	}

	/// <summary>
	/// Spawn a piercing projectile that passes through multiple targets.
	/// </summary>
	public static ProjectileBase SpawnPiercing( Scene scene, Vector3 position, Rotation rotation,
		AttackData attack, Component attacker, float speed, int maxTargets = 3 )
	{
		var proj = Spawn( scene, position, rotation, attack, attacker, speed, 0f );
		proj.Pierce = true;
		proj.MaxPierceTargets = maxTargets;
		return proj;
	}

	/// <summary>
	/// Spawn a homing projectile that steers toward a target.
	/// </summary>
	public static ProjectileBase SpawnHoming( Scene scene, Vector3 position, Rotation rotation,
		AttackData attack, Component attacker, float speed, GameObject target, float homingStrength = 5f )
	{
		var proj = Spawn( scene, position, rotation, attack, attacker, speed, 0f );
		proj.HomingTarget = target;
		proj.HomingStrength = homingStrength;
		return proj;
	}
}
