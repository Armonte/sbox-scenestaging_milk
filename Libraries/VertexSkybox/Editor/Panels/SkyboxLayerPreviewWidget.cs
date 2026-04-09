using Sandbox;
using System;
using System.Collections.Generic;

namespace Editor;

/// <summary>
/// 3D layer preview widget: renders the skybox sphere in a mini viewport.
/// Active layer shown in full color, other layers greyscale/dimmed.
/// Camera syncs with the main editor viewport angle.
/// </summary>
public class SkyboxLayerPreviewWidget : SceneRenderingWidget
{
	public enum ViewMode
	{
		AllLayers,
		ActiveOnly,
		ActiveHighlight
	}

	private SkyboxEditorToolEntry _tool;
	private CameraComponent _camera;
	private SceneObject _sceneObject;
	private Model _model;
	private ViewMode _viewMode = ViewMode.ActiveHighlight;
	private int _lastLayer = -1;
	private int _lastVertCount = -1;

	public SkyboxLayerPreviewWidget( SkyboxEditorToolEntry tool, Widget parent = null ) : base( parent )
	{
		_tool = tool;

		MinimumSize = 200;
		FixedHeight = 200;

		Scene = new Scene();

		using ( Scene.Push() )
		{
			var camGo = new GameObject( true, "preview_camera" );
			_camera = camGo.GetOrAddComponent<CameraComponent>( false );
			_camera.BackgroundColor = Color.Black;
			_camera.FieldOfView = 60;
			_camera.ZFar = 50000;
			_camera.Enabled = true;

			// Position camera looking at origin
			_camera.WorldPosition = new Vector3( 300, 0, 100 );
			_camera.WorldRotation = Rotation.LookAt( -_camera.WorldPosition.Normal, Vector3.Up );
		}

		Camera = _camera;
		OnPreFrame += UpdatePreview;
	}

	public ViewMode CurrentViewMode
	{
		get => _viewMode;
		set
		{
			if ( _viewMode == value ) return;
			_viewMode = value;
			_lastLayer = -1; // Force rebuild
		}
	}

	private void UpdatePreview()
	{
		if ( _tool?.Session?.Target?.Data == null ) return;

		SyncCameraFromEditor();
		RebuildIfNeeded();
	}

	private void SyncCameraFromEditor()
	{
		if ( !_camera.IsValid() ) return;

		// Try to get the main editor camera's rotation
		var editorCam = _tool.Camera;
		if ( editorCam != null )
		{
			// Use editor camera rotation but keep fixed distance
			var rotation = editorCam.WorldRotation;
			float distance = 300f;
			var forward = rotation.Forward;
			_camera.WorldPosition = -forward * distance;
			_camera.WorldRotation = rotation;
		}

		// Sync background color
		_camera.BackgroundColor = _tool.BgColor;
	}

	private void RebuildIfNeeded()
	{
		var data = _tool.Session?.Target?.Data;
		if ( data == null ) return;

		int currentLayer = _tool.Session.CurrentLayer;
		int vertCount = data.Vertices.Count;

		// Only rebuild when layer or data changes
		if ( currentLayer == _lastLayer && vertCount == _lastVertCount )
			return;

		_lastLayer = currentLayer;
		_lastVertCount = vertCount;

		RebuildModel( data, currentLayer );
	}

	private void RebuildModel( SkyboxData data, int activeLayer )
	{
		_sceneObject?.Delete();
		_sceneObject = null;
		_model = null;

		if ( data.Vertices.Count == 0 || data.Triangles.Count == 0 ) return;

		var mat = Material.Load( "materials/vertexcolor_skybox.vmat" );
		if ( mat == null || !mat.IsValid )
			mat = Material.Load( "materials/dev/reflectivity_30.vmat" );

		var vertices = new List<Vertex>( data.Vertices.Count );
		for ( int i = 0; i < data.Vertices.Count; i++ )
		{
			var sv = data.Vertices[i];
			var rp = sv.RenderPosition;
			var localPos = new Vector3( -rp.x, rp.y, rp.z );

			Color32 color;
			switch ( _viewMode )
			{
				case ViewMode.ActiveOnly:
					if ( sv.LayerDepth != activeLayer )
					{
						// Skip vertices not on active layer (make transparent/invisible)
						color = new Color32( 0, 0, 0, 0 );
					}
					else
					{
						color = sv.Color;
					}
					break;

				case ViewMode.ActiveHighlight:
					if ( sv.LayerDepth != activeLayer )
					{
						// Desaturate: convert to greyscale and dim
						float luma = sv.Color.r * 0.299f + sv.Color.g * 0.587f + sv.Color.b * 0.114f;
						luma *= 0.4f; // Dim to 40%
						byte g = (byte)luma;
						color = new Color32( g, g, g, 255 );
					}
					else
					{
						color = sv.Color;
					}
					break;

				default: // AllLayers
					color = sv.Color;
					break;
			}

			vertices.Add( new Vertex( localPos, color ) { Normal = rp.Normal } );
		}

		var indices = new List<int>( data.Triangles.Count * 3 );
		for ( int i = 0; i < data.Triangles.Count; i++ )
		{
			var tri = data.Triangles[i];

			// In ActiveOnly mode, skip triangles with invisible verts
			if ( _viewMode == ViewMode.ActiveOnly )
			{
				bool allActive = true;
				if ( tri.V0 < data.Vertices.Count && data.Vertices[tri.V0].LayerDepth != activeLayer ) allActive = false;
				if ( tri.V1 < data.Vertices.Count && data.Vertices[tri.V1].LayerDepth != activeLayer ) allActive = false;
				if ( tri.V2 < data.Vertices.Count && data.Vertices[tri.V2].LayerDepth != activeLayer ) allActive = false;
				if ( !allActive ) continue;
			}

			indices.Add( tri.V2 );
			indices.Add( tri.V1 );
			indices.Add( tri.V0 );
		}

		if ( indices.Count == 0 ) return;

		var mesh = new Mesh( mat );
		mesh.CreateVertexBuffer( vertices.Count, Vertex.Layout, vertices );
		mesh.CreateIndexBuffer( indices.Count, indices );
		mesh.Bounds = BBox.FromPositionAndSize( Vector3.Zero, 200f );

		var builder = new ModelBuilder();
		builder.AddMesh( mesh );
		_model = builder.Create();

		using ( Scene.Push() )
		{
			_sceneObject = new SceneObject( Scene.SceneWorld, _model, Transform.Zero );
			_sceneObject.Flags.CastShadows = false;
			_sceneObject.Flags.IsOpaque = true;
		}
	}

	/// <summary>
	/// Force a rebuild on next frame (call when data changes from painting etc).
	/// </summary>
	public void Invalidate()
	{
		_lastVertCount = -1;
	}

	public override void OnDestroyed()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
		Scene?.Delete();
		Scene = null;
		base.OnDestroyed();
	}
}
