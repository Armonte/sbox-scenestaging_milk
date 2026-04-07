
namespace Sandbox;

/// <summary>
/// Export SkyboxData to common 3D formats.
/// Direct port of export_obj/export_ply from skye_parser.py.
/// </summary>
public static class MeshExporter
{
	/// <summary>
	/// Export to Wavefront OBJ format with vertex colors as extended v attributes.
	/// </summary>
	public static void ExportObj( SkyboxData sky, string filepath )
	{
		var sb = new StringBuilder();
		sb.AppendLine( "# Vertex Skybox Editor - .skye to .obj converter" );
		sb.AppendLine( $"# Vertices: {sky.Vertices.Count}, Triangles: {sky.Triangles.Count}" );
		sb.AppendLine( $"# Background: RGB({sky.BackgroundColor.r}, {sky.BackgroundColor.g}, {sky.BackgroundColor.b})" );
		sb.AppendLine();

		// Vertices with color (OBJ extension: v x y z r g b)
		foreach ( var v in sky.Vertices )
		{
			var rp = v.RenderPosition;
			float r = v.Color.r / 255f;
			float g = v.Color.g / 255f;
			float b = v.Color.b / 255f;
			sb.AppendLine( $"v {rp.x:F6} {rp.y:F6} {rp.z:F6} {r:F4} {g:F4} {b:F4}" );
		}

		sb.AppendLine();
		sb.AppendLine( "# Triangles" );

		// Faces (OBJ is 1-indexed, winding: v1, v0, v2 matching original)
		foreach ( var t in sky.Triangles )
		{
			sb.AppendLine( $"f {t.V1 + 1} {t.V0 + 1} {t.V2 + 1}" );
		}

		FileSystem.Data.WriteAllText( filepath, sb.ToString() );
	}

	/// <summary>
	/// Export to Stanford PLY format with vertex colors.
	/// </summary>
	public static void ExportPly( SkyboxData sky, string filepath )
	{
		var sb = new StringBuilder();
		sb.AppendLine( "ply" );
		sb.AppendLine( "format ascii 1.0" );
		sb.AppendLine( "comment Exported from Vertex Skybox Editor" );
		sb.AppendLine( $"element vertex {sky.Vertices.Count}" );
		sb.AppendLine( "property float x" );
		sb.AppendLine( "property float y" );
		sb.AppendLine( "property float z" );
		sb.AppendLine( "property uchar red" );
		sb.AppendLine( "property uchar green" );
		sb.AppendLine( "property uchar blue" );
		sb.AppendLine( $"element face {sky.Triangles.Count}" );
		sb.AppendLine( "property list uchar int vertex_indices" );
		sb.AppendLine( "end_header" );

		foreach ( var v in sky.Vertices )
		{
			var rp = v.RenderPosition;
			sb.AppendLine( $"{rp.x:F6} {rp.y:F6} {rp.z:F6} {v.Color.r} {v.Color.g} {v.Color.b}" );
		}

		// Face winding: v1, v0, v2 (matching original export)
		foreach ( var t in sky.Triangles )
		{
			sb.AppendLine( $"3 {t.V1} {t.V0} {t.V2}" );
		}

		FileSystem.Data.WriteAllText( filepath, sb.ToString() );
	}
}
