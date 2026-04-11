/// <summary>
/// Result returned by DamageResolver after resolving damage.
/// </summary>
public readonly struct DamageResult
{
	public readonly float FinalDamage;
	public readonly bool WasCrit;
	public readonly bool TargetDied;
	public readonly HitContext Context;

	public DamageResult( float finalDamage, bool wasCrit, bool targetDied, HitContext context )
	{
		FinalDamage = finalDamage;
		WasCrit = wasCrit;
		TargetDied = targetDied;
		Context = context;
	}
}

/// <summary>
/// Central damage authority. Uses IDamageable interface — no component lookups.
/// </summary>
public static class DamageResolver
{
	public static DamageResult Resolve(
		AttackData attack,
		Component attacker,
		GameObject targetObject,
		HitContext ctx )
	{
		if ( targetObject is null ) return default;

		targetObject = FindEntityRoot( targetObject );

		var target = targetObject.Components.Get<global::IDamageable>( FindMode.EverythingInSelfAndDescendants );
		if ( target is null || target.IsDead )
			return default;

		// Faction check
		var attackerDamageable = attacker.Components.Get<global::IDamageable>( FindMode.EverythingInSelfAndDescendants );
		if ( attackerDamageable is not null )
		{
			if ( attackerDamageable.Faction == target.Faction )
				return default; // Friendly fire blocked
		}

		var dmg = attack.BaseDamage;

		// 1. Armor resistance
		var armor = targetObject.Components.Get<RogueliteArmorComponent>( FindMode.EverythingInSelfAndDescendants );
		if ( armor is not null )
			dmg *= armor.GetResistance( attack.Type );

		// 2. Crit roll
		var isCrit = attack.CanCrit && RollCrit( attacker );
		if ( isCrit )
			dmg *= 2f;

		// 3. Positional bonuses
		if ( ctx.IsBackstab )
			dmg *= 1.5f;
		if ( ctx.IsHeadshot )
			dmg *= 1.25f;

		// 4. Hitbox weak-point bonuses — applied when the trace used hitboxes and
		//    the hit hitbox has one of these tags. "weakpoint" is the explicit big
		//    multiplier; "head" doubles up with the positional heuristic above so
		//    designers can either tag a head hitbox or rely on the height check.
		//    Track which visual style the floating damage number should use.
		var numberStyle = isCrit ? DamageNumberStyle.Crit : DamageNumberStyle.Normal;

		if ( ctx.HasHitboxTag( "weakpoint" ) )
		{
			dmg *= 3f;
			numberStyle = DamageNumberStyle.Weakpoint;
			Log.Info( $"[DamageResolver] WEAKPOINT hit on {targetObject.Name} — ×3.0 → {dmg:F0} dmg" );
		}
		else if ( ctx.HasHitboxTag( "head" ) )
		{
			dmg *= 2f;
			numberStyle = DamageNumberStyle.Headshot;
			Log.Info( $"[DamageResolver] HEAD hit on {targetObject.Name} — ×2.0 → {dmg:F0} dmg" );
		}
		else if ( ctx.HitHitbox )
		{
			// Useful when testing: a hitbox was involved but it had no recognized
			// weak-point tag. If you expected a multiplier here, check the tags on
			// the ManualHitbox component — casing is ignored, but the tag must
			// literally be "head" or "weakpoint".
			Log.Info( $"[DamageResolver] hitbox hit on {targetObject.Name} — no weak-point tag matched" );
		}

		// 4. Floor at 0
		dmg = MathF.Max( 0f, dmg );

		// 5. Apply damage
		target.ApplyDamage( dmg, attack.Type, attacker );

		// 6. Knockback
		if ( attack.CanKnockback && attack.KnockbackForce > 0 )
			target.ApplyKnockback( ctx.Direction, attack.KnockbackForce );

		// 7. Floating damage number — broadcast to every client. We do this here (not inside
		//    the target's ApplyDamage) because crit + final damage live in this scope, and the
		//    IDamageable interface doesn't expose them.
		if ( target is RogueliteEnemyBase enemy )
		{
			var popPos = ctx.ImpactPoint != Vector3.Zero ? ctx.ImpactPoint : targetObject.WorldPosition;
			enemy.BroadcastDamageNumber( dmg, numberStyle, popPos );
		}

		return new DamageResult( dmg, isCrit, target.IsDead, ctx );
	}

	public static List<DamageResult> MeleeTrace(
		Component attacker,
		AttackData attack,
		float range,
		float radius )
	{
		var results = new List<DamageResult>();
		var player = attacker.Components.Get<RoguelitePlayer>();

		Vector3 eyePos;
		Vector3 forward;

		if ( player is not null )
		{
			eyePos = attacker.WorldPosition + Vector3.Up * player.EyeHeight;
			forward = player.EyeAngles.ToRotation().Forward;
		}
		else
		{
			eyePos = attacker.WorldPosition + Vector3.Up * 40f;
			forward = attacker.WorldRotation.Forward;
		}

		var tr = attacker.Scene.Trace
			.Ray( eyePos, eyePos + forward * range )
			.Size( radius )
			.IgnoreGameObjectHierarchy( attacker.GameObject )
			.WithoutTags( "trigger" )
			.Run();

		if ( tr.Hit && tr.GameObject is not null )
		{
			var ctx = HitContext.FromTrace( tr, attacker );
			var result = Resolve( attack, attacker, tr.GameObject, ctx );
			results.Add( result );
		}

		return results;
	}

	private static bool RollCrit( Component attacker )
	{
		return Random.Shared.NextSingle() < 0.05f;
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
}
