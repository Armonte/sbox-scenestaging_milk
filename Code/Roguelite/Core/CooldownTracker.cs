/// <summary>
/// Reusable cooldown tracker. Used by WeaponBase and AbilityComponent.
/// Zero-allocation — no LINQ, no ToList, no temporary collections.
/// </summary>
public sealed class CooldownTracker
{
	// Fixed-size arrays instead of Dictionary — most entities have 1-4 cooldowns max
	private readonly string[] _ids = new string[8];
	private readonly float[] _times = new float[8];
	private int _count;

	public bool IsReady( string id )
	{
		for ( int i = 0; i < _count; i++ )
			if ( _ids[i] == id ) return _times[i] <= 0;
		return true;
	}

	public float Remaining( string id )
	{
		for ( int i = 0; i < _count; i++ )
			if ( _ids[i] == id ) return MathF.Max( 0, _times[i] );
		return 0;
	}

	public void Start( string id, float duration )
	{
		for ( int i = 0; i < _count; i++ )
		{
			if ( _ids[i] == id )
			{
				_times[i] = duration;
				return;
			}
		}

		if ( _count < _ids.Length )
		{
			_ids[_count] = id;
			_times[_count] = duration;
			_count++;
		}
	}

	public void Tick( float delta )
	{
		for ( int i = 0; i < _count; i++ )
			_times[i] = MathF.Max( 0, _times[i] - delta );
	}

	public void Reset()
	{
		_count = 0;
	}

	public void Accelerate( float seconds )
	{
		for ( int i = 0; i < _count; i++ )
			_times[i] = MathF.Max( 0, _times[i] - seconds );
	}

	public void ScaleAll( float multiplier )
	{
		for ( int i = 0; i < _count; i++ )
			_times[i] *= multiplier;
	}
}
