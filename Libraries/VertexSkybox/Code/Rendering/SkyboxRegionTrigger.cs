namespace Sandbox;

[Title( "Skybox Region Trigger" )]
[Category( "Rendering" )]
[Icon( "door_sliding" )]
public sealed class SkyboxRegionTrigger : Component, Component.ITriggerListener
{
	[Property] public string TargetRegionId { get; set; } = "default";

	[Property, Description( "Only these tags will cause a switch. Leave empty to accept any." )]
	public TagSet ActivatorTags { get; set; }

	[Property, Description( "Optional. If set, the activator is teleported to this transform — turns this trigger into a portal." )]
	public GameObject Destination { get; set; }

	[Property, Description( "If true, also copy Destination's rotation to the activator." )]
	public bool PreserveDestinationRotation { get; set; } = true;

	[Property, Description( "Seconds before this trigger can fire again, prevents ping-pong between linked portals." )]
	public float RetriggerCooldown { get; set; } = 0.5f;

	RealTimeSince _sinceLastTrigger = 10f;

	public void OnTriggerEnter( Collider other )
	{
		if ( _sinceLastTrigger < RetriggerCooldown ) return;

		if ( ActivatorTags is not null && !ActivatorTags.IsEmpty )
		{
			if ( !other.GameObject.Tags.HasAny( ActivatorTags ) ) return;
		}

		_sinceLastTrigger = 0f;

		if ( Destination.IsValid() )
		{
			var target = other.GameObject.Root ?? other.GameObject;
			target.WorldPosition = Destination.WorldPosition;
			if ( PreserveDestinationRotation )
				target.WorldRotation = Destination.WorldRotation;

			Log.Info( $"[SkyboxRegionTrigger] Portal: teleporting '{target.Name}' to '{Destination.Name}' (region '{TargetRegionId}')" );
		}

		SkyboxRegionManager.Current?.SwitchTo( TargetRegionId );
	}

	public void OnTriggerExit( Collider other ) { }
}
