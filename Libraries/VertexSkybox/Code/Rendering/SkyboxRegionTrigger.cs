namespace Sandbox;

[Title( "Skybox Region Trigger" )]
[Category( "Rendering" )]
[Icon( "door_sliding" )]
public sealed class SkyboxRegionTrigger : Component, Component.ITriggerListener
{
	[Property] public string TargetRegionId { get; set; } = "default";

	[Property, Description( "Only these tags will cause a switch. Leave empty to accept any." )]
	public TagSet ActivatorTags { get; set; }

	public void OnTriggerEnter( Collider other )
	{
		if ( ActivatorTags is not null && !ActivatorTags.IsEmpty )
		{
			if ( !other.GameObject.Tags.HasAny( ActivatorTags ) ) return;
		}

		SkyboxRegionManager.Current?.SwitchTo( TargetRegionId );
	}

	public void OnTriggerExit( Collider other ) { }
}
