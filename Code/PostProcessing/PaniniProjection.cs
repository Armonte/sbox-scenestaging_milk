using Sandbox;
using Sandbox.Rendering;

[Title( "Panini Projection" )]
[Category( "Post Processing" )]
[Icon( "panorama_wide_angle" )]
public sealed class PaniniProjection : BasePostProcess<PaniniProjection>
{
	[Property, Range( -1, 1 )]
	public float Brightness { get; set; } = 0.0f;

	private bool _logged;

	public override void Render()
	{
		float brightness = GetWeighted( x => x.Brightness );
		if ( brightness.AlmostEqual( 0.0f ) ) return;

		Attributes.Set( "brightness", 1 + brightness );

		var shader = Material.FromShader( "shaders/postprocess/panini_projection.shader" );
		var blit = BlitMode.WithBackbuffer( shader, Stage.AfterPostProcess, 200, false );
		Blit( blit, "Brightness" );

		if ( !_logged )
		{
			_logged = true;
			Log.Info( $"[Panini] EXACT DOCS PATTERN: brightness={1 + brightness}, shader={shader is not null}" );
		}
	}
}
