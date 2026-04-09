/// <summary>
/// Enemy that spawns minions instead of directly attacking. Keeps distance from players.
/// When max minions are alive, falls back to a weak ranged attack.
/// </summary>
[Title( "Summoner Enemy" )]
[Icon( "group_add" )]
public class SummonerEnemy : RogueliteEnemyBase
{
	[Property] public GameObject MinionPrefab { get; set; }
	[Property] public int MaxMinions { get; set; } = 3;
	[Property] public float SummonCooldown { get; set; } = 4f;
	[Property] public float SummonRange { get; set; } = 200f;
	[Property] public float FleeDistance { get; set; } = 400f;
	[Property] public float FallbackProjectileDamage { get; set; } = 10f;
	[Property] public float CastTime { get; set; } = 1.2f;
	[Property] public float SummonConeAngle { get; set; } = 45f;

	private readonly List<GameObject> _activeMinions = new();
	private readonly CooldownTracker _cooldowns = new();
	private bool _isCasting;
	private bool _isRecovering;
	private float _castTimer;
	private Vector3 _summonSpawnPos;
	private GameObject _summonIndicator;

	// Point animation: 26 frames at 30fps = 0.867s
	// Frame 11 = hold point, frames 12-26 = followthrough recovery
	private const float PointHoldTime = 11f / 30f; // ~0.367s
	private const float PointTotalTime = 26f / 30f; // ~0.867s

	protected override void OnStart()
	{
		base.OnStart();
		EnemyName = "Summoner";
		GameObject.Name = EnemyName;

		AttackRange = 1000f;
		DetectionRange = 1500f;
		FleeDistance = 400f;
	}

	protected override EnemyBrain CreateBrain() => new SummonerBrain( this );

	protected override void OnUpdate()
	{
		_cooldowns.Tick( Time.Delta );
		CleanupDeadMinions();

		// Casting: play point anim to frame 11, hold until cast finishes
		if ( _isCasting && Networking.IsHost && !IsDead )
		{
			Nav.Stop();

			var toSpawn = (_summonSpawnPos - WorldPosition).WithZ( 0 );
			if ( toSpawn.Length > 1f )
				WorldRotation = Rotation.Lerp( WorldRotation, Rotation.LookAt( toSpawn, Vector3.Up ), Time.Delta * 8f );

			_castTimer -= Time.Delta;

			// Expand decal during cast
			ExpandIndicator( _castTimer / CastTime );

			if ( _castTimer <= 0 )
			{
				CompleteSummon();
				// Let the recovery frames play (12-26), then return to animgraph
				// DirectPlayback auto-blends back
				_isCasting = false;
				_isRecovering = true;
			}

			// Ensure point sequence is playing and hold at frame 11
			var model = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
			if ( model is not null )
			{
				if ( model.Sequence.Name != "point" )
				{
					model.Sequence.Name = "point";
					model.Sequence.Time = 0;
					model.Sequence.Looping = false;
				}
				if ( model.Sequence.Time >= PointHoldTime )
					model.Sequence.Time = PointHoldTime;
			}
			return;
		}

		// Recovery: brief pause after summon before resuming AI
		if ( _isRecovering && Networking.IsHost && !IsDead )
		{
			Nav.Stop();
			_castTimer -= Time.Delta;
			if ( _castTimer <= 0 )
			{
				_isRecovering = false;
			}
			return;
		}

		base.OnUpdate();
	}

	protected override void PerformAttack( RoguelitePlayer target )
	{
		// Don't start new actions while casting
		if ( _isCasting ) return;

		// Summon if we can — starts a cast, doesn't instant summon
		if ( _activeMinions.Count < MaxMinions && _cooldowns.IsReady( "summon" ) )
		{
			StartSummonCast();
			return;
		}

		// Fallback: weak projectile with cast animation
		if ( _cooldowns.IsReady( "projectile" ) )
		{
			FireFallbackProjectile( target );
			PlayCastAnim();
			_cooldowns.Start( "projectile", AttackCooldown );
		}
	}

	private void StartSummonCast()
	{
		_isCasting = true;
		_castTimer = CastTime;
		_cooldowns.Start( "summon", SummonCooldown );

		// Pick a random spot in a cone in front of us
		var halfAngle = SummonConeAngle * 0.5f;
		var randomAngle = Game.Random.Float( -halfAngle, halfAngle );
		var spawnDir = (WorldRotation * Rotation.FromYaw( randomAngle )).Forward.WithZ( 0 ).Normal;
		var spawnDist = Game.Random.Float( SummonRange * 0.5f, SummonRange );
		_summonSpawnPos = WorldPosition + spawnDir * spawnDist;

		// Don't snap rotation — the OnUpdate lerp will handle turning
		PlayPointAnim();

		// Show spawn indicator on the ground — red ring + expanding decal
		DestroySummonIndicator();
		var groundPos = SnapToGround( _summonSpawnPos );
		_summonIndicator = Scene.CreateObject();
		_summonIndicator.Name = "summon_indicator";
		_summonIndicator.WorldPosition = groundPos;

		// Red ring
		var ring = CreateIndicatorRing( groundPos, 60f, Color.Red.WithAlpha( 0.6f ) );
		ring.SetParent( _summonIndicator );

		// Decal — starts tiny, will expand during cast
		var decalObj = Scene.CreateObject();
		decalObj.Name = "summon_decal";
		decalObj.WorldPosition = groundPos + Vector3.Up * 50f;
		decalObj.WorldRotation = Rotation.FromPitch( 90f );
		decalObj.LocalScale = Vector3.One * 0.1f; // start tiny
		decalObj.SetParent( _summonIndicator );

		var decal = decalObj.Components.Create<Decal>();
		decal.Size = new Vector2( 1f, 1f );
		decal.Depth = 100f;
		decal.Scale = 1f;
		decal.Decals = new List<DecalDefinition> { ResourceLibrary.Get<DecalDefinition>( "decals/circle.decal" ) };
	}

	private void CompleteSummon()
	{
		if ( MinionPrefab is null )
		{
			Log.Warning( "[SummonerEnemy] No MinionPrefab assigned" );
			return;
		}

		var minion = MinionPrefab.Clone( _summonSpawnPos );
		minion.NetworkSpawn();
		_activeMinions.Add( minion );

		// Fade minion in, then fade indicator out
		_ = FadeMinionThenIndicator( minion );

		Log.Info( $"[Summoner] Spawned minion {_activeMinions.Count}/{MaxMinions}" );
	}

	private async Task FadeMinionThenIndicator( GameObject minion )
	{
		// Phase 1: fade minion in
		var renderer = minion.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		if ( renderer is not null )
		{
			var duration = 0.6f;
			var elapsed = 0f;
			var originalScale = minion.LocalScale;
			minion.LocalScale = originalScale * 0.1f;
			renderer.Tint = renderer.Tint.WithAlpha( 0f );

			while ( elapsed < duration && minion.IsValid() )
			{
				elapsed += Time.Delta;
				var t = MathF.Min( 1f, elapsed / duration );
				var eased = 1f - (1f - t) * (1f - t);

				minion.LocalScale = originalScale * eased;
				renderer.Tint = renderer.Tint.WithAlpha( eased );
				await Task.Frame();
			}

			if ( minion.IsValid() )
			{
				minion.LocalScale = originalScale;
				renderer.Tint = renderer.Tint.WithAlpha( 1f );
			}
		}

		// Phase 2: now fade out the indicator
		await FadeOutIndicator();
	}

	// The decal at scale 5 roughly fills a 60-unit ring
	private const float DecalFullScale = 5f;

	private void ExpandIndicator( float normalizedTimeRemaining )
	{
		if ( !_summonIndicator.IsValid() ) return;

		// Expand decal from tiny to full as cast progresses
		var castProgress = 1f - normalizedTimeRemaining; // 0→1
		var currentScale = MathF.Max( 0.1f, castProgress * DecalFullScale );

		foreach ( var child in _summonIndicator.Children )
		{
			if ( child.Name == "summon_decal" )
				child.LocalScale = Vector3.One * currentScale;
		}
	}

	private async Task FadeOutIndicator()
	{
		if ( !_summonIndicator.IsValid() ) return;

		var duration = 0.5f;
		var elapsed = 0f;

		while ( elapsed < duration && _summonIndicator.IsValid() )
		{
			elapsed += Time.Delta;
			var alpha = 1f - (elapsed / duration);

			foreach ( var lr in _summonIndicator.Components.GetAll<LineRenderer>( FindMode.EverythingInSelfAndDescendants ) )
			{
				var color = Color.Red.WithAlpha( 0.6f * alpha );
				lr.Color = new Gradient( new Gradient.ColorFrame( 0, color ), new Gradient.ColorFrame( 1, color ) );
			}

			foreach ( var child in _summonIndicator.Children )
			{
				if ( child.Name == "summon_decal" )
					child.LocalScale = Vector3.One * DecalFullScale * alpha;
			}

			await Task.Frame();
		}

		DestroySummonIndicator();
	}

	protected override void FleeFromTarget()
	{
		// Don't summon while fleeing — just run
		base.FleeFromTarget();
	}

	private bool _isCastingProjectile;
	private float _castAnimTimer;

	private void PlayPointAnim()
	{
		var model = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		if ( model is null ) return;
		model.Sequence.Name = "point";
		model.Sequence.Time = 0;
		model.Sequence.Looping = false;
	}

	private SkinnedModelRenderer GetModel() =>
		Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );

	private void PlayCastAnim()
	{
		var model = GetModel();
		if ( model is null ) return;
		model.Sequence.Name = "attack_weapon";
		model.Sequence.Time = 0;
		model.Sequence.Looping = false;
		_isCastingProjectile = true;
		_castAnimTimer = model.Sequence.Duration > 0 ? model.Sequence.Duration : 0.8f;
	}

	protected override void UpdateAnimation()
	{
		if ( _isCasting ) return;

		if ( _isCastingProjectile )
		{
			_castAnimTimer -= Time.Delta;
			if ( _castAnimTimer <= 0 )
				_isCastingProjectile = false;
			return;
		}

		var model = GetModel();
		if ( model is null ) return;

		var speed = Nav.Velocity.WithZ( 0 ).Length;
		string desired;

		if ( Brain?.State == EnemyBrainState.Flee && speed > 5f )
			desired = "run_N";
		else if ( speed > 5f )
			desired = "walk_N";
		else
			desired = "idle";

		if ( model.Sequence.Name != desired )
		{
			model.Sequence.Name = desired;
			model.Sequence.Time = 0;
			model.Sequence.Looping = true;
		}
	}

	private void FireFallbackProjectile( RoguelitePlayer target )
	{
		var aimPoint = target.WorldPosition + Vector3.Up * 64f;
		var firePos = WorldPosition + Vector3.Up * 50f;
		var dir = (aimPoint - firePos).Normal;
		var attack = new AttackData( FallbackProjectileDamage, DamageType.Arcane );

		ProjectileBase.Spawn( Scene, firePos,
			Rotation.LookAt( dir, Vector3.Up ), attack, this, 1200f, 0f );
	}

	private void CleanupDeadMinions()
	{
		_activeMinions.RemoveAll( m => m is null || !m.IsValid() );
	}

	private GameObject CreateIndicatorRing( Vector3 center, float radius, Color color )
	{
		var obj = Scene.CreateObject();
		obj.Name = "summon_ring";
		obj.WorldPosition = center;

		var line = obj.Components.Create<LineRenderer>();
		line.UseVectorPoints = true;
		line.Opaque = false;
		line.Color = new Gradient( new Gradient.ColorFrame( 0, color ), new Gradient.ColorFrame( 1, color ) );
		line.Width = new Curve( new Curve.Frame( 0, 3f ), new Curve.Frame( 1, 3f ) );

		var segments = 32;
		var points = new List<Vector3>();
		for ( int i = 0; i <= segments; i++ )
		{
			var angle = (float)i / segments * MathF.PI * 2f;
			points.Add( center + new Vector3( MathF.Cos( angle ) * radius, MathF.Sin( angle ) * radius, 2f ) );
		}
		line.VectorPoints = points;

		return obj;
	}

	private Vector3 SnapToGround( Vector3 point )
	{
		var tr = Scene.Trace
			.Ray( point + Vector3.Up * 500f, point + Vector3.Down * 500f )
			.UseHitboxes( false )
			.Run();

		return tr.Hit ? tr.HitPosition : point.WithZ( WorldPosition.z );
	}

	private void DestroySummonIndicator()
	{
		if ( _summonIndicator.IsValid() )
		{
			_summonIndicator.Destroy();
			_summonIndicator = null;
		}
	}

	protected override void OnDeath()
	{
		DestroySummonIndicator();
		_activeMinions.Clear();
		base.OnDeath();
	}

	private class SummonerBrain : EnemyBrain
	{
		private readonly SummonerEnemy _summoner;
		private bool _isFleeing;

		public SummonerBrain( SummonerEnemy owner ) : base( owner )
		{
			_summoner = owner;
		}

		protected override bool ShouldFlee()
		{
			if ( _summoner.CurrentTarget is null ) { _isFleeing = false; return false; }

			// Don't flee until we have at least one minion — need to summon first
			if ( _summoner._activeMinions.Count == 0 ) { _isFleeing = false; return false; }

			var dist = _summoner.WorldPosition.Distance( _summoner.CurrentTarget.WorldPosition );

			// Hysteresis: start fleeing at 400, keep fleeing until 800
			if ( _isFleeing )
				_isFleeing = dist < _summoner.FleeDistance * 2f;
			else
				_isFleeing = dist < _summoner.FleeDistance;

			return _isFleeing;
		}
	}
}
