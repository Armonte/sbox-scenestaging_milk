using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Editor;

/// <summary>
/// Holds the editing state for the skybox editor tool.
/// Persists across tool updates, tracks selection, cursor, display options.
/// </summary>
public class SkyboxEditorSession
{
	/// <summary>
	/// The SkyboxComponent being edited.
	/// </summary>
	public SkyboxComponent Target { get; private set; }

	// --- Cursor State ---

	/// <summary>
	/// Whether the cursor is currently intersecting the skybox sphere.
	/// </summary>
	public bool CursorOnSphere { get; set; }

	/// <summary>
	/// The cursor's position on the sphere surface (local render space).
	/// </summary>
	public Vector3 CursorPosition { get; set; }

	/// <summary>
	/// The cursor's position in world space (from Scene.Trace).
	/// </summary>
	public Vector3 CursorWorldPosition { get; set; }

	/// <summary>
	/// Index of the vertex nearest to the cursor, or -1.
	/// </summary>
	public int HoveredVertex { get; set; } = -1;

	// --- Selection ---

	/// <summary>
	/// Currently selected vertex indices.
	/// </summary>
	public HashSet<int> SelectedVertices { get; set; } = new();

	/// <summary>
	/// Current editing layer (0-5).
	/// </summary>
	public int CurrentLayer { get; set; } = 0;

	// --- Brush ---

	/// <summary>
	/// Brush radius for painting and geometry tools.
	/// </summary>
	public float BrushRadius { get; set; } = 5f;

	// --- Colors ---

	/// <summary>
	/// Left mouse button color.
	/// </summary>
	public Color32 LeftColor { get; set; } = new Color32( 255, 255, 255, 255 );

	/// <summary>
	/// Right mouse button color.
	/// </summary>
	public Color32 RightColor { get; set; } = new Color32( 0, 0, 0, 255 );

	/// <summary>
	/// Left button paint opacity (0-1).
	/// </summary>
	public float LeftOpacity { get; set; } = 1f;

	/// <summary>
	/// Right button paint opacity (0-1).
	/// </summary>
	public float RightOpacity { get; set; } = 1f;

	// --- Display ---

	public bool ShowEdges { get; set; } = false;
	public bool ShowVertices { get; set; } = false;
	public bool ShowSelection { get; set; } = true;
	public bool ShowTriangles { get; set; } = true;
	public bool ShowSketch { get; set; } = false;

	// --- Clipboard ---
	public List<SkyboxVertex> ClipboardVertices { get; set; }
	public List<SkyboxEdge> ClipboardEdges { get; set; }
	public List<SkyboxTriangle> ClipboardTriangles { get; set; }

	/// <summary>
	/// Set the target component.
	/// </summary>
	public void SetTarget( SkyboxComponent component )
	{
		Target = component;
		SelectedVertices.Clear();
		HoveredVertex = -1;
		CursorOnSphere = false;
	}

	/// <summary>Select all vertices in the current layer.</summary>
	public void SelectAll()
	{
		var data = Target?.Data;
		if ( data == null ) return;

		for ( int i = 0; i < data.Vertices.Count; i++ )
		{
			if ( data.Vertices[i].LayerDepth == CurrentLayer )
				SelectedVertices.Add( i );
		}
	}

	/// <summary>Deselect all vertices.</summary>
	public void DeselectAll()
	{
		SelectedVertices.Clear();
	}

	/// <summary>Invert the selection within the current layer.</summary>
	public void SelectInverse()
	{
		var data = Target?.Data;
		if ( data == null ) return;

		var newSel = new HashSet<int>();
		for ( int i = 0; i < data.Vertices.Count; i++ )
		{
			if ( data.Vertices[i].LayerDepth == CurrentLayer && !SelectedVertices.Contains( i ) )
				newSel.Add( i );
		}
		SelectedVertices = newSel;
	}

	/// <summary>Grow selection to include connected vertices.</summary>
	public void SelectLinked()
	{
		var data = Target?.Data;
		if ( data == null || SelectedVertices.Count == 0 ) return;

		var frontier = new Queue<int>( SelectedVertices );
		while ( frontier.Count > 0 )
		{
			int vi = frontier.Dequeue();
			var edges = data.GetEdgesForVertex( vi );
			foreach ( var ei in edges )
			{
				if ( ei >= data.Edges.Count ) continue;
				var edge = data.Edges[ei];
				int other = edge.V1 == vi ? edge.V2 : edge.V1;
				if ( other >= 0 && other < data.Vertices.Count && SelectedVertices.Add( other ) )
					frontier.Enqueue( other );
			}
		}
	}

	/// <summary>Grow selection by one ring of connected vertices.</summary>
	public void SelectMore()
	{
		var data = Target?.Data;
		if ( data == null || SelectedVertices.Count == 0 ) return;

		var toAdd = new HashSet<int>();
		foreach ( var vi in SelectedVertices )
		{
			var edges = data.GetEdgesForVertex( vi );
			foreach ( var ei in edges )
			{
				if ( ei >= data.Edges.Count ) continue;
				var edge = data.Edges[ei];
				int other = edge.V1 == vi ? edge.V2 : edge.V1;
				if ( other >= 0 && other < data.Vertices.Count )
					toAdd.Add( other );
			}
		}
		foreach ( var v in toAdd )
			SelectedVertices.Add( v );
	}

	/// <summary>Shrink selection by removing boundary vertices.</summary>
	public void SelectLess()
	{
		var data = Target?.Data;
		if ( data == null || SelectedVertices.Count == 0 ) return;

		var toRemove = new HashSet<int>();
		foreach ( var vi in SelectedVertices )
		{
			var edges = data.GetEdgesForVertex( vi );
			foreach ( var ei in edges )
			{
				if ( ei >= data.Edges.Count ) continue;
				var edge = data.Edges[ei];
				int other = edge.V1 == vi ? edge.V2 : edge.V1;
				if ( !SelectedVertices.Contains( other ) )
				{
					toRemove.Add( vi );
					break;
				}
			}
		}
		foreach ( var v in toRemove )
			SelectedVertices.Remove( v );
	}

	/// <summary>Copy selected geometry to clipboard.</summary>
	public void CopySelection()
	{
		var data = Target?.Data;
		if ( data == null || SelectedVertices.Count == 0 ) return;

		var indexMap = new Dictionary<int, int>();
		ClipboardVertices = new List<SkyboxVertex>();
		int newIdx = 0;
		foreach ( var vi in SelectedVertices.OrderBy( x => x ) )
		{
			if ( vi < 0 || vi >= data.Vertices.Count ) continue;
			indexMap[vi] = newIdx++;
			ClipboardVertices.Add( data.Vertices[vi] );
		}

		ClipboardEdges = new List<SkyboxEdge>();
		for ( int i = 0; i < data.Edges.Count; i++ )
		{
			var e = data.Edges[i];
			if ( indexMap.ContainsKey( e.V1 ) && indexMap.ContainsKey( e.V2 ) )
				ClipboardEdges.Add( new SkyboxEdge( indexMap[e.V1], indexMap[e.V2] ) );
		}

		ClipboardTriangles = new List<SkyboxTriangle>();
		for ( int i = 0; i < data.Triangles.Count; i++ )
		{
			var t = data.Triangles[i];
			if ( indexMap.ContainsKey( t.V0 ) && indexMap.ContainsKey( t.V1 ) && indexMap.ContainsKey( t.V2 ) )
			{
				// Edge indices will be rebuilt on paste
				ClipboardTriangles.Add( new SkyboxTriangle(
					indexMap[t.V0], indexMap[t.V1], indexMap[t.V2], -1, -1, -1 ) );
			}
		}
	}

	/// <summary>Paste clipboard geometry into the current data.</summary>
	public void PasteSelection()
	{
		var data = Target?.Data;
		if ( data == null || ClipboardVertices == null || ClipboardVertices.Count == 0 ) return;

		int baseVert = data.Vertices.Count;
		int baseEdge = data.Edges.Count;

		// Add vertices
		foreach ( var v in ClipboardVertices )
		{
			var pasted = v;
			pasted.LayerDepth = (byte)CurrentLayer;
			data.Vertices.Add( pasted );
		}

		// Add edges (offset indices)
		foreach ( var e in ClipboardEdges )
			data.Edges.Add( new SkyboxEdge( e.V1 + baseVert, e.V2 + baseVert ) );

		// Add triangles (offset vertex indices, find/create edges)
		foreach ( var t in ClipboardTriangles )
		{
			int v0 = t.V0 + baseVert, v1 = t.V1 + baseVert, v2 = t.V2 + baseVert;
			data.InvalidateAdjacency();
			int e0 = data.FindEdge( v0, v1 );
			int e1 = data.FindEdge( v1, v2 );
			int e2 = data.FindEdge( v0, v2 );
			if ( e0 < 0 ) { e0 = data.Edges.Count; data.Edges.Add( new SkyboxEdge( v0, v1 ) ); }
			if ( e1 < 0 ) { e1 = data.Edges.Count; data.Edges.Add( new SkyboxEdge( v1, v2 ) ); }
			if ( e2 < 0 ) { e2 = data.Edges.Count; data.Edges.Add( new SkyboxEdge( v0, v2 ) ); }
			data.Triangles.Add( new SkyboxTriangle( v0, v1, v2, e0, e1, e2 ) );
		}

		// Select pasted vertices
		SelectedVertices.Clear();
		for ( int i = baseVert; i < data.Vertices.Count; i++ )
			SelectedVertices.Add( i );

		data.InvalidateAdjacency();
	}
}
