namespace Sandbox;

[Title( "Skybox Region Debug Keys" )]
[Category( "Rendering" )]
[Icon( "keyboard" )]
public sealed class SkyboxRegionDebugKeys : Component
{
	[Property, Description( "Region ids mapped to number keys 1..9 in order." )]
	public string[] RegionIds { get; set; } = new[] { "default" };

	protected override void OnUpdate()
	{
		if ( SkyboxRegionManager.Current is null )
		{
			if ( Input.Pressed( "Slot1" ) )
				Log.Warning( "[SkyboxRegionDebugKeys] No SkyboxRegionManager in scene" );
			return;
		}

		for ( int i = 0; i < RegionIds.Length && i < 9; i++ )
		{
			if ( Input.Pressed( $"Slot{i + 1}" ) )
			{
				Log.Info( $"[SkyboxRegionDebugKeys] Slot{i + 1} -> '{RegionIds[i]}'" );
				SkyboxRegionManager.Current.SwitchTo( RegionIds[i] );
			}
		}
	}
}
