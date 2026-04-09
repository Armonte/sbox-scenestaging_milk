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

	private string _hideTag;
	private bool _bodyHidden;

	protected override void OnEnabled()
	{
		// Unique hide tag per player instance so we only hide OUR body
		_hideTag = $"viewer_{GameObject.Id}";

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
	/// Hide the local player's body using the camera exclude tag.
	/// The tag goes on OUR renderers, the exclude goes on OUR camera.
	/// Since each client only runs this for their own player (!IsProxy),
	/// each client's camera only excludes their own body.
	/// </summary>
	private void HideLocalBody( CameraComponent cam )
	{
		if ( _bodyHidden ) return;
		_bodyHidden = true;

		// Tag our renderers
		var renderers = Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		foreach ( var r in renderers )
			r.Tags.Add( _hideTag );

		// Tell camera to skip our tag — this only runs on the LOCAL client
		cam.RenderExcludeTags.Add( _hideTag );
	}

	private void ShowLocalBody( CameraComponent cam )
	{
		if ( !_bodyHidden ) return;
		_bodyHidden = false;

		var renderers = Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		foreach ( var r in renderers )
			r.Tags.Remove( _hideTag );

		cam.RenderExcludeTags.Remove( _hideTag );
	}

	protected override void OnDisabled()
	{
		if ( _bodyHidden )
		{
			var cam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
			if ( cam.IsValid() )
				ShowLocalBody( cam );
		}
	}
}
