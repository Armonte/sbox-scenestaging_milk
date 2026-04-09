/// <summary>
/// Simple state machine for enemy AI. Plain class (not a Component) — no networking overhead.
/// EnemyBase owns an EnemyBrain and calls Tick() each frame.
/// </summary>
public enum EnemyBrainState
{
	Idle,
	Chase,
	Attack,
	Flee,
	Stunned
}

public class EnemyBrain
{
	public EnemyBrainState State { get; private set; } = EnemyBrainState.Idle;

	protected readonly RogueliteEnemyBase Owner;

	public EnemyBrain( RogueliteEnemyBase owner )
	{
		Owner = owner;
	}

	public void Tick()
	{
		if ( Owner.IsStunned )
		{
			State = EnemyBrainState.Stunned;
			return;
		}

		Owner.DecayThreat();

		var target = SelectTarget();

		if ( target is null || !target.IsAlive )
		{
			State = EnemyBrainState.Idle;
			return;
		}

		Owner.CurrentTarget = target;

		var dist = Owner.WorldPosition.Distance( target.WorldPosition );

		if ( ShouldFlee() )
		{
			State = EnemyBrainState.Flee;
			return;
		}

		var stopAt = MathF.Max( Owner.StopDistance, Owner.AttackRange );

		if ( dist <= stopAt && HasLineOfSight( target ) )
		{
			State = EnemyBrainState.Attack;
		}
		else if ( dist <= Owner.DetectionRange )
		{
			State = EnemyBrainState.Chase;
		}
		else
		{
			State = EnemyBrainState.Idle;
		}
	}

	// Cached player list — refreshed once per frame across all enemies
	private static int _cachedFrame;
	private static readonly List<RoguelitePlayer> _cachedPlayers = new();

	protected RoguelitePlayer SelectTarget()
	{
		var frame = (int)(Time.Now * 60);
		if ( _cachedFrame != frame )
		{
			_cachedFrame = frame;
			_cachedPlayers.Clear();
			foreach ( var p in Owner.Scene.GetAllComponents<RoguelitePlayer>() )
			{
				if ( p.IsAlive )
					_cachedPlayers.Add( p );
			}
		}

		if ( _cachedPlayers.Count == 0 ) return null;

		// Use inlined aggro on owner
		var aggroTarget = Owner.SelectAggroTarget( _cachedPlayers );
		if ( aggroTarget is not null ) return aggroTarget;

		// Fallback: nearest alive player
		RoguelitePlayer nearest = null;
		float nearestDist = float.MaxValue;
		foreach ( var p in _cachedPlayers )
		{
			var dist = Owner.WorldPosition.DistanceSquared( p.WorldPosition );
			if ( dist < nearestDist )
			{
				nearestDist = dist;
				nearest = p;
			}
		}
		return nearest;
	}

	protected bool HasLineOfSight( RoguelitePlayer target )
	{
		var from = Owner.WorldPosition + Vector3.Up * 40f;
		var to = target.WorldPosition + Vector3.Up * 40f;

		var tr = Owner.Scene.Trace
			.Ray( from, to )
			.IgnoreGameObjectHierarchy( Owner.GameObject )
			.IgnoreGameObjectHierarchy( target.GameObject )
			.WithoutTags( "trigger", "enemy" )
			.Run();

		return !tr.Hit;
	}

	protected virtual bool ShouldFlee() => false;
}
