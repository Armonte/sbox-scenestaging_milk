/// <summary>
/// Debug tool for testing all roguelite systems. Drop on a GameObject in the scene.
/// Hold T + press a key to activate. T is the modifier so nothing fires during normal play.
///
/// Controls (hold T + press):
///   T+1 — Spawn basic enemy at crosshair
///   T+2 — Spawn rusher at crosshair
///   T+3 — Spawn flyer at crosshair
///   T+4 — Spawn summoner at crosshair
///   T+5 — Explosion at crosshair
///   T+6 — Fire damage zone at crosshair (8s)
///   T+7 — Hitscan shotgun blast
///   T+8 — Projectile ring from crosshair
///   T+9 — Homing volley at nearest enemy
///   T+0 — Toggle beam on/off
///   T+H — Heal player to full
///   T+K — Kill all enemies
///   T+G — Give CubeAbility to slot 1
/// </summary>
[Title( "Roguelite Debug" )]
[Icon( "bug_report" )]
public sealed class RogueliteDebug : Component
{
	[Property] public GameObject EnemyPrefab { get; set; }
	[Property] public GameObject RusherPrefab { get; set; }
	[Property] public GameObject FlyerPrefab { get; set; }
	[Property] public GameObject SummonerPrefab { get; set; }
	[Property] public GameObject MinionPrefab { get; set; }

	private Beam _testBeam;
	private RoguelitePlayer _localPlayer;

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		// Find local player
		if ( _localPlayer is null || !_localPlayer.IsValid() )
		{
			_localPlayer = Scene.GetAllComponents<RoguelitePlayer>()
				.FirstOrDefault( p => !p.IsProxy );
			if ( _localPlayer is null ) return;
		}

		// T is our modifier key — must be held
		if ( !Input.Down( "use" ) ) return;

		var eyePos = _localPlayer.WorldPosition + Vector3.Up * _localPlayer.Camera.EyeHeight;
		var forward = _localPlayer.Camera.EyeAngles.ToRotation().Forward;
		var aimPoint = GetAimPoint( eyePos, forward );

		// --- Enemy Spawning (T + 1-4) ---
		if ( Pressed( "1" ) )
			SpawnEnemy( EnemyPrefab, aimPoint, "Enemy" );
		if ( Pressed( "2" ) )
			SpawnEnemy( RusherPrefab, aimPoint, "Rusher" );
		if ( Pressed( "3" ) )
			SpawnEnemy( FlyerPrefab, aimPoint, "Flyer" );
		if ( Pressed( "4" ) )
			SpawnEnemy( SummonerPrefab, aimPoint, "Summoner" );

		// --- Combat Effects (T + 5-9) ---
		if ( Pressed( "5" ) )
		{
			var attack = new AttackData( 80f, DamageType.Fire, canKnockback: true, knockbackForce: 500f );
			Explosion.At( Scene, aimPoint, 300f, attack, _localPlayer, 0.3f );
			Log.Info( $"[Debug] Explosion at {aimPoint}" );
		}

		if ( Pressed( "6" ) )
		{
			var groundPoint = SnapToGround( aimPoint );
			DamageZone.Spawn( Scene, groundPoint, 200f, 15f, DamageType.Fire, 0.5f, 8f,
				_localPlayer, affectsPlayers: false, affectsEnemies: true );
			Log.Info( $"[Debug] Fire zone at {groundPoint} (8s)" );
		}

		if ( Pressed( "7" ) )
		{
			var attack = new AttackData( 20f, DamageType.Pierce );
			var result = Hitscan.FireSpread( Scene, eyePos, forward, 1500f, attack, _localPlayer,
				pelletCount: 12, spreadAngle: 8f );
			Log.Info( $"[Debug] Shotgun: hit {result.Hits.Count} targets" );
		}

		if ( Pressed( "8" ) )
		{
			var attack = new AttackData( 25f, DamageType.Arcane );
			AttackPattern.Ring( Scene, aimPoint + Vector3.Up * 20f, attack, _localPlayer, 16, speed: 600f );
			Log.Info( $"[Debug] Projectile ring at {aimPoint}" );
		}

		if ( Pressed( "9" ) )
		{
			var nearest = FindNearestEnemy();
			var attack = new AttackData( 30f, DamageType.Arcane );

			if ( nearest is not null )
			{
				AttackPattern.HomingVolley( Scene, eyePos + forward * 50f, forward,
					attack, _localPlayer, nearest.GameObject, 5, speed: 600f, homingStrength: 8f );
				Log.Info( $"[Debug] Homing volley at {nearest.EnemyName}" );
			}
			else
			{
				// No target — fire straight fan instead
				AttackPattern.Fan( Scene, eyePos + forward * 50f, forward,
					attack, _localPlayer, 5, 20f, speed: 600f );
				Log.Info( "[Debug] Fan volley (no target)" );
			}
		}

		// E+0 — Toggle beam (cycles: off → fire beam → chain lightning → off)
		if ( Pressed( "0" ) )
		{
			if ( _testBeam is not null && _testBeam.IsValid() )
			{
				// Destroy current beam, spawn next type or turn off
				var wasChain = _testBeam.CanChain;
				_testBeam.Destroy();
				_testBeam = null;

				if ( !wasChain )
				{
					// Was fire beam → switch to chain lightning
					_testBeam = Beam.CreateChain( _localPlayer.GameObject, _localPlayer,
						40f, DamageType.Lightning, maxChains: 3, chainChance: 0.7f, chainRange: 500f );
					_testBeam.IsFiring = true;
					Log.Info( "[Debug] Chain Lightning ON (3 chains, 70% chance, 500 range)" );
				}
				else
				{
					Log.Info( "[Debug] Beam OFF" );
				}
			}
			else
			{
				// No beam → spawn fire beam
				_testBeam = Beam.Create( _localPlayer.GameObject, _localPlayer,
					50f, DamageType.Fire, 1200f, Color.Red );
				_testBeam.IsFiring = true;
				Log.Info( "[Debug] Fire Beam ON" );
			}
		}

		// E+Jump — Heal
		if ( Input.Pressed( "Jump" ) )
		{
			_localPlayer.Heal( _localPlayer.HealthMax );
			Log.Info( "[Debug] Healed to full" );
		}

		// E+Reload — Kill all enemies
		if ( Input.Pressed( "Reload" ) )
		{
			var enemies = Scene.GetAllComponents<RogueliteEnemyBase>().ToList();
			foreach ( var e in enemies )
				e.ApplyDamage( e.HealthCurrent, DamageType.True, null );
			Log.Info( $"[Debug] Killed {enemies.Count} enemies" );
		}

		// E+Run — Give CubeAbility
		if ( Input.Pressed( "Run" ) )
		{
			_localPlayer.Abilities.Equip( 0, new CubeAbility() );
			Log.Info( "[Debug] CubeAbility equipped to slot 1" );
		}

	}

	private bool Pressed( string key )
	{
		return Input.Pressed( $"Slot{key}" );
	}

	private Vector3 SnapToGround( Vector3 point )
	{
		// Use the XY from the aim point but find the actual ground Z
		// by tracing down and ignoring all damageable entities
		var top = point.WithZ( point.z + 2000f );
		var bottom = point.WithZ( point.z - 5000f );

		var trace = Scene.Trace
			.Ray( top, bottom )
			.UseHitboxes( false );

		// Ignore every enemy and player so we only hit world geometry
		foreach ( var damageable in Scene.GetAllComponents<global::IDamageable>() )
		{
			var comp = damageable as Component;
			if ( comp is not null )
				trace = trace.IgnoreGameObjectHierarchy( comp.GameObject );
		}

		var tr = trace.Run();

		if ( tr.Hit ) return tr.HitPosition;

		// Fallback: place at the player's feet
		var playerGround = Scene.Trace
			.Ray( _localPlayer.WorldPosition, _localPlayer.WorldPosition + Vector3.Down * 100f )
			.IgnoreGameObjectHierarchy( _localPlayer.GameObject )
			.Run();

		return playerGround.Hit ? point.WithZ( playerGround.HitPosition.z ) : point.WithZ( _localPlayer.WorldPosition.z );
	}

	private Vector3 GetAimPoint( Vector3 eyePos, Vector3 forward )
	{
		var tr = Scene.Trace
			.Ray( eyePos, eyePos + forward * 5000f )
			.IgnoreGameObjectHierarchy( _localPlayer.GameObject )
			.WithoutTags( "projectile" )
			.Run();

		return tr.Hit ? tr.HitPosition : eyePos + forward * 2000f;
	}

	private void SpawnEnemy( GameObject prefab, Vector3 position, string name )
	{
		if ( prefab is not null )
		{
			var obj = prefab.Clone( position );
			obj.NetworkSpawn();
			Log.Info( $"[Debug] Spawned {name} (prefab) at {position}" );
			return;
		}

		// No prefab — build from scratch
		var go = Scene.CreateObject();
		go.WorldPosition = position;
		go.Name = name;

		var model = go.Components.Create<SkinnedModelRenderer>();
		model.Model = Model.Load( "models/citizen/citizen.vmdl" );

		go.Components.Create<NavMeshAgent>();
		go.Components.Create<CapsuleCollider>();

		switch ( name )
		{
			case "Rusher":
				var rusher = go.Components.Create<RusherEnemy>();
				rusher.AttackDamage = 30f;
				rusher.MoveSpeed = 200f;
				rusher.HealthMax = 150f;
				break;

			case "Flyer":
				var flyer = go.Components.Create<FlyingEnemy>();
				flyer.HealthMax = 80f;
				break;

			case "Summoner":
				var summoner = go.Components.Create<SummonerEnemy>();
				summoner.HealthMax = 200f;
				if ( MinionPrefab is not null )
					summoner.MinionPrefab = MinionPrefab;
				else if ( EnemyPrefab is not null )
					summoner.MinionPrefab = EnemyPrefab;
				break;

			default:
				var basic = go.Components.Create<RogueliteEnemyBase>();
				basic.HealthMax = 100f;
				break;
		}

		go.NetworkSpawn();
		Log.Info( $"[Debug] Spawned {name} at {position}" );
	}

	private RogueliteEnemyBase FindNearestEnemy()
	{
		return Scene.GetAllComponents<RogueliteEnemyBase>()
			.Where( e => !e.IsDead )
			.OrderBy( e => _localPlayer.WorldPosition.Distance( e.WorldPosition ) )
			.FirstOrDefault();
	}
}
