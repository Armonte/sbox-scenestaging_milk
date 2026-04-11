using System;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.UI;

namespace Iconify;

/// <summary>
/// Renders an icon from the Iconify API.
/// Usage: <iconify icon="ph:house" Size=@(24) Color="white" />
/// Browse icons: https://icones.js.org/
/// </summary>
[Alias( "iconify" )]
public partial class IconifyPanel : Panel
{
	private Texture? _texture;
	private string? _loadedKey;

	/// <summary>
	/// The icon identifier. Format: "prefix:name" e.g. "ph:house", "mdi:home", "tabler:settings"
	/// Browse: https://icones.js.org/
	/// </summary>
	public string Icon { get; set; } = "";

	/// <summary>
	/// Icon color as a hex or named color. Default: white.
	/// </summary>
	public string Color { get; set; } = "white";

	/// <summary>
	/// Icon size in pixels. Default: 24.
	/// </summary>
	public int Size { get; set; } = 24;

	protected override void OnAfterTreeRender( bool firstTime )
	{
		var key = $"{Icon}_{Color}_{Size}";
		if ( key == _loadedKey ) return;
		_loadedKey = key;

		_ = LoadIcon();
	}

	protected override int BuildHash() => HashCode.Combine( Icon, Color, Size, _texture );

	private async Task LoadIcon()
	{
		if ( string.IsNullOrWhiteSpace( Icon ) ) return;

		var parts = Icon.Split( ':', 2 );
		if ( parts.Length != 2 ) return;

		var prefix = parts[0];
		var name = parts[1];

		try
		{
			// Try cache first
			var texture = await IconCache.GetOrFetch( prefix, name, Color, Size );
			if ( texture is not null )
			{
				_texture = texture;
				StateHasChanged();
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Iconify] Failed to load '{Icon}': {e.Message}" );
		}
	}
}
