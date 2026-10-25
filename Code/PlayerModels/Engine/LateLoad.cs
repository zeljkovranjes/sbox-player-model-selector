using System;
using System.Linq;
using System.Threading.Tasks;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// For a client whose copy of this addon's code arrives after the selector did. Sandbox spawns an entity on the host
/// (Cloud.Load, Clone, NetworkSpawn) and a client still loading the package gets the object first, with this addon's
/// components held as missing; the engine never makes them once the code is there. The engine reads each console
/// variable of a package as it registers the code, which runs this class's static constructor; a moment later the
/// client asks the host to send each missing component again. Copied from the Skateboard addon, which found this out.
/// </summary>
internal static class LateLoad
{
	/// <summary>Whether a client asks the host for this addon's components that reached it before the code.</summary>
	[ConVar( "player_models_fetch_missing", Help = "Ask the host for Player Model Selector parts that arrived before its code" )]
	public static bool FetchMissing { get; set; } = true;

	// Registering FetchMissing reads it, which runs this as the code loads; the objects that came first are in the
	// scene a moment later.
	static LateLoad() => _ = CheckSoon();

	private static async Task CheckSoon()
	{
		await GameTask.DelaySeconds( 0.5f );

		var scene = Game.ActiveScene;
		if ( !FetchMissing || !Networking.IsActive || Networking.IsHost || !scene.IsValid() || scene.IsEditor )
			return;

		Ask( scene );
	}

	// Asks the host for each component of this addon held as missing here.
	private static void Ask( Scene scene )
	{
		const string ours = nameof( PlayerModels ) + ".";

		foreach ( var missing in scene.Components.GetAll<MissingComponent>( FindMode.EverythingInSelfAndDescendants ).ToArray() )
		{
			var json = missing.GetJson();

			if ( json?[ "__type" ]?.ToString() is { } type && type.StartsWith( ours )
				&& Guid.TryParse( json[ "__guid" ]?.ToString(), out var id ) )
				SendAgain( id );
		}
	}

	/// <summary>Host: sends one component again to every client, asked by one whose copy of it is missing.</summary>
	[Rpc.Host]
	public static void SendAgain( Guid component )
	{
		if ( Game.ActiveScene?.Directory.FindComponentByGuid( component ) is { } found && found.GameObject.IsValid() )
			found.GameObject.Network.Refresh( found );
	}
}
