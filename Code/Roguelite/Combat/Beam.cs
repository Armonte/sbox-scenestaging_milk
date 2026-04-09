/// <summary>
/// Continuous damage beam. Deals damage every tick to whatever it's pointing at.
/// Supports chain arcing — beam can jump to nearby enemies (chain lightning style).
/// Chain count, chance, and range are all upgradeable via properties.
/// </summary>
[Title( "Damage Beam" )]
[Icon( "electric_bolt" )]
public class Beam : Component
{
	[Property] public float MaxRange { get; set; } = 1000f;
	[Property] public float DamagePerSecond { get; set; } = 30f;
	[Property] public DamageType Type { get; set; } = DamageType.Fire;
	[Property] public float TraceRadius { get; set; } = 4f;
	[Property] public float TickRate { get; set; } = 0.1f;
	[Property] public Color BeamColor { get; set; } = Color.Red;
	[Property] public float BeamWidth { get; set; } = 4f;
	[Property] public bool AffectsPlayers { get; set; } = false;
	[Property] public bool AffectsEnemies { get; set; } = true;

	// Chain arc properties
	[Property] public bool CanChain { get; set; } = false;
	[Property] public int MaxChains { get; set; } = 2;
	[Property] public float ChainChance { get; set; } = 0.5f;
	[Property] public float ChainRange { get; set; } = 400f;
	[Property] public float ChainDamageFalloff { get; set; } = 0.7f;
	[Property] public Color ChainColor { get; set; } = Color.Cyan;

	public Component Owner { get; set; }
	public bool IsFiring { get; set; }

	public Vector3 BeamEndPoint { get; private set; }
	public GameObject BeamTarget { get; private set; }

	private LineRenderer _lineRenderer;
	private readonly List<LineRenderer> _chainRenderers = new();
	private float _tickTimer;

	protected override void OnDestroy()
	{
		// Clean up all line renderers we created
		_lineRenderer?.Destroy();
		foreach ( var lr in _chainRenderers )
		{
			if ( lr.IsValid() )
				lr.GameObject.Destroy();
		}
		_chainRenderers.Clear();
	}

	protected override void OnStart()
	{
		_lineRenderer = Components.Get<LineRenderer>( FindMode.EverythingInSelfAndDescendants );
		if ( _lineRenderer is null )
			_lineRenderer = Components.Create<LineRenderer>();

		SetupRenderer( _lineRenderer, BeamColor );
	}

	private void SetupRenderer( LineRenderer lr, Color color )
	{
		lr.Color = new Gradient(
			new Gradient.ColorFrame( 0, color ),
			new Gradient.ColorFrame( 1, color )
		);
		lr.Width = new Curve( new Curve.Frame( 0, BeamWidth ), new Curve.Frame( 1, BeamWidth ) );
		lr.UseVectorPoints = true;
		lr.Enabled = false;
	}

	protected override void OnUpdate()
	{
		if ( !IsFiring )
		{
			_lineRenderer.Enabled = false;
			HideChains();
			BeamTarget = null;
			return;
		}

		// Pull origin/direction from the owner — offset so beam is visible in first person
		Vector3 eyePos;
		Vector3 direction;
		Vector3 origin;

		if ( Owner is not null )
		{
			var player = Owner.Components.Get<RoguelitePlayer>();
			if ( player is not null )
			{
				var rot = player.Camera.EyeAngles.ToRotation();
				eyePos = player.WorldPosition + Vector3.Up * player.Camera.EyeHeight;
				direction = rot.Forward;
				origin = eyePos + rot.Right * 20f + rot.Down * 8f + rot.Forward * 10f;
			}
			else
			{
				eyePos = Owner.WorldPosition + Vector3.Up * 40f;
				direction = Owner.WorldRotation.Forward;
				origin = eyePos;
			}
		}
		else
		{
			eyePos = WorldPosition;
			direction = WorldRotation.Forward;
			origin = eyePos;
		}

		// Trace from eye (accurate aim) but draw beam from offset origin
		var endPoint = eyePos + direction * MaxRange;
		var tr = HitDetection.Ray( Scene, eyePos, endPoint, TraceRadius, Owner?.GameObject );

		if ( tr.Hit )
		{
			BeamEndPoint = tr.HitPosition;
			BeamTarget = tr.GameObject;
		}
		else
		{
			BeamEndPoint = endPoint;
			BeamTarget = null;
		}

		// Draw main beam
		_lineRenderer.Enabled = true;
		_lineRenderer.VectorPoints = new List<Vector3> { origin, BeamEndPoint };

		// Draw chain arcs
		UpdateChainVisuals();

		// Damage tick
		if ( !Networking.IsHost ) return;

		_tickTimer += Time.Delta;
		if ( _tickTimer >= TickRate )
		{
			_tickTimer -= TickRate;
			ApplyBeamDamage();
		}
	}

	private void ApplyBeamDamage()
	{
		if ( BeamTarget is null ) return;

		var primaryTarget = FindEntityRoot( BeamTarget );
		if ( !CanDamage( primaryTarget ) ) return;

		var dmgThisTick = DamagePerSecond * TickRate;
		var attack = new AttackData( dmgThisTick, Type, canCrit: false );
		var ctx = new HitContext( BeamEndPoint, Vector3.Down, BeamEndPoint,
			Vector3.Up, 0f, false, false );

		DamageResolver.Resolve( attack, Owner, primaryTarget, ctx );

		// Chain arcs
		if ( CanChain )
			ApplyChainDamage( primaryTarget, dmgThisTick );
	}

	private readonly List<(Vector3 from, Vector3 to)> _activeChains = new();

	private void ApplyChainDamage( GameObject source, float baseDamage )
	{
		_activeChains.Clear();

		var hitTargets = new HashSet<GameObject> { source };
		var currentSource = source;
		var currentDamage = baseDamage;

		for ( int i = 0; i < MaxChains; i++ )
		{
			// Roll for chain
			if ( Random.Shared.NextSingle() > ChainChance ) break;

			// Find nearest valid target within chain range
			var nextTarget = FindChainTarget( currentSource.WorldPosition, hitTargets );
			if ( nextTarget is null ) break;

			currentDamage *= ChainDamageFalloff;

			var attack = new AttackData( currentDamage, Type, canCrit: false );
			var ctx = new HitContext( currentSource.WorldPosition,
				(nextTarget.WorldPosition - currentSource.WorldPosition).Normal,
				nextTarget.WorldPosition, Vector3.Up,
				currentSource.WorldPosition.Distance( nextTarget.WorldPosition ),
				false, false );

			DamageResolver.Resolve( attack, Owner, nextTarget, ctx );

			_activeChains.Add( (currentSource.WorldPosition + Vector3.Up * 40f,
				nextTarget.WorldPosition + Vector3.Up * 40f) );

			hitTargets.Add( nextTarget );
			currentSource = nextTarget;
		}
	}

	private GameObject FindChainTarget( Vector3 from, HashSet<GameObject> exclude )
	{
		GameObject best = null;
		var bestDist = ChainRange;

		foreach ( var damageable in Scene.GetAllComponents<global::IDamageable>() )
		{
			if ( damageable.IsDead ) continue;
			var obj = (damageable as Component)?.GameObject;
			if ( obj is null || exclude.Contains( obj ) ) continue;

			if ( damageable.Faction == global::Faction.Player && !AffectsPlayers ) continue;
			if ( damageable.Faction == global::Faction.Enemy && !AffectsEnemies ) continue;

			var dist = from.Distance( obj.WorldPosition );
			if ( dist < bestDist )
			{
				bestDist = dist;
				best = obj;
			}
		}

		return best;
	}

	// --- Chain visuals ---

	private void UpdateChainVisuals()
	{
		// Ensure we have enough chain renderers
		while ( _chainRenderers.Count < MaxChains )
		{
			var chainObj = Scene.CreateObject();
			chainObj.Name = "beam_chain";
			chainObj.SetParent( GameObject );

			var lr = chainObj.Components.Create<LineRenderer>();
			SetupRenderer( lr, ChainColor );
			lr.Width = new Curve( new Curve.Frame( 0, BeamWidth * 0.6f ), new Curve.Frame( 1, BeamWidth * 0.6f ) );
			_chainRenderers.Add( lr );
		}

		// Update chain line positions
		for ( int i = 0; i < _chainRenderers.Count; i++ )
		{
			if ( i < _activeChains.Count )
			{
				var chain = _activeChains[i];
				_chainRenderers[i].Enabled = true;
				_chainRenderers[i].VectorPoints = new List<Vector3> { chain.from, chain.to };
			}
			else
			{
				_chainRenderers[i].Enabled = false;
			}
		}
	}

	private void HideChains()
	{
		foreach ( var lr in _chainRenderers )
		{
			if ( lr.IsValid() )
				lr.Enabled = false;
		}
		_activeChains.Clear();
	}

	private bool CanDamage( GameObject target )
	{
		var damageable = target.Components.Get<global::IDamageable>( FindMode.EverythingInSelfAndDescendants );
		if ( damageable is null ) return false;
		if ( damageable.Faction == global::Faction.Player && !AffectsPlayers ) return false;
		if ( damageable.Faction == global::Faction.Enemy && !AffectsEnemies ) return false;
		return true;
	}

	private static GameObject FindEntityRoot( GameObject obj )
	{
		var current = obj;
		while ( current is not null )
		{
			if ( current.Components.Get<global::IDamageable>( FindMode.EverythingInSelfAndDescendants ) is not null )
				return current;
			current = current.Parent;
		}
		return obj;
	}

	/// <summary>
	/// Create a basic beam.
	/// </summary>
	public static Beam Create( GameObject parent, Component owner,
		float dps, DamageType type, float range = 1000f, Color? color = null )
	{
		var beam = parent.Components.Create<Beam>();
		beam.Owner = owner;
		beam.DamagePerSecond = dps;
		beam.Type = type;
		beam.MaxRange = range;
		beam.BeamColor = color ?? Color.Red;
		return beam;
	}

	/// <summary>
	/// Create a chain beam (chain lightning style).
	/// </summary>
	public static Beam CreateChain( GameObject parent, Component owner,
		float dps, DamageType type, int maxChains = 2, float chainChance = 0.5f,
		float chainRange = 400f, float range = 1000f )
	{
		var beam = Create( parent, owner, dps, type, range, Color.Cyan );
		beam.CanChain = true;
		beam.MaxChains = maxChains;
		beam.ChainChance = chainChance;
		beam.ChainRange = chainRange;
		beam.ChainColor = Color.Cyan.WithAlpha( 0.7f );
		return beam;
	}
}
