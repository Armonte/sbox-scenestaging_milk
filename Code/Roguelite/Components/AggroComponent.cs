/// <summary>
/// Threat table for enemy target selection. Tracks damage dealt by each attacker
/// and decays over time. Supports multiple targeting strategies.
/// </summary>
public enum TargetStrategy
{
	HighestThreat,
	LowestHP,
	LowestArmor,
	Nearest,
	ClassPriority,
	DensestCluster
}

[Title( "Aggro" )]
[Icon( "priority_high" )]
public sealed class AggroComponent : Component
{
	[Property] public TargetStrategy Strategy { get; set; } = TargetStrategy.HighestThreat;
	[Property] public PlayerClass PreferredTargetClass { get; set; } = PlayerClass.Warrior;

	private const float DecayRate = 0.998f;
	private const float ClusterRadius = 300f;

	private readonly Dictionary<GameObject, float> _threatTable = new();

	public void RecordDamage( GameObject source, float amount )
	{
		if ( source is null ) return;
		_threatTable.TryGetValue( source, out var current );
		_threatTable[source] = current + amount;
	}

	private static readonly List<GameObject> _keysToRemove = new();

	public void DecayThreat()
	{
		if ( _threatTable.Count == 0 ) return;

		_keysToRemove.Clear();

		foreach ( var kvp in _threatTable )
		{
			if ( kvp.Value * DecayRate < 0.1f )
				_keysToRemove.Add( kvp.Key );
		}

		foreach ( var key in _keysToRemove )
			_threatTable.Remove( key );

		// Decay remaining — iterate entries to avoid Keys allocation
		_keysToRemove.Clear();
		foreach ( var kvp in _threatTable )
			_keysToRemove.Add( kvp.Key );

		foreach ( var key in _keysToRemove )
			_threatTable[key] *= DecayRate;
	}

	public void ZeroThreat( GameObject source )
	{
		_threatTable.Remove( source );
	}

	public void TransferThreat( GameObject from, GameObject to )
	{
		if ( _threatTable.TryGetValue( from, out var threat ) )
		{
			_threatTable.Remove( from );
			_threatTable.TryGetValue( to, out var existing );
			_threatTable[to] = existing + threat;
		}
	}

	public float GetThreat( GameObject source )
	{
		return _threatTable.TryGetValue( source, out var t ) ? t : 0f;
	}

	/// <summary>
	/// Select the best target from alive players based on the configured strategy.
	/// Returns null if no valid target found.
	/// </summary>
	public RoguelitePlayer SelectTarget( List<RoguelitePlayer> candidates )
	{
		if ( candidates.Count == 0 ) return null;

		// Default to highest threat — most common, no LINQ
		RoguelitePlayer best = null;
		float bestScore = float.MinValue;

		foreach ( var p in candidates )
		{
			float score = Strategy switch
			{
				TargetStrategy.HighestThreat => GetThreat( p.GameObject ),
				TargetStrategy.LowestHP => -p.Health.Current,
				TargetStrategy.Nearest => -WorldPosition.DistanceSquared( p.WorldPosition ),
				TargetStrategy.ClassPriority => p.Class == PreferredTargetClass ? 1000f + GetThreat( p.GameObject ) : GetThreat( p.GameObject ),
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
}
