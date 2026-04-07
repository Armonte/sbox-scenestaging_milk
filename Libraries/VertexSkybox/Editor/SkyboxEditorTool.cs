using Sandbox;

namespace Editor;

/// <summary>
/// Component-specific tool: logs a hint when SkyboxComponent is selected in Object mode.
/// </summary>
public class SkyboxComponentEditorTool : EditorTool<SkyboxComponent>
{
	public override void OnEnabled() { }
	public override void OnUpdate() { }
	public override void OnDisabled() { }

	public override void OnSelectionChanged()
	{
		var target = GetSelectedComponent<SkyboxComponent>();
		if ( target != null )
		{
			Log.Info( "SkyboxComponent selected - switch to 'Spyro Skybox' tool to edit" );
		}
	}
}

/// <summary>
/// Overlay window for the skybox editor tool.
/// </summary>
public class SkyboxToolWindow : WidgetWindow
{
	private SkyboxEditorSession _session;

	public SkyboxToolWindow( SkyboxEditorSession session )
	{
		_session = session;
		ContentMargins = 0;
		Layout = Layout.Column();
		MaximumWidth = 300;
		MinimumWidth = 200;
		MinimumSize = new Vector2( 200, 80 );

		Rebuild();
	}

	public void OnTargetChanged()
	{
		Rebuild();
	}

	void Rebuild()
	{
		Layout.Clear( true );
		Layout.Margin = 0;

		WindowTitle = "Skybox";
		Icon = "cloud";
		IsGrabbable = true;

		var data = _session?.Target?.Data;
		int verts = data?.Vertices?.Count ?? 0;
		int tris = data?.Triangles?.Count ?? 0;
		int edges = data?.Edges?.Count ?? 0;

		Layout.AddSpacingCell( 8 );
		Layout.Add( new Label( $"  Verts: {verts}   Tris: {tris}   Edges: {edges}" ) { FixedHeight = 24 } );
		Layout.AddSpacingCell( 4 );

		var loadBtn = new Button( "Load .skye File", "upload_file" ) { FixedHeight = 28 };
		loadBtn.Clicked = LoadSkyeFile;
		Layout.Add( loadBtn );
		Layout.AddSpacingCell( 4 );

		Layout.Margin = 8;
	}

	void LoadSkyeFile()
	{
		if ( _session?.Target == null )
		{
			Log.Warning( "No SkyboxComponent found in scene. Add one first." );
			return;
		}

		var fd = new FileDialog( null );
		fd.Title = "Load Skybox";
		fd.SetNameFilter( "Skybox Files (*.skye)" );

		if ( !fd.Execute() ) return;

		var path = fd.SelectedFile;
		if ( string.IsNullOrEmpty( path ) ) return;

		var content = System.IO.File.ReadAllText( path );
		if ( string.IsNullOrEmpty( content ) )
		{
			Log.Warning( "Failed to read skybox file" );
			return;
		}

		_session.Target.LoadFromString( content );
		Rebuild();
		Log.Info( $"Loaded skybox: {_session.Target.Data.Vertices.Count} verts, {_session.Target.Data.Triangles.Count} tris" );
	}
}
