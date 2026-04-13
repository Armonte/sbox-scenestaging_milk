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
	[Property] public float TrailWidth { get; set; } = 3f;
	[Property] public float TrailLifetime { get; set; } = 0.3f;

	/// <summary>
	/// Visual-only rotation offset for the projectile's renderer. Use this when the
	/// imported mesh doesn't point along +X — e.g. the dungeon arrow needs yaw 180.
	/// Doesn't affect direction of travel; velocity is tracked independently.
	/// </summary>
	[Property] public Angles ModelAngleOffset { get; set; } = Angles.Zero;

	/// <summary>
	/// Traces pass <c>.UseHitboxes( true )</c> so per-hitbox weak-point damage works.
	/// Small perf cost — only enable for projectiles that care about weak-points.
	/// </summary>
	[Property] public bool UseHitboxTraces { get; set; } = false;

	/// <summary>
	/// When true, every hit is logged: what GameObject, what bone, whether a hitbox
	/// was involved. Use this to debug why a weakpoint multiplier isn't firing.
	/// </summary>
	[Property] public bool DebugHits { get; set; } = false;

	// --- Sticking ---
	/// <summary>
	/// On non-piercing hit, lodge the projectile into what it hit instead of destroying.
	/// It reparents to the hit object so it follows moving enemies.
	/// </summary>
	[Property, Group( "Stick" )] public bool StickOnHit { get; set; } = false;

	/// <summary>
	/// How long (seconds) the stuck projectile lingers before auto-destroying.
	/// </summary>
	[Property, Group( "Stick" )] public float StickLifetime { get; set; } = 5f;

	/// <summary>
	/// How far past the impact point to push the arrow so the head visually embeds
	/// in the surface instead of sitting on top of it.
	/// </summary>
	[Property, Group( "Stick" )] public float StickEmbedDepth { get; set; } = 6f;

	public AttackData Attack { get; set; }
	public Component Attacker { get; set; }

	private Vector3 _velocity;
	private TimeSince _spawnTime;
	private readonly HashSet<GameObject> _alreadyHit = new();
	private bool _stuck;
	private TimeSince _stuckTime;

	protected override void OnStart()
	{
		// Capture forward from the un-offset rotation so direction of travel
		// stays aligned with where we were aimed, independent of any visual offset.
		_velocity = WorldRotation.Forward * Speed;
		_spawnTime = 0;

		// Apply the visual rotation offset now so the first rendered frame is correct.
		if ( ModelAngleOffset != Angles.Zero )
			WorldRotation = WorldRotation * ModelAngleOffset.ToRotation();

		if ( UseTrail )
		{
			var trail = Components.Create<TrailRenderer>();
			trail.Color = new Gradient(
				new Gradient.ColorFrame( 0, TrailColor ),
				new Gradient.ColorFrame( 1, TrailColor.WithAlpha( 0 ) )
			);
			trail.Width = new Curve( new Curve.Frame( 0, TrailWidth ), new Curve.Frame( 1, TrailWidth ) );
			trail.LifeTime = TrailLifetime;
			trail.Opaque = false;
		}
	}

	protected override void OnUpdate()
	{
		// Once lodged, the projectile is frozen mid-flight and just counts down
		// until it despawns. Parenting keeps it visually attached to whatever it hit.
		if ( _stuck )
		{
			if ( _stuckTime > StickLifetime )
				GameObject.Destroy();
			return;
		}

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

		var tr = HitDetection.Ray( Scene, from, to, TraceRadius, Attacker?.GameObject, UseHitboxTraces );

		if ( tr.Hit && tr.GameObject is not null && !_alreadyHit.Contains( tr.GameObject ) )
		{
			_alreadyHit.Add( tr.GameObject );
			OnHit( tr );

			if ( !Pierce || _alreadyHit.Count >= MaxPierceTargets )
			{
				if ( StickOnHit )
				{
					StickInto( tr );
					return;
				}

				GameObject.Destroy();
				return;
			}
		}

		WorldPosition = to;
		if ( _velocity.Length > 0.1f )
			WorldRotation = Rotation.LookAt( _velocity.Normal ) * ModelAngleOffset.ToRotation();
	}

	/// <summary>
	/// Lodge the projectile into whatever it hit: stop moving, embed slightly into
	/// the surface along the travel direction, and reparent to the hit GameObject so
	/// it follows moving enemies. Each client does this locally off its own hit trace.
	/// </summary>
	private void StickInto( SceneTraceResult tr )
	{
		_stuck = true;
		_stuckTime = 0;

		// Embed along the incoming direction so the arrow's head sinks into the surface
		// and the shaft sticks out the way it flew in.
		var dir = _velocity.LengthSquared > 0.001f ? _velocity.Normal : tr.Direction.Normal;
		WorldPosition = tr.HitPosition + dir * StickEmbedDepth;

		// Face the travel direction one final time (with the visual offset) and freeze.
		WorldRotation = Rotation.LookAt( dir ) * ModelAngleOffset.ToRotation();
		_velocity = Vector3.Zero;

		// Stop the trail from appending new points at the stuck position. Existing
		// points still fade out over TrailLifetime, so we get a clean tail-off
		// instead of a stationary clump of points at the arrow's resting spot.
		var trail = Components.Get<TrailRenderer>();
		if ( trail.IsValid() )
			trail.Emitting = false;

		// Reparent so we ride along with moving enemies / physics props. If the parent
		// object is later destroyed the child (us) goes with it, which is what we want.
		if ( tr.GameObject.IsValid() )
		{
			GameObject.SetParent( tr.GameObject, true );
		}
	}

	protected virtual void OnHit( SceneTraceResult tr )
	{
		if ( !Networking.IsHost ) return;
		if ( !Attacker.IsValid() ) return;

		if ( DebugHits )
		{
			// Build the hitbox info without touching tr.Hitbox.Tags directly —
			// we reuse FromTrace's try/catch path via a temp context so we never
			// NRE here. This is the single source of truth for "what did I hit".
			var probe = HitContext.FromTrace( tr, Attacker );
			var hitboxPart = probe.HitHitbox
				? $"HITBOX (head={probe.HasHitboxTag( "head" )}, weakpoint={probe.HasHitboxTag( "weakpoint" )})"
				: "collider";
			Log.Info( $"[Projectile] hit {tr.GameObject?.Name ?? "?"} bone={tr.Bone} via {hitboxPart}" );
		}

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
	/// Pass a <paramref name="model"/> to render a specific mesh — otherwise a
	/// small dev-sphere placeholder is used. Trail properties are set before
	/// <c>NetworkSpawn</c> so they're picked up by <c>OnStart</c>.
	/// </summary>
	public static ProjectileBase Spawn( Scene scene, Vector3 position, Rotation rotation,
		AttackData attack, Component attacker, float speed = 2000f, float gravity = 0f,
		Model model = null, bool useTrail = false, Color? trailColor = null,
		Angles modelAngleOffset = default, float modelScale = 1f,
		bool stickOnHit = false, bool useHitboxes = false, bool debugHits = false )
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
		proj.UseTrail = useTrail;
		proj.ModelAngleOffset = modelAngleOffset;
		proj.StickOnHit = stickOnHit;
		proj.UseHitboxTraces = useHitboxes;
		proj.DebugHits = debugHits;
		if ( trailColor.HasValue )
			proj.TrailColor = trailColor.Value;

		var renderer = obj.Components.Create<ModelRenderer>();
		if ( model is not null )
		{
			renderer.Model = model;
			obj.LocalScale = Vector3.One * modelScale;
		}
		else
		{
			// Dev sphere model is much larger than we want for a projectile — shrink
			// to a reasonable size, then apply the caller's extra scale on top.
			renderer.Model = Model.Load( "models/dev/sphere.vmdl" );
			obj.LocalScale = Vector3.One * 0.1f * modelScale;
		}

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
