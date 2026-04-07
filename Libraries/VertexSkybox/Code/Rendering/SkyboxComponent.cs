namespace Sandbox;

/// <summary>
/// Component that renders a vertex-colored 3D skybox sphere.
/// The skybox mesh follows the camera so it always appears at infinity.
/// </summary>
[Title( "Spyro Skybox" )]
[Category( "Rendering" )]
[Icon( "cloud" )]
public sealed class SkyboxComponent : Component, Component.ExecuteInEditor
{
	[Hide]
	public SkyboxData Data { get; set; }

	/// <summary>
	/// Background sky color. Fills the sky behind the vertex mesh.
	/// </summary>
	[Property, Title( "Background Color" )]
	public Color BackgroundColor
	{
		get => _bgColor;
		set
		{
			_bgColor = value;
			if ( Scene?.SceneWorld != null )
				Scene.SceneWorld.ClearColor = value;
		}
	}
	private Color _bgColor = Color.Black;

	/// <summary>
	/// Scale of the skybox sphere. Keep within camera ZFar.
	/// </summary>
	[Property, Range( 1f, 1000f ), Title( "Scale" )]
	public float SkyboxScale
	{
		get => _skyboxScale;
		set { if ( _skyboxScale != value ) { _skyboxScale = value; MarkDirty(); } }
	}
	private float _skyboxScale = 10f;

	[Property, Range( 0.5f, 3.0f ), Title( "Saturation" )]
	public float ColorSaturation
	{
		get => _colorSaturation;
		set { if ( _colorSaturation != value ) { _colorSaturation = value; MarkDirty(); } }
	}
	private float _colorSaturation = 1.2f;

	[Property, Range( 0.5f, 2.0f ), Title( "Brightness" )]
	public float ColorBrightness
	{
		get => _colorBrightness;
		set { if ( _colorBrightness != value ) { _colorBrightness = value; MarkDirty(); } }
	}
	private float _colorBrightness = 1.0f;

	[Property, Range( 0.3f, 2.0f ), Title( "Gamma" )]
	public float ColorGamma
	{
		get => _colorGamma;
		set { if ( _colorGamma != value ) { _colorGamma = value; MarkDirty(); } }
	}
	private float _colorGamma = 2.0f;

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
				Data = SkyeFormat.ParseString( SerializedSkye );
			else
				Data = SphereGeometry.GenerateSphere( 100f, 12, 24 );
		}

		RebuildMesh();

		// Disable any existing 2D skyboxes so our background color shows
		foreach ( var sky2d in Scene.GetAllComponents<SkyBox2D>() )
		{
			sky2d.Enabled = false;
		}

		// Apply background color
		if ( Scene?.SceneWorld != null )
			Scene.SceneWorld.ClearColor = _bgColor;
	}

	protected override void OnDisabled()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
		_vertexBuffer = null;
		_material = null;
	}

	protected override void OnUpdate()
	{
		if ( _meshDirty || SkyboxScale != _lastScale || WorldPosition != _lastPosition || WorldRotation != _lastRotation )
		{
			RebuildMesh();
			_meshDirty = false;
			_lastScale = SkyboxScale;
			_lastPosition = WorldPosition;
			_lastRotation = WorldRotation;
		}

		// In editor: follow the GameObject's position so you can place it
		// In game: follow the camera so it appears at infinity
		if ( _sceneObject != null )
		{
			if ( Scene.IsEditor )
				_sceneObject.Transform = new Transform( WorldPosition, WorldRotation );
			else if ( Scene?.Camera != null )
				_sceneObject.Transform = new Transform( Scene.Camera.WorldPosition );
		}

		// Set background clear color
		if ( Scene?.SceneWorld != null )
			Scene.SceneWorld.ClearColor = _bgColor;
	}

	public void LoadFromString( string skyeContent )
	{
		Data = SkyeFormat.ParseString( skyeContent );
		SerializedSkye = skyeContent;

		var bg = Data.BackgroundColor;
		BackgroundColor = new Color( bg.r / 255f, bg.g / 255f, bg.b / 255f );

		MarkDirty();
		RebuildMesh();
	}

	/// <summary>
	/// Load a SkyboxData directly (e.g. from Spyro JSON import).
	/// Serializes to .skye for persistence, applies background color.
	/// </summary>
	public void LoadData( SkyboxData data )
	{
		Data = data;
		SerializedSkye = SkyeFormat.WriteString( data );

		var bg = Data.BackgroundColor;
		BackgroundColor = new Color( bg.r / 255f, bg.g / 255f, bg.b / 255f );

		MarkDirty();
		RebuildMesh();
	}

	public void SaveState()
	{
		if ( Data != null )
			SerializedSkye = SkyeFormat.WriteString( Data );
	}

	public void MarkDirty()
	{
		_meshDirty = true;
	}

	public void RebuildMesh()
	{
		if ( Data == null ) return;

		_material ??= CreateSkyboxMaterial();
		if ( _material == null ) return;

		// Build vertices centered at origin — SceneObject follows camera each frame
		_vertexBuffer = new VertexBuffer();
		_vertexBuffer.Init( true );

		float scale = SkyboxScale;
		var center = WorldPosition;
		var rot = WorldRotation;

		for ( int i = 0; i < Data.Vertices.Count; i++ )
		{
			var sv = Data.Vertices[i];
			var rp = sv.RenderPosition;
			var localPos = new Vector3( -rp.x, rp.y, rp.z ) * scale;
			var worldPos = center + rot * localPos;
			var c = BoostColor( sv.Color );
			// Swap R and B for GPU BGRA vertex color format
			var gpu = new Color32( c.b, c.g, c.r, c.a );
			_vertexBuffer.Add( new Vertex( worldPos, gpu ) { Normal = rp.Normal } );

			// Debug: log first vertex's color chain
			if ( i == 0 )
			{
				var raw = sv.Color;
				Log.Info( $"[SkyDebug] Vert0 raw Color32: r={raw.r} g={raw.g} b={raw.b} a={raw.a}" );
				Log.Info( $"[SkyDebug] Vert0 boosted Color32: r={c.r} g={c.g} b={c.b} a={c.a}" );
				Log.Info( $"[SkyDebug] Vert0 pos: {worldPos}" );
			}
		}

		Log.Info( $"[SkyDebug] RebuildMesh: {Data.Vertices.Count} verts, {Data.Triangles.Count} tris, scale={scale}" );

		for ( int i = 0; i < Data.Triangles.Count; i++ )
		{
			var tri = Data.Triangles[i];
			_vertexBuffer.AddRawIndex( tri.V2 );
			_vertexBuffer.AddRawIndex( tri.V1 );
			_vertexBuffer.AddRawIndex( tri.V0 );
		}

		_sceneObject?.Delete();
		if ( Scene?.SceneWorld != null )
		{
			_sceneObject = new SceneCustomObject( Scene.SceneWorld );
			_sceneObject.Flags.CastShadows = false;
			_sceneObject.Flags.IsOpaque = true;
			_sceneObject.Bounds = BBox.FromPositionAndSize( Vector3.Zero, 999999f );
			_sceneObject.RenderLayer = SceneRenderLayer.OverlayWithDepth;

			_sceneObject.RenderOverride = RenderSkybox;
		}
	}

	private void RenderSkybox( SceneObject so )
	{
		_vertexBuffer?.Draw( _material );
	}

	private Color32 BoostColor( Color32 c )
	{
		float r = c.r / 255f;
		float g = c.g / 255f;
		float b = c.b / 255f;

		float luma = r * 0.299f + g * 0.587f + b * 0.114f;
		r = luma + (r - luma) * _colorSaturation;
		g = luma + (g - luma) * _colorSaturation;
		b = luma + (b - luma) * _colorSaturation;

		r *= _colorBrightness;
		g *= _colorBrightness;
		b *= _colorBrightness;

		if ( _colorGamma != 1.0f )
		{
			r = MathF.Pow( Math.Clamp( r, 0f, 1f ), _colorGamma );
			g = MathF.Pow( Math.Clamp( g, 0f, 1f ), _colorGamma );
			b = MathF.Pow( Math.Clamp( b, 0f, 1f ), _colorGamma );
		}

		return new Color32(
			(byte)(Math.Clamp( r, 0f, 1f ) * 255),
			(byte)(Math.Clamp( g, 0f, 1f ) * 255),
			(byte)(Math.Clamp( b, 0f, 1f ) * 255),
			c.a
		);
	}

	private Material CreateSkyboxMaterial()
	{
		var mat = Material.Load( "materials/vertexcolor_skybox.vmat" );
		if ( mat != null && mat.IsValid )
		{
			Log.Info( "SkyboxComponent: Loaded vertexcolor_skybox.vmat" );
			return mat;
		}

		Log.Warning( "SkyboxComponent: vmat not found, using dev material" );
		return Material.Load( "materials/dev/reflectivity_30.vmat" );
	}
}
