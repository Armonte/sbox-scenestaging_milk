
namespace Sandbox;

/// <summary>
/// Triangle in the skybox mesh. Stores both vertex and edge indices
/// to match the .skye format and enable edge-based editing tools.
/// </summary>
public struct SkyboxTriangle
{
	public int V0, V1, V2;
	public int E0, E1, E2;

	public SkyboxTriangle( int v0, int v1, int v2, int e0, int e1, int e2 )
	{
		V0 = v0; V1 = v1; V2 = v2;
		E0 = e0; E1 = e1; E2 = e2;
	}
}

/// <summary>
/// Edge in the skybox mesh, connecting two vertices.
/// </summary>
public struct SkyboxEdge
{
	public int V1, V2;

	public SkyboxEdge( int v1, int v2 )
	{
		V1 = v1;
		V2 = v2;
	}

	/// <summary>
	/// Returns a sorted tuple key for edge lookup (smaller index first).
	/// </summary>
	public readonly (int, int) SortedKey => V1 < V2 ? (V1, V2) : (V2, V1);
}

/// <summary>
/// Complete skybox data model. Direct C# equivalent of the Python SkyboxData class.
/// Contains all geometry, color, and metadata for a vertex-colored sphere skybox.
/// </summary>
public class SkyboxData
{
	/// <summary>
	/// Background clear color for the sky.
	/// </summary>
	public Color32 BackgroundColor { get; set; } = new Color32( 0, 0, 0, 255 );

	/// <summary>
	/// All vertices on the sphere surface.
	/// </summary>
	public List<SkyboxVertex> Vertices { get; set; } = new();

	/// <summary>
	/// Edge connectivity.
	/// </summary>
	public List<SkyboxEdge> Edges { get; set; } = new();

	/// <summary>
	/// Triangle faces with vertex and edge indices.
	/// </summary>
	public List<SkyboxTriangle> Triangles { get; set; } = new();

	/// <summary>
	/// Sketch point data for the sketch pencil tool.
	/// </summary>
	public List<float> SketchPoints { get; set; } = new();

	/// <summary>
	/// Number of palette slots actively used.
	/// </summary>
	public int PaletteUsedCount { get; set; }

	/// <summary>
	/// 40-slot color palette.
	/// </summary>
	public Color32[] Palette { get; set; } = new Color32[40];

	/// <summary>
	/// Sphere radius. Derived from vertex positions, default 100.
	/// </summary>
	public float SphereRadius
	{
		get
		{
			if ( Vertices.Count == 0 ) return 100f;

			float maxDist = 0f;
			foreach ( var v in Vertices )
			{
				float dist = v.Position.Length;
				if ( dist > maxDist ) maxDist = dist;
			}
			return maxDist;
		}
	}

	// --- Adjacency caches (rebuilt on topology change) ---

	private Dictionary<int, List<int>> _vertexToTriangles;
	private Dictionary<int, List<int>> _vertexToEdges;
	private Dictionary<(int, int), int> _edgeLookup;
	private bool _adjacencyDirty = true;

	/// <summary>
	/// Mark adjacency caches as needing rebuild (call after any topology change).
	/// </summary>
	public void InvalidateAdjacency()
	{
		_adjacencyDirty = true;
	}

	/// <summary>
	/// Rebuild adjacency caches if dirty.
	/// </summary>
	public void RebuildAdjacency()
	{
		if ( !_adjacencyDirty ) return;

		_vertexToTriangles = new Dictionary<int, List<int>>();
		_vertexToEdges = new Dictionary<int, List<int>>();
		_edgeLookup = new Dictionary<(int, int), int>();

		for ( int i = 0; i < Triangles.Count; i++ )
		{
			var tri = Triangles[i];
			AddToListDict( _vertexToTriangles, tri.V0, i );
			AddToListDict( _vertexToTriangles, tri.V1, i );
			AddToListDict( _vertexToTriangles, tri.V2, i );
		}

		for ( int i = 0; i < Edges.Count; i++ )
		{
			var edge = Edges[i];
			AddToListDict( _vertexToEdges, edge.V1, i );
			AddToListDict( _vertexToEdges, edge.V2, i );
			_edgeLookup[edge.SortedKey] = i;
		}

		_adjacencyDirty = false;
	}

	/// <summary>
	/// Get triangle indices adjacent to a vertex.
	/// </summary>
	public List<int> GetTrianglesForVertex( int vertexIndex )
	{
		RebuildAdjacency();
		return _vertexToTriangles.TryGetValue( vertexIndex, out var list ) ? list : new List<int>();
	}

	/// <summary>
	/// Get edge indices adjacent to a vertex.
	/// </summary>
	public List<int> GetEdgesForVertex( int vertexIndex )
	{
		RebuildAdjacency();
		return _vertexToEdges.TryGetValue( vertexIndex, out var list ) ? list : new List<int>();
	}

	/// <summary>
	/// Find edge index by vertex pair, or -1 if not found.
	/// </summary>
	public int FindEdge( int v1, int v2 )
	{
		RebuildAdjacency();
		var key = v1 < v2 ? (v1, v2) : (v2, v1);
		return _edgeLookup.TryGetValue( key, out var idx ) ? idx : -1;
	}

	/// <summary>
	/// Find vertices within a given distance of a point.
	/// </summary>
	public List<int> FindVerticesInRadius( Vector3 center, float radius )
	{
		var result = new List<int>();
		float radiusSq = radius * radius;

		for ( int i = 0; i < Vertices.Count; i++ )
		{
			if ( Vertices[i].Position.DistanceSquared( center ) <= radiusSq )
			{
				result.Add( i );
			}
		}

		return result;
	}

	/// <summary>
	/// Find the nearest vertex to a point. Returns -1 if no vertices exist.
	/// </summary>
	public int FindNearestVertex( Vector3 point )
	{
		if ( Vertices.Count == 0 ) return -1;

		int nearest = 0;
		float nearestDist = Vertices[0].Position.DistanceSquared( point );

		for ( int i = 1; i < Vertices.Count; i++ )
		{
			float dist = Vertices[i].Position.DistanceSquared( point );
			if ( dist < nearestDist )
			{
				nearestDist = dist;
				nearest = i;
			}
		}

		return nearest;
	}

	/// <summary>
	/// Compute the bounding box of all vertices.
	/// </summary>
	public BBox GetBounds()
	{
		if ( Vertices.Count == 0 )
			return new BBox( Vector3.Zero, Vector3.Zero );

		var mins = new Vector3( float.MaxValue );
		var maxs = new Vector3( float.MinValue );

		foreach ( var v in Vertices )
		{
			mins = Vector3.Min( mins, v.Position );
			maxs = Vector3.Max( maxs, v.Position );
		}

		return new BBox( mins, maxs );
	}

	private static void AddToListDict( Dictionary<int, List<int>> dict, int key, int value )
	{
		if ( !dict.TryGetValue( key, out var list ) )
		{
			list = new List<int>();
			dict[key] = list;
		}
		list.Add( value );
	}
}
