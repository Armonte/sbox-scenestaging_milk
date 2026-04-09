namespace Sandbox;

[Title( "Spyro Skybox" )]
[Category( "Rendering" )]
[Icon( "cloud" )]
public sealed class SkyboxComponent : Renderer, Component.ExecuteInEditor
{
	[Hide]
	public SkyboxData Data { get; set; }

	[Property, Title( "Background Color" )]
	public Color BackgroundColor
	{
		get => _bgColor;
		set
		{
			_bgColor = value;
			ApplyBackgroundColor();
		}
	}
	private Color _bgColor = Color.Black;

	[Property, Range( 1f, 1000f ), Title( "Scale" )]
	public float SkyboxScale
	{
		get => _skyboxScale;
		set { if ( _skyboxScale != value ) { _skyboxScale = value; RebuildMesh(); } }
	}
	private float _skyboxScale = 10f;

	[Property, Range( 0.5f, 3.0f ), Title( "Saturation" )]
	public float ColorSaturation
	{
		get => _colorSaturation;
		set { if ( _colorSaturation != value ) { _colorSaturation = value; RebuildMesh(); } }
	}
	private float _colorSaturation = 1.2f;

	[Property, Range( 0.5f, 2.0f ), Title( "Brightness" )]
	public float ColorBrightness
	{
		get => _colorBrightness;
		set { if ( _colorBrightness != value ) { _colorBrightness = value; RebuildMesh(); } }
	}
	private float _colorBrightness = 1.0f;

	[Property, Range( 0.3f, 3.0f ), Title( "Gamma" )]
	public float ColorGamma
	{
		get => _colorGamma;
		set { if ( _colorGamma != value ) { _colorGamma = value; RebuildMesh(); } }
	}
	private float _colorGamma = 2.0f;

	[Property, Range( -100f, 100f ), Title( "R Shift" )]
	public float RedShift
	{
		get => _redShift;
		set { if ( _redShift != value ) { _redShift = value; RebuildMesh(); } }
	}
	private float _redShift = 0f;

	[Property, Range( -100f, 100f ), Title( "G Shift" )]
	public float GreenShift
	{
		get => _greenShift;
		set { if ( _greenShift != value ) { _greenShift = value; RebuildMesh(); } }
	}
	private float _greenShift = 0f;

	[Property, Range( -100f, 100f ), Title( "B Shift" )]
	public float BlueShift
	{
		get => _blueShift;
		set { if ( _blueShift != value ) { _blueShift = value; RebuildMesh(); } }
	}
	private float _blueShift = 0f;

	[Property, Hide]
	public string SerializedSkye { get; set; }

	SceneObject _sceneObject;
	Model _model;
	string _lastKnownSkye;

	protected override void OnEnabled()
	{
		if ( Data == null )
		{
			if ( !string.IsNullOrEmpty( SerializedSkye ) )
				Data = SkyeFormat.ParseString( SerializedSkye );
			else
				Data = SphereGeometry.GenerateSphere( 100f, 12, 24 );
		}

		_lastKnownSkye = SerializedSkye;
		BuildModel();

		if ( _model == null )
		{
			Log.Warning( "[Skybox] Failed to build model — no material or no data" );
			return;
		}

		_sceneObject = new SceneObject( Scene.SceneWorld, _model, WorldTransform );
		_sceneObject.Flags.CastShadows = false;
		_sceneObject.Flags.IsOpaque = true;
		_sceneObject.Flags.IsTranslucent = false;
		_sceneObject.RenderingEnabled = true;
		_sceneObject.Tags.SetFrom( Tags );

		Transform.OnTransformChanged += OnTransformChanged;
		ApplyBackgroundColor();
	}

	protected override void OnTagsChanged()
	{
		if ( _sceneObject != null && _sceneObject.IsValid() )
			_sceneObject.Tags.SetFrom( Tags );
	}

	protected override void OnDisabled()
	{
		Transform.OnTransformChanged -= OnTransformChanged;
		_sceneObject?.Delete();
		_sceneObject = null;
		_model = null;
	}

	protected override void OnUpdate()
	{
		ApplyBackgroundColor();

		// Detect undo: if SerializedSkye changed externally, re-parse and rebuild
		if ( SerializedSkye != _lastKnownSkye && !string.IsNullOrEmpty( SerializedSkye ) )
		{
			_lastKnownSkye = SerializedSkye;
			Data = SkyeFormat.ParseString( SerializedSkye );
			RebuildMesh();
		}
	}

	/// <summary>
	/// Runs every editor frame regardless of which tool is active.
	/// Sets the editor viewport background color.
	/// </summary>
	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = _bgColor;

		// Access the editor viewport camera through the gizmo system
		var cam = Gizmo.Camera;
		if ( cam != null )
			cam.BackgroundColor = _bgColor;
	}

	private void OnTransformChanged()
	{
		if ( _sceneObject != null && _sceneObject.IsValid() )
			_sceneObject.Transform = WorldTransform;
	}

	public void LoadFromString( string skyeContent )
	{
		Data = SkyeFormat.ParseString( skyeContent );
		SerializedSkye = skyeContent;
		_lastKnownSkye = skyeContent;

		var bg = Data.BackgroundColor;
		BackgroundColor = new Color( bg.r / 255f, bg.g / 255f, bg.b / 255f );

		RebuildMesh();
	}

	public void LoadData( SkyboxData data )
	{
		Data = data;
		SerializedSkye = SkyeFormat.WriteString( data );
		_lastKnownSkye = SerializedSkye;

		var bg = Data.BackgroundColor;
		BackgroundColor = new Color( bg.r / 255f, bg.g / 255f, bg.b / 255f );

		RebuildMesh();
	}

	public void SaveState()
	{
		if ( Data != null )
		{
			SerializedSkye = SkyeFormat.WriteString( Data );
			_lastKnownSkye = SerializedSkye;
		}
	}

	public void MarkDirty() => RebuildMesh();

	public void RebuildMesh()
	{
		if ( Data == null ) return;
		if ( Scene?.SceneWorld == null ) return;

		BuildModel();
		if ( _model == null ) return;

		// Recreate SceneObject with new model (setting .Model doesn't always refresh)
		var oldTransform = _sceneObject?.Transform ?? WorldTransform;
		_sceneObject?.Delete();
		_sceneObject = new SceneObject( Scene.SceneWorld, _model, oldTransform );
		_sceneObject.Flags.CastShadows = false;
		_sceneObject.Tags.SetFrom( Tags );
	}

	private void BuildModel()
	{
		var mat = Material.Load( "materials/vertexcolor_skybox.vmat" );
		if ( mat == null || !mat.IsValid )
		{
			Log.Warning( "[Skybox] vertexcolor_skybox.vmat not found" );
			mat = Material.Load( "materials/dev/reflectivity_30.vmat" );
		}

		float scale = SkyboxScale;

		var vertices = new List<Vertex>( Data.Vertices.Count );
		for ( int i = 0; i < Data.Vertices.Count; i++ )
		{
			var sv = Data.Vertices[i];
			var rp = sv.RenderPosition;
			var localPos = new Vector3( -rp.x, rp.y, rp.z ) * scale;
			var c = BoostColor( sv.Color );
			vertices.Add( new Vertex( localPos, c ) { Normal = rp.Normal } );
		}

		var indices = new List<int>( Data.Triangles.Count * 3 );
		for ( int i = 0; i < Data.Triangles.Count; i++ )
		{
			var tri = Data.Triangles[i];
			indices.Add( tri.V2 );
			indices.Add( tri.V1 );
			indices.Add( tri.V0 );
		}

		var mesh = new Mesh( mat );
		mesh.CreateVertexBuffer( vertices.Count, vertices );
		mesh.CreateIndexBuffer( indices.Count, indices );
		mesh.Bounds = BBox.FromPositionAndSize( Vector3.Zero, scale * 150f );

		var builder = new ModelBuilder();
		builder.AddMesh( mesh );
		_model = builder.Create();
	}

	private void ApplyBackgroundColor()
	{
		foreach ( var cam in Scene.GetAllComponents<CameraComponent>() )
			cam.BackgroundColor = _bgColor;

#pragma warning disable CS0618
		if ( Scene?.SceneWorld != null )
			Scene.SceneWorld.ClearColor = _bgColor;
#pragma warning restore CS0618
	}

	private Color32 BoostColor( Color32 c )
	{
		float r = c.r / 255f;
		float g = c.g / 255f;
		float b = c.b / 255f;

		// Channel shift
		r += _redShift / 255f;
		g += _greenShift / 255f;
		b += _blueShift / 255f;

		// Saturation
		float luma = r * 0.299f + g * 0.587f + b * 0.114f;
		r = luma + (r - luma) * _colorSaturation;
		g = luma + (g - luma) * _colorSaturation;
		b = luma + (b - luma) * _colorSaturation;

		// Brightness
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
}
