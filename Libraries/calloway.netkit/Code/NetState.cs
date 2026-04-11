using System;
using Sandbox;

/// <summary>
/// Replicated state machine. Wraps a synced int (on a Component) with typed enum access,
/// transition events, previous-state memory, and time-in-state tracking.
/// </summary>
/// <remarks>
/// <para><b>Setup (on a Component):</b></para>
/// <code>
/// [Sync] public int _state { get; set; }
/// public NetState&lt;DoorState&gt; Door;
///
/// protected override void OnStart()
/// {
///     Door = new NetState&lt;DoorState&gt;( () =&gt; _state, v =&gt; _state = v );
///     Door.OnTransition += ( from, to ) =&gt; PlayAnim( to );
/// }
///
/// protected override void OnUpdate() =&gt; Door.Poll(); // detect client-side transitions
/// </code>
/// <para><b>Host transitions:</b> <c>Door.TransitionTo( DoorState.Opening );</c></para>
/// </remarks>
public class NetState<T> where T : struct, Enum
{
	private readonly Func<int> _getter;
	private readonly Action<int> _setter;
	private int _lastKnownValue;

	/// <summary>Current state.</summary>
	public T Current
	{
		get => (T)(object)_getter();
		set => _setter( (int)(object)value );
	}

	/// <summary>State before the most recent transition.</summary>
	public T Previous { get; private set; }

	/// <summary>Time since last transition.</summary>
	public RealTimeSince TimeSinceTransition { get; private set; }

	/// <summary>Fires on every transition (host and client). Args: (from, to).</summary>
	public event Action<T, T> OnTransition;

	public NetState( Func<int> getter, Action<int> setter )
	{
		_getter = getter;
		_setter = setter;
		_lastKnownValue = getter();
		Previous = (T)(object)_lastKnownValue;
		TimeSinceTransition = 0;
	}

	/// <summary>Host: transition to a new state. Fires OnTransition.</summary>
	public void TransitionTo( T newState )
	{
		var newValue = (int)(object)newState;
		if ( _getter() == newValue ) return;

		var oldState = (T)(object)_getter();
		Previous = oldState;
		TimeSinceTransition = 0;
		_setter( newValue );
		_lastKnownValue = newValue;
		OnTransition?.Invoke( oldState, newState );
	}

	/// <summary>Client: call every frame in OnUpdate to detect transitions from sync.</summary>
	public void Poll()
	{
		var currentValue = _getter();
		if ( currentValue == _lastKnownValue ) return;

		var oldState = (T)(object)_lastKnownValue;
		var newState = (T)(object)currentValue;
		Previous = oldState;
		TimeSinceTransition = 0;
		_lastKnownValue = currentValue;
		OnTransition?.Invoke( oldState, newState );
	}
}
