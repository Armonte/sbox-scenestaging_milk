/// <summary>
/// Camera controller. Manages mouse look, camera positioning,
/// and hiding the local player body in first-person mode.
/// Uses the same "viewer" tag pattern as Facepunch's PlayerController.
/// </summary>
[Title( "Roguelite Camera" )]
[Icon( "videocam" )]
public sealed class PlayerCamera : Component
{
	[Property] public float EyeHeight { get; set; } = 64f;
	[Property] public bool FirstPerson { get; set; } = true;
	[Property] public float ThirdPersonDistance { get; set; } = 300f;

	[Sync] public Angles EyeAngles { get; set; }

	protected override void OnEnabled()
	{
		if ( IsProxy ) return;

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
		// Only the owner handles input and camera positioning
		if ( !IsProxy )
		{
			var ee = EyeAngles;
			ee += Input.AnalogLook * 0.5f;
			ee.pitch = ee.pitch.Clamp( -89f, 89f );
			ee.roll = 0;
			EyeAngles = ee;

			var cam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
			if ( cam.IsValid() )
			{
				var lookDir = EyeAngles.ToRotation();
				var eyePos = WorldPosition + Vector3.Up * EyeHeight;

				if ( FirstPerson )
				{
					cam.WorldPosition = eyePos;
					cam.WorldRotation = lookDir;
				}
				else
				{
					cam.WorldPosition = eyePos + lookDir.Backward * ThirdPersonDistance + Vector3.Up * 40f;
					cam.WorldRotation = lookDir;
				}
			}
		}

		// Body visibility — same pattern as Facepunch PlayerController.
		// Tags.Set("viewer", bool) on the body GameObject.
		// The scene camera automatically excludes "viewer" tagged objects.
		// Proxies always set viewer=false so other players see our body.
		UpdateBodyVisibility();
	}

	/// <summary>
	/// Matches Facepunch PlayerController.UpdateBodyVisibility exactly.
	/// Sets "viewer" tag on the body — the engine hides viewer-tagged objects from the camera.
	/// Only the local player (!IsProxy) in first person gets tagged as viewer.
	/// Proxies are NEVER tagged, so other clients always see our body.
	/// </summary>
	private void UpdateBodyVisibility()
	{
		bool viewer = FirstPerson && !IsProxy;

		// Find the body renderer's GameObject (child "Body" or self)
		var renderer = Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		var bodyGo = renderer?.GameObject ?? GameObject;

		if ( bodyGo.IsValid() )
			bodyGo.Tags.Set( "viewer", viewer );
	}
}
