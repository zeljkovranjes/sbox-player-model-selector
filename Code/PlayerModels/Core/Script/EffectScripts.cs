using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// A look's effects as their scripts build them on this client. The look keeps what each script built when its author
/// saved it (what shows where scripts are off, or a script doesn't compile); a client that runs scripts builds each again
/// from its script and chosen parameters, so what a look claims its layers are never matters. The host does none of
/// this: it only holds scripts to their sizes.
/// </summary>
public static class EffectScripts
{
	/// <summary>
	/// <paramref name="effect"/>'s script built with its chosen parameters, naming <paramref name="library"/>, compiled by
	/// <paramref name="cache"/>; null for an effect without a script. A script that doesn't compile or build gives a failed
	/// build. Allocates: once per look change, never per frame.
	/// </summary>
	public static ScriptBuild Build( Effect effect, ScriptCache cache, IPlayerLibrary library, ScriptStack stack = null )
	{
		if ( effect?.Script is null )
			return null;

		var compiled = cache.Compile( effect.Script, ScriptKind.Effect, library );
		if ( !compiled.Succeeded )
			return ScriptBuild.Failed( compiled.Errors.Count > 0 ? compiled.Errors[ 0 ] : new ScriptError( "The script didn't compile", 0, 1, 1, 1 ) );

		var machine = new ScriptMachine( compiled.Program );
		machine.SetParameters( effect.Parameters );
		return machine.Build( library, stack );
	}

	/// <summary>
	/// What <paramref name="stored"/> shows as once its script built <paramref name="build"/>: the build's layers and
	/// fields, each layer marked with its place in the build (<see cref="EffectLayer.ScriptLayer"/>) and on (its script
	/// switches it while it runs, so the budget counts it), with the stored effect's id, switch, key action, origin,
	/// script and parameters. Null when the build has nothing to show.
	/// </summary>
	public static Effect Shown( Effect stored, ScriptBuild build )
	{
		if ( build?.Effect is not { } made )
			return null;

		for ( var i = 0; i < made.Layers.Count; i++ )
			made.Layers[ i ].ScriptLayer = i;

		var shown = made.Sanitized();
		if ( shown is null || shown.Layers.Count != made.Layers.Count )
			return null;

		foreach ( var layer in shown.Layers )
			layer.Off = false;

		shown.Id = stored.Id;
		shown.Off = stored.Off;
		shown.KeyMode = stored.KeyMode;
		shown.ForkedFrom = stored.ForkedFrom;
		shown.Script = stored.Script;
		shown.Parameters = stored.Parameters;
		return shown;
	}

	/// <summary>
	/// <paramref name="look"/> with each of its scripted effects as <paramref name="build"/> builds it (see
	/// <see cref="Shown"/>), the drawn shapes the scripts made carried by the look. An effect whose script fails shows
	/// what its look kept, without its script (nothing of it runs); one <paramref name="build"/> gives null for (its turn to
	/// compile hasn't come) does too, and <paramref name="pending"/> says to ask again. This very look when it has no
	/// scripts, else a copy.
	/// </summary>
	public static PlayerLook WithScriptsBuilt( PlayerLook look, Func<Effect, ScriptBuild> build, out bool pending )
	{
		pending = false;
		if ( look is null || !look.Effects.Exists( x => x.Script is not null ) )
			return look;

		var copy = look.Sanitized();
		for ( var i = 0; i < copy.Effects.Count; i++ )
		{
			var stored = copy.Effects[ i ];
			if ( stored.Script is null )
				continue;

			var made = build( stored );
			pending |= made is null;
			if ( made is { Succeeded: true } && Shown( stored, made ) is { } shown )
			{
				foreach ( var shape in shown.Shapes ?? new List<DrawnShape>() )
				{
					if ( !copy.DrawnShapes.Exists( x => x.Id == shape.Id ) )
						copy.DrawnShapes.Add( shape );
				}

				shown.Shapes = null;
				copy.Effects[ i ] = shown;
				continue;
			}

			stored.Script = null;
			stored.Parameters = null;
		}

		return copy;
	}

	/// <summary>
	/// <paramref name="look"/> without running any script: each effect shows what its look kept. What a client shows where
	/// the host switched scripts off, or of a script too long for the host's limit (<paramref name="mostBytes"/>). This very
	/// look when nothing changes, else a copy.
	/// </summary>
	public static PlayerLook WithoutScripts( PlayerLook look, int mostBytes = 0 )
	{
		if ( look is null || !look.Effects.Exists( x => x.Script is not null && ScriptLimits.Utf8Bytes( x.Script ) > mostBytes ) )
			return look;

		var copy = look.Sanitized();
		foreach ( var effect in copy.Effects )
		{
			if ( effect.Script is not null && ScriptLimits.Utf8Bytes( effect.Script ) > mostBytes )
			{
				effect.Script = null;
				effect.Parameters = null;
			}
		}

		return copy;
	}
}
