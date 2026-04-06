/// <summary>
/// Melee charger enemy. Sprints toward the player and deals bonus damage on charge impact.
/// Falls back to normal melee when charge is on cooldown.
/// </summary>
[Title( "Rusher Enemy" )]
[Icon( "directions_run" )]
public class RusherEnemy : RogueliteEnemyBase
{
	[Property] public float ChargeSpeed { get; set; } = 500f;
	[Property] public float ChargeRange { get; set; } = 1500f;
	[Property] public float ChargeDamageMultiplier { get; set; } = 2f;
	[Property] public float ChargeCooldown { get; set; } = 5f;
	[Property] public float ChargeImpactRadius { get; set; } = 60f;

	private readonly CooldownTracker _chargeCooldown = new();
	private bool _isCharging;
	private Vector3 _chargeDirection;
	private float _chargeDistanceTraveled;
	private float _maxChargeDistance;

	protected override void OnStart()
	{
		base.OnStart();
		EnemyName = "Rusher";
		GameObject.Name = EnemyName;
		DetectionRange = 2000f;
	}

	/// <summary>
	/// Charge initiates during Chase, not Attack — because the rusher needs to be
	/// farther than AttackRange to start a charge.
	/// </summary>
	protected override void ChaseTarget()
	{
		_chargeCooldown.Tick( Time.Delta );

		if ( _isCharging )
		{
			UpdateCharge();
			return;
		}

		// Check if we should start a charge while chasing
		if ( CurrentTarget is not null && _chargeCooldown.IsReady( "charge" ) )
		{
			var dist = WorldPosition.Distance( CurrentTarget.WorldPosition );
			if ( dist <= ChargeRange && dist > AttackRange )
			{
				StartCharge( CurrentTarget );
				return;
			}
		}

		base.ChaseTarget();
	}

	protected override void PerformAttack( RoguelitePlayer target )
	{
		// Normal melee when in close range
		base.PerformAttack( target );
	}

	private void StartCharge( RoguelitePlayer target )
	{
		_isCharging = true;
		_chargeDirection = (target.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
		_chargeDistanceTraveled = 0;
		_maxChargeDistance = WorldPosition.Distance( target.WorldPosition ) + 100f;
		_chargeCooldown.Start( "charge", ChargeCooldown );
	}

	private void UpdateCharge()
	{
		var moveAmount = ChargeSpeed * Time.Delta;
		WorldPosition += _chargeDirection * moveAmount;
		_chargeDistanceTraveled += moveAmount;

		WorldRotation = Rotation.LookAt( _chargeDirection, Vector3.Up );

		// Check for impact with players
		var hit = Scene.Trace
			.Ray( WorldPosition, WorldPosition + _chargeDirection * ChargeImpactRadius )
			.Size( 30f )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( hit.Hit && hit.GameObject is not null )
		{
			var player = hit.GameObject.Components.Get<RoguelitePlayer>( FindMode.EverythingInSelfAndDescendants );
			if ( player is not null && player.IsAlive )
			{
				var attack = new AttackData( AttackDamage * ChargeDamageMultiplier, DamageType.Blunt,
					canKnockback: true, knockbackForce: 400f );

				var ctx = new HitContext(
					WorldPosition,
					_chargeDirection,
					player.WorldPosition,
					Vector3.Up,
					WorldPosition.Distance( player.WorldPosition ),
					false, false );

				DamageResolver.Resolve( attack, this, player.GameObject, ctx );
				EndCharge();
				return;
			}
		}

		// End charge if we've gone far enough
		if ( _chargeDistanceTraveled >= _maxChargeDistance )
			EndCharge();

		Nav.SetAgentPosition( WorldPosition );
	}

	private void EndCharge()
	{
		_isCharging = false;
	}

	protected override void UpdateAnimation()
	{
		if ( _isCharging )
		{
			var model = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
			if ( model is not null )
			{
				if ( model.Sequence.Name != "run_N" )
				{
					model.Sequence.Name = "run_N";
					model.Sequence.Time = 0;
					model.Sequence.Looping = true;
				}
			}
			return;
		}

		base.UpdateAnimation();
	}
}
