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

		if ( dist <= Owner.AttackRange )
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

	protected RoguelitePlayer SelectTarget()
	{
		var candidates = Owner.Scene.GetAllComponents<RoguelitePlayer>();

		// Use aggro if we have threat entries
		if ( Owner.Aggro is not null )
		{
			var aggroTarget = Owner.Aggro.SelectTarget( candidates );
			if ( aggroTarget is not null ) return aggroTarget;
		}

		// Fallback: nearest alive player
		return candidates
			.Where( p => p.IsAlive )
			.OrderBy( p => Owner.WorldPosition.Distance( p.WorldPosition ) )
			.FirstOrDefault();
	}

	/// <summary>
	/// Override in subclasses for enemy-type-specific flee behavior.
	/// Default: never flee.
	/// </summary>
	protected virtual bool ShouldFlee() => false;
}
