using Sandbox;

namespace Editor;

/// <summary>
/// Spyro Skybox editor tool — appears in the toolbar dropdown.
/// Sidebar with color controls, file loading, and display settings.
/// </summary>
[EditorTool]
[Title( "Spyro Skybox" )]
[Icon( "cloud" )]
public class SkyboxEditorToolEntry : EditorTool
{
	private SkyboxEditorSession _session;
	private SkyboxBrushPreview _brushPreview;
	[Property, Title( "Show Edges" )] public bool ShowEdges { get; set; } = false;
	[Property, Title( "Show Vertices" )] public bool ShowVertices { get; set; } = true;

	// Serialized properties for sidebar controls
	[Property, Title( "Saturation" ), Range( 0.5f, 3.0f )] public float Saturation { get; set; } = 1.2f;
	[Property, Title( "Brightness" ), Range( 0.5f, 2.0f )] public float Brightness { get; set; } = 1.0f;
	[Property, Title( "Gamma" ), Range( 0.3f, 3.0f )] public float Gamma { get; set; } = 2.0f;
	[Property, Title( "Scale" ), Range( 1f, 1000f )] public float SkyScale { get; set; } = 10f;
	[Property, Title( "Background Color" )] public Color BgColor { get; set; } = Color.Black;
	[Property, Title( "R Shift" ), Range( -100f, 100f )] public float RedShift { get; set; } = 0f;
	[Property, Title( "G Shift" ), Range( -100f, 100f )] public float GreenShift { get; set; } = 0f;
	[Property, Title( "B Shift" ), Range( -100f, 100f )] public float BlueShift { get; set; } = 0f;
	[Property, Title( "Left Paint Color" )] public Color LeftPaintColor { get; set; } = Color.White;
	[Property, Title( "Right Paint Color" )] public Color RightPaintColor { get; set; } = Color.Black;
	[Property, Title( "Paint Opacity" ), Range( 0f, 1f )] public float PaintOpacity { get; set; } = 1f;
	[Property, Title( "Brush Radius" ), Range( 1f, 50f )] public float BrushRadius { get; set; } = 5f;

	public SkyboxEditorToolEntry()
	{
		RebuildSidebarOnSelectionChange = false;
	}

	public override void OnEnabled()
	{
		_session = new SkyboxEditorSession();
		FindSkyboxInScene();
		SyncFromComponent();
	}

	public override Widget CreateToolSidebar()
	{
		var sidebar = new ToolSidebarWidget();
		sidebar.AddTitle( "Spyro Skybox", "cloud" );
		sidebar.MinimumWidth = 340;

		var so = this.GetSerialized();

		// File operations
		{
			var group = sidebar.AddGroup( "File" );
			var loadBtn = new Button( "Load .skye", "upload_file" );
			loadBtn.Clicked = LoadSkyeFile;
			group.Add( loadBtn );

			var importBtn = new Button( "Import Spyro Sky (.json)", "videogame_asset" );
			importBtn.Clicked = ImportSpyroSky;
			group.Add( importBtn );

			var statsLabel = new Label( GetStatsText() );
			statsLabel.SetStyles( "color: #888; font-size: 11px; margin: 4px;" );
			group.Add( statsLabel );
		}

		// Color adjustments
		{
			var group = sidebar.AddGroup( "Color" );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( Saturation ) ) ) );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( Brightness ) ) ) );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( Gamma ) ) ) );
		}

		// RGB Shift
		{
			var group = sidebar.AddGroup( "Channel Shift" );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( RedShift ) ) ) );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( GreenShift ) ) ) );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( BlueShift ) ) ) );
		}

		// Paint
		{
			var group = sidebar.AddGroup( "Paint" );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( LeftPaintColor ) ) ) );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( RightPaintColor ) ) ) );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( PaintOpacity ) ) ) );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( BrushRadius ) ) ) );
		}

		// Display
		{
			var group = sidebar.AddGroup( "Display" );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( SkyScale ) ) ) );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( BgColor ) ) ) );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( ShowEdges ) ) ) );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( ShowVertices ) ) ) );
		}

		return sidebar;
	}

	public override void OnUpdate()
	{
		if ( _session.Target == null || !_session.Target.IsValid() )
		{
			FindSkyboxInScene();
			if ( _session.Target == null ) return;
		}

		// Push sidebar values to the component
		SyncToComponent();

		// Set background color on the editor viewport camera
		if ( Camera != null )
			Camera.BackgroundColor = BgColor;

		// Sync paint settings to session
		_session.BrushRadius = BrushRadius;
		_session.LeftColor = new Color32( (byte)(LeftPaintColor.r * 255), (byte)(LeftPaintColor.g * 255), (byte)(LeftPaintColor.b * 255), 255 );
		_session.RightColor = new Color32( (byte)(RightPaintColor.r * 255), (byte)(RightPaintColor.g * 255), (byte)(RightPaintColor.b * 255), 255 );
		_session.LeftOpacity = PaintOpacity;
		_session.RightOpacity = PaintOpacity;

		SkyboxEditorGizmos.UpdateCursor( _session );
		SkyboxEditorGizmos.DrawOverlay( _session, ShowEdges, ShowVertices );
		SkyboxEditorGizmos.HandleInput( _session );
	}

	public override void OnDisabled()
	{
		_brushPreview?.Delete();
		_brushPreview = null;
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
			SyncFromComponent();
		}
	}

	/// <summary>
	/// Push sidebar values to the SkyboxComponent.
	/// </summary>
	private void SyncToComponent()
	{
		var t = _session?.Target;
		if ( t == null ) return;

		if ( t.ColorSaturation != Saturation ) t.ColorSaturation = Saturation;
		if ( t.ColorBrightness != Brightness ) t.ColorBrightness = Brightness;
		if ( t.ColorGamma != Gamma ) t.ColorGamma = Gamma;
		if ( t.SkyboxScale != SkyScale ) t.SkyboxScale = SkyScale;
		if ( t.RedShift != RedShift ) t.RedShift = RedShift;
		if ( t.GreenShift != GreenShift ) t.GreenShift = GreenShift;
		if ( t.BlueShift != BlueShift ) t.BlueShift = BlueShift;
		if ( t.BackgroundColor != BgColor ) t.BackgroundColor = BgColor;
	}

	/// <summary>
	/// Pull values from the component to the sidebar.
	/// </summary>
	private void SyncFromComponent()
	{
		var t = _session?.Target;
		if ( t == null ) return;

		Saturation = t.ColorSaturation;
		Brightness = t.ColorBrightness;
		Gamma = t.ColorGamma;
		SkyScale = t.SkyboxScale;
		RedShift = t.RedShift;
		GreenShift = t.GreenShift;
		BlueShift = t.BlueShift;
		BgColor = t.BackgroundColor;
	}

	private string GetStatsText()
	{
		var data = _session?.Target?.Data;
		if ( data == null ) return "No skybox loaded";
		return $"Verts: {data.Vertices.Count}  Tris: {data.Triangles.Count}  Edges: {data.Edges.Count}";
	}

	private void LoadSkyeFile()
	{
		if ( _session?.Target == null )
		{
			Log.Warning( "No SkyboxComponent in scene. Add one first." );
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
		SyncFromComponent();
		Log.Info( $"Loaded: {_session.Target.Data.Vertices.Count} verts, {_session.Target.Data.Triangles.Count} tris" );
	}

	private void ImportSpyroSky()
	{
		if ( _session?.Target == null )
		{
			Log.Warning( "No SkyboxComponent in scene. Add one first." );
			return;
		}

		var fd = new FileDialog( null );
		fd.Title = "Import Spyro Sky";
		fd.SetNameFilter( "Spyro Sky JSON (*.json)" );

		if ( !fd.Execute() ) return;

		var path = fd.SelectedFile;
		if ( string.IsNullOrEmpty( path ) ) return;

		var content = System.IO.File.ReadAllText( path );
		if ( string.IsNullOrEmpty( content ) )
		{
			Log.Warning( "Failed to read Spyro sky file" );
			return;
		}

		var data = SpyroSkyFormat.ParseJson( content );
		if ( data == null )
		{
			Log.Warning( "Failed to parse Spyro sky JSON" );
			return;
		}

		_session.Target.LoadData( data );
		SyncFromComponent();

		var bg = data.BackgroundColor;
		Log.Info( $"Imported Spyro sky: {data.Vertices.Count} verts, {data.Triangles.Count} tris, bg=({bg.r},{bg.g},{bg.b})" );
	}
}
