using System;
using System.Linq;
using Sandbox;

/// <summary>
/// Declarative network visibility rules. Drop on any networked GameObject and configure
/// who can see it without implementing <c>INetworkVisible</c> yourself.
/// </summary>
/// <remarks>
/// <para><b>Distance:</b> <c>vis.Mode = NetVisibilityMode.Distance; vis.MaxDistance = 500f;</c></para>
/// <para><b>Owner only:</b> <c>vis.Mode = NetVisibilityMode.OwnerOnly;</c></para>
/// <para><b>Custom:</b> <c>vis.Mode = NetVisibilityMode.Custom; vis.Filter = conn =&gt; MyCheck(conn);</c></para>
/// </remarks>
[Title( "NetKit - Visibility" )]
[Category( "NetKit" )]
[Icon( "visibility" )]
public sealed class NetVisibility : Component, Component.INetworkVisible
{
	/// <summary>Visibility mode.</summary>
	[Property] public NetVisibilityMode Mode { get; set; } = NetVisibilityMode.Always;

	/// <summary>Max distance for Distance mode.</summary>
	[Property] public float MaxDistance { get; set; } = 1000f;

	/// <summary>Custom filter delegate for Custom mode. Set from code.</summary>
	public Func<Connection, bool> Filter { get; set; }

	bool Component.INetworkVisible.IsVisibleToConnection( Connection connection, in BBox bounds )
	{
		return Mode switch
		{
			NetVisibilityMode.Always => true,
			NetVisibilityMode.Never => false,
			NetVisibilityMode.OwnerOnly => connection.Id == Network.Owner?.Id,
			NetVisibilityMode.Distance => IsWithinDistance( connection ),
			NetVisibilityMode.Custom => Filter?.Invoke( connection ) ?? true,
			_ => true
		};
	}

	private bool IsWithinDistance( Connection connection )
	{
		// Find any networked object owned by this connection to get their position.
		// Generic — works with any gamemode, not tied to a specific Player class.
		var owned = Scene.GetAllObjects( false )
			.FirstOrDefault( go => go.Network.Active && go.Network.Owner?.Id == connection.Id );

		if ( owned == null ) return true; // can't determine position, default visible

		return owned.WorldPosition.Distance( WorldPosition ) <= MaxDistance;
	}
}

/// <summary>Visibility modes for <see cref="NetVisibility"/>.</summary>
public enum NetVisibilityMode
{
	/// <summary>Always visible (default — same as no component).</summary>
	Always,
	/// <summary>Never visible.</summary>
	Never,
	/// <summary>Only visible to the network owner.</summary>
	OwnerOnly,
	/// <summary>Visible within MaxDistance of the connection's owned objects.</summary>
	Distance,
	/// <summary>Determined by the Filter delegate.</summary>
	Custom
}
