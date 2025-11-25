using Sandbox;

/// <summary>
/// Trigger component for detecting when the bowling ball reaches the end of the lane.
/// Place this on a GameObject with a trigger collider at the end of the lane behind the pins.
/// When the ball enters this trigger, it signals the game manager to end the roll.
/// </summary>
public sealed class LaneEndTrigger : Component, Component.ITriggerListener
{
	[Property] public float DespawnDelay { get; set; } = 0.5f; // Delay before destroying the ball

	protected override void OnStart()
	{
		base.OnStart();

		// Ensure we have a trigger collider
		var collider = Components.Get<Collider>();
		if (collider.IsValid())
		{
			collider.IsTrigger = true;
		}
		else
		{
			Log.Warning("LaneEndTrigger requires a Collider component!");
		}

		// Tag for identification
		Tags.Add("lane_end_trigger");
	}

	void ITriggerListener.OnTriggerEnter(Collider other)
	{
		// Check if it's a bowling ball
		if (other.GameObject.Tags.Has("bowling_ball"))
		{
			var ball = other.GameObject.Components.Get<BowlingBall>();
			if (ball.IsValid() && ball.IsThrown)
			{
				Log.Info("Ball reached end of lane!");

				// Notify game manager
				var gameManager = Scene.GetAllComponents<BowlingGameManager>().FirstOrDefault();
				if (gameManager.IsValid())
				{
					// The game manager will handle despawning and state transitions
					// We just need to let physics settle for a moment
				}

				// Slow down the ball dramatically
				var rb = other.GameObject.Components.Get<Rigidbody>(FindMode.EverythingInSelfAndDescendants);
				if (rb.IsValid())
				{
					rb.Velocity *= 0.3f;
					rb.LinearDamping = 5f;
				}
			}
		}

		// Also destroy any pins that somehow made it to the end
		if (other.GameObject.Tags.Has("bowling_pin"))
		{
			var pin = other.GameObject.Components.Get<BowlingPin>();
			if (pin.IsValid() && !pin.IsKnockedOver)
			{
				pin.KnockOver();
			}
		}
	}

	void ITriggerListener.OnTriggerExit(Collider other)
	{
		// Not needed for end-of-lane
	}
}

