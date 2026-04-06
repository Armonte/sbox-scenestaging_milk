/// <summary>
/// Aerial enemy. Hovers above the ground and uses direct 3D movement instead of NavMeshAgent.
/// Has ranged projectile attacks and a swoop dive attack.
/// </summary>
[Title( "Flying Enemy" )]
[Icon( "flight" )]
public class FlyingEnemy : RogueliteEnemyBase
{
	[Property] public float HoverHeight { get; set; } = 200f;
	[Property] public float FlySpeed { get; set; } = 200f;
	[Property] public float ProjectileDamage { get; set; } = 15f;
	[Property] public float ProjectileSpeed { get; set; } = 1500f;
	[Property] public float SwoopSpeed { get; set; } = 600f;
	[Property] public float SwoopCooldown { get; set; } = 8f;
	[Property] public float SwoopDamageMultiplier { get; set; } = 2.5f;

	[Property] public float BobAmplitude { get; set; } = 12f;
	[Property] public float BobFrequency { get; set; } = 1.5f;

	private readonly CooldownTracker _cooldowns = new();
	private bool _isSwooping;
	private bool _swoopHasHit;
	private Vector3 _swoopStartPos;
	private Vector3 _swoopPassThroughPos;
	private Vector3 _swoopDirection;
	private float _swoopProgress;
	private const float SwoopDuration = 1.2f;
	private float _bobTimer;

	protected override void OnStart()
	{
		base.OnStart();
		EnemyName = "Flyer";
		GameObject.Name = EnemyName;

		// Disable NavMeshAgent — we handle our own 3D movement
		Nav.Enabled = false;

		// Set higher detection and attack ranges
		AttackRange = 800f;
		DetectionRange = 1500f;
	}

	private float _bobOffset;

	protected override void OnUpdate()
	{
		_cooldowns.Tick( Time.Delta );
		_bobTimer += Time.Delta;
		_bobOffset = MathF.Sin( _bobTimer * BobFrequency * MathF.PI * 2f ) * BobAmplitude;

		// Drive swoop movement every frame regardless of brain state
		if ( _isSwooping && Networking.IsHost && !Health.IsDead )
		{
			UpdateSwoop();
			UpdateAnimation();
			return;
		}

		base.OnUpdate();

		// Apply bob after base movement (so it stacks on top of hover position)
		if ( !_isSwooping && Networking.IsHost && !Health.IsDead )
			WorldPosition += Vector3.Up * _bobOffset * Time.Delta * 5f;
	}

	/// <summary>
	/// Flying enemies hover toward the target. Not used during swoop.
	/// </summary>
	protected override void ChaseTarget()
	{
		if ( CurrentTarget is null ) return;

		var targetPos = CurrentTarget.WorldPosition + Vector3.Up * HoverHeight;
		var dir = (targetPos - WorldPosition);

		if ( dir.Length > 10f )
		{
			WorldPosition += dir.Normal * FlySpeed * Time.Delta;
			WorldRotation = Rotation.Lerp( WorldRotation,
				Rotation.LookAt( (CurrentTarget.WorldPosition - WorldPosition).WithZ( 0 ), Vector3.Up ),
				Time.Delta * 3f );
		}
	}



	/// <summary>
	/// Attack handles projectiles, swoop initiation, AND swoop movement all in one place.
	/// This runs every frame while in Attack state (brain sees player within 800 range).
	/// </summary>
	protected override void PerformAttack( RoguelitePlayer target )
	{
		// Maintain hover position while attacking
		var hoverTarget = target.WorldPosition + Vector3.Up * HoverHeight;
		var hoverDir = (hoverTarget - WorldPosition);
		if ( hoverDir.Length > 30f )
			WorldPosition += hoverDir.Normal * FlySpeed * 0.5f * Time.Delta;

		var dist = WorldPosition.Distance( target.WorldPosition );

		// Swoop attack if close-ish and off cooldown
		if ( dist < 500f && _cooldowns.IsReady( "swoop" ) )
		{
			StartSwoop( target );
			return;
		}

		// Ranged projectile attack
		FireProjectile( target );
	}

	private void FireProjectile( RoguelitePlayer target )
	{
		var aimPoint = target.WorldPosition + Vector3.Up * 50f;
		var dir = (aimPoint - WorldPosition).Normal;
		var attack = new AttackData( ProjectileDamage, DamageType.Pierce );

		ProjectileBase.Spawn( Scene, WorldPosition, Rotation.LookAt( dir, Vector3.Up ),
			attack, this, ProjectileSpeed, 0f );
	}

	private void StartSwoop( RoguelitePlayer target )
	{
		_isSwooping = true;
		_swoopHasHit = false;
		_swoopProgress = 0f;
		_swoopStartPos = WorldPosition;

		// Fly THROUGH the player and out the other side
		_swoopDirection = (target.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
		_swoopPassThroughPos = target.WorldPosition + Vector3.Up * 50f;

		// Pull back slightly before diving (wind-up)
		_swoopStartPos += Vector3.Up * 30f + (-_swoopDirection) * 40f;
		WorldPosition = WorldPosition; // stay put, the arc will handle it

		_cooldowns.Start( "swoop", SwoopCooldown );
	}

	private void UpdateSwoop()
	{
		_swoopProgress += Time.Delta / SwoopDuration;

		if ( _swoopProgress >= 1f )
		{
			_isSwooping = false;
			return;
		}

		// t=0.00–0.15: wind-up (slow ease-in, barely moves)
		// t=0.15–0.50: dive (accelerating down to player chest)
		// t=0.50–1.00: fly past and curve back up
		var t = _swoopProgress;

		// Ease-in: slow start, ramps into the dive
		// Cubic ease-in gives a nice "coiling then pouncing" feel
		var easedT = t * t * t / (0.15f * 0.15f * 0.15f); // fast ramp after wind-up
		easedT = MathF.Min( t < 0.15f ? t * t * (1f / 0.15f) : t, 1f ); // gentle wind-up, then full speed

		// Horizontal: lerp from start, through player, and keep going past
		var totalHorizontalDist = _swoopStartPos.WithZ( 0 ).Distance( _swoopPassThroughPos.WithZ( 0 ) );
		var overshoot = totalHorizontalDist * 0.8f;
		var endPos = _swoopPassThroughPos + _swoopDirection * overshoot;
		var horizontalPos = Vector3.Lerp( _swoopStartPos, endPos, easedT );

		// Vertical: parabolic dip — lowest at t=0.45
		var peakT = 0.45f;
		var startZ = _swoopStartPos.z;
		var lowestZ = _swoopPassThroughPos.z;
		var endZ = startZ;

		float currentZ;
		if ( easedT < peakT )
		{
			// Descending — ease in (slow→fast)
			var descend = easedT / peakT;
			currentZ = MathX.Lerp( startZ, lowestZ, descend * descend );
		}
		else
		{
			// Ascending — ease out (fast→slow)
			var ascend = (easedT - peakT) / (1f - peakT);
			currentZ = MathX.Lerp( lowestZ, endZ, ascend * ascend );
		}

		var newPos = horizontalPos.WithZ( currentZ );

		// Hit detection along the movement
		var moveDir = (newPos - WorldPosition);
		if ( moveDir.Length > 0.1f )
		{
			var hit = Scene.Trace
				.Ray( WorldPosition, newPos )
				.Size( 25f )
				.IgnoreGameObjectHierarchy( GameObject )
				.WithoutTags( "trigger", "projectile" )
				.Run();

			if ( hit.Hit && hit.GameObject is not null && !_swoopHasHit )
			{
				var attack = new AttackData( AttackDamage * SwoopDamageMultiplier, DamageType.Slash,
						canKnockback: true, knockbackForce: 300f );
				var ctx = HitContext.FromTrace( hit, this );
				DamageResolver.Resolve( attack, this, hit.GameObject, ctx );
				_swoopHasHit = true;
			}

			WorldRotation = Rotation.LookAt( moveDir.Normal, Vector3.Up );
		}

		WorldPosition = newPos;
	}

	protected override void StickToGround() { }

	protected override void SeparateFromOtherEnemies()
	{
		foreach ( var other in Scene.GetAllComponents<FlyingEnemy>() )
		{
			if ( other == this || other.Health.IsDead ) continue;

			var diff = (WorldPosition - other.WorldPosition).WithZ( 0 );
			var dist = diff.Length;
			var minDist = 80f;

			if ( dist < minDist && dist > 0.1f )
			{
				WorldPosition += diff.Normal * (minDist - dist) * 2f * Time.Delta;
			}
		}
	}
}
