using Sandbox;

namespace Editor;

/// <summary>
/// Draws a brush circle on the skybox surface.
/// Uses SceneCustomObject + VertexBuffer.Draw like the clutter/terrain tools.
/// </summary>
public class SkyboxBrushPreview : SceneCustomObject
{
	public float Radius { get; set; } = 50f;
	public Color BrushColor { get; set; } = Color.White;

	public SkyboxBrushPreview( SceneWorld world ) : base( world )
	{
		RenderLayer = SceneRenderLayer.OverlayWithDepth;
	}

	public override void RenderSceneObject()
	{
		var material = Material.FromShader( "shaders/line.shader" );
		if ( material == null ) return;

		var vb = new VertexBuffer();
		vb.Init( false );

		// Draw a circle as line segments (thin triangles)
		int segments = 32;
		var col = new Color32(
			(byte)(BrushColor.r * 255),
			(byte)(BrushColor.g * 255),
			(byte)(BrushColor.b * 255),
			(byte)(BrushColor.a * 255)
		);

		for ( int i = 0; i < segments; i++ )
		{
			float a1 = (float)i / segments * MathF.PI * 2f;
			float a2 = (float)(i + 1) / segments * MathF.PI * 2f;

			var p1 = new Vector3( MathF.Cos( a1 ) * Radius, MathF.Sin( a1 ) * Radius, 0 );
			var p2 = new Vector3( MathF.Cos( a2 ) * Radius, MathF.Sin( a2 ) * Radius, 0 );

			// Degenerate triangle for line
			vb.Add( new Vertex( p1, col ) );
			vb.Add( new Vertex( p2, col ) );
			vb.Add( new Vertex( p2, col ) );
		}

		// Center dot
		float dotSize = Radius * 0.05f;
		vb.Add( new Vertex( new Vector3( -dotSize, -dotSize, 0 ), col ) );
		vb.Add( new Vertex( new Vector3( dotSize, -dotSize, 0 ), col ) );
		vb.Add( new Vertex( new Vector3( 0, dotSize, 0 ), col ) );

		vb.Draw( material );
	}
}
