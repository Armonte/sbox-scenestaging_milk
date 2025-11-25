using Sandbox;

/// <summary>
/// Trigger component for detecting when the bowling ball enters the gutter.
/// Place this on a GameObject with a trigger collider in the gutter area.
/// </summary>
public sealed class GutterTrigger : Component, Component.ITriggerListener
{
	[Property] public bool IsLeftGutter { get; set; } = false;
	[Property] public bool IsRightGutter { get; set; } = false;
	
	private int _ballsInGutter = 0;
	
	protected override void OnStart()
	{
		base.OnStart();
		
		// Ensure we have a trigger collider
		var collider = Components.Get<Collider>();
		if ( collider.IsValid() )
		{
			collider.IsTrigger = true;
		}
		else
		{
			Log.Warning( "GutterTrigger requires a Collider component!" );
		}
		
		// Tag for identification
		Tags.Add( "gutter_trigger" );
	}
	
	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		// Check if it's a bowling ball
		if ( other.GameObject.Tags.Has( "bowling_ball" ) )
		{
			var ball = other.GameObject.Components.Get<BowlingBall>();
			if ( ball.IsValid() && !ball.IsInGutter )
			{
				ball.IsInGutter = true;
				_ballsInGutter++;
				
				Log.Info( $"Ball entered {(IsLeftGutter ? "left" : "right")} gutter!" );
				
				// Notify game manager
				var gameManager = Scene.GetAllComponents<BowlingGameManager>().FirstOrDefault();
				if ( gameManager.IsValid() )
				{
					gameManager.OnBallEnterGutter( ball, IsLeftGutter );
				}
			}
		}
	}
	
	void ITriggerListener.OnTriggerExit( Collider other )
	{
		// Ball left the gutter (shouldn't happen in normal gameplay, but handle it)
		if ( other.GameObject.Tags.Has( "bowling_ball" ) )
		{
			var ball = other.GameObject.Components.Get<BowlingBall>();
			if ( ball.IsValid() && ball.IsInGutter )
			{
				_ballsInGutter--;
				if ( _ballsInGutter <= 0 )
				{
					_ballsInGutter = 0;
					ball.IsInGutter = false;
					Log.Info( "Ball exited gutter" );
				}
			}
		}
	}
}

