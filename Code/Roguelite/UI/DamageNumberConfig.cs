using Sandbox;

/// <summary>
/// Drop this on a scene GameObject (RogueliteDebug works fine, or its own
/// "Roguelite Settings" GO) and tune the damage-number popup feel from the
/// inspector. <see cref="DamageNumber.Spawn"/> finds the first instance in
/// the scene and uses its values; if none exists the spawn falls back to
/// hardcoded defaults so nothing breaks if you forget to add it.
/// </summary>
[Title( "Damage Number Config" )]
[Icon( "filter_1" )]
public sealed class DamageNumberConfig : Component
{
	// --- Motion ---

	/// <summary>
	/// Total seconds the number is visible before self-destruct.
	/// </summary>
	[Property, Range( 0.1f, 5f )] public float Lifetime { get; set; } = 0.4f;

	/// <summary>
	/// World units the number floats upward over its lifetime. Eased so the
	/// motion is fast at the start and slows at the top.
	/// </summary>
	[Property, Range( 0f, 400f )] public float RiseAmount { get; set; } = 20f;

	/// <summary>
	/// Fraction of lifetime before fade begins (0 = fade immediately, 1 = no fade).
	/// </summary>
	[Property, Range( 0f, 1f )] public float FadeStart { get; set; } = 0.5f;

	// --- Spawn position ---

	/// <summary>
	/// World units above the hit point where the number first appears.
	/// </summary>
	[Property, Range( 0f, 200f )] public float StartOffsetUp { get; set; } = 20f;

	/// <summary>
	/// Random XY jitter radius so back-to-back hits at the same spot don't stack.
	/// </summary>
	[Property, Range( 0f, 80f )] public float JitterRadius { get; set; } = 10f;

	// --- Panel rendering ---

	/// <summary>
	/// The internal pixel size of the WorldPanel quad. Bigger = sharper text but
	/// uses more fillrate. Combined with RenderScale to control on-screen size.
	/// </summary>
	[Property] public Vector2 PanelSize { get; set; } = new Vector2( 1024, 512 );

	/// <summary>
	/// World scale of the WorldPanel quad. Tune alongside PanelSize.
	/// </summary>
	[Property, Range( 0.1f, 4f )] public float RenderScale { get; set; } = 1f;
}
