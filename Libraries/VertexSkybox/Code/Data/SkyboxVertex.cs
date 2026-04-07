namespace Sandbox;

/// <summary>
/// A vertex on the skybox sphere surface.
/// Position is always on the sphere, color is per-vertex RGB.
/// </summary>
public struct SkyboxVertex
{
	/// <summary>
	/// Position on the sphere surface.
	/// </summary>
	public Vector3 Position;

	/// <summary>
	/// Vertex color (alpha always 255).
	/// </summary>
	public Color32 Color;

	/// <summary>
	/// Selection group index (0-9). Used by the editor for group-based selection.
	/// Maps to flag1 in the .skye format.
	/// </summary>
	public byte SelectionGroup;

	/// <summary>
	/// Layer depth index (0-5). Creates parallax layers when rendered.
	/// Maps to flag2 in the .skye format.
	/// RenderScale = LayerDepth * 0.1f + 1.0f
	/// </summary>
	public byte LayerDepth;

	/// <summary>
	/// The render scale factor derived from LayerDepth.
	/// Vertices further out create parallax when the camera moves.
	/// </summary>
	public readonly float RenderScale => LayerDepth * 0.1f + 1.0f;

	/// <summary>
	/// Position scaled by the layer depth factor.
	/// </summary>
	public readonly Vector3 RenderPosition
	{
		get
		{
			float s = RenderScale;
			return Position * s;
		}
	}

	public SkyboxVertex( Vector3 position, Color32 color, byte selectionGroup = 0, byte layerDepth = 0 )
	{
		Position = position;
		Color = color;
		SelectionGroup = selectionGroup;
		LayerDepth = layerDepth;
	}

	public SkyboxVertex( float x, float y, float z, int r, int g, int b, int flag1 = 0, int flag2 = 0 )
	{
		Position = new Vector3( x, y, z );
		Color = new Color32( (byte)r, (byte)g, (byte)b, 255 );
		SelectionGroup = (byte)flag1;
		LayerDepth = (byte)flag2;
	}
}
