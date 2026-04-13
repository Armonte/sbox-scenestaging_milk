namespace Sandbox;

/// <summary>
/// Source-style 3D-skybox pass. Renders GameObjects tagged <see cref="SkyboxRegion.SkyTag"/>
/// through a second CameraComponent that sits at a scaled translation of the main camera.
/// Main camera is reconfigured to skip the sky tag and keep the sky pass's color buffer.
/// </summary>
[Title( "Sky Pass Camera" )]
[Category( "Rendering" )]
[Icon( "camera_outdoor" )]
public sealed class SkyPassCamera : Component
{
	[Property] public CameraComponent MainCamera { get; set; }

	[Property, Range( 1f, 64f ), Description( "Parallax scale. Higher = sky moves slower with the main camera (more 'distant')." )]
	public float SkyScale { get; set; } = 16f;

	[Property] public Vector3 SkyOrigin { get; set; } = Vector3.Zero;

	[Property, Range( 1f, 100000f )] public float SkyZFar { get; set; } = 25000f;

	CameraComponent _sky;
	ClearFlags _mainOrigClearFlags;
	bool _mainTagAdded;
	bool _applied;

	protected override void OnEnabled()
	{
		MainCamera ??= Scene?.Camera;
		if ( !MainCamera.IsValid() ) return;

		_sky = GameObject.GetOrAddComponent<CameraComponent>();
		_sky.IsMainCamera = false;
		_sky.Priority = MainCamera.Priority + 10;
		_sky.ClearFlags = ClearFlags.All;
		_sky.RenderTags ??= new TagSet();
		_sky.RenderTags.Add( SkyboxRegion.SkyTag );
		_sky.ZNear = 1f;
		_sky.ZFar = SkyZFar;
		_sky.EnablePostProcessing = false;

		_mainOrigClearFlags = MainCamera.ClearFlags;
		MainCamera.ClearFlags = ClearFlags.Depth | ClearFlags.Stencil;

		MainCamera.RenderExcludeTags ??= new TagSet();
		if ( !MainCamera.RenderExcludeTags.Has( SkyboxRegion.SkyTag ) )
		{
			MainCamera.RenderExcludeTags.Add( SkyboxRegion.SkyTag );
			_mainTagAdded = true;
		}

		_applied = true;
	}

	protected override void OnDisabled()
	{
		if ( !_applied ) return;
		_applied = false;

		if ( MainCamera.IsValid() )
		{
			MainCamera.ClearFlags = _mainOrigClearFlags;
			if ( _mainTagAdded && MainCamera.RenderExcludeTags is not null )
				MainCamera.RenderExcludeTags.Remove( SkyboxRegion.SkyTag );
		}
		_mainTagAdded = false;

		if ( _sky.IsValid() )
			_sky.Enabled = false;
	}

	protected override void OnUpdate()
	{
		if ( !_applied || !_sky.IsValid() || !MainCamera.IsValid() ) return;

		_sky.FieldOfView = MainCamera.FieldOfView;
		_sky.Orthographic = MainCamera.Orthographic;
		_sky.OrthographicHeight = MainCamera.OrthographicHeight;

		WorldRotation = MainCamera.WorldRotation;
		WorldPosition = SkyOrigin + MainCamera.WorldPosition / SkyScale;
	}
}
