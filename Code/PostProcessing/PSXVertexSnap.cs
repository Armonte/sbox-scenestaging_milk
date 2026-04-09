using Sandbox;

[Title( "PSX Vertex Snap" )]
[Category( "Post Processing" )]
[Icon( "videogame_asset" )]
public sealed class PSXVertexSnap : Component
{
	/// <summary>
	/// Vertex snap resolution. Lower = more wobble.
	/// PS1 was ~256x224 internal, so try 80-160.
	/// 0 = disabled.
	/// </summary>
	[Property, Range( 0, 640 )]
	public float SnapResolution { get; set; } = 160.0f;

	private Material _material;
	private readonly List<(ModelRenderer renderer, Material original)> _overrides = new();
	private bool _applied;

	protected override void OnEnabled()
	{
		_material = Material.FromShader( "shaders/retro_psx_lit.shader" );

		if ( _material is null )
		{
			Log.Warning( "[PSX] retro_psx_lit.shader failed to load" );
			return;
		}

		Log.Info( "[PSX] Vertex snap shader loaded OK" );
		_applied = false;
	}

	protected override void OnDisabled()
	{
		Restore();
	}

	protected override void OnDestroy()
	{
		Restore();
	}

	protected override void OnUpdate()
	{
		if ( _material is null ) return;

		_material.Set( "SnapResolution", SnapResolution );

		if ( !_applied )
		{
			foreach ( var r in Scene.GetAllComponents<ModelRenderer>() )
			{
				_overrides.Add( (r, r.MaterialOverride) );
				r.MaterialOverride = _material;
			}
			_applied = true;
			Log.Info( $"[PSX] Applied vertex snap to {_overrides.Count} renderers" );
		}
	}

	private void Restore()
	{
		foreach ( var (r, orig) in _overrides )
		{
			if ( r.IsValid() )
				r.MaterialOverride = orig;
		}
		_overrides.Clear();
		_applied = false;
	}
}
