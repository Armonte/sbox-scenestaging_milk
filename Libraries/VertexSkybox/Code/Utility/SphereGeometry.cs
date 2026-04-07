
namespace Sandbox;

/// <summary>
/// Generates UV sphere geometry for creating new skyboxes.
/// Direct port of generate_sphere() from skye_parser.py.
/// </summary>
public static class SphereGeometry
{
	/// <summary>
	/// Generate a UV sphere skybox with procedural vertex coloring.
	/// </summary>
	/// <param name="radius">Sphere radius (default 100, matching the original editor)</param>
	/// <param name="rings">Number of horizontal rings (latitude divisions)</param>
	/// <param name="segments">Number of vertical segments (longitude divisions)</param>
	/// <param name="colorFn">Optional color function(theta, phi) -> Color32.
	/// theta is 0..PI (top pole to bottom), phi is 0..2PI (around).
	/// If null, uses a default sky gradient.</param>
	public static SkyboxData GenerateSphere(
		float radius = 100f,
		int rings = 16,
		int segments = 32,
		Func<float, float, Color32> colorFn = null )
	{
		colorFn ??= DefaultSkyGradient;

		var sky = new SkyboxData();
		sky.BackgroundColor = new Color32( 0, 0, 0, 255 );
		sky.Palette = new Color32[40];

		var vertices = new List<SkyboxVertex>();
		var edges = new List<SkyboxEdge>();
		var triangles = new List<SkyboxTriangle>();
		var edgeMap = new Dictionary<(int, int), int>();

		// Top pole
		var topColor = colorFn( 0, 0 );
		vertices.Add( new SkyboxVertex( 0, 0, radius, topColor.r, topColor.g, topColor.b ) );

		// Ring vertices
		for ( int ring = 1; ring < rings; ring++ )
		{
			float theta = MathF.PI * ring / rings;
			for ( int seg = 0; seg < segments; seg++ )
			{
				float phi = 2f * MathF.PI * seg / segments;
				float x = radius * MathF.Sin( theta ) * MathF.Cos( phi );
				float y = radius * MathF.Sin( theta ) * MathF.Sin( phi );
				float z = radius * MathF.Cos( theta );
				var color = colorFn( theta, phi );
				vertices.Add( new SkyboxVertex( x, y, z, color.r, color.g, color.b ) );
			}
		}

		// Bottom pole
		var bottomColor = colorFn( MathF.PI, 0 );
		vertices.Add( new SkyboxVertex( 0, 0, -radius, bottomColor.r, bottomColor.g, bottomColor.b ) );

		int GetOrCreateEdge( int a, int b )
		{
			var key = a < b ? (a, b) : (b, a);
			if ( edgeMap.TryGetValue( key, out int idx ) )
				return idx;

			idx = edges.Count;
			edgeMap[key] = idx;
			edges.Add( new SkyboxEdge( a, b ) );
			return idx;
		}

		// Top cap triangles
		for ( int seg = 0; seg < segments; seg++ )
		{
			int v1 = 1 + seg;
			int v2 = 1 + (seg + 1) % segments;
			int e0 = GetOrCreateEdge( 0, v1 );
			int e1 = GetOrCreateEdge( v1, v2 );
			int e2 = GetOrCreateEdge( 0, v2 );
			triangles.Add( new SkyboxTriangle( 0, v1, v2, e0, e1, e2 ) );
		}

		// Middle band triangles
		for ( int ring = 1; ring < rings - 1; ring++ )
		{
			for ( int seg = 0; seg < segments; seg++ )
			{
				int curr = 1 + (ring - 1) * segments + seg;
				int nextSeg = 1 + (ring - 1) * segments + (seg + 1) % segments;
				int below = 1 + ring * segments + seg;
				int belowNext = 1 + ring * segments + (seg + 1) % segments;

				int e0 = GetOrCreateEdge( curr, nextSeg );
				int e1 = GetOrCreateEdge( nextSeg, belowNext );
				int e2 = GetOrCreateEdge( curr, belowNext );
				triangles.Add( new SkyboxTriangle( curr, nextSeg, belowNext, e0, e1, e2 ) );

				int e3 = GetOrCreateEdge( curr, belowNext );
				int e4 = GetOrCreateEdge( belowNext, below );
				int e5 = GetOrCreateEdge( curr, below );
				triangles.Add( new SkyboxTriangle( curr, belowNext, below, e3, e4, e5 ) );
			}
		}

		// Bottom cap triangles
		int bottomIdx = vertices.Count - 1;
		int lastRingStart = 1 + (rings - 2) * segments;
		for ( int seg = 0; seg < segments; seg++ )
		{
			int v1 = lastRingStart + seg;
			int v2 = lastRingStart + (seg + 1) % segments;
			int e0 = GetOrCreateEdge( v1, v2 );
			int e1 = GetOrCreateEdge( v2, bottomIdx );
			int e2 = GetOrCreateEdge( v1, bottomIdx );
			triangles.Add( new SkyboxTriangle( v1, v2, bottomIdx, e0, e1, e2 ) );
		}

		sky.Vertices = vertices;
		sky.Edges = edges;
		sky.Triangles = triangles;

		return sky;
	}

	/// <summary>
	/// Default sky gradient: blue at top, warm horizon at bottom.
	/// Matches the Python generate_sphere() default.
	/// </summary>
	public static Color32 DefaultSkyGradient( float theta, float phi )
	{
		float t = theta / MathF.PI; // 0 at top, 1 at bottom

		int r, g, b;

		if ( t < 0.5f )
		{
			// Upper hemisphere: deep blue to light blue
			float blend = t * 2f;
			r = (int)(50 + blend * 130);
			g = (int)(100 + blend * 120);
			b = (int)(220 + blend * 35);
		}
		else
		{
			// Lower hemisphere: light blue to warm horizon
			float blend = (t - 0.5f) * 2f;
			r = (int)(180 + blend * 75);
			g = (int)(220 - blend * 40);
			b = (int)(255 - blend * 100);
		}

		return new Color32(
			(byte)Math.Clamp( r, 0, 255 ),
			(byte)Math.Clamp( g, 0, 255 ),
			(byte)Math.Clamp( b, 0, 255 ),
			255
		);
	}
}
