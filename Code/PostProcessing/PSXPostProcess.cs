using Sandbox;
using Sandbox.Rendering;

[Title( "PSX Post Process" )]
[Category( "Post Processing" )]
[Icon( "videogame_asset" )]
public sealed class PSXPostProcess : BasePostProcess<PSXPostProcess>
{
	[Property, Range( 0, 1 ), Group( "General" )]
	public float Strength { get; set; } = 1.0f;

	/// <summary>
	/// Internal resolution width. PS1 was 256-640. Lower = chunkier.
	/// </summary>
	[Property, Range( 64, 1920 ), Group( "Resolution" )]
	public int ResolutionX { get; set; } = 320;

	[Property, Range( 64, 1080 ), Group( "Resolution" )]
	public int ResolutionY { get; set; } = 240;

	/// <summary>
	/// Pixel grid jitter — makes pixels crawl as camera moves (PS1 integer rasterization look).
	/// </summary>
	[Property, Group( "Resolution" )]
	public bool JitterEnabled { get; set; } = true;

	/// <summary>
	/// Jitter intensity. Higher = more aggressive pixel crawl.
	/// PS1 authentic is around 50-100. Crank to 500+ for extreme wobble.
	/// </summary>
	[Property, Range( 1, 1000 ), Group( "Resolution" )]
	public float JitterAmount { get; set; } = 100.0f;

	/// <summary>
	/// Color bits per channel. PS1 = 5 (32 levels). Lower = more banding.
	/// </summary>
	[Property, Range( 1, 8 ), Group( "Color" )]
	public int ColorDepth { get; set; } = 5;

	/// <summary>
	/// PS1-authentic ordered dithering to smooth color banding.
	/// Uses the real PS1 dither matrix from hardware docs.
	/// </summary>
	[Property, Group( "Dithering" )]
	public bool DitherEnabled { get; set; } = true;

	/// <summary>
	/// Dither noise intensity. 1.0 = authentic PS1 spread.
	/// </summary>
	[Property, Range( 0, 4 ), Group( "Dithering" )]
	public float DitherSpread { get; set; } = 1.0f;

	/// <summary>
	/// CRT scanline darkening. 0 = off.
	/// </summary>
	[Property, Range( 0, 1 ), Group( "Scanlines" )]
	public float ScanlineStrength { get; set; } = 0.0f;

	[Property, Range( 0.01f, 1 ), Group( "Scanlines" )]
	public float ScanlineWidth { get; set; } = 0.4f;

	public override void Render()
	{
		float strength = GetWeighted( x => x.Strength );
		if ( strength.AlmostEqual( 0.0f ) ) return;

		Attributes.Set( "strength", strength );
		Attributes.Set( "internal_resolution", new Vector2( ResolutionX, ResolutionY ) );
		Attributes.Set( "jitter_enabled", JitterEnabled ? 1.0f : 0.0f );
		Attributes.Set( "jitter_amount", JitterAmount );
		Attributes.Set( "color_depth", (float)ColorDepth );
		Attributes.Set( "dither_enabled", DitherEnabled ? 1.0f : 0.0f );
		Attributes.Set( "dither_spread", DitherSpread );
		Attributes.Set( "scanline_strength", ScanlineStrength );
		Attributes.Set( "scanline_width", ScanlineWidth );

		var shader = Material.FromShader( "shaders/postprocess/retro_psx.shader" );
		if ( shader is null ) return;

		Blit( BlitMode.WithBackbuffer( shader, Stage.AfterPostProcess, 300, false ), "PSXPostProcess" );
	}
}
