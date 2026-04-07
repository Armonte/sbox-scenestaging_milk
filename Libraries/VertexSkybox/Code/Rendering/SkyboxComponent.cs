namespace Sandbox;

/// <summary>
/// Component that renders a vertex-colored 3D skybox sphere.
/// Place on a GameObject at the scene origin. The skybox mesh is rendered
/// as an inverted sphere viewed from inside, using per-vertex colors
/// just like the original PSX Spyro games.
/// </summary>
[Title( "Spyro Skybox" )]
[Category( "Rendering" )]
[Icon( "cloud" )]
public sealed class SkyboxComponent : Component, Component.ExecuteInEditor
{
	/// <summary>
	/// The skybox data containing all vertices, edges, triangles, and palette.
	/// </summary>
	[Hide]
	public SkyboxData Data { get; set; }

	/// <summary>
	/// Scale multiplier for the skybox sphere. Default 10 = 1000 unit radius.
	/// </summary>
	[Property, Range( 1f, 100f )]
	public float SkyboxScale
	{
		get => _skyboxScale;
		set
		{
			if ( _skyboxScale != value )
			{
				_skyboxScale = value;
				MarkDirty();
			}
		}
	}
	private float _skyboxScale = 10f;

	/// <summary>
	/// Serialized .skye content for scene persistence.
	/// </summary>
	[Property, Hide]
	public string SerializedSkye { get; set; }

	private SceneCustomObject _sceneObject;
	private Material _material;
	private VertexBuffer _vertexBuffer;
	private bool _meshDirty = true;
	private float _lastScale;
	private Vector3 _lastPosition;
	private Rotation _lastRotation;

	protected override void OnEnabled()
	{
		_material = CreateSkyboxMaterial();

		if ( Data == null )
		{
			if ( !string.IsNullOrEmpty( SerializedSkye ) )
			{
				Data = SkyeFormat.ParseString( SerializedSkye );
			}
			else
			{
				Data = SphereGeometry.GenerateSphere( 100f, 12, 24 );
			}
		}

		RebuildMesh();
	}

	protected override void OnDisabled()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
		_vertexBuffer = null;
	}

	protected override void OnUpdate()
	{
		// Rebuild if data changed or transform changed (since we bake world positions)
		if ( _meshDirty || SkyboxScale != _lastScale || WorldPosition != _lastPosition || WorldRotation != _lastRotation )
		{
			RebuildMesh();
			_meshDirty = false;
			_lastScale = SkyboxScale;
			_lastPosition = WorldPosition;
			_lastRotation = WorldRotation;
		}
	}

	/// <summary>
	/// Load skybox data from a string (the .skye file contents).
	/// Called by the editor tool after reading the file.
	/// </summary>
	public void LoadFromString( string skyeContent )
	{
		Data = SkyeFormat.ParseString( skyeContent );
		SerializedSkye = skyeContent;
		MarkDirty();
		RebuildMesh();
	}

	/// <summary>
	/// Persist current data to the serialized property (for scene save).
	/// </summary>
	public void SaveState()
	{
		if ( Data != null )
		{
			SerializedSkye = SkyeFormat.WriteString( Data );
		}
	}

	/// <summary>
	/// Mark the mesh as needing rebuild (call after any data change).
	/// </summary>
	public void MarkDirty()
	{
		_meshDirty = true;
	}

	/// <summary>
	/// Force immediate mesh rebuild.
	/// </summary>
	public void RebuildMesh()
	{
		if ( Data == null ) return;

		_material ??= CreateSkyboxMaterial();
		if ( _material == null ) return;

		// Build a VertexBuffer with vertices in world space
		// (VertexBuffer.Draw renders in world space, no transform applied)
		_vertexBuffer = new VertexBuffer();
		_vertexBuffer.Init( true );

		var pos = WorldPosition;
		var rot = WorldRotation;
		float scale = SkyboxScale;

		for ( int i = 0; i < Data.Vertices.Count; i++ )
		{
			var sv = Data.Vertices[i];
			var rp = sv.RenderPosition;
			var worldPos = pos + rot * (rp * scale);
			var worldNormal = rot * rp.Normal;
			_vertexBuffer.Add( new Vertex( worldPos, sv.Color ) { Normal = worldNormal } );
		}

		for ( int i = 0; i < Data.Triangles.Count; i++ )
		{
			var tri = Data.Triangles[i];
			_vertexBuffer.AddRawIndex( tri.V0 );
			_vertexBuffer.AddRawIndex( tri.V1 );
			_vertexBuffer.AddRawIndex( tri.V2 );
		}

		// Create a SceneCustomObject that draws our vertex buffer
		_sceneObject?.Delete();
		if ( Scene?.SceneWorld != null )
		{
			_sceneObject = new SceneCustomObject( Scene.SceneWorld );
			_sceneObject.Transform = new Transform( pos, rot, scale );
			_sceneObject.Flags.CastShadows = false;
			_sceneObject.Bounds = BBox.FromPositionAndSize( pos, scale * 200f );

			var mat = _material;
			var vb = _vertexBuffer;
			_sceneObject.RenderOverride = ( so ) =>
			{
				vb.Draw( mat );
			};
		}
	}

	private Material CreateSkyboxMaterial()
	{
		var mat = Material.FromShader( "shaders/line.shader" );
		if ( mat != null && mat.IsValid )
		{
			Log.Info( "SkyboxComponent: Using line.shader" );
			return mat;
		}

		Log.Warning( "SkyboxComponent: line.shader not found" );
		return Material.Load( "materials/dev/reflectivity_30.vmat" );
	}
}
