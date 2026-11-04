using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PlayerModels.Core.Script;

/// <summary>
/// Keeps a scripted effect and its script in step both ways. The visual editor changes the effect's fields as it always
/// did; <see cref="Reconcile"/> then writes what changed into the script in place (see <see cref="ScriptEdit"/>), or
/// says why it can't (a field the script works out, a layer moved). The other way, <see cref="Adopt"/> gives the effect
/// what its script built. The script is the source of truth: a scripted effect's fields are always what it builds.
/// </summary>
public static class ScriptSync
{
	/// <summary>
	/// <paramref name="source"/> changed so it builds <paramref name="edited"/> (see the other overload), building it
	/// first with the effect's chosen parameters, naming <paramref name="library"/>: the edit's places are always this
	/// very text's. A failed result when the source has errors.
	/// </summary>
	public static EditResult Reconcile( string source, Effect edited, IPlayerLibrary library = null, Func<string, string> shapeName = null )
	{
		var compiled = ScriptCompiler.Compile( source, ScriptKind.Effect, library );
		if ( !compiled.Succeeded )
			return new EditResult( source, "The script has errors: fix them in code first", compiled.Errors.Count > 0 ? compiled.Errors[ 0 ].Line : 0 );

		var machine = new ScriptMachine( compiled.Program );
		machine.SetParameters( edited?.Parameters );
		return Reconcile( source, machine.Build( library ), edited, shapeName );
	}

	/// <summary>
	/// <paramref name="source"/> changed so it builds <paramref name="edited"/>: what differs from
	/// <paramref name="build"/> (what this very <paramref name="source"/> builds now: its places are this text's) rewritten
	/// in place, or one layer removed. The
	/// source as it was when nothing differs; a failed result, with the reason and its line, when the change can only be
	/// made in code (the caller puts the effect back as built).
	/// </summary>
	public static EditResult Reconcile( string source, ScriptBuild build, Effect edited, Func<string, string> shapeName = null )
	{
		if ( build?.Effect is not { } before || edited is null )
			return new EditResult( source, "The script has errors: fix them in code first" );

		if ( Same( before, edited ) )
			return new EditResult( source );

		var was = before.Layers;
		var now = edited.Layers;
		if ( now.Count < was.Count )
		{
			var removed = now.Count == was.Count - 1 ? RemovedLayer( was, now ) : -1;
			return removed < 0
				? new EditResult( source, "Remove layers one at a time" )
				: ScriptEdit.RemoveLayer( source, build, removed );
		}

		if ( now.Count == was.Count && IsReordered( was, now ) )
			return new EditResult( source, "Layers are moved in code: move the lines that make them" );

		return ScriptEdit.Apply( source, build, before, edited, shapeName );
	}

	/// <summary>
	/// Gives <paramref name="effect"/> what its script built (<paramref name="built"/>): its layers and every field the
	/// script sets. What stays outside the script (its id, key action, origin, script and parameters) is kept.
	/// </summary>
	public static void Adopt( Effect effect, Effect built )
	{
		var copy = built.Sanitized();
		if ( copy is null )
			return;

		effect.Name = copy.Name;
		effect.Key = copy.Key;
		effect.Off = copy.Off;
		effect.Anchor = copy.Anchor;
		effect.Layers = copy.Layers;
		effect.Hue = copy.Hue;
		effect.Size = copy.Size;
		effect.Amount = copy.Amount;
		effect.Speed = copy.Speed;
		effect.Shapes = copy.Shapes;
	}

	/// <summary>Whether <paramref name="a"/> and <paramref name="b"/> build the same: what the script decides, nothing outside it.</summary>
	public static bool Same( Effect a, Effect b ) => Comparable( a ) == Comparable( b );

	private static string Comparable( Effect effect )
	{
		var copy = effect?.Sanitized();
		if ( copy is null )
			return "";

		copy.Id = "";
		copy.ForkedFrom = null;
		copy.Shapes = null;
		copy.Script = null;
		copy.Parameters = null;
		copy.KeyMode = null;
		return JsonSerializer.Serialize( copy );
	}

	private static string Data( EffectLayer layer ) => JsonSerializer.Serialize( layer.Sanitized( x => x ) );

	// Which layer of was is gone from now (the rest the same, in order); -1 when it isn't one removal
	private static int RemovedLayer( IReadOnlyList<EffectLayer> was, IReadOnlyList<EffectLayer> now )
	{
		for ( var k = 0; k < was.Count; k++ )
		{
			var same = true;
			for ( var i = 0; i < now.Count && same; i++ )
				same = Data( was[ i < k ? i : i + 1 ] ) == Data( now[ i ] );

			if ( same )
				return k;
		}

		return -1;
	}

	// Whether now has was's layers in another order (not just changed)
	private static bool IsReordered( IReadOnlyList<EffectLayer> was, IReadOnlyList<EffectLayer> now )
	{
		var moved = false;
		var left = new List<string>();
		foreach ( var layer in was )
			left.Add( Data( layer ) );

		for ( var i = 0; i < now.Count; i++ )
		{
			var data = Data( now[ i ] );
			var at = left.IndexOf( data );
			if ( at < 0 )
				return false;

			moved |= Data( was[ i ] ) != data;
			left[ at ] = null;
		}

		return moved;
	}
}
