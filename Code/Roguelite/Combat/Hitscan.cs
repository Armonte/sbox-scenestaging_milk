/// <summary>
/// Instant-hit traces. No travel time — damage is applied immediately.
/// Use for: bullets, sniper shots, shotgun pellets, melee swings.
/// Supports penetration (hitting multiple targets along the ray).
/// </summary>
public static class Hitscan
{
	public struct HitscanResult
	{
		public List<DamageResult> Hits;
		public List<SceneTraceResult> Traces;
		public Vector3 EndPoint;
	}

	/// <summary>
	/// Single instant ray. Returns the first thing hit.
	/// </summary>
	public static HitscanResult Fire( Scene scene, Vector3 from, Vector3 to,
		AttackData attack, Component attacker, float traceRadius = 2f )
	{
		var result = new HitscanResult
		{
			Hits = new List<DamageResult>(),
			Traces = new List<SceneTraceResult>(),
			EndPoint = to
		};

		var tr = HitDetection.Ray( scene, from, to, traceRadius, attacker?.GameObject );

		if ( tr.Hit && tr.GameObject is not null )
		{
			result.Traces.Add( tr );
			result.EndPoint = tr.HitPosition;

			var ctx = HitContext.FromTrace( tr, attacker );
			var dmg = DamageResolver.Resolve( attack, attacker, tr.GameObject, ctx );
			result.Hits.Add( dmg );
		}

		// Visual trace — offset origin so it's visible in first person
		var visualRot = Rotation.LookAt( (to - from).Normal );
		var visualOrigin = from + visualRot.Right * 15f + visualRot.Down * 5f + visualRot.Forward * 20f;
		CombatVfx.Line( scene, visualOrigin, result.EndPoint, Color.Yellow, 0.15f, 1.5f );
		if ( tr.Hit )
			CombatVfx.Impact( scene, result.EndPoint, Color.Red );

		return result;
	}

	/// <summary>
	/// Penetrating ray — passes through up to maxTargets.
	/// Each penetration reduces damage by falloffPerHit multiplier.
	/// </summary>
	public static HitscanResult FirePenetrating( Scene scene, Vector3 from, Vector3 to,
		AttackData attack, Component attacker, int maxTargets = 3,
		float falloffPerHit = 0.7f, float traceRadius = 2f )
	{
		var result = new HitscanResult
		{
			Hits = new List<DamageResult>(),
			Traces = new List<SceneTraceResult>(),
			EndPoint = to
		};

		var currentFrom = from;
		var direction = (to - from).Normal;
		var totalDist = (to - from).Length;
		var hitObjects = new HashSet<GameObject>();
		var currentDmg = attack.BaseDamage;

		for ( int i = 0; i < maxTargets; i++ )
		{
			var tr = HitDetection.Ray( scene, currentFrom, to, traceRadius, attacker?.GameObject );

			if ( !tr.Hit || tr.GameObject is null ) break;
			if ( hitObjects.Contains( tr.GameObject ) ) break;

			hitObjects.Add( tr.GameObject );
			result.Traces.Add( tr );
			result.EndPoint = tr.HitPosition;

			var scaledAttack = new AttackData( currentDmg, attack.Type, attack.CanCrit,
				attack.CanKnockback, attack.KnockbackForce );

			var ctx = HitContext.FromTrace( tr, attacker );
			var dmg = DamageResolver.Resolve( scaledAttack, attacker, tr.GameObject, ctx );
			result.Hits.Add( dmg );

			// Continue past this hit
			currentFrom = tr.HitPosition + direction * 5f;
			currentDmg *= falloffPerHit;
		}

		return result;
	}

	/// <summary>
	/// Shotgun spread — fires multiple rays in a cone from the origin.
	/// </summary>
	public static HitscanResult FireSpread( Scene scene, Vector3 from, Vector3 direction,
		float range, AttackData attack, Component attacker,
		int pelletCount = 8, float spreadAngle = 10f, float traceRadius = 2f )
	{
		var result = new HitscanResult
		{
			Hits = new List<DamageResult>(),
			Traces = new List<SceneTraceResult>(),
			EndPoint = from + direction * range
		};

		var hitObjects = new HashSet<GameObject>();

		// Offset visual origin so traces are visible in first person
		var rot = Rotation.LookAt( direction );
		var visualOrigin = from + rot.Right * 15f + rot.Down * 5f + rot.Forward * 20f;

		for ( int i = 0; i < pelletCount; i++ )
		{
			// Random offset within cone
			var offsetX = (Random.Shared.NextSingle() - 0.5f) * 2f * spreadAngle;
			var offsetY = (Random.Shared.NextSingle() - 0.5f) * 2f * spreadAngle;

			var pelletDir = (rot * Rotation.FromPitch( offsetX ) * Rotation.FromYaw( offsetY )).Forward;
			var to = from + pelletDir * range;

			// Trace from actual eye for accuracy
			var tr = HitDetection.Ray( scene, from, to, traceRadius, attacker?.GameObject );

			var endPoint = tr.Hit ? tr.HitPosition : to;

			// Visual: draw from offset origin to hit point
			CombatVfx.Line( scene, visualOrigin, endPoint, Color.Yellow.WithAlpha( 0.6f ), 0.12f, 1.5f );

			if ( tr.Hit )
			{
				// Show impact on every pellet, not just unique
				CombatVfx.Impact( scene, endPoint, Color.Red, 3f );
			}

			if ( tr.Hit && tr.GameObject is not null && hitObjects.Add( tr.GameObject ) )
			{
				result.Traces.Add( tr );

				var ctx = HitContext.FromTrace( tr, attacker );
				var dmg = DamageResolver.Resolve( attack, attacker, tr.GameObject, ctx );
				result.Hits.Add( dmg );
			}
		}

		return result;
	}
}
