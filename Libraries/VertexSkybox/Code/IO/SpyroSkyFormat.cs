
namespace Sandbox;

/// <summary>
/// Importer for Spyro PS1 sky data extracted by wad_sky_extractor.py.
/// Reads the JSON format and converts to SkyboxData for the editor.
///
/// JSON structure:
///   background_color: [R, G, B, A]
///   sectors: [ { vertices: [{pos:[x,y,z], color:[r,g,b,a]}], triangles: [[v0,v1,v2]] } ]
///
/// Triangle indices are already global (across all sectors), so sectors
/// just need to be merged into a flat vertex list.
/// </summary>
public static class SpyroSkyFormat
{
	// ── JSON DTO classes for Json.Deserialize<T>() ──

	private class SpyroSkyJson
	{
		public string name { get; set; }
		public int game { get; set; }
		public int wad_entry { get; set; }
		public int total_vertices { get; set; }
		public int total_triangles { get; set; }
		public List<int> background_color { get; set; }
		public List<SpyroSectorJson> sectors { get; set; }
	}

	private class SpyroSectorJson
	{
		public List<SpyroVertexJson> vertices { get; set; }
		public List<List<int>> triangles { get; set; }
	}

	private class SpyroVertexJson
	{
		public List<float> pos { get; set; }
		public List<int> color { get; set; }
	}

	/// <summary>
	/// Parse a Spyro sky JSON string into SkyboxData.
	/// Merges sectors, normalizes to radius 100, builds edges.
	/// </summary>
	public static SkyboxData ParseJson( string jsonContent )
	{
		var dto = Json.Deserialize<SpyroSkyJson>( jsonContent );
		if ( dto == null || dto.sectors == null )
			return null;

		var sky = new SkyboxData();

		// Background color
		if ( dto.background_color != null && dto.background_color.Count >= 3 )
		{
			sky.BackgroundColor = new Color32(
				(byte)dto.background_color[0],
				(byte)dto.background_color[1],
				(byte)dto.background_color[2],
				255
			);
		}

		// Collect raw vertex data and triangle indices across all sectors
		var positions = new List<Vector3>();
		var colors = new List<(int r, int g, int b)>();
		var triangleIndices = new List<(int v0, int v1, int v2)>();

		foreach ( var sector in dto.sectors )
		{
			if ( sector.vertices != null )
			{
				foreach ( var vert in sector.vertices )
				{
					float x = vert.pos?.Count >= 1 ? vert.pos[0] : 0f;
					float y = vert.pos?.Count >= 2 ? vert.pos[1] : 0f;
					float z = vert.pos?.Count >= 3 ? vert.pos[2] : 0f;
					positions.Add( new Vector3( x, y, z ) );

					int r = vert.color?.Count >= 1 ? vert.color[0] : 128;
					int g = vert.color?.Count >= 2 ? vert.color[1] : 128;
					int b = vert.color?.Count >= 3 ? vert.color[2] : 128;
					colors.Add( (r, g, b) );
				}
			}

			if ( sector.triangles != null )
			{
				foreach ( var tri in sector.triangles )
				{
					if ( tri?.Count >= 3 )
						triangleIndices.Add( (tri[0], tri[1], tri[2]) );
				}
			}
		}

		// Normalize positions to sphere of radius 100
		float maxDist = 0f;
		foreach ( var pos in positions )
		{
			float dist = pos.Length;
			if ( dist > maxDist ) maxDist = dist;
		}

		float scale = maxDist > 0f ? 100f / maxDist : 1f;

		// Create vertices with scaled positions
		sky.Vertices = new List<SkyboxVertex>( positions.Count );
		for ( int i = 0; i < positions.Count; i++ )
		{
			var p = positions[i] * scale;
			var c = colors[i];
			sky.Vertices.Add( new SkyboxVertex( p.x, p.y, p.z, c.r, c.g, c.b ) );
		}

		// Build edges from triangles
		var edgeMap = new Dictionary<(int, int), int>();
		var edges = new List<SkyboxEdge>();

		int GetOrCreateEdge( int a, int b )
		{
			var key = a < b ? (a, b) : (b, a);
			if ( !edgeMap.TryGetValue( key, out int idx ) )
			{
				idx = edges.Count;
				edgeMap[key] = idx;
				edges.Add( new SkyboxEdge( a, b ) );
			}
			return idx;
		}

		sky.Triangles = new List<SkyboxTriangle>( triangleIndices.Count );
		foreach ( var (v0, v1, v2) in triangleIndices )
		{
			if ( v0 >= positions.Count || v1 >= positions.Count || v2 >= positions.Count )
				continue;

			int e0 = GetOrCreateEdge( v0, v1 );
			int e1 = GetOrCreateEdge( v1, v2 );
			int e2 = GetOrCreateEdge( v0, v2 );
			sky.Triangles.Add( new SkyboxTriangle( v0, v1, v2, e0, e1, e2 ) );
		}

		sky.Edges = edges;

		// Generate a palette from vertex colors
		sky.Palette = new Color32[40];
		sky.PaletteUsedCount = BuildPaletteFromVertices( sky.Vertices, sky.Palette );

		sky.InvalidateAdjacency();
		return sky;
	}

	/// <summary>
	/// Build a simple palette by sampling evenly spaced vertices.
	/// Returns the number of palette slots used (max 40).
	/// </summary>
	private static int BuildPaletteFromVertices( List<SkyboxVertex> vertices, Color32[] palette )
	{
		if ( vertices.Count == 0 ) return 0;

		int count = Math.Min( 40, vertices.Count );
		float step = vertices.Count / (float)count;

		for ( int i = 0; i < count; i++ )
		{
			int idx = (int)(i * step);
			var c = vertices[idx].Color;
			// SkyboxVertex stores BGRA internally, convert back to RGB for palette
			palette[i] = new Color32( c.b, c.g, c.r, 255 );
		}

		return count;
	}
}
