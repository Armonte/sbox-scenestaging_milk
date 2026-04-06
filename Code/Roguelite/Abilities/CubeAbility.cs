/// <summary>
/// Ice Cube ability. On enemy: stuns for 3 seconds. On ally: freezes movement + accelerates cooldowns.
/// Target is determined by what the player is aiming at.
/// </summary>
public sealed class CubeAbility : IAbility
{
	public string Id => "cube";
	public float Cooldown => 12f;

	private const float EnemyStunDuration = 3f;
	private const float AllyFreezeDuration = 2f;
	private const float CooldownAcceleration = 4f;

	public bool TryActivate( RoguelitePlayer caster, GameObject target )
	{
		if ( target is null ) return false;

		var targetFaction = target.Components.Get<FactionComponent>( FindMode.EverythingInSelfAndDescendants );
		if ( targetFaction is null ) return false;

		if ( caster.Faction.IsHostile( targetFaction ) )
		{
			// Enemy: stun
			var enemy = target.Components.Get<RogueliteEnemyBase>( FindMode.EverythingInSelfAndDescendants );
			if ( enemy is null ) return false;

			enemy.ApplyStun( EnemyStunDuration );
			Log.Info( $"[CubeAbility] Stunned {target.Name} for {EnemyStunDuration}s" );
			return true;
		}
		else if ( caster.Faction.IsFriendly( targetFaction ) )
		{
			// Ally: freeze + accelerate cooldowns
			var player = target.Components.Get<RoguelitePlayer>( FindMode.EverythingInSelfAndDescendants );
			if ( player is null ) return false;

			player.Movement.SetFrozen( true );

			// Accelerate their ability cooldowns
			player.Abilities.AccelerateCooldowns( CooldownAcceleration );

			// Schedule unfreeze
			_ = UnfreezeAfterDelay( player, AllyFreezeDuration );

			Log.Info( $"[CubeAbility] Froze ally {target.Name} for {AllyFreezeDuration}s, accelerated cooldowns by {CooldownAcceleration}s" );
			return true;
		}

		return false;
	}

	private async Task UnfreezeAfterDelay( RoguelitePlayer player, float seconds )
	{
		await GameTask.DelayRealtimeSeconds( seconds );
		if ( player.IsValid() )
			player.Movement.SetFrozen( false );
	}
}
