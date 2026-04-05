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
/// Central damage authority. ALL damage in the game flows through this class.
/// No exceptions — weapons, abilities, DoTs, environmental damage all call here.
/// </summary>
public static class DamageResolver
{
	/// <summary>
	/// Resolve a single attack against a target. Returns the result of the damage calculation.
	/// </summary>
	public static DamageResult Resolve(
		AttackData attack,
		Component attacker,
		GameObject targetObject,
		HitContext ctx )
	{
		if ( targetObject is null ) return default;

		// Search the hit object AND its ancestors for a HealthComponent
		var health = targetObject.Components.Get<RogueliteHealthComponent>( FindMode.EverythingInSelfAndDescendants );
		if ( health is null || health.IsDead )
			return default;

		var attackerFaction = attacker.Components.Get<FactionComponent>( FindMode.EverythingInSelfAndDescendants );
		var targetFaction = targetObject.Components.Get<FactionComponent>( FindMode.EverythingInSelfAndDescendants );

		// Don't damage friendlies
		if ( attackerFaction is not null && targetFaction is not null )
		{
			if ( !attackerFaction.IsHostile( targetFaction ) )
				return default;
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

		// 4. Floor at 1 damage
		dmg = MathF.Max( 1f, dmg );

		// 5. Apply damage
		health.ApplyDamage( dmg, attack.Type );

		// 6. Knockback
		if ( attack.CanKnockback && attack.KnockbackForce > 0 )
			ApplyKnockback( targetObject, ctx.Direction, attack.KnockbackForce );

		Log.Info( $"[DamageResolver] {attacker.GameObject.Name} hit {targetObject.Name} for {dmg:F0} {attack.Type} damage{(isCrit ? " (CRIT!)" : "")}{(ctx.IsBackstab ? " (backstab)" : "")} — HP: {health.Current:F0}/{health.MaxHealth:F0}" );

		return new DamageResult( dmg, isCrit, health.IsDead, ctx );
	}

	/// <summary>
	/// Convenience: trace a melee arc from the attacker and resolve damage on all hits.
	/// </summary>
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
			var camera = player.Components.Get<PlayerCamera>();
			if ( camera is not null )
			{
				eyePos = attacker.WorldPosition + Vector3.Up * 64f;
				forward = camera.EyeAngles.ToRotation().Forward;
			}
			else
			{
				eyePos = attacker.WorldPosition + Vector3.Up * 64f;
				forward = attacker.WorldRotation.Forward;
			}
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
			Log.Info( $"[MeleeTrace] Hit: {tr.GameObject.Name} (has Health: {tr.GameObject.Components.Get<RogueliteHealthComponent>( FindMode.EverythingInSelfAndDescendants ) is not null}, has Faction: {tr.GameObject.Components.Get<FactionComponent>( FindMode.EverythingInSelfAndDescendants ) is not null})" );
			var ctx = HitContext.FromTrace( tr, attacker );
			var result = Resolve( attack, attacker, tr.GameObject, ctx );
			results.Add( result );
		}

		return results;
	}

	private static bool RollCrit( Component attacker )
	{
		// Base 5% crit chance — RunModifiers will adjust this in Phase 5
		return Random.Shared.NextSingle() < 0.05f;
	}

	private static void ApplyKnockback( GameObject target, Vector3 direction, float force )
	{
		var rb = target.Components.Get<Rigidbody>();
		if ( rb.IsValid() )
			rb.ApplyImpulse( direction.Normal * force );
	}
}
