namespace Sandbox;

/// <summary>
/// Builds a renderable Model from SkyboxData.
/// Uses the standard Sandbox.Vertex struct which puts Color in COLOR0.
/// Our custom shader reads COLOR0 and passes it to vVertexColor.
/// </summary>
public static class SkyboxMeshBuilder
{
	public static Model Build( SkyboxData sky, Material material )
	{
		if ( sky.Vertices.Count == 0 || sky.Triangles.Count == 0 )
			return null;

		var mesh = new Mesh( material );

		var vertices = new List<Vertex>( sky.Vertices.Count );
		for ( int i = 0; i < sky.Vertices.Count; i++ )
		{
			var sv = sky.Vertices[i];
			var renderPos = sv.RenderPosition;
			var normal = renderPos.Normal;

			vertices.Add( new Vertex( renderPos, sv.Color )
			{
				Normal = normal,
				Tangent = new Vector4( 1, 0, 0, 1 )
			} );
		}

		var indices = new List<int>( sky.Triangles.Count * 3 );
		for ( int i = 0; i < sky.Triangles.Count; i++ )
		{
			var tri = sky.Triangles[i];
			indices.Add( tri.V0 );
			indices.Add( tri.V1 );
			indices.Add( tri.V2 );
		}

		mesh.CreateVertexBuffer( vertices.Count, Vertex.Layout, vertices );
		mesh.CreateIndexBuffer( indices.Count, indices );
		mesh.Bounds = ComputeBounds( sky );

		var builder = new ModelBuilder();
		builder.AddMesh( mesh );

		return builder.Create();
	}

	public static BBox ComputeBounds( SkyboxData sky )
	{
		if ( sky.Vertices.Count == 0 )
			return new BBox( Vector3.Zero, Vector3.Zero );

		var mins = new Vector3( float.MaxValue );
		var maxs = new Vector3( float.MinValue );

		foreach ( var v in sky.Vertices )
		{
			var rp = v.RenderPosition;
			mins = Vector3.Min( mins, rp );
			maxs = Vector3.Max( maxs, rp );
		}

		return new BBox( mins, maxs );
	}
}
