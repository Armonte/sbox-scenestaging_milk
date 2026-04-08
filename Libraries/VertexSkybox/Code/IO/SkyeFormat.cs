
namespace Sandbox;

/// <summary>
/// Parser and writer for the .skye file format (Vertex Skybox Editor v1.0).
/// Direct port of skye_parser.py.
///
/// Format: Plain text, space-separated values on a single line.
/// bg_r bg_g bg_b vertex_count [x y z r g b flag1 flag2]*N
/// edge_count [v1 v2]*M triangle_count [v0 v1 v2 e0 e1 e2]*T
/// sketch_count [sketch_data]*S palette_meta [r g b]*40
/// </summary>
public static class SkyeFormat
{
	/// <summary>
	/// Parse a .skye file from the s&box data filesystem.
	/// </summary>
	public static SkyboxData Parse( string filepath )
	{
		var content = FileSystem.Data.ReadAllText( filepath );
		return ParseString( content );
	}
	/// <summary>
	/// Parse .skye format from a string.
	/// </summary>
	public static SkyboxData ParseString( string content )
	{
		var tokens = content.Split( (char[])null, StringSplitOptions.RemoveEmptyEntries );
		int idx = 0;
		var sky = new SkyboxData();

		int ReadInt() => (int)float.Parse( tokens[idx++] );
		float ReadFloat() => float.Parse( tokens[idx++] );

		// Background color
		int bgR = ReadInt(), bgG = ReadInt(), bgB = ReadInt();
		sky.BackgroundColor = new Color32( (byte)bgR, (byte)bgG, (byte)bgB, 255 );

		// Vertices
		int vertexCount = ReadInt();
		sky.Vertices = new List<SkyboxVertex>( vertexCount );
		for ( int i = 0; i < vertexCount; i++ )
		{
			float x = ReadFloat(), y = ReadFloat(), z = ReadFloat();
			int r = ReadInt(), g = ReadInt(), b = ReadInt();
			int flag1 = ReadInt(), flag2 = ReadInt();
			sky.Vertices.Add( new SkyboxVertex( x, y, z, r, g, b, flag1, flag2 ) );
		}

		// Edges
		int edgeCount = ReadInt();
		sky.Edges = new List<SkyboxEdge>( edgeCount );
		for ( int i = 0; i < edgeCount; i++ )
		{
			int v1 = ReadInt(), v2 = ReadInt();
			sky.Edges.Add( new SkyboxEdge( v1, v2 ) );
		}

		// Triangles
		int triCount = ReadInt();
		sky.Triangles = new List<SkyboxTriangle>( triCount );
		for ( int i = 0; i < triCount; i++ )
		{
			int v0 = ReadInt(), v1 = ReadInt(), v2 = ReadInt();
			int e0 = ReadInt(), e1 = ReadInt(), e2 = ReadInt();
			sky.Triangles.Add( new SkyboxTriangle( v0, v1, v2, e0, e1, e2 ) );
		}

		// Sketch points
		int sketchCount = ReadInt();
		sky.SketchPoints = new List<float>( sketchCount );
		for ( int i = 0; i < sketchCount; i++ )
		{
			sky.SketchPoints.Add( ReadFloat() );
		}

		// Palette
		sky.PaletteUsedCount = ReadInt();
		sky.Palette = new Color32[40];
		for ( int i = 0; i < 40; i++ )
		{
			int r = ReadInt(), g = ReadInt(), b = ReadInt();
			sky.Palette[i] = new Color32( (byte)r, (byte)g, (byte)b, 255 );
		}

		return sky;
	}

	/// <summary>
	/// Write a SkyboxData object to a .skye file in the data filesystem.
	/// </summary>
	public static void Write( SkyboxData sky, string filepath )
	{
		var content = WriteString( sky );
		FileSystem.Data.WriteAllText( filepath, content );
	}

	/// <summary>
	/// Serialize SkyboxData to .skye format string.
	/// </summary>
	public static string WriteString( SkyboxData sky )
	{
		var parts = new List<string>();

		// Background color
		parts.Add( sky.BackgroundColor.r.ToString() );
		parts.Add( sky.BackgroundColor.g.ToString() );
		parts.Add( sky.BackgroundColor.b.ToString() );

		// Vertices
		parts.Add( sky.Vertices.Count.ToString() );
		foreach ( var v in sky.Vertices )
		{
			parts.Add( v.Position.x.ToString() );
			parts.Add( v.Position.y.ToString() );
			parts.Add( v.Position.z.ToString() );
			// Swap back R↔B when writing — data stores BGR, file expects RGB
			parts.Add( v.Color.b.ToString() );
			parts.Add( v.Color.g.ToString() );
			parts.Add( v.Color.r.ToString() );
			parts.Add( v.SelectionGroup.ToString() );
			parts.Add( v.LayerDepth.ToString() );
		}

		// Edges
		parts.Add( sky.Edges.Count.ToString() );
		foreach ( var e in sky.Edges )
		{
			parts.Add( e.V1.ToString() );
			parts.Add( e.V2.ToString() );
		}

		// Triangles
		parts.Add( sky.Triangles.Count.ToString() );
		foreach ( var t in sky.Triangles )
		{
			parts.Add( t.V0.ToString() );
			parts.Add( t.V1.ToString() );
			parts.Add( t.V2.ToString() );
			parts.Add( t.E0.ToString() );
			parts.Add( t.E1.ToString() );
			parts.Add( t.E2.ToString() );
		}

		// Sketch points
		parts.Add( sky.SketchPoints.Count.ToString() );
		foreach ( var s in sky.SketchPoints )
		{
			parts.Add( s.ToString() );
		}

		// Palette
		parts.Add( sky.PaletteUsedCount.ToString() );
		for ( int i = 0; i < 40; i++ )
		{
			if ( i < sky.Palette.Length )
			{
				parts.Add( sky.Palette[i].r.ToString() );
				parts.Add( sky.Palette[i].g.ToString() );
				parts.Add( sky.Palette[i].b.ToString() );
			}
			else
			{
				parts.Add( "0" );
				parts.Add( "0" );
				parts.Add( "0" );
			}
		}

		return string.Join( ' ', parts );
	}
}
