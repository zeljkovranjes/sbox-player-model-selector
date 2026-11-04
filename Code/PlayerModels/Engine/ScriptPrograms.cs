using System;
using System.Collections.Generic;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The effect scripts this machine runs, compiled once each (see <see cref="ScriptCache"/>): a look worn again, or one
/// script on a hundred players, costs one compile, and each player's new scripts are paced (see
/// <see cref="CompileBudget"/>). Shared by every scene on the machine (the world and the editors' previews), like the
/// textures of drawn shapes (see <see cref="ShapeTextures"/>). Also the stack every run shares: runs never overlap.
/// </summary>
internal static class ScriptPrograms
{
	/// <summary>Every script compiled on this machine, by its text.</summary>
	internal static readonly ScriptCache Cache = new();

	/// <summary>How fast each player's new scripts are compiled here.</summary>
	internal static readonly CompileBudget Budget = new();

	/// <summary>The scratch every script run on this machine uses, one at a time.</summary>
	internal static readonly ScriptStack Stack = new();

	// The looks built last, by what they are: a preview asks every frame for the same look, built once
	private const int MostRecent = 8;
	private static readonly Dictionary<string, PlayerLook> Recent = new();
	private static readonly Queue<string> RecentOrder = new();

	/// <summary>
	/// <paramref name="look"/> with its effects as their scripts build them here (see
	/// <see cref="EffectScripts.WithScriptsBuilt"/>), for <paramref name="player"/>: scripts at most
	/// <paramref name="mostBytes"/> long run (0: none do), and a script not compiled yet waits for the player's turn
	/// unless <paramref name="paced"/> is off (the player's own, on their own machine). <paramref name="pending"/> says some
	/// waited: ask again soon.
	/// </summary>
	internal static PlayerLook Built( PlayerLook look, Guid player, int mostBytes, bool paced, out bool pending )
	{
		pending = false;
		if ( !EffectScripts.HasScripts( look ) )
			return look;

		var key = $"{player}|{mostBytes}|{look.Serialize()}";
		if ( Recent.TryGetValue( key, out var known ) )
			return known;

		look = EffectScripts.WithoutScripts( look, mostBytes );
		var library = new LookLibrary( look.DrawnShapes );
		var now = RealTime.Now;
		var built = EffectScripts.WithScriptsBuilt( look, effect =>
		{
			if ( paced && !Cache.Has( effect.Script, ScriptKind.Effect, library ) && !Budget.TryTake( player, now ) )
				return null;

			return EffectScripts.Build( effect, Cache, library, Stack );
		}, out pending );

		if ( !pending )
			Remember( key, built );

		return built;
	}

	private static void Remember( string key, PlayerLook built )
	{
		Recent[ key ] = built;
		RecentOrder.Enqueue( key );
		while ( RecentOrder.Count > MostRecent )
			Recent.Remove( RecentOrder.Dequeue() );
	}
}
