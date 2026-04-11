using Sandbox;
using System.Linq;

/// <summary>
/// Visual variants for floating damage numbers. Drives both the CSS class
/// the panel gets and any special-case styling. Mutually exclusive — a
/// crit-headshot still picks one style (headshot wins in DamageResolver).
/// </summary>
public enum DamageNumberStyle
{
	Normal,
	Crit,
	Headshot,
	Weakpoint,
}

/// <summary>
/// Spawns a transient floating damage number at a world position.
/// Mirrors the test.ui.scene pattern: a GameObject with a sibling
/// <see cref="WorldPanel"/> + <see cref="DamageNumberPanel"/>. The
/// panel handles its own rise/fade animation and self-destroys.
///
/// Tunable knobs live on a <see cref="DamageNumberConfig"/> Component
/// dropped anywhere in the scene. If none exists the spawner falls
/// back to the defaults below so it still works without setup.
/// </summary>
public static class DamageNumber
{
	// Defaults — used when no DamageNumberConfig exists in the scene.
	private const float DefaultLifetime = 0.4f;
	private const float DefaultRiseAmount = 20f;
	private const float DefaultFadeStart = 0.5f;
	private const float DefaultStartOffsetUp = 20f;
	private const float DefaultJitterRadius = 10f;
	private static readonly Vector2 DefaultPanelSize = new Vector2( 1024, 512 );
	private const float DefaultRenderScale = 1f;

	/// <summary>
	/// Spawn a floating damage number at the given world position.
	/// Safe to call from any client — call sites that want everyone to
	/// see the number should wrap this in an [Rpc.Broadcast].
	/// </summary>
	public static void Spawn( Scene scene, Vector3 position, float amount, DamageNumberStyle style = DamageNumberStyle.Normal )
	{
		if ( scene is null ) return;
		if ( amount <= 0f ) return;

		// Look up the scene config every spawn — cheap (one component query) and lets the
		// user tune values live in the inspector while the game is running.
		var cfg = scene.GetAllComponents<DamageNumberConfig>().FirstOrDefault();

		var lifetime = cfg?.Lifetime ?? DefaultLifetime;
		var riseAmount = cfg?.RiseAmount ?? DefaultRiseAmount;
		var fadeStart = cfg?.FadeStart ?? DefaultFadeStart;
		var startOffsetUp = cfg?.StartOffsetUp ?? DefaultStartOffsetUp;
		var jitterRadius = cfg?.JitterRadius ?? DefaultJitterRadius;
		var panelSize = cfg?.PanelSize ?? DefaultPanelSize;
		var renderScale = cfg?.RenderScale ?? DefaultRenderScale;

		var jitter = new Vector3(
			(Random.Shared.NextSingle() - 0.5f) * jitterRadius * 2f,
			(Random.Shared.NextSingle() - 0.5f) * jitterRadius * 2f,
			0f
		);

		var go = scene.CreateObject();
		go.Name = "DamageNumber";
		go.WorldPosition = position + Vector3.Up * startOffsetUp + jitter;

		// WorldPanel renders any sibling PanelComponents into 3D space.
		// LookAtCamera billboards it toward the local player every frame.
		var worldPanel = go.Components.Create<WorldPanel>();
		worldPanel.LookAtCamera = true;
		worldPanel.PanelSize = panelSize;
		worldPanel.RenderScale = renderScale;

		var panel = go.Components.Create<DamageNumberPanel>();
		panel.Amount = amount;
		panel.Style = style;
		panel.Lifetime = lifetime;
		panel.RiseAmount = riseAmount;
		panel.FadeStart = fadeStart;
	}
}
