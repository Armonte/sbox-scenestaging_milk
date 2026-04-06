/// <summary>
/// Static helpers for spawning projectiles in patterns.
/// Use for: shotgun spread, burst fire, ring blasts, spirals, volleys.
/// All methods return the spawned projectiles so you can configure them further.
/// </summary>
public static class AttackPattern
{
	/// <summary>
	/// Fan of projectiles in a horizontal spread. Like a shotgun but with travel time.
	/// </summary>
	public static List<ProjectileBase> Fan( Scene scene, Vector3 origin, Vector3 direction,
		AttackData attack, Component attacker, int count, float spreadAngle,
		float speed = 2000f, float gravity = 0f )
	{
		var results = new List<ProjectileBase>();
		var baseRot = Rotation.LookAt( direction );

		var startAngle = -spreadAngle * 0.5f;
		var step = count > 1 ? spreadAngle / (count - 1) : 0;

		for ( int i = 0; i < count; i++ )
		{
			var angle = startAngle + step * i;
			var rot = baseRot * Rotation.FromYaw( angle );
			var proj = ProjectileBase.Spawn( scene, origin, rot, attack, attacker, speed, gravity );
			results.Add( proj );
		}

		return results;
	}

	/// <summary>
	/// Ring of projectiles radiating outward from a point. Like a nova/explosion pattern.
	/// </summary>
	public static List<ProjectileBase> Ring( Scene scene, Vector3 origin,
		AttackData attack, Component attacker, int count,
		float speed = 1000f, float gravity = 0f )
	{
		var results = new List<ProjectileBase>();
		var angleStep = 360f / count;

		for ( int i = 0; i < count; i++ )
		{
			var rot = Rotation.FromYaw( angleStep * i );
			var proj = ProjectileBase.Spawn( scene, origin, rot, attack, attacker, speed, gravity );
			results.Add( proj );
		}

		return results;
	}

	/// <summary>
	/// Burst fire — spawns projectiles one after another with a delay.
	/// Returns immediately; projectiles spawn over time.
	/// </summary>
	public static async Task<List<ProjectileBase>> Burst( Scene scene, Vector3 origin, Vector3 direction,
		AttackData attack, Component attacker, int count, float delayBetween,
		float speed = 2000f, float gravity = 0f, float spreadPerShot = 0f )
	{
		var results = new List<ProjectileBase>();
		var baseRot = Rotation.LookAt( direction );

		for ( int i = 0; i < count; i++ )
		{
			var rot = baseRot;
			if ( spreadPerShot > 0 )
			{
				var offsetX = (Random.Shared.NextSingle() - 0.5f) * 2f * spreadPerShot;
				var offsetY = (Random.Shared.NextSingle() - 0.5f) * 2f * spreadPerShot;
				rot = baseRot * Rotation.FromPitch( offsetX ) * Rotation.FromYaw( offsetY );
			}

			var proj = ProjectileBase.Spawn( scene, origin, rot, attack, attacker, speed, gravity );
			results.Add( proj );

			if ( i < count - 1 )
				await GameTask.DelaySeconds( delayBetween );
		}

		return results;
	}

	/// <summary>
	/// Spiral pattern — projectiles spawn in a rotating pattern over time.
	/// Good for boss attacks.
	/// </summary>
	public static async Task<List<ProjectileBase>> Spiral( Scene scene, Vector3 origin,
		AttackData attack, Component attacker, int totalShots, float duration,
		float rotationsPerSecond = 1f, float speed = 800f )
	{
		var results = new List<ProjectileBase>();
		var delay = duration / totalShots;

		for ( int i = 0; i < totalShots; i++ )
		{
			var t = (float)i / totalShots * duration;
			var angle = t * rotationsPerSecond * 360f;
			var rot = Rotation.FromYaw( angle );

			var proj = ProjectileBase.Spawn( scene, origin, rot, attack, attacker, speed, 0f );
			results.Add( proj );

			if ( i < totalShots - 1 )
				await GameTask.DelaySeconds( delay );
		}

		return results;
	}

	/// <summary>
	/// Volley — multiple projectiles aimed at a target with slight spread and arc.
	/// Like an artillery barrage.
	/// </summary>
	public static List<ProjectileBase> Volley( Scene scene, Vector3 origin, Vector3 targetPos,
		AttackData attack, Component attacker, int count,
		float speed = 1500f, float gravity = 300f, float spread = 100f )
	{
		var results = new List<ProjectileBase>();

		for ( int i = 0; i < count; i++ )
		{
			var offset = new Vector3(
				(Random.Shared.NextSingle() - 0.5f) * 2f * spread,
				(Random.Shared.NextSingle() - 0.5f) * 2f * spread,
				0 );
			var target = targetPos + offset;
			var dir = (target - origin).Normal;
			var rot = Rotation.LookAt( dir );

			var proj = ProjectileBase.Spawn( scene, origin, rot, attack, attacker, speed, gravity );
			results.Add( proj );
		}

		return results;
	}

	/// <summary>
	/// Homing volley — multiple homing projectiles that each track the target.
	/// </summary>
	public static List<ProjectileBase> HomingVolley( Scene scene, Vector3 origin, Vector3 direction,
		AttackData attack, Component attacker, GameObject target, int count,
		float speed = 800f, float homingStrength = 5f, float spreadAngle = 30f )
	{
		var results = new List<ProjectileBase>();
		var baseRot = Rotation.LookAt( direction );

		for ( int i = 0; i < count; i++ )
		{
			// Spread initial directions so they arc inward
			var offsetYaw = (Random.Shared.NextSingle() - 0.5f) * 2f * spreadAngle;
			var offsetPitch = (Random.Shared.NextSingle() - 0.5f) * 2f * spreadAngle;
			var rot = baseRot * Rotation.FromYaw( offsetYaw ) * Rotation.FromPitch( offsetPitch );

			var proj = ProjectileBase.SpawnHoming( scene, origin, rot, attack, attacker, speed, target, homingStrength );
			results.Add( proj );
		}

		return results;
	}
}
