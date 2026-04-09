/// <summary>
/// One-shot radial damage. Call Explosion.At() to damage everything in a sphere.
/// Use for: grenade detonation, barrel explosion, ability burst, enemy death nova.
/// </summary>
public static class Explosion
{
	public struct ExplosionResult
	{
		public List<DamageResult> Hits;
		public int TotalHit;
	}

	/// <summary>
	/// Deal damage to all damageable entities within radius.
	/// Damage falls off linearly from center (full damage at center, falloffMultiplier at edge).
	/// </summary>
	public static ExplosionResult At( Scene scene, Vector3 center, float radius,
		AttackData attack, Component attacker, float falloffMultiplier = 0.5f )
	{
		var result = new ExplosionResult { Hits = new List<DamageResult>() };

		var targets = HitDetection.Sphere( scene, center, radius, attacker?.GameObject );

		foreach ( var target in targets )
		{
			var entityRoot = FindEntityRoot( target );

			var dist = entityRoot.WorldPosition.Distance( center );
			var falloff = 1f - (dist / radius) * (1f - falloffMultiplier);
			falloff = MathF.Max( falloffMultiplier, falloff );

			var scaledAttack = new AttackData(
				attack.BaseDamage * falloff,
				attack.Type,
				attack.CanCrit,
				canKnockback: true,
				knockbackForce: attack.KnockbackForce * falloff
			);

			var direction = (entityRoot.WorldPosition - center);
			if ( direction.Length < 0.1f ) direction = Vector3.Up;

			var ctx = new HitContext(
				center, direction.Normal, entityRoot.WorldPosition,
				Vector3.Up, dist, false, false );

			var dmgResult = DamageResolver.Resolve( scaledAttack, attacker, entityRoot, ctx );
			result.Hits.Add( dmgResult );
		}

		result.TotalHit = result.Hits.Count;

		// Visual feedback
		CombatVfx.Sphere( scene, center, radius, Color.Orange.WithAlpha( 0.3f ), 0.4f );
		CombatVfx.Sphere( scene, center, radius * 0.5f, Color.Yellow.WithAlpha( 0.5f ), 0.2f );

		return result;
	}

	private static GameObject FindEntityRoot( GameObject obj )
	{
		var current = obj;
		while ( current is not null )
		{
			if ( current.Components.Get<IDamageable>( FindMode.EverythingInSelfAndDescendants ) is not null )
				return current;
			current = current.Parent;
		}
		return obj;
	}
}
