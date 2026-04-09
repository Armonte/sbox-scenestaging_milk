using Sandbox;
using System;
using System.Collections.Generic;

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
	public SkyboxEditorSession Session { get; private set; }
	private SkyboxBrushPreview _brushPreview;
	private IDisposable _propertyUndoScope;
	private HashSet<KeyCode> _keysLastFrame = new();
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
		Session = new SkyboxEditorSession();
		FindSkyboxInScene();
		SyncFromComponent();
	}

	public override IEnumerable<EditorTool> GetSubtools()
	{
		yield return new SkyboxPaintTool( this );
		yield return new SkyboxPipetteTool( this );
		yield return new SkyboxGradientTool( this );
		yield return new SkyboxSketchTool( this );
		yield return new SkyboxGrabTool( this );
		yield return new SkyboxSelectTool( this );
		yield return new SkyboxPipetteSelectTool( this );
		yield return new SkyboxSelectionGroupTool( this );
		yield return new SkyboxCreateTool( this );
		yield return new SkyboxDeleteTool( this );
		yield return new SkyboxEdgeFlipTool( this );
		yield return new SkyboxEdgeCollapseTool( this );
		yield return new SkyboxTriFillTool( this );
		yield return new SkyboxAutofillTool( this );
		yield return new SkyboxBeautifyTool( this );
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

			var saveBtn = new Button( "Save .skye", "save" );
			saveBtn.Clicked = SaveSkyeFile;
			group.Add( saveBtn );

			var importBtn = new Button( "Import Spyro Sky (.json)", "videogame_asset" );
			importBtn.Clicked = ImportSpyroSky;
			group.Add( importBtn );

			var exportObjBtn = new Button( "Export OBJ", "upload" );
			exportObjBtn.Clicked = ExportObj;
			group.Add( exportObjBtn );

			var exportPlyBtn = new Button( "Export PLY", "upload" );
			exportPlyBtn.Clicked = ExportPly;
			group.Add( exportPlyBtn );

			var newSkyBtn = new Button( "New Sky", "add_circle" );
			newSkyBtn.Clicked = NewSky;
			group.Add( newSkyBtn );

			var fixBtn = new Button( "Fix Errors", "build" );
			fixBtn.Clicked = FixErrors;
			group.Add( fixBtn );

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

		// Palette
		{
			var group = sidebar.AddGroup( "Palette" );
			group.Add( new SkyboxPaletteWidget( this ) );

			var genBtn = new Button( "Palette from Sky", "palette" );
			genBtn.Clicked = () =>
			{
				var data = Session?.Target?.Data;
				if ( data == null ) return;
				SkyboxPaletteWidget.GeneratePaletteFromSky( data );
			};
			group.Add( genBtn );
		}

		// Display
		{
			var group = sidebar.AddGroup( "Display" );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( SkyScale ) ) ) );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( BgColor ) ) ) );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( ShowEdges ) ) ) );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( ShowVertices ) ) ) );
		}

		// Layers
		{
			var group = sidebar.AddGroup( "Layer" );

			// 3D layer preview
			var preview = new SkyboxLayerPreviewWidget( this );
			group.Add( preview );

			// View mode buttons
			var modeRow = new Widget();
			modeRow.Layout = Layout.Row();
			modeRow.Layout.Spacing = 2;

			var allBtn = new Button( "All" );
			allBtn.Clicked = () => preview.CurrentViewMode = SkyboxLayerPreviewWidget.ViewMode.AllLayers;
			modeRow.Layout.Add( allBtn );

			var highlightBtn = new Button( "Highlight" );
			highlightBtn.Clicked = () => preview.CurrentViewMode = SkyboxLayerPreviewWidget.ViewMode.ActiveHighlight;
			modeRow.Layout.Add( highlightBtn );

			var soloBtn = new Button( "Solo" );
			soloBtn.Clicked = () => preview.CurrentViewMode = SkyboxLayerPreviewWidget.ViewMode.ActiveOnly;
			modeRow.Layout.Add( soloBtn );

			group.Add( modeRow );

			var layerLabel = new Label( $"Current Layer: {Session?.CurrentLayer ?? 0}" );
			layerLabel.SetStyles( "font-size: 12px; margin: 4px;" );
			group.Add( layerLabel );

			var btnRow = new Widget();
			btnRow.Layout = Layout.Row();
			btnRow.Layout.Spacing = 4;

			var downBtn = new Button( "Layer Down", "arrow_downward" );
			downBtn.Clicked = () => LayerMove( -1 );
			btnRow.Layout.Add( downBtn );

			var upBtn = new Button( "Layer Up", "arrow_upward" );
			upBtn.Clicked = () => LayerMove( 1 );
			btnRow.Layout.Add( upBtn );

			group.Add( btnRow );
		}

		return sidebar;
	}

	public override void OnUpdate()
	{
		if ( Session == null ) return;

		if ( Session.Target == null || !Session.Target.IsValid() )
		{
			FindSkyboxInScene();
			if ( Session.Target == null ) return;
		}

		SyncToComponent();
		HandleKeyboardShortcuts();

		if ( Camera != null )
			Camera.BackgroundColor = BgColor;

		// Sync paint settings to session so sub-tools can read them
		Session.BrushRadius = BrushRadius;
		Session.LeftColor = new Color32( (byte)(LeftPaintColor.r * 255), (byte)(LeftPaintColor.g * 255), (byte)(LeftPaintColor.b * 255), 255 );
		Session.RightColor = new Color32( (byte)(RightPaintColor.r * 255), (byte)(RightPaintColor.g * 255), (byte)(RightPaintColor.b * 255), 255 );
		Session.LeftOpacity = PaintOpacity;
		Session.RightOpacity = PaintOpacity;

		UpdateKeyTracking();
	}

	public override void OnDisabled()
	{
		_propertyUndoScope?.Dispose();
		_propertyUndoScope = null;
		_brushPreview?.Delete();
		_brushPreview = null;
		Session = null;
	}

	/// <summary>
	/// Move selected vertices (and connected geometry) to a higher or lower layer.
	/// Layers range from 0-5. Affects LayerDepth which controls parallax RenderScale.
	/// </summary>
	private void LayerMove( int direction )
	{
		var data = Session?.Target?.Data;
		if ( data == null || Session.SelectedVertices.Count == 0 ) return;

		int newLayer = (Session.CurrentLayer + direction).Clamp( 0, 5 );
		if ( newLayer == Session.CurrentLayer ) return;

		Session.Target.SaveState();
		using ( SceneEditorSession.Active
			.UndoScope( direction > 0 ? "Layer Up" : "Layer Down" )
			.WithComponentChanges( Session.Target )
			.Push() )
		{
			// Move selected vertices to the new layer
			foreach ( var idx in Session.SelectedVertices )
			{
				if ( idx < 0 || idx >= data.Vertices.Count ) continue;
				var v = data.Vertices[idx];
				v.LayerDepth = (byte)newLayer;
				data.Vertices[idx] = v;
			}

			Session.Target.SaveState();
		}

		Session.CurrentLayer = newLayer;
		Session.Target.RebuildMesh();
	}

	private void HandleKeyboardShortcuts()
	{
		if ( Session?.Target?.Data == null ) return;

		// Selection: Ctrl+A = Select All / Deselect All (toggle)
		if ( Gizmo.IsCtrlPressed && WasKeyJustPressed( KeyCode.A ) )
		{
			if ( Session.SelectedVertices.Count > 0 )
				Session.DeselectAll();
			else
				Session.SelectAll();
		}

		// Ctrl+I = Select Inverse
		if ( Gizmo.IsCtrlPressed && WasKeyJustPressed( KeyCode.I ) )
			Session.SelectInverse();

		// Ctrl+L = Select Linked
		if ( Gizmo.IsCtrlPressed && WasKeyJustPressed( KeyCode.L ) )
			Session.SelectLinked();

		// Numpad +/- = Select More/Less
		if ( WasKeyJustPressed( KeyCode.BracketRight ) )
			Session.SelectMore();
		if ( WasKeyJustPressed( KeyCode.BracketLeft ) )
			Session.SelectLess();

		// Ctrl+C = Copy
		if ( Gizmo.IsCtrlPressed && WasKeyJustPressed( KeyCode.C ) )
			Session.CopySelection();

		// Ctrl+V = Paste
		if ( Gizmo.IsCtrlPressed && WasKeyJustPressed( KeyCode.V ) )
		{
			Session.Target.SaveState();
			using ( SceneEditorSession.Active
				.UndoScope( "Paste Geometry" )
				.WithComponentChanges( Session.Target )
				.Push() )
			{
				Session.PasteSelection();
				Session.Target.SaveState();
			}
			Session.Target.RebuildMesh();
		}

		// Tool hotkeys (only when Ctrl is NOT held, to avoid conflicting with Ctrl+shortcuts)
		if ( !Gizmo.IsCtrlPressed && !Gizmo.IsShiftPressed )
		{
			SwitchToolOnKey( KeyCode.B, nameof( SkyboxPaintTool ) );
			SwitchToolOnKey( KeyCode.I, nameof( SkyboxPipetteTool ) );
			SwitchToolOnKey( KeyCode.G, nameof( SkyboxGradientTool ) );
			SwitchToolOnKey( KeyCode.P, nameof( SkyboxSketchTool ) );
			SwitchToolOnKey( KeyCode.O, nameof( SkyboxGrabTool ) );
			SwitchToolOnKey( KeyCode.S, nameof( SkyboxSelectTool ) );
			SwitchToolOnKey( KeyCode.C, nameof( SkyboxCreateTool ) );
			SwitchToolOnKey( KeyCode.X, nameof( SkyboxDeleteTool ) );
			SwitchToolOnKey( KeyCode.F, nameof( SkyboxEdgeFlipTool ) );
			SwitchToolOnKey( KeyCode.E, nameof( SkyboxEdgeCollapseTool ) );
			SwitchToolOnKey( KeyCode.T, nameof( SkyboxTriFillTool ) );
			SwitchToolOnKey( KeyCode.A, nameof( SkyboxAutofillTool ) );
			SwitchToolOnKey( KeyCode.D, nameof( SkyboxBeautifyTool ) );
		}
	}

	private void SwitchToolOnKey( KeyCode key, string toolName )
	{
		if ( WasKeyJustPressed( key ) )
			EditorToolManager.SetSubTool( toolName );
	}

	/// <summary>
	/// Returns true if the key was just pressed this frame (down now, not down last frame).
	/// </summary>
	private bool WasKeyJustPressed( KeyCode key )
	{
		return Application.IsKeyDown( key ) && !_keysLastFrame.Contains( key );
	}

	/// <summary>
	/// Call at the end of OnUpdate to snapshot key states for next-frame edge detection.
	/// </summary>
	private void UpdateKeyTracking()
	{
		_keysLastFrame.Clear();
		// Track keys we use for shortcuts
		KeyCode[] tracked = {
			KeyCode.A, KeyCode.B, KeyCode.C, KeyCode.D, KeyCode.E, KeyCode.F,
			KeyCode.G, KeyCode.I, KeyCode.L, KeyCode.O, KeyCode.P, KeyCode.S,
			KeyCode.T, KeyCode.V, KeyCode.X,
			KeyCode.Num0, KeyCode.Num1, KeyCode.Num2, KeyCode.Num3, KeyCode.Num4,
			KeyCode.Num5, KeyCode.Num6, KeyCode.Num7, KeyCode.Num8, KeyCode.Num9,
			KeyCode.BracketRight, KeyCode.BracketLeft
		};
		foreach ( var k in tracked )
		{
			if ( Application.IsKeyDown( k ) )
				_keysLastFrame.Add( k );
		}
	}

	private void FindSkyboxInScene()
	{
		var scene = SceneEditorSession.Active?.Scene;
		if ( scene == null ) return;

		var skybox = scene.GetAllComponents<SkyboxComponent>().FirstOrDefault();
		if ( skybox != null && skybox != Session.Target )
		{
			Session.SetTarget( skybox );
			SyncFromComponent();
		}
	}

	/// <summary>
	/// Push sidebar values to the SkyboxComponent, wrapped in undo scope
	/// so slider drags are undoable. Opens scope on first change, closes
	/// when mouse is released (batches entire drag into one undo entry).
	/// </summary>
	private void SyncToComponent()
	{
		var t = Session?.Target;
		if ( t == null ) return;

		bool changed =
			t.ColorSaturation != Saturation ||
			t.ColorBrightness != Brightness ||
			t.ColorGamma != Gamma ||
			t.SkyboxScale != SkyScale ||
			t.RedShift != RedShift ||
			t.GreenShift != GreenShift ||
			t.BlueShift != BlueShift ||
			t.BackgroundColor != BgColor;

		if ( changed )
		{
			// Open undo scope on first change (start of slider drag)
			if ( _propertyUndoScope == null )
			{
				_propertyUndoScope = SceneEditorSession.Active
					.UndoScope( "Skybox Property Change" )
					.WithComponentChanges( t )
					.Push();
			}

			t.ColorSaturation = Saturation;
			t.ColorBrightness = Brightness;
			t.ColorGamma = Gamma;
			t.SkyboxScale = SkyScale;
			t.RedShift = RedShift;
			t.GreenShift = GreenShift;
			t.BlueShift = BlueShift;
			t.BackgroundColor = BgColor;
		}

		// Close scope when mouse is released (end of slider drag)
		if ( _propertyUndoScope != null && !Gizmo.IsLeftMouseDown )
		{
			_propertyUndoScope.Dispose();
			_propertyUndoScope = null;
		}
	}

	/// <summary>
	/// Pull values from the component to the sidebar.
	/// </summary>
	private void SyncFromComponent()
	{
		var t = Session?.Target;
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
		var data = Session?.Target?.Data;
		if ( data == null ) return "No skybox loaded";
		return $"Verts: {data.Vertices.Count}  Tris: {data.Triangles.Count}  Edges: {data.Edges.Count}";
	}

	private void LoadSkyeFile()
	{
		if ( Session?.Target == null )
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

		Session.Target.LoadFromString( content );
		SyncFromComponent();
		Log.Info( $"Loaded: {Session.Target.Data.Vertices.Count} verts, {Session.Target.Data.Triangles.Count} tris" );
	}

	private void ImportSpyroSky()
	{
		if ( Session?.Target == null )
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

		Session.Target.LoadData( data );
		SyncFromComponent();

		var bg = data.BackgroundColor;
		Log.Info( $"Imported Spyro sky: {data.Vertices.Count} verts, {data.Triangles.Count} tris, bg=({bg.r},{bg.g},{bg.b})" );
	}

	private void SaveSkyeFile()
	{
		if ( Session?.Target?.Data == null ) return;

		var fd = new FileDialog( null );
		fd.Title = "Save Skybox";
		fd.SetNameFilter( "Skybox Files (*.skye)" );

		if ( !fd.Execute() ) return;

		var path = fd.SelectedFile;
		if ( string.IsNullOrEmpty( path ) ) return;

		var content = SkyeFormat.WriteString( Session.Target.Data );
		System.IO.File.WriteAllText( path, content );
		Log.Info( $"Saved: {path}" );
	}

	private void ExportObj()
	{
		if ( Session?.Target?.Data == null ) return;

		var fd = new FileDialog( null );
		fd.Title = "Export OBJ";
		fd.SetNameFilter( "Wavefront OBJ (*.obj)" );

		if ( !fd.Execute() ) return;

		var path = fd.SelectedFile;
		if ( string.IsNullOrEmpty( path ) ) return;

		MeshExporter.ExportObj( Session.Target.Data, path );
		Log.Info( $"Exported OBJ: {path}" );
	}

	private void ExportPly()
	{
		if ( Session?.Target?.Data == null ) return;

		var fd = new FileDialog( null );
		fd.Title = "Export PLY";
		fd.SetNameFilter( "Stanford PLY (*.ply)" );

		if ( !fd.Execute() ) return;

		var path = fd.SelectedFile;
		if ( string.IsNullOrEmpty( path ) ) return;

		MeshExporter.ExportPly( Session.Target.Data, path );
		Log.Info( $"Exported PLY: {path}" );
	}

	private void NewSky()
	{
		if ( Session?.Target == null ) return;

		Session.Target.SaveState();
		using ( SceneEditorSession.Active
			.UndoScope( "New Sky" )
			.WithComponentChanges( Session.Target )
			.Push() )
		{
			Session.Target.LoadData( SphereGeometry.GenerateSphere( 100f, 12, 24 ) );
			Session.Target.SaveState();
		}

		SyncFromComponent();
		Log.Info( "Created new blank sky" );
	}

	/// <summary>
	/// Fix errors in the mesh data. Ported from solve_errors in the original editor:
	/// 1. Remove self-loop edges (v1==v2)
	/// 2. Remove duplicate edges
	/// 3. Remove degenerate triangles (any 2 verts same)
	/// 4. Fix reversed winding (dot(normal,centroid) &lt;= 0)
	/// 5. Rebuild adjacency
	/// </summary>
	private void FixErrors()
	{
		var data = Session?.Target?.Data;
		if ( data == null ) return;

		Session.Target.SaveState();
		using ( SceneEditorSession.Active
			.UndoScope( "Fix Errors" )
			.WithComponentChanges( Session.Target )
			.Push() )
		{
			int fixes = 0;

			// 1. Remove self-loop edges
			for ( int i = data.Edges.Count - 1; i >= 0; i-- )
			{
				if ( data.Edges[i].V1 == data.Edges[i].V2 )
				{
					data.Edges.RemoveAt( i );
					fixes++;
				}
			}

			// 2. Remove duplicate edges
			var seen = new HashSet<(int, int)>();
			for ( int i = data.Edges.Count - 1; i >= 0; i-- )
			{
				var key = data.Edges[i].SortedKey;
				if ( !seen.Add( key ) )
				{
					data.Edges.RemoveAt( i );
					fixes++;
				}
			}

			// 3. Remove degenerate triangles
			for ( int i = data.Triangles.Count - 1; i >= 0; i-- )
			{
				var tri = data.Triangles[i];
				if ( tri.V0 == tri.V1 || tri.V1 == tri.V2 || tri.V0 == tri.V2 )
				{
					data.Triangles.RemoveAt( i );
					fixes++;
				}
			}

			// 4. Fix reversed winding
			for ( int i = 0; i < data.Triangles.Count; i++ )
			{
				var tri = data.Triangles[i];
				if ( tri.V0 >= data.Vertices.Count || tri.V1 >= data.Vertices.Count || tri.V2 >= data.Vertices.Count )
					continue;

				var p0 = data.Vertices[tri.V0].Position;
				var p1 = data.Vertices[tri.V1].Position;
				var p2 = data.Vertices[tri.V2].Position;

				var normal = Vector3.Cross( p1 - p0, p2 - p0 );
				var centroid = (p0 + p1 + p2) / 3f;

				if ( Vector3.Dot( normal, centroid ) <= 1e-10f )
				{
					// Swap v0 and v1 to fix winding
					data.Triangles[i] = new SkyboxTriangle( tri.V1, tri.V0, tri.V2, tri.E0, tri.E1, tri.E2 );
					fixes++;
				}
			}

			data.InvalidateAdjacency();
			Session.Target.SaveState();

			Log.Info( $"Fix Errors: {fixes} issues fixed" );
		}

		Session.Target.RebuildMesh();
	}
}
