using Sandbox;

namespace Editor;

/// <summary>
/// Efficient batch renderer for skybox edit mode overlays.
/// Uses SceneCustomObject with RenderOverride for proper rendering context.
/// </summary>
public class SkyboxEditorRenderer
{
	private VertexBuffer _edgeVB;
	private VertexBuffer _vertVB;
	private Material _material;
	private SceneCustomObject _edgeObject;
	private SceneCustomObject _vertObject;

	private int _lastVertCount;
	private int _lastEdgeCount;
	private int _lastHovered = -1;
	private float _lastScale;
	private Vector3 _lastPos;
	private bool _dirty = true;

	public bool ShowEdges { get; set; }
	public bool ShowVertices { get; set; }
	public float VertexDotSize { get; set; } = 1.5f;

	public void MarkDirty() => _dirty = true;

	public void Update( SkyboxEditorSession session, bool showEdges, bool showVertices )
	{
		ShowEdges = showEdges;
		ShowVertices = showVertices;

		if ( !EnsureMaterial() ) return;

		var target = session?.Target;
		if ( target == null || target.Data == null ) return;

		var scene = target.Scene?.SceneWorld;
		if ( scene == null ) return;

		// Rebuild buffers if needed
		bool edgesRebuilt = false;
		bool vertsRebuilt = false;

		if ( showEdges )
		{
			var data = target.Data;
			var pos = target.WorldPosition;
			float scale = target.SkyboxScale;

			if ( _dirty || _edgeVB == null || data.Edges.Count != _lastEdgeCount || pos != _lastPos || scale != _lastScale )
			{
				RebuildEdgeBuffer( session );
				_lastEdgeCount = data.Edges.Count;
				_lastPos = pos;
				_lastScale = scale;
				edgesRebuilt = true;
			}
		}

		if ( showVertices )
		{
			var data = target.Data;
			if ( _dirty || _vertVB == null || data.Vertices.Count != _lastVertCount || session.HoveredVertex != _lastHovered )
			{
				RebuildVertexBuffer( session );
				_lastVertCount = data.Vertices.Count;
				_lastHovered = session.HoveredVertex;
				vertsRebuilt = true;
			}
		}

		_dirty = false;

		// Create/recreate scene objects if needed
		if ( showEdges && (_edgeObject == null || !_edgeObject.IsValid() || edgesRebuilt) )
		{
			_edgeObject?.Delete();
			_edgeObject = CreateOverlayObject( scene );
			_edgeObject.RenderOverride = RenderEdges;
		}
		else if ( !showEdges && _edgeObject != null )
		{
			_edgeObject.Delete();
			_edgeObject = null;
		}

		if ( showVertices && (_vertObject == null || !_vertObject.IsValid() || vertsRebuilt) )
		{
			_vertObject?.Delete();
			_vertObject = CreateOverlayObject( scene );
			_vertObject.RenderOverride = RenderVertices;
		}
		else if ( !showVertices && _vertObject != null )
		{
			_vertObject.Delete();
			_vertObject = null;
		}
	}

	private SceneCustomObject CreateOverlayObject( SceneWorld scene )
	{
		var obj = new SceneCustomObject( scene );
		obj.Flags.CastShadows = false;
		obj.Flags.IsOpaque = true;
		obj.Bounds = BBox.FromPositionAndSize( Vector3.Zero, 999999f );
		obj.RenderLayer = SceneRenderLayer.OverlayWithDepth;
		return obj;
	}

	private void RenderEdges( SceneObject so )
	{
		_edgeVB?.Draw( _material );
	}

	private void RenderVertices( SceneObject so )
	{
		_vertVB?.Draw( _material );
	}

	private bool EnsureMaterial()
	{
		_material ??= Material.FromShader( "shaders/line.shader" );
		return _material != null && _material.IsValid;
	}

	private void RebuildEdgeBuffer( SkyboxEditorSession session )
	{
		var data = session.Target.Data;
		var pos = session.Target.WorldPosition;
		var rot = session.Target.WorldRotation;
		float scale = session.Target.SkyboxScale;

		_edgeVB = new VertexBuffer();
		_edgeVB.Init( false );

		var col = new Color32( 120, 120, 120, 200 );
		var wp = ComputeWorldPositions( data, pos, rot, scale );

		foreach ( var edge in data.Edges )
		{
			if ( edge.V1 >= wp.Length || edge.V2 >= wp.Length ) continue;
			// Degenerate triangle = visible as a thin line
			_edgeVB.Add( new Vertex( wp[edge.V1], col ) );
			_edgeVB.Add( new Vertex( wp[edge.V2], col ) );
			_edgeVB.Add( new Vertex( wp[edge.V2], col ) );
		}
	}

	private void RebuildVertexBuffer( SkyboxEditorSession session )
	{
		var data = session.Target.Data;
		var pos = session.Target.WorldPosition;
		var rot = session.Target.WorldRotation;
		float scale = session.Target.SkyboxScale;

		_vertVB = new VertexBuffer();
		_vertVB.Init( false );

		var wp = ComputeWorldPositions( data, pos, rot, scale );

		Vector3 camPos = Vector3.Zero;
		var cam = SceneEditorSession.Active?.Scene?.Camera;
		if ( cam != null ) camPos = cam.WorldPosition;

		float dotSize = VertexDotSize * scale * 0.01f;

		for ( int i = 0; i < data.Vertices.Count; i++ )
		{
			var p = wp[i];
			Color32 c;
			float s = dotSize;

			if ( session.SelectedVertices.Contains( i ) )
			{
				c = new Color32( 255, 128, 0, 255 );
				s *= 1.5f;
			}
			else if ( i == session.HoveredVertex )
			{
				c = new Color32( 255, 255, 0, 255 );
				s *= 2f;
			}
			else
			{
				c = data.Vertices[i].Color;
			}

			// Billboard quad facing camera
			var dir = (p - camPos);
			if ( dir.LengthSquared < 0.001f ) dir = Vector3.Forward;
			dir = dir.Normal;

			var right = Vector3.Cross( dir, Vector3.Up ).Normal * s;
			var up = Vector3.Cross( right, dir ).Normal * s;

			if ( right.LengthSquared < 0.001f )
			{
				right = Vector3.Forward * s;
				up = Vector3.Right * s;
			}

			var tl = p - right + up;
			var tr = p + right + up;
			var bl = p - right - up;
			var br = p + right - up;

			_vertVB.Add( new Vertex( tl, c ) );
			_vertVB.Add( new Vertex( tr, c ) );
			_vertVB.Add( new Vertex( br, c ) );
			_vertVB.Add( new Vertex( tl, c ) );
			_vertVB.Add( new Vertex( br, c ) );
			_vertVB.Add( new Vertex( bl, c ) );
		}
	}

	private static Vector3[] ComputeWorldPositions( SkyboxData data, Vector3 pos, Rotation rot, float scale )
	{
		var wp = new Vector3[data.Vertices.Count];
		for ( int i = 0; i < data.Vertices.Count; i++ )
		{
			var rp = data.Vertices[i].RenderPosition;
			wp[i] = pos + rot * (new Vector3( -rp.x, rp.y, rp.z ) * scale);
		}
		return wp;
	}

	public void Destroy()
	{
		_edgeObject?.Delete();
		_vertObject?.Delete();
		_edgeObject = null;
		_vertObject = null;
		_edgeVB = null;
		_vertVB = null;
	}
}
