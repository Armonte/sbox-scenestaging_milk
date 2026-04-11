/// <summary>
/// Base enemy component. Uses NavMeshAgent for pathfinding direction only.
/// We drive WorldPosition ourselves to avoid NavAgent snapping/teleporting.
/// EnemyBrain handles state machine logic. Subclasses override PerformAttack and CreateBrain.
/// </summary>
[Title( "Roguelite Enemy" )]
[Icon( "pest_control" )]
public class RogueliteEnemyBase : Component, global::IDamageable
{
	[RequireComponent] public NavMeshAgent Nav { get; set; }

	// Inlined health — was RogueliteHealthComponent
	[Property] public float HealthMax { get; set; } = 100f;
	[Sync] public float HealthCurrent { get; set; }
	[Sync] public bool IsDead { get; set; }

	// Inlined faction — always Enemy
	public Faction Faction => global::Faction.Enemy;

	// Inlined aggro
	[Property] public TargetStrategy AggroStrategy { get; set; } = TargetStrategy.HighestThreat;
	private readonly Dictionary<GameObject, float> _threatTable = new();
	private const float ThreatDecayRate = 0.998f;

	[Property] public float AttackDamage { get; set; } = 25f;
	[Property] public float AttackRange { get; set; } = 120f;
	[Property] public float StopDistance { get; set; } = 100f;
	[Property] public float DetectionRange { get; set; } = 1200f;
	[Property] public float AttackCooldown { get; set; } = 0.8f;
	[Property] public float MoveSpeed { get; set; } = 150f;

	/// <summary>
	/// Attack speed multiplier. 1.0 = normal, 2.0 = double speed (half duration).
	/// Scales: animation playback, hit delay, attack commitment time, cooldown.
	/// </summary>
	[Property] public float AttackSpeed { get; set; } = 1.0f;
	[Property] public string EnemyName { get; set; } = "Enemy";
	[Property, Title( "Passive (No AI)" )] public bool IsPassive { get; set; } = false;

	[Sync] public bool IsStunned { get; set; }
	[Sync] public bool IsMoving { get; set; }

	// --- Hit flash ---
	/// <summary>
	/// Seconds the unlit-white override stays on after a hit. Short = snappy pop.
	/// </summary>
	[Property, Group( "Feedback" )] public float HitFlashDuration { get; set; } = 0.03f;

	/// <summary>
	/// Intensity pushed into the hit_flash shader's FlashIntensity attribute. &gt;1 blows out under bloom.
	/// </summary>
	[Property, Group( "Feedback" )] public float HitFlashIntensity { get; set; } = 4f;

	// --- Sound ---
	/// <summary>
	/// Plays when the enemy takes damage. Fires for everyone via the hit-flash RPC.
	/// </summary>
	[Property, Group( "Sound" )] public SoundEvent HitSound { get; set; }

	/// <summary>
	/// Plays the instant the attack windup begins (same frame as the attack animation start).
	/// Use this for charge-up grunts, weapon raises, telegraph cues.
	/// </summary>
	[Property, Group( "Sound" )] public SoundEvent AttackWindupSound { get; set; }

	/// <summary>
	/// Plays at the moment of impact, AttackHitDelay seconds into the swing. Each client schedules
	/// this locally off the same animation-start RPC, so visual + audio stay in lockstep without
	/// a separate network round-trip.
	/// </summary>
	[Property, Group( "Sound" )] public SoundEvent AttackImpactSound { get; set; }

	/// <summary>
	/// Plays once when the enemy dies.
	/// </summary>
	[Property, Group( "Sound" )] public SoundEvent DeathSound { get; set; }

	public event Action OnDeathEvent;

	public RoguelitePlayer CurrentTarget;

	protected EnemyBrain Brain;
	private float _attackTimer;
	private float _stunTimer;
	private bool _wantsToAttack;
	private SkinnedModelRenderer _model;
	private float _hitFlashTimer;
	private bool _flashApplied;
	// Local-client timer for the attack impact sound. Set when BroadcastAttackAnim fires,
	// counts down per frame on every client (including the host), plays AttackImpactSound when it hits 0.
	private float _impactSoundTimer;
	// Per-enemy unique Material instance (Material.FromShader returns a cached singleton —
	// we have to use Material.Create to get a fresh instance, otherwise renderer dedup
	// silently no-ops every other override toggle).
	private Material _flashMaterial;


	protected override void OnStart()
	{
		Tags.Add( "enemy" );

		foreach ( var col in Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
		{
			col.Tags.Add( "enemy" );
		}

		// Init health
		HealthCurrent = HealthMax;
		IsDead = false;

		_model = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );

		// Disable animgraph — we drive all animations via Sequence
		if ( _model is not null )
		{
			_model.UseAnimGraph = false;
			_model.Sequence.Name = "idle";
			_model.Sequence.Looping = true;
		}

		// Per-enemy unique Material — Material.Create with anonymous=true skips the shader cache
		// so this instance is genuinely unique to this enemy.
		_flashMaterial = Material.Create( $"flash_{GameObject.Id}", "shaders/hit_flash.shader", true );
		if ( _flashMaterial is null )
			Log.Warning( "[RogueliteEnemy] hit_flash.shader failed to load" );

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
		// LOD runs every frame (cheap — one distance check)
		UpdateLOD();

		var shouldTick = true;

		// Animation update — only once per frame (also called on clients via shouldAnimate)
		if ( _shouldAnimate )
			UpdateAnimation();

		// Hit flash decays on every client so the visual pop is seen by everyone.
		TickHitFlash();

		// Each client times its own attack-impact sound off the BroadcastAttackAnim RPC,
		// so audio + animation stay in lockstep without a second network round-trip.
		TickAttackImpactSound();

		if ( !Networking.IsHost ) return;
		if ( IsDead ) return;

		// Timers + attack execution run every frame — not throttled
		UpdatePendingDamage();
		_attackTimer = MathF.Max( 0, _attackTimer - Time.Delta );

		if ( IsStunned )
		{
			_stunTimer -= Time.Delta;
			if ( _stunTimer <= 0 )
				IsStunned = false;
			if ( Nav.IsValid() && Nav.Enabled ) { Nav.Stop(); Nav.Enabled = false; }
			return;
		}

		if ( _inKnockback )
		{
			UpdateKnockback();
			return;
		}

		// Attack execution runs every frame — timer check can't be delayed by tick rate
		if ( _wantsToAttack && _attackTimer <= 0 && !IsAttacking )
		{
			if ( CurrentTarget is not null && CurrentTarget.IsValid() && CurrentTarget.IsAlive )
			{
				PerformAttack( CurrentTarget );
				_attackTimer = AttackCooldown / AttackSpeed;
			}
		}

		// During attack animation — disable nav
		if ( IsAttacking && Nav.IsValid() && Nav.Enabled )
		{
			Nav.Stop();
			Nav.Enabled = false;
		}

		// Everything below only runs at tick rate
		if ( !shouldTick ) return;

		// Passive enemies just idle
		if ( IsPassive )
		{
			Nav.Stop();
			return;
		}

		Brain.Tick();

		var needsNav = Brain.State == EnemyBrainState.Chase || Brain.State == EnemyBrainState.Flee;

		if ( needsNav && Nav.IsValid() && !Nav.Enabled )
		{
			Nav.Enabled = true;
			Nav.SetAgentPosition( WorldPosition );
		}
		else if ( !needsNav && Nav.IsValid() && Nav.Enabled )
		{
			Nav.Stop();
			Nav.Enabled = false;
		}

		switch ( Brain.State )
		{
			case EnemyBrainState.Idle:
				break;

			case EnemyBrainState.Chase:
				ChaseTarget();
				break;

			case EnemyBrainState.Attack:
				IsMoving = false;
				_wantsToAttack = true;
				FaceTarget();
				break;

			case EnemyBrainState.Flee:
				FleeFromTarget();
				break;

			case EnemyBrainState.Stunned:
				break;
		}

		// Clear attack intent if brain left attack state
		if ( Brain.State != EnemyBrainState.Attack )
			_wantsToAttack = false;
	}

	// --- Movement ---

	private float _nextPathUpdate;

	protected virtual void ChaseTarget()
	{
		if ( CurrentTarget is null ) return;

		// Recompute path infrequently — agent follows existing path between updates
		if ( Time.Now >= _nextPathUpdate )
		{
			Nav.MaxSpeed = MoveSpeed;
			Nav.MoveTo( CurrentTarget.WorldPosition );

			// Close: 0.3s, far: 1.5s between path recomputes
			_nextPathUpdate = Time.Now + (_isClose ? 0.3f : 1.5f);
		}

		var vel = Nav.Velocity.WithZ( 0 );
		if ( vel.Length > 1f )
			WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( vel, Vector3.Up ), Time.Delta * 5f );
	}

	protected virtual void FleeFromTarget()
	{
		if ( CurrentTarget is null ) return;

		if ( Time.Now >= _nextPathUpdate )
		{
			var awayDir = (WorldPosition - CurrentTarget.WorldPosition).WithZ( 0 );
			if ( awayDir.Length < 1f ) awayDir = Vector3.Random.WithZ( 0 );

			var fleeTarget = WorldPosition + awayDir.Normal * 400f;
			Nav.MaxSpeed = MoveSpeed;
			Nav.MoveTo( fleeTarget );

			_nextPathUpdate = Time.Now + 0.3f;
		}

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
		_pendingDamageTimer = AttackHitDelay / AttackSpeed;
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

		if ( !IsValid || IsDead ) return;
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
		_attackAnimTimer = 0.8f / AttackSpeed;
		BroadcastAttackAnim( AttackSpeed );
	}

	[Rpc.Broadcast]
	private void BroadcastAttackAnim( float speed )
	{
		IsAttacking = true;
		_attackAnimTimer = 0.8f / speed;

		// Audio fires regardless of LOD model state — an enemy attacking off-screen
		// should still be audible. Animation setup below is the only LOD-gated bit.
		if ( AttackWindupSound is not null )
			Sound.Play( AttackWindupSound, WorldPosition );

		// Schedule the impact sound on this client, locked to the same animation start so
		// visual + audio stay in sync without a second network round-trip.
		if ( AttackImpactSound is not null )
			_impactSoundTimer = AttackHitDelay / speed;

		if ( _model is null || !_model.Enabled ) return;
		_model.Sequence.Name = "attack";
		_model.Sequence.Time = 0;
		_model.Sequence.Looping = false;
		_model.PlaybackRate = speed; // Speed up/slow down animation
		if ( _model.Sequence.Duration > 0 )
			_attackAnimTimer = _model.Sequence.Duration / speed;
	}

	private void TickAttackImpactSound()
	{
		if ( _impactSoundTimer <= 0f ) return;

		_impactSoundTimer -= Time.Delta;
		if ( _impactSoundTimer <= 0f && AttackImpactSound is not null )
		{
			Sound.Play( AttackImpactSound, WorldPosition );
		}
	}

	public void ApplyDamage( float amount, DamageType type, Component attacker )
	{
		if ( IsDead ) return;

		HealthCurrent = MathF.Max( 0, HealthCurrent - amount );

		if ( attacker is not null )
			RecordThreat( attacker.GameObject, amount );

		BroadcastHitFlash();

		if ( HealthCurrent <= 0 )
		{
			IsDead = true;
			OnDeath();
		}
	}

	[Rpc.Broadcast]
	private void BroadcastHitFlash()
	{
		_hitFlashTimer = HitFlashDuration;
		ApplyFlashOverride( true );

		if ( HitSound is not null )
			Sound.Play( HitSound, WorldPosition );
	}

	private void TickHitFlash()
	{
		if ( _hitFlashTimer <= 0f ) return;

		_hitFlashTimer -= Time.Delta;
		if ( _hitFlashTimer <= 0f )
		{
			ApplyFlashOverride( false );
		}
	}

	private void ApplyFlashOverride( bool on )
	{
		if ( _model is null || _flashMaterial is null ) return;
		if ( on == _flashApplied ) return; // already in the requested state

		// `< Attribute("FlashIntensity") >` in the shader binds to the renderer's RenderAttributes
		// table, NOT to a material parameter — so we have to push it on the renderer here, not on
		// the material in OnStart. Pushing it every time we toggle on lets HitFlashIntensity be
		// hot-tuned in the inspector at runtime.
		if ( on )
			_model.Attributes.Set( "FlashIntensity", HitFlashIntensity );

		// Per-slot override via MaterialAccessor — different code path from the global
		// MaterialOverride property, with explicit "set null to clear" semantics.
		var count = _model.Materials.Count;
		for ( int i = 0; i < count; i++ )
		{
			_model.Materials.SetOverride( i, on ? _flashMaterial : null );
		}
		_flashApplied = on;
	}

	// --- Inlined Aggro ---

	public void RecordThreat( GameObject source, float amount )
	{
		if ( source is null ) return;
		_threatTable.TryGetValue( source, out var current );
		_threatTable[source] = current + amount;
	}

	public float GetThreat( GameObject source )
	{
		return _threatTable.TryGetValue( source, out var t ) ? t : 0f;
	}

	private static readonly List<GameObject> _threatKeysToRemove = new();

	public void DecayThreat()
	{
		if ( _threatTable.Count == 0 ) return;

		_threatKeysToRemove.Clear();
		foreach ( var kvp in _threatTable )
		{
			if ( kvp.Value * ThreatDecayRate < 0.1f )
				_threatKeysToRemove.Add( kvp.Key );
		}

		foreach ( var key in _threatKeysToRemove )
			_threatTable.Remove( key );

		_threatKeysToRemove.Clear();
		foreach ( var kvp in _threatTable )
			_threatKeysToRemove.Add( kvp.Key );

		foreach ( var key in _threatKeysToRemove )
			_threatTable[key] *= ThreatDecayRate;
	}

	public RoguelitePlayer SelectAggroTarget( List<RoguelitePlayer> candidates )
	{
		if ( candidates.Count == 0 ) return null;

		RoguelitePlayer best = null;
		float bestScore = float.MinValue;

		foreach ( var p in candidates )
		{
			float score = AggroStrategy switch
			{
				TargetStrategy.HighestThreat => GetThreat( p.GameObject ),
				TargetStrategy.LowestHP => -p.HealthCurrent,
				TargetStrategy.Nearest => -WorldPosition.DistanceSquared( p.WorldPosition ),
				_ => GetThreat( p.GameObject )
			};

			if ( score > bestScore )
			{
				bestScore = score;
				best = p;
			}
		}

		return best;
	}

	// --- Nav Agent Budget ---
	// All chasing enemies stay on navmesh, but only the closest N
	// get frequent MoveTo updates. The rest follow their last path.

	// --- LOD ---

	private const float LodClose = 800f;
	private const float LodCull = 3000f;
	private int _lodFrameCounter;
	private bool _shouldAnimate;
	private bool _isClose;

	// Shared enemy density counter — updated once per frame by first enemy to tick
	private static int _frameId;
	private static int _nearbyEnemyCount;

	private void UpdateLOD()
	{
		if ( _model is null ) return;

		var cam = Scene.Camera;
		if ( cam is null ) return;

		_lodFrameCounter++;

		// Reset density counter once per frame
		var currentFrame = (int)(Time.Now * 60);
		if ( _frameId != currentFrame )
		{
			_frameId = currentFrame;
			_nearbyEnemyCount = 0;
		}

		var distSq = WorldPosition.DistanceSquared( cam.WorldPosition );

		// Viewport check
		var toEnemy = (WorldPosition - cam.WorldPosition).Normal;
		var inView = Vector3.Dot( cam.WorldRotation.Forward, toEnemy ) > -0.2f;

		if ( distSq > LodCull * LodCull || !inView )
		{
			_model.Enabled = false;
			_shouldAnimate = false;
			_isClose = false;
			return;
		}

		_model.Enabled = true;
		_isClose = distSq < LodClose * LodClose;

		if ( _isClose )
			_nearbyEnemyCount++;

		// Animation rate scales with density — more enemies = lower rate
		// 1-10 nearby: every 2 frames, 10-30: every 4, 30+: every 8
		int animInterval;
		if ( _isClose )
			animInterval = _nearbyEnemyCount > 30 ? 8 : _nearbyEnemyCount > 10 ? 4 : 2;
		else
			animInterval = 8;

		_shouldAnimate = _lodFrameCounter % animInterval == 0;
	}

	// --- Knockback ---

	private bool _inKnockback;
	private float _knockbackTimer;
	private Rigidbody _knockbackRb;

	public void ApplyKnockback( Vector3 direction, float force )
	{
		if ( !Networking.IsHost ) return;
		if ( IsDead ) return;
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
			if ( Nav.IsValid() )
			{
				Nav.SetAgentPosition( WorldPosition );
				Nav.UpdatePosition = true;
			}
		}
	}

	// --- Separation ---

	protected virtual void SeparateFromOtherEnemies()
	{
		var minDist = Nav.Radius * 3f;

		foreach ( var other in Scene.GetAllComponents<RogueliteEnemyBase>() )
		{
			if ( other == this || other.IsDead ) continue;

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
	private void BroadcastDeath()
	{
		if ( DeathSound is not null )
			Sound.Play( DeathSound, WorldPosition );
	}

	// --- Animation ---

	protected virtual void UpdateAnimation()
	{
		if ( _model is null || !_model.Enabled ) return;

		// Attack anim playing — let it finish
		if ( IsAttacking )
		{
			_attackAnimTimer -= Time.Delta;
			if ( _attackAnimTimer <= 0 )
			{
				IsAttacking = false;
				_model.PlaybackRate = 1f; // Reset to normal speed
			}
			return;
		}

		// Use synced IsMoving flag so clients can animate too
		if ( Networking.IsHost && Nav.IsValid() && Nav.Enabled )
			IsMoving = Nav.Velocity.WithZ( 0 ).Length > 5f;

		string desired = IsMoving ? "walk_N" : "idle";

		if ( _model.Sequence.Name != desired )
		{
			_model.PlaybackRate = 1f;
			_model.Sequence.Name = desired;
			_model.Sequence.Time = 0;
			_model.Sequence.Looping = true;
		}
	}
}
