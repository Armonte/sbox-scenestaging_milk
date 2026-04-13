using System.Linq;

namespace Sandbox;

[Title( "Skybox Region Manager" )]
[Category( "Rendering" )]
[Icon( "public" )]
public sealed class SkyboxRegionManager : Component
{
	[Property] public string ActiveRegionId { get; set; } = "default";

	public static SkyboxRegionManager Current { get; private set; }

	protected override void OnEnabled()
	{
		Current = this;
		Apply();
	}

	protected override void OnDisabled()
	{
		if ( Current == this ) Current = null;
	}

	public void SwitchTo( string regionId )
	{
		if ( string.IsNullOrEmpty( regionId ) ) return;
		if ( ActiveRegionId == regionId )
		{
			Log.Info( $"[SkyboxRegionManager] SwitchTo('{regionId}') skipped (already active)" );
			return;
		}

		Log.Info( $"[SkyboxRegionManager] SwitchTo: '{ActiveRegionId}' -> '{regionId}'" );
		ActiveRegionId = regionId;
		Apply();
	}

	void Apply()
	{
		if ( Scene is null ) return;

		int matched = 0, total = 0;
		foreach ( var r in Scene.GetAllComponents<SkyboxRegion>() )
		{
			total++;
			bool active = r.RegionId == ActiveRegionId;
			if ( active ) matched++;
			r.SetActive( active );
		}
		Log.Info( $"[SkyboxRegionManager] Apply: active='{ActiveRegionId}' matched={matched}/{total}" );
	}
}
