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

	public RoguelitePlayer CurrentTarget;

	protected EnemyBrain Brain;
	private float _attackTimer;
	private float _stunTimer;
	private SkinnedModelRenderer _model;


	protected override void OnStart()
	{
		Faction.Faction = global::Faction.Enemy;

		Tags.Add( "enemy" );

		// Trigger colliders don't push each other but still get hit by traces
		foreach ( var col in Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			col.IsTrigger = true;
			col.Tags.Add( "enemy" );
		}

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
		Nav.Separation = 0f;

		// Disable rigidbody until needed for knockback — saves physics overhead
		var rb = Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
		if ( rb.IsValid() )
			rb.Enabled = false;

		Brain = CreateBrain();
		GameObject.Name = EnemyName;
	}

	/// <summary>
	/// Override in subclasses to provide a custom brain with different behavior.
	/// </summary>
	protected virtual EnemyBrain CreateBrain() => new EnemyBrain( this );

	protected override void OnUpdate()
	{
		// LOD: disable model rendering + animation for distant enemies
		UpdateLOD();

		// Animation runs on ALL clients — throttled by distance LOD
		if ( _shouldAnimate )
			UpdateAnimation();

		if ( !Networking.IsHost ) return;
		if ( Health.IsDead ) return;

		// Timer-based updates — no async tasks
		UpdatePendingDamage();

		if ( _inKnockback )
		{
			UpdateKnockback();
			return;
		}

		if ( IsStunned )
		{
			if ( Nav.Enabled ) { Nav.Stop(); Nav.Enabled = false; }
			_stunTimer -= Time.Delta;
			if ( _stunTimer <= 0 )
				IsStunned = false;
			return;
		}

		_attackTimer = MathF.Max( 0, _attackTimer - Time.Delta );

		// Committed to attack — disable navmesh entirely, no pathfinding cost
		if ( IsAttacking )
		{
			if ( Nav.Enabled )
			{
				Nav.Stop();
				Nav.Enabled = false;
			}
			FaceTarget();
			return;
		}
		else if ( !Nav.Enabled )
		{
			Nav.Enabled = true;
			Nav.SetAgentPosition( WorldPosition );
		}

		// Passive enemies just idle
		if ( IsPassive )
		{
			Nav.Stop();
			return;
		}

		// Throttle brain + navmesh by distance — distant enemies think less often
		if ( !_shouldAnimate && _lodFrameCounter % 10 != 0 )
			return;

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
				IsMoving = false;
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

	private float _pendingDamageTimer = -1f;
	private RoguelitePlayer _pendingDamageTarget;

	protected virtual void PerformAttack( RoguelitePlayer target )
	{
		PlayAttackAnim();
		_pendingDamageTimer = AttackHitDelay;
		_pendingDamageTarget = target;
	}

	private void UpdatePendingDamage()
	{
		if ( _pendingDamageTimer < 0f ) return;

		_pendingDamageTimer -= Time.Delta;
		if ( _pendingDamageTimer > 0f ) return;

		// Fire
		_pendingDamageTimer = -1f;
		var target = _pendingDamageTarget;
		_pendingDamageTarget = null;

		if ( !IsValid || Health.IsDead ) return;
		if ( target is null || !target.IsValid() || !target.IsAlive ) return;

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

	private bool IsAttacking;
	private float _attackAnimTimer;

	protected void PlayAttackAnim()
	{
		IsAttacking = true;
		_attackAnimTimer = 0.8f; // Default, broadcast will override with actual duration
		BroadcastAttackAnim();
	}

	[Rpc.Broadcast]
	private void BroadcastAttackAnim()
	{
		// Set on all clients so UpdateAnimation doesn't override before [Sync] arrives
		IsAttacking = true;
		_attackAnimTimer = 0.8f;

		if ( _model is null ) return;
		_model.Sequence.Name = "attack";
		_model.Sequence.Time = 0;
		_model.Sequence.Looping = false;
		if ( _model.Sequence.Duration > 0 )
			_attackAnimTimer = _model.Sequence.Duration;
	}

	private void OnDamageTakenFull( float amount, DamageType type, Component attacker )
	{
		if ( attacker is not null )
			Aggro.RecordDamage( attacker.GameObject, amount );
	}

	// --- LOD ---

	private const float LodClose = 800f;
	private const float LodMedium = 1500f;
	private const float LodFar = 2500f;
	private const float LodCull = 4000f;
	private int _lodFrameCounter;
	private bool _shouldAnimate;
	private bool _isClose; // Within close LOD — full AI rate

	private void UpdateLOD()
	{
		if ( _model is null ) return;

		var cam = Scene.Camera;
		if ( cam is null ) return;

		var distSq = WorldPosition.DistanceSquared( cam.WorldPosition );
		_lodFrameCounter++;

		// Viewport check — is this enemy in front of the camera?
		var toEnemy = (WorldPosition - cam.WorldPosition).Normal;
		var inView = Vector3.Dot( cam.WorldRotation.Forward, toEnemy ) > 0f; // In front hemisphere

		if ( distSq > LodCull * LodCull || !inView )
		{
			_model.Enabled = false;
			_shouldAnimate = false;
			_isClose = false;
		}
		else
		{
			_model.Enabled = true;
			_isClose = distSq < LodClose * LodClose;

			if ( _isClose )
				_shouldAnimate = true;
			else if ( distSq < LodMedium * LodMedium )
				_shouldAnimate = _lodFrameCounter % 3 == 0;
			else
				_shouldAnimate = _lodFrameCounter % 6 == 0;
		}
	}

	// --- Knockback ---

	private bool _inKnockback;
	private float _knockbackTimer;
	private Rigidbody _knockbackRb;

	public void ApplyKnockback( Vector3 direction, float force )
	{
		if ( !Networking.IsHost ) return;
		if ( Health.IsDead ) return;
		if ( _inKnockback ) return;

		var rb = Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
		if ( !rb.IsValid() ) return;

		_inKnockback = true;
		_knockbackTimer = 0f;
		_knockbackRb = rb;

		Nav.UpdatePosition = false;
		Nav.Stop();

		rb.Enabled = true;
		rb.Velocity = direction.WithZ( 0 ).Normal * force;
	}

	private void UpdateKnockback()
	{
		_knockbackTimer += Time.Delta;
		Nav.SetAgentPosition( WorldPosition );

		var done = _knockbackTimer > 1f;
		if ( !done && _knockbackTimer > 0.1f && _knockbackRb.IsValid() )
			done = _knockbackRb.Velocity.WithZ( 0 ).Length < 20f;

		if ( done )
		{
			if ( _knockbackRb.IsValid() )
			{
				_knockbackRb.Velocity = Vector3.Zero;
				_knockbackRb.Enabled = false;
			}

			_inKnockback = false;
			_knockbackRb = null;
			Nav.SetAgentPosition( WorldPosition );
			Nav.UpdatePosition = true;
		}
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
