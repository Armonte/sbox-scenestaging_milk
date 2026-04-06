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

	public void DecayThreat()
	{
		foreach ( var key in _threatTable.Keys.ToList() )
		{
			_threatTable[key] *= DecayRate;
			if ( _threatTable[key] < 0.1f )
				_threatTable.Remove( key );
		}
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
	public RoguelitePlayer SelectTarget( IEnumerable<RoguelitePlayer> candidates )
	{
		var alive = candidates.Where( p => p is not null && p.IsAlive ).ToList();
		if ( alive.Count == 0 ) return null;

		return Strategy switch
		{
			TargetStrategy.HighestThreat => alive.OrderByDescending( p => GetThreat( p.GameObject ) ).First(),
			TargetStrategy.LowestHP => alive.OrderBy( p => p.Health.Current ).First(),
			TargetStrategy.LowestArmor => SelectLowestArmor( alive ),
			TargetStrategy.Nearest => alive.OrderBy( p => WorldPosition.Distance( p.WorldPosition ) ).First(),
			TargetStrategy.ClassPriority => SelectByClass( alive ),
			TargetStrategy.DensestCluster => SelectDensestCluster( alive ),
			_ => alive.OrderByDescending( p => GetThreat( p.GameObject ) ).First()
		};
	}

	private RoguelitePlayer SelectLowestArmor( List<RoguelitePlayer> alive )
	{
		return alive.OrderBy( p =>
		{
			var armor = p.Components.Get<RogueliteArmorComponent>();
			return armor?.BaseArmor ?? 0f;
		} ).First();
	}

	private RoguelitePlayer SelectByClass( List<RoguelitePlayer> alive )
	{
		var preferred = alive.FirstOrDefault( p => p.Class == PreferredTargetClass );
		return preferred ?? alive.OrderByDescending( p => GetThreat( p.GameObject ) ).First();
	}

	private RoguelitePlayer SelectDensestCluster( List<RoguelitePlayer> alive )
	{
		if ( alive.Count == 1 ) return alive[0];

		RoguelitePlayer best = null;
		var bestCount = 0;

		foreach ( var candidate in alive )
		{
			var count = alive.Count( other =>
				candidate.WorldPosition.Distance( other.WorldPosition ) <= ClusterRadius );

			if ( count > bestCount )
			{
				bestCount = count;
				best = candidate;
			}
		}

		return best ?? alive[0];
	}
}
