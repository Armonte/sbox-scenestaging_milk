using Sandbox;

namespace Editor;

/// <summary>
/// Registers the Spyro Skybox tool in the editor toolbar.
/// Must be in the main editor assembly to appear in the toolbar dropdown.
/// </summary>
[EditorTool]
[Title( "Spyro Skybox" )]
[Icon( "cloud" )]
public class SkyboxEditorToolEntry : EditorTool
{
	private SkyboxEditorSession _session;
	private SkyboxToolWindow _window;

	public override void OnEnabled()
	{
		_session = new SkyboxEditorSession();
		_window = new SkyboxToolWindow( _session );
		AddOverlay( _window, TextFlag.RightBottom, 10 );
		FindSkyboxInScene();
	}

	public override void OnUpdate()
	{
		if ( _session.Target == null || !_session.Target.IsValid() )
		{
			FindSkyboxInScene();
			if ( _session.Target == null ) return;
		}

		SkyboxEditorGizmos.UpdateCursor( _session );
		SkyboxEditorGizmos.DrawOverlay( _session );
	}

	public override void OnDisabled()
	{
		_session = null;
	}

	private void FindSkyboxInScene()
	{
		var scene = SceneEditorSession.Active?.Scene;
		if ( scene == null ) return;

		var skybox = scene.GetAllComponents<SkyboxComponent>().FirstOrDefault();
		if ( skybox != null && skybox != _session.Target )
		{
			_session.SetTarget( skybox );
			_window?.OnTargetChanged();
		}
	}
}
