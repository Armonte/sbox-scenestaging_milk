/// <summary>
/// Base enemy component. Uses NavMeshAgent for pathfinding direction only.
/// We drive WorldPosition ourselves to avoid NavAgent snapping/teleporting.
/// EnemyBrain handles state machine logic. Subclasses override PerformAttack and CreateBrain.
/// </summary>
[Title( "Roguelite Enemy" )]
[Icon( "pest_control" )]
public class RogueliteEnemyBase : Component
{
	[RequireComponent] public NavMeshAgent Nav { get; set; }
	[RequireComponent] public RogueliteHealthComponent Health { get; set; }
	[RequireComponent] public FactionComponent Faction { get; set; }
	[RequireComponent] public AggroComponent Aggro { get; set; }

	[Property] public float AttackDamage { get; set; } = 25f;
	[Property] public float AttackRange { get; set; } = 120f;
	[Property] public float StopDistance { get; set; } = 100f;
	[Property] public float DetectionRange { get; set; } = 1200f;
	[Property] public float AttackCooldown { get; set; } = 1.5f;
	[Property] public float MoveSpeed { get; set; } = 150f;
	[Property] public string EnemyName { get; set; } = "Enemy";
	[Property, Title( "Passive (No AI)" )] public bool IsPassive { get; set; } = false;

	[Sync] public bool IsStunned { get; set; }
	[Sync] public bool IsMoving { get; set; }
	[Sync] public bool IsAttacking { get; set; }

	public RoguelitePlayer CurrentTarget;

	protected EnemyBrain Brain;
	private float _attackTimer;
	private float _stunTimer;
	private SkinnedModelRenderer _model;


	protected override void OnStart()
	{
		Faction.Faction = global::Faction.Enemy;

		Tags.Add( "enemy" );

		Health.Init( Health.MaxHealth );
		Health.OnDeath += OnDeath;

		Health.OnDamageTakenFull += OnDamageTakenFull;

		_model = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );

		// Disable animgraph — we drive all animations via Sequence
		if ( _model is not null )
		{
			_model.UseAnimGraph = false;
			_model.Sequence.Name = "idle";
			_model.Sequence.Looping = true;
		}

		// Let NavMeshAgent handle position — it respects walls and navmesh topology
		Nav.UpdatePosition = true;
		Nav.UpdateRotation = false;
		Nav.MaxSpeed = MoveSpeed;

		Brain = CreateBrain();
		GameObject.Name = EnemyName;
	}

	/// <summary>
	/// Override in subclasses to provide a custom brain with different behavior.
	/// </summary>
	protected virtual EnemyBrain CreateBrain() => new EnemyBrain( this );

	protected override void OnUpdate()
	{
		// Animation runs on ALL clients
		UpdateAnimation();

		if ( !Networking.IsHost ) return;
		if ( Health.IsDead ) return;

		if ( _inKnockback ) return;

		// Stun timer management (brain reports Stunned state, but timer lives here)
		if ( IsStunned )
		{
			_stunTimer -= Time.Delta;
			if ( _stunTimer <= 0 )
				IsStunned = false;
			return;
		}

		_attackTimer = MathF.Max( 0, _attackTimer - Time.Delta );

		// Passive enemies just idle — useful for testing damage/procs
		if ( IsPassive )
		{
			Nav.Stop();
			UpdateAnimation();
			return;
		}

		// Let the brain compute the state
		Brain.Tick();

		switch ( Brain.State )
		{
			case EnemyBrainState.Idle:
				Nav.Stop();
				break;

			case EnemyBrainState.Chase:
				ChaseTarget();
				break;

			case EnemyBrainState.Attack:
				Nav.Stop();
				FaceTarget();
				if ( _attackTimer <= 0 )
				{
					PerformAttack( CurrentTarget );
					_attackTimer = AttackCooldown;
				}
				break;

			case EnemyBrainState.Flee:
				FleeFromTarget();
				break;

			case EnemyBrainState.Stunned:
				break;
		}

		UpdateAnimation();
	}

	// --- Movement ---

	protected virtual void ChaseTarget()
	{
		if ( CurrentTarget is null ) return;

		Nav.MaxSpeed = MoveSpeed;
		Nav.MoveTo( CurrentTarget.WorldPosition );

		// NavAgent drives position — we just handle smooth rotation
		var vel = Nav.Velocity.WithZ( 0 );
		if ( vel.Length > 1f )
			WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( vel, Vector3.Up ), Time.Delta * 5f );
	}

	protected virtual void FleeFromTarget()
	{
		if ( CurrentTarget is null ) return;

		var awayDir = (WorldPosition - CurrentTarget.WorldPosition).WithZ( 0 );
		if ( awayDir.Length < 1f ) awayDir = Vector3.Random.WithZ( 0 );

		var fleeTarget = WorldPosition + awayDir.Normal * 400f;
		Nav.MaxSpeed = MoveSpeed;
		Nav.MoveTo( fleeTarget );

		var vel = Nav.Velocity.WithZ( 0 );
		if ( vel.Length > 1f )
			WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( vel, Vector3.Up ), Time.Delta * 5f );
	}

	protected void FaceTarget()
	{
		if ( CurrentTarget is null ) return;
		var dirToTarget = (CurrentTarget.WorldPosition - WorldPosition).WithZ( 0 );
		if ( dirToTarget.Length > 1f )
			WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( dirToTarget, Vector3.Up ), Time.Delta * 5f );
	}

	// --- Combat ---

	[Property] public float AttackHitDelay { get; set; } = 0.35f;

	protected virtual void PerformAttack( RoguelitePlayer target )
	{
		// Start the animation first, delay the actual damage
		PlayAttackAnim();
		_ = DelayedDamage( target, AttackHitDelay );
	}

	private async Task DelayedDamage( RoguelitePlayer target, float delay )
	{
		await GameTask.DelaySeconds( delay );

		if ( !IsValid || Health.IsDead ) return;
		if ( target is null || !target.IsValid() || !target.IsAlive ) return;

		// Re-check range — target may have moved away during windup
		var dist = WorldPosition.Distance( target.WorldPosition );
		if ( dist > AttackRange * 1.5f ) return;

		var attack = new AttackData( AttackDamage, DamageType.Blunt );

		var ctx = new HitContext(
			WorldPosition,
			(target.WorldPosition - WorldPosition).Normal,
			target.WorldPosition,
			Vector3.Up,
			dist,
			false,
			false );

		DamageResolver.Resolve( attack, this, target.GameObject, ctx );
	}

	private float _attackAnimTimer;

	protected void PlayAttackAnim()
	{
		IsAttacking = true;
		BroadcastAttackAnim();
	}

	[Rpc.Broadcast]
	private void BroadcastAttackAnim()
	{
		if ( _model is null ) return;
		_model.Sequence.Name = "attack";
		_model.Sequence.Time = 0;
		_model.Sequence.Looping = false;
		_attackAnimTimer = _model.Sequence.Duration;
		if ( _attackAnimTimer <= 0 ) _attackAnimTimer = 0.8f;
	}

	private void OnDamageTakenFull( float amount, DamageType type, Component attacker )
	{
		if ( attacker is not null )
			Aggro.RecordDamage( attacker.GameObject, amount );
	}

	// --- Knockback ---

	private bool _inKnockback;

	public void ApplyKnockback( Vector3 direction, float force )
	{
		if ( !Networking.IsHost ) return;
		if ( Health.IsDead ) return;
		if ( _inKnockback ) return;

		var rb = Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
		if ( !rb.IsValid() ) return;

		_inKnockback = true;
		Nav.UpdatePosition = false;
		Nav.Stop();

		// Flatten to horizontal only — no air launch
		var flat = direction.WithZ( 0 ).Normal;

		// Set velocity directly instead of impulse — bypasses mass entirely
		rb.Velocity = flat * force;

		_ = EndKnockback( rb );
	}

	private async Task EndKnockback( Rigidbody rb )
	{
		// Wait for rigidbody to slow down or timeout
		TimeSince elapsed = 0;
		while ( elapsed < 1f )
		{
			if ( !IsValid || Health.IsDead ) return;

			// Sync nav agent position each frame
			Nav.SetAgentPosition( WorldPosition );

			// Done when slow
			if ( elapsed > 0.1f && rb.IsValid() && rb.Velocity.WithZ( 0 ).Length < 20f )
				break;

			await Task.Frame();
		}

		if ( !IsValid ) return;

		// Kill remaining velocity
		if ( rb.IsValid() )
			rb.Velocity = rb.Velocity.WithZ( rb.Velocity.z ) * 0f;

		_inKnockback = false;
		Nav.SetAgentPosition( WorldPosition );
		Nav.UpdatePosition = true;
	}

	// --- Separation ---

	protected virtual void SeparateFromOtherEnemies()
	{
		var minDist = Nav.Radius * 3f;

		foreach ( var other in Scene.GetAllComponents<RogueliteEnemyBase>() )
		{
			if ( other == this || other.Health.IsDead ) continue;

			var diff = WorldPosition - other.WorldPosition;
			var dist = diff.WithZ( 0 ).Length;

			if ( dist < minDist && dist > 0.1f )
			{
				var pushDir = diff.WithZ( 0 ).Normal;
				var pushStrength = (minDist - dist) * 2f * Time.Delta;
				WorldPosition += pushDir * pushStrength;
			}
		}
	}

	// --- Ground stick ---

	protected virtual void StickToGround()
	{
		var tr = Scene.Trace
			.Ray( WorldPosition + Vector3.Up * 20f, WorldPosition + Vector3.Down * 200f )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger" )
			.Run();

		if ( tr.Hit )
		{
			var targetZ = tr.HitPosition.z;
			var currentZ = WorldPosition.z;
			WorldPosition = WorldPosition.WithZ( MathX.Lerp( currentZ, targetZ, Time.Delta * 10f ) );
		}
	}

	// --- Stun ---

	public void ApplyStun( float duration )
	{
		if ( !Networking.IsHost ) return;
		IsStunned = true;
		_stunTimer = MathF.Max( _stunTimer, duration );
	}

	// --- Death ---

	protected virtual void OnDeath()
	{
		BroadcastDeath();
		GameObject.Destroy();
	}

	[Rpc.Broadcast]
	private void BroadcastDeath() { }

	// --- Animation ---

	protected virtual void UpdateAnimation()
	{
		if ( _model is null ) return;

		// Attack anim playing — let it finish
		if ( IsAttacking )
		{
			_attackAnimTimer -= Time.Delta;
			if ( _attackAnimTimer <= 0 )
				IsAttacking = false;
			return;
		}

		// Use synced IsMoving flag so clients can animate too
		if ( Networking.IsHost )
			IsMoving = Nav.Velocity.WithZ( 0 ).Length > 5f;

		string desired = IsMoving ? "walk_N" : "idle";

		if ( _model.Sequence.Name != desired )
		{
			_model.Sequence.Name = desired;
			_model.Sequence.Time = 0;
			_model.Sequence.Looping = true;
		}
	}
}
