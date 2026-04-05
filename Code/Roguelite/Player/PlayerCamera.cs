/// <summary>
/// First-person camera controller. Manages mouse look, camera positioning,
/// and hiding the local player body in first-person mode.
/// </summary>
[Title( "Roguelite Camera" )]
[Icon( "videocam" )]
public sealed class PlayerCamera : Component
{
	[Property] public float EyeHeight { get; set; } = 64f;
	[Property] public bool FirstPerson { get; set; } = true;
	[Property] public float ThirdPersonDistance { get; set; } = 300f;

	[Sync] public Angles EyeAngles { get; set; }

	private const string FirstPersonHideTag = "viewer";
	private bool _bodyHidden;

	protected override void OnEnabled()
	{
		if ( IsProxy ) return;

		// Initialize eye angles from current camera
		var cam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
		if ( cam.IsValid() )
		{
			var angles = cam.WorldRotation.Angles();
			angles.roll = 0;
			EyeAngles = angles;
		}
	}

	protected override void OnUpdate()
	{
		if ( !IsProxy )
		{
			// Mouse look
			var ee = EyeAngles;
			ee += Input.AnalogLook * 0.5f;
			ee.pitch = ee.pitch.Clamp( -89f, 89f );
			ee.roll = 0;
			EyeAngles = ee;

			// Position camera
			var cam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
			if ( cam.IsValid() )
			{
				var lookDir = EyeAngles.ToRotation();
				var eyePos = WorldPosition + Vector3.Up * EyeHeight;

				if ( FirstPerson )
				{
					cam.WorldPosition = eyePos;
					cam.WorldRotation = lookDir;
					HideLocalBody( cam );
				}
				else
				{
					cam.WorldPosition = eyePos + lookDir.Backward * ThirdPersonDistance + Vector3.Up * 40f;
					cam.WorldRotation = lookDir;
					ShowLocalBody( cam );
				}
			}
		}
	}

	/// <summary>
	/// Tag the local player's body renderers so the camera excludes them in first-person.
	/// Other players' cameras won't have this exclude tag, so they still see our body.
	/// </summary>
	private void HideLocalBody( CameraComponent cam )
	{
		if ( _bodyHidden ) return;
		_bodyHidden = true;

		// Tag all renderers on this player
		var renderers = Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		foreach ( var r in renderers )
		{
			r.Tags.Add( FirstPersonHideTag );
		}

		// Tell the camera to exclude objects with this tag
		cam.RenderExcludeTags.Add( FirstPersonHideTag );
	}

	private void ShowLocalBody( CameraComponent cam )
	{
		if ( !_bodyHidden ) return;
		_bodyHidden = false;

		var renderers = Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		foreach ( var r in renderers )
		{
			r.Tags.Remove( FirstPersonHideTag );
		}

		cam.RenderExcludeTags.Remove( FirstPersonHideTag );
	}

	protected override void OnDisabled()
	{
		// Clean up tags if component is disabled
		if ( _bodyHidden )
		{
			var cam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
			if ( cam.IsValid() )
				ShowLocalBody( cam );
		}
	}
}
