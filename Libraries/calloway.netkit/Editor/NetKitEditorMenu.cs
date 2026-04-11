using Editor;
using Sandbox;
using System.Linq;

/// <summary>
/// NetKit editor tools. Accessible via Editor → NetKit in the menu bar.
/// </summary>
public static class NetKitEditorMenu
{
	[Menu( "Editor", "NetKit/Show Active Cooldowns" )]
	public static void ShowCooldowns()
	{
		Log.Info( "=== NetKit Active Cooldowns ===" );

		if ( !Networking.IsHost )
		{
			Log.Info( "(client-side view only)" );
			return;
		}

		Log.Info( "(run in-game to see active cooldowns)" );
	}

	[Menu( "Editor", "NetKit/About" )]
	public static void About()
	{
		EditorUtility.DisplayDialog(
			"NetKit",
			"Networking primitives for s&box.\n\n" +
			"• NetRequest — async request/response\n" +
			"• NetChannel — typed event bus\n" +
			"• NetCooldown — shared cooldowns\n" +
			"• NetTransaction — client prediction\n" +
			"• NetState<T> — replicated state machines\n" +
			"• NetVisibility — declarative visibility\n\n" +
			"github.com/CallowayIsWeird/netkit"
		);
	}
}
