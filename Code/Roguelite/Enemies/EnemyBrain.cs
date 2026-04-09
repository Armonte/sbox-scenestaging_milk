/// <summary>
/// Simple state machine for enemy AI. Plain class (not a Component) — no networking overhead.
/// EnemyBase owns an EnemyBrain and calls Tick() each frame.
/// Subclass and override ShouldFlee() for enemy-type-specific behavior.
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

		// Decay aggro each frame
		Owner.Aggro?.DecayThreat();

		// Select target via aggro or fallback to nearest
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

		// StopDistance = stop moving. AttackRange = can deal damage.
		// Enemy stops at StopDistance OR AttackRange, whichever is larger.
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
		// Refresh player cache once per frame
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

		// Use aggro if we have threat entries
		if ( Owner.Aggro is not null )
		{
			var aggroTarget = Owner.Aggro.SelectTarget( _cachedPlayers );
			if ( aggroTarget is not null ) return aggroTarget;
		}

		// Fallback: nearest alive player (no LINQ — avoid allocation)
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

	/// <summary>
	/// Check if there's a clear line of sight to the target (no walls/floors between us).
	/// Traces from enemy eye height to target eye height.
	/// </summary>
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

	/// <summary>
	/// Override in subclasses for enemy-type-specific flee behavior.
	/// Default: never flee.
	/// </summary>
	protected virtual bool ShouldFlee() => false;
}
