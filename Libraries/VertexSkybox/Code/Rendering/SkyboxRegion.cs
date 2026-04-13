namespace Sandbox;

[Title( "Skybox Region" )]
[Category( "Rendering" )]
[Icon( "landscape" )]
public sealed class SkyboxRegion : Component
{
	public const string SkyTag = "skybox_region";

	[Property] public string RegionId { get; set; } = "default";

	[Property] public SkyboxComponent Skybox { get; set; }

	[Property, Title( "Follow Main Camera" )]
	public bool FollowMainCamera { get; set; } = true;

	[Property, Title( "Use Sky Pass" ), Description( "Tag this region for rendering by a SkyPassCamera instead of the main camera." )]
	public bool UseSkyPass { get; set; } = false;

	protected override void OnEnabled()
	{
		Skybox ??= GetComponentInChildren<SkyboxComponent>( true );
		ApplyTag();
	}

	protected override void OnUpdate()
	{
		if ( !FollowMainCamera ) return;

		var cam = Scene?.Camera;
		if ( !cam.IsValid() ) return;

		WorldPosition = cam.WorldPosition;
	}

	public void ApplyTag()
	{
		if ( !Skybox.IsValid() ) return;

		var tags = Skybox.GameObject.Tags;
		if ( UseSkyPass )
			tags.Add( SkyTag );
		else
			tags.Remove( SkyTag );
	}

	public void SetActive( bool active )
	{
		if ( Skybox.IsValid() )
		{
			if ( Skybox.Enabled != active )
				Skybox.Enabled = active;
			if ( Skybox.GameObject != GameObject && Skybox.GameObject.Enabled != active )
				Skybox.GameObject.Enabled = active;
		}
	}
}
