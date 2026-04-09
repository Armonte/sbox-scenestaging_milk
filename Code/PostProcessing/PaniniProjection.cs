using Sandbox;
using Sandbox.Rendering;

[Title( "Panini Projection" )]
[Category( "Post Processing" )]
[Icon( "panorama_wide_angle" )]
public sealed class PaniniProjection : BasePostProcess<PaniniProjection>
{
	/// <summary>
	/// 0 = Rectilinear, 1 = Panini, 2 = Equirectangular, 3 = Stereographic
	/// </summary>
	[Property, Range( 0, 3 )]
	public int Mode { get; set; } = 1;

	[Property, Range( 0, 1 )]
	public float Strength { get; set; } = 1.0f;

	[Property, Range( 0, 1 )]
	public float Fill { get; set; } = 0.5f;

	[Property, Range( -1, 1 )]
	public float VerticalCompression { get; set; } = 0.0f;

	[Property, Range( 0, 1 )]
	public float VignetteStrength { get; set; } = 0.0f;

	[Property, Range( 0, 1 )]
	public float ChromaticAberration { get; set; } = 0.0f;

	private bool _logged;

	public override void Render()
	{
		var strength = GetWeighted( x => x.Strength );
		if ( strength < 0.001f ) return;

		var shader = Material.FromShader( "shaders/postprocess/panini_projection.shader" );
		if ( shader is null ) return;

		// === APPROACH 1: Standard Blit (documented) ===
		Attributes.Set( "strength", strength );
		Blit( BlitMode.WithBackbuffer( shader, Stage.AfterPostProcess, 200, false ), "Panini" );

		// === APPROACH 2: Manual CommandList + InsertCommandList ===
		var cl = new CommandList( "Panini Manual" );
		var fb = cl.GrabFrameTexture( "ColorBuffer", false );
		var attrs = new RenderAttributes();
		attrs.Set( "ColorBuffer", fb.ColorTexture );
		attrs.Set( "strength", strength );
		cl.Blit( shader, attrs );
		InsertCommandList( cl, Stage.BeforePostProcess, 100, "Panini CL" );

		// === APPROACH 3: Draw red text on screen to prove we can affect output ===
		if ( Camera is not null )
		{
			Camera.Hud.DrawText(
				new Rect( 50, 50, 400, 50 ),
				$"PANINI ACTIVE strength={strength:F2}",
				Color.Red,
				"Poppins",
				24,
				600,
				TextFlag.LeftCenter
			);
		}

		if ( !_logged )
		{
			_logged = true;
			Log.Info( $"[Panini] All approaches fired, camera={Camera is not null}" );
		}
	}
}
