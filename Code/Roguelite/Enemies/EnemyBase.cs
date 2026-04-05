/// <summary>
/// Base enemy component. Uses NavMeshAgent for pathfinding direction only.
/// We drive WorldPosition ourselves to avoid NavAgent snapping/teleporting.
/// </summary>
[Title( "Roguelite Enemy" )]
[Icon( "pest_control" )]
public class RogueliteEnemyBase : Component
{
	[RequireComponent] public NavMeshAgent Nav { get; set; }
	[RequireComponent] public RogueliteHealthComponent Health { get; set; }
	[RequireComponent] public FactionComponent Faction { get; set; }

	[Property] public float AttackDamage { get; set; } = 25f;
	[Property] public float AttackRange { get; set; } = 120f;
	[Property] public float StopDistance { get; set; } = 100f;
	[Property] public float DetectionRange { get; set; } = 1200f;
	[Property] public float AttackCooldown { get; set; } = 1.5f;
	[Property] public float MoveSpeed { get; set; } = 150f;
	[Property] public string EnemyName { get; set; } = "Enemy";

	[Sync] public bool IsStunned { get; set; }

	protected RoguelitePlayer CurrentTarget;
	private float _attackTimer;
	private float _stunTimer;
	private SkinnedModelRenderer _model;

	// Knockback state
	private bool _inKnockback;
	private Vector3 _knockVelocity;

	protected override void OnStart()
	{
		Faction.Faction = global::Faction.Enemy;

		Health.Init( Health.MaxHealth );
		Health.OnDeath += OnDeath;

		_model = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );

		// We drive position ourselves — NavAgent is only for pathfinding direction
		Nav.UpdatePosition = false;
		Nav.UpdateRotation = false;

		GameObject.Name = EnemyName;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( Health.IsDead ) return;

		// Knockback takes priority over everything
		if ( _inKnockback )
		{
			UpdateKnockback();

			// Slowly turn toward player while airborne
			if ( CurrentTarget is not null )
			{
				var dirToTarget = (CurrentTarget.WorldPosition - WorldPosition).WithZ( 0 );
				if ( dirToTarget.Length > 1f )
					WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( dirToTarget, Vector3.Up ), Time.Delta * 2f );
			}
			return;
		}

		if ( IsStunned )
		{
			_stunTimer -= Time.Delta;
			if ( _stunTimer <= 0 )
				IsStunned = false;
			return;
		}

		_attackTimer = MathF.Max( 0, _attackTimer - Time.Delta );

		FindTarget();

		if ( CurrentTarget is not null && CurrentTarget.IsAlive )
		{
			var distance = WorldPosition.Distance( CurrentTarget.WorldPosition );
			var dirToTarget = (CurrentTarget.WorldPosition - WorldPosition).WithZ( 0 );

			if ( distance <= AttackRange )
			{
				// In attack range — smoothly face target, attack if ready
				if ( dirToTarget.Length > 1f )
					WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( dirToTarget, Vector3.Up ), Time.Delta * 5f );

				if ( _attackTimer <= 0 )
				{
					PerformAttack( CurrentTarget );
					_attackTimer = AttackCooldown;
				}
			}
			else if ( distance <= DetectionRange )
			{
				// Chase — use NavAgent for direction, move ourselves
				Nav.MoveTo( CurrentTarget.WorldPosition );

				var wishDir = Nav.WishVelocity;
				if ( wishDir.Length > 1f )
				{
					WorldPosition += wishDir.Normal * MoveSpeed * Time.Delta;
					WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( wishDir.WithZ( 0 ), Vector3.Up ), Time.Delta * 5f );
				}

				// Keep NavAgent in sync with our position
				Nav.SetAgentPosition( WorldPosition );
			}
		}

		if ( !_inKnockback )
			StickToGround();
		UpdateAnimation();
	}

	private void FindTarget()
	{
		if ( CurrentTarget is not null && CurrentTarget.IsAlive )
		{
			var dist = WorldPosition.Distance( CurrentTarget.WorldPosition );
			if ( dist <= DetectionRange ) return;
		}

		CurrentTarget = Scene.GetAllComponents<RoguelitePlayer>()
			.Where( p => p.IsAlive )
			.OrderBy( p => WorldPosition.Distance( p.WorldPosition ) )
			.FirstOrDefault();
	}

	protected virtual void PerformAttack( RoguelitePlayer target )
	{
		var attack = new AttackData( AttackDamage, DamageType.Blunt );

		var ctx = new HitContext(
			WorldPosition,
			(target.WorldPosition - WorldPosition).Normal,
			target.WorldPosition,
			Vector3.Up,
			WorldPosition.Distance( target.WorldPosition ),
			false,
			false );

		DamageResolver.Resolve( attack, this, target.GameObject, ctx );
		BroadcastAttack();
	}

	[Rpc.Broadcast]
	private void BroadcastAttack() { }

	// --- Knockback ---

	public void ApplyKnockback( Vector3 direction, float force )
	{
		if ( !Networking.IsHost ) return;
		if ( Health.IsDead ) return;

		_inKnockback = true;
		_knockVelocity = direction * force * 8f;
	}

	private void UpdateKnockback()
	{
		// Gravity
		_knockVelocity -= Vector3.Up * 800f * Time.Delta;

		// Move in small steps to avoid overshooting the ground
		var movement = _knockVelocity * Time.Delta;
		var newPos = WorldPosition + movement;

		// Ground check — trace from well above to well below
		var tr = Scene.Trace
			.Ray( newPos + Vector3.Up * 50f, newPos + Vector3.Down * 500f )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger" )
			.Run();

		if ( tr.Hit && _knockVelocity.z < 0 && newPos.z <= tr.HitPosition.z )
		{
			// Don't go below ground — lerp to landing
			newPos = newPos.WithZ( tr.HitPosition.z );

			// Bounce or stop
			_knockVelocity = _knockVelocity.WithZ( 0 );

			if ( _knockVelocity.WithZ( 0 ).Length < 20f )
			{
				_inKnockback = false;
				Nav.SetAgentPosition( newPos );
			}
		}

		WorldPosition = newPos;

		// Horizontal drag
		var hz = _knockVelocity.WithZ( 0 ) * 0.95f;
		_knockVelocity = hz.WithZ( _knockVelocity.z );
	}

	// --- Ground stick ---

	private void StickToGround()
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

	private void UpdateAnimation()
	{
		if ( _model is null ) return;

		var vel = Nav.WishVelocity;
		var forward = WorldRotation.Forward.Dot( vel );
		var sideward = WorldRotation.Right.Dot( vel );
		var angle = MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees();

		_model.Set( "move_direction", angle );
		_model.Set( "move_speed", vel.Length );
		_model.Set( "move_groundspeed", vel.WithZ( 0 ).Length );
		_model.Set( "move_y", sideward );
		_model.Set( "move_x", forward );
	}
}
