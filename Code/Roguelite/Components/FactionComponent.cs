public enum Faction
{
	Player,
	Enemy,
	Neutral
}

/// <summary>
/// Marks a GameObject as belonging to a faction. Used by DamageResolver to determine hostility.
/// </summary>
[Title( "Faction" )]
[Icon( "groups" )]
public sealed class FactionComponent : Component
{
	[Property] public Faction Faction { get; set; } = Faction.Neutral;

	public bool IsHostile( FactionComponent other )
	{
		if ( other is null ) return false;
		if ( Faction == Faction.Neutral || other.Faction == Faction.Neutral ) return false;
		return Faction != other.Faction;
	}

	public bool IsFriendly( FactionComponent other )
	{
		if ( other is null ) return false;
		return Faction == other.Faction;
	}
}
