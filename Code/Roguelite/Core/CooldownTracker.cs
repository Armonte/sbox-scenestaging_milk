/// <summary>
/// Reusable cooldown tracker. Used by WeaponBase and AbilityComponent.
/// </summary>
public sealed class CooldownTracker
{
	private readonly Dictionary<string, float> _cooldowns = new();

	public bool IsReady( string id ) =>
		!_cooldowns.ContainsKey( id ) || _cooldowns[id] <= 0;

	public float Remaining( string id ) =>
		_cooldowns.TryGetValue( id, out var t ) ? MathF.Max( 0, t ) : 0;

	public void Start( string id, float duration ) =>
		_cooldowns[id] = duration;

	public void Tick( float delta )
	{
		foreach ( var key in _cooldowns.Keys.ToList() )
			_cooldowns[key] = MathF.Max( 0, _cooldowns[key] - delta );
	}

	public void Reset() => _cooldowns.Clear();

	/// <summary>
	/// Accelerate all active cooldowns by burning seconds off remaining time.
	/// </summary>
	public void Accelerate( float seconds )
	{
		foreach ( var key in _cooldowns.Keys.ToList() )
			_cooldowns[key] = MathF.Max( 0, _cooldowns[key] - seconds );
	}

	/// <summary>
	/// Scale all active cooldowns by a multiplier. Used by RunModifiers.
	/// </summary>
	public void ScaleAll( float multiplier )
	{
		foreach ( var key in _cooldowns.Keys.ToList() )
			_cooldowns[key] *= multiplier;
	}
}
