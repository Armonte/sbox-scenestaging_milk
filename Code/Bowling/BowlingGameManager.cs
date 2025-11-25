using Sandbox;

/// <summary>
/// Manages the bowling game state, scoring, and pin/ball management.
/// Add this to a GameObject in your scene to manage the game.
/// </summary>
public sealed class BowlingGameManager : Component
{
	[Property] public int CurrentFrame { get; private set; } = 1;
	[Property] public int CurrentRoll { get; private set; } = 1;
	[Property] public int Score { get; private set; } = 0;
	
	[Property] public List<BowlingPin> Pins { get; private set; } = new();
	[Property] public BowlingBall CurrentBall { get; private set; }
	
	private int _pinsKnockedThisRoll = 0;
	
	protected override void OnStart()
	{
		base.OnStart();
		
		// Find all pins in the scene
		RefreshPins();
		
		// Find the ball
		var ballObject = Scene.GetAllObjects( true ).FirstOrDefault( go => go.Tags.Has( "bowling_ball" ) );
		if ( ballObject.IsValid() )
		{
			CurrentBall = ballObject.Components.Get<BowlingBall>();
		}
		
		Log.Info( $"Bowling game started. Found {Pins.Count} pins." );
	}
	
	/// <summary>
	/// Refresh the list of pins in the scene
	/// </summary>
	public void RefreshPins()
	{
		Pins.Clear();
		var pinObjects = Scene.GetAllObjects( true ).Where( go => go.Tags.Has( "bowling_pin" ) );
		
		foreach ( var pinObject in pinObjects )
		{
			var pin = pinObject.Components.Get<BowlingPin>();
			if ( pin.IsValid() )
			{
				Pins.Add( pin );
			}
		}
	}
	
	/// <summary>
	/// Called when a pin is knocked over
	/// </summary>
	public void OnPinKnockedOver( BowlingPin pin )
	{
		_pinsKnockedThisRoll++;
		Log.Info( $"Pin knocked over! Total this roll: {_pinsKnockedThisRoll}" );
		
		// Check if all pins are down
		var remainingPins = Pins.Count( p => !p.IsKnockedOver );
		if ( remainingPins == 0 )
		{
			Log.Info( "STRIKE! All pins knocked down!" );
			OnStrike();
		}
	}
	
	/// <summary>
	/// Called when the ball enters the gutter
	/// </summary>
	public void OnBallEnterGutter( BowlingBall ball, bool isLeftGutter )
	{
		Log.Info( $"Ball entered {(isLeftGutter ? "left" : "right")} gutter - Gutter ball!" );
		// Handle gutter ball logic here
	}
	
	/// <summary>
	/// Called when a strike is achieved
	/// </summary>
	private void OnStrike()
	{
		Score += 10; // Simplified scoring
		// In real bowling, strikes have special scoring rules
	}
	
	/// <summary>
	/// Reset all pins to their starting positions
	/// </summary>
	public void ResetPins()
	{
		foreach ( var pin in Pins )
		{
			pin.Reset();
		}
		_pinsKnockedThisRoll = 0;
	}
	
	/// <summary>
	/// Reset the ball to starting position
	/// </summary>
	public void ResetBall( Vector3 position, Rotation rotation )
	{
		if ( CurrentBall.IsValid() )
		{
			CurrentBall.Reset( position, rotation );
		}
	}
}

