using Sandbox;

namespace Editor;

/// <summary>
/// Importer for Spyro PS1 sky data extracted by wad_sky_extractor.py.
/// Lives in the editor assembly since it only runs during import.
/// </summary>
public static class SpyroSkyFormat
{
	/// <summary>
	/// Parse a Spyro sky JSON string into SkyboxData.
	/// Merges sectors, normalizes vertex positions to sphere radius 100, builds edges.
	/// </summary>
	public static SkyboxData ParseJson( string jsonContent )
	{
		var dto = Json.Deserialize<SpyroSkyDto>( jsonContent );
		if ( dto == null || dto.sectors == null )
			return null;

		var sky = new SkyboxData();

		// Background color
		if ( dto.background_color != null && dto.background_color.Length >= 3 )
		{
			sky.BackgroundColor = new Color32(
				(byte)dto.background_color[0],
				(byte)dto.background_color[1],
				(byte)dto.background_color[2],
				255
			);
		}

		// Collect raw vertex data from all sectors
		var rawX = new List<float>();
		var rawY = new List<float>();
		var rawZ = new List<float>();
		var rawR = new List<int>();
		var rawG = new List<int>();
		var rawB = new List<int>();
		var triV0 = new List<int>();
		var triV1 = new List<int>();
		var triV2 = new List<int>();

		foreach ( var sector in dto.sectors )
		{
			if ( sector.vertices != null )
			{
				foreach ( var vert in sector.vertices )
				{
					rawX.Add( vert.pos != null && vert.pos.Length >= 1 ? vert.pos[0] : 0f );
					rawY.Add( vert.pos != null && vert.pos.Length >= 2 ? vert.pos[1] : 0f );
					rawZ.Add( vert.pos != null && vert.pos.Length >= 3 ? vert.pos[2] : 0f );
					rawR.Add( vert.color != null && vert.color.Length >= 1 ? vert.color[0] : 128 );
					rawG.Add( vert.color != null && vert.color.Length >= 2 ? vert.color[1] : 128 );
					rawB.Add( vert.color != null && vert.color.Length >= 3 ? vert.color[2] : 128 );
				}
			}

			if ( sector.triangles != null )
			{
				foreach ( var tri in sector.triangles )
				{
					if ( tri != null && tri.Length >= 3 )
					{
						triV0.Add( tri[0] );
						triV1.Add( tri[1] );
						triV2.Add( tri[2] );
					}
				}
			}
		}

		// Normalize to sphere radius 100
		float maxDist = 0f;
		for ( int i = 0; i < rawX.Count; i++ )
		{
			float x = rawX[i], y = rawY[i], z = rawZ[i];
			float dist = (float)System.Math.Sqrt( x * x + y * y + z * z );
			if ( dist > maxDist ) maxDist = dist;
		}

		float scale = maxDist > 0f ? 100f / maxDist : 1f;

		// Create vertices
		int vertCount = rawX.Count;
		sky.Vertices = new List<SkyboxVertex>( vertCount );
		for ( int i = 0; i < vertCount; i++ )
		{
			sky.Vertices.Add( new SkyboxVertex(
				rawX[i] * scale, rawY[i] * scale, rawZ[i] * scale,
				rawR[i], rawG[i], rawB[i]
			) );
		}

		// Build edges from triangles
		var edgeMap = new Dictionary<long, int>();
		var edges = new List<SkyboxEdge>();

		sky.Triangles = new List<SkyboxTriangle>( triV0.Count );
		for ( int i = 0; i < triV0.Count; i++ )
		{
			int v0 = triV0[i], v1 = triV1[i], v2 = triV2[i];
			if ( v0 >= vertCount || v1 >= vertCount || v2 >= vertCount )
				continue;

			int e0 = GetOrCreateEdge( edgeMap, edges, v0, v1 );
			int e1 = GetOrCreateEdge( edgeMap, edges, v1, v2 );
			int e2 = GetOrCreateEdge( edgeMap, edges, v0, v2 );
			sky.Triangles.Add( new SkyboxTriangle( v0, v1, v2, e0, e1, e2 ) );
		}

		sky.Edges = edges;

		// Palette from vertex samples
		sky.Palette = new Color32[40];
		int palCount = vertCount < 40 ? vertCount : 40;
		if ( palCount > 0 )
		{
			float palStep = vertCount / (float)palCount;
			for ( int i = 0; i < palCount; i++ )
			{
				int idx = (int)(i * palStep);
				var c = sky.Vertices[idx].Color;
				sky.Palette[i] = new Color32( c.b, c.g, c.r, 255 );
			}
		}
		sky.PaletteUsedCount = palCount;

		sky.InvalidateAdjacency();
		return sky;
	}

	private static int GetOrCreateEdge( Dictionary<long, int> map, List<SkyboxEdge> edges, int a, int b )
	{
		int lo = a < b ? a : b;
		int hi = a < b ? b : a;
		long key = ((long)lo << 32) | (long)(uint)hi;

		if ( map.TryGetValue( key, out int idx ) )
			return idx;

		idx = edges.Count;
		map[key] = idx;
		edges.Add( new SkyboxEdge( a, b ) );
		return idx;
	}
}

// DTO classes for Json.Deserialize — must be public for s&box serializer
public class SpyroSkyDto
{
	public string name { get; set; }
	public int game { get; set; }
	public int[] background_color { get; set; }
	public SpyroSectorDto[] sectors { get; set; }
}

public class SpyroSectorDto
{
	public SpyroVertexDto[] vertices { get; set; }
	public int[][] triangles { get; set; }
}

public class SpyroVertexDto
{
	public float[] pos { get; set; }
	public int[] color { get; set; }
}
