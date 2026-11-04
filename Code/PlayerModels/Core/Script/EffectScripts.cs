using System;
using System.Collections.Generic;
using System.Linq;

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
	/// <paramref name="look"/> with each of its scripted effects (its body's and its weapons') as <paramref name="build"/>
	/// builds it (see <see cref="Shown"/>), the drawn shapes the scripts made carried by the look. An effect whose script
	/// fails shows what its look kept, without its script (nothing of it runs); one <paramref name="build"/> gives null for
	/// (its turn to compile hasn't come) does too, and <paramref name="pending"/> says to ask again. This very look when it
	/// has no scripts, else a copy.
	/// </summary>
	public static PlayerLook WithScriptsBuilt( PlayerLook look, Func<Effect, ScriptBuild> build, out bool pending, Func<WeaponDesign, ScriptBuild> buildDesign = null )
	{
		pending = false;
		if ( !HasScripts( look ) )
			return look;

		var copy = look.Sanitized();

		// A weapon's design first: its script names the effects it puts on, whose own scripts are built after
		for ( var i = 0; i < copy.Weapons.Count; i++ )
		{
			var stored = copy.Weapons[ i ];
			if ( stored.Script is null || buildDesign is null )
				continue;

			var made = buildDesign( stored );
			pending |= made is null;
			if ( made is { Succeeded: true } && WeaponScripts.Shown( stored, made ) is { } shown )
			{
				foreach ( var shape in shown.Shapes ?? new List<DrawnShape>() )
				{
					if ( !copy.DrawnShapes.Exists( x => x.Id == shape.Id ) )
						copy.DrawnShapes.Add( shape );
				}

				shown.Shapes = null;
				copy.Weapons[ i ] = shown;
				continue;
			}

			stored.Script = null;
		}

		pending |= BuildEach( copy.Effects, copy, build, false );
		foreach ( var design in copy.Weapons )
			pending |= BuildEach( design.Effects, copy, build, true );

		return copy;
	}

	/// <summary>Whether any effect of <paramref name="look"/>, its body's or a weapon's, has a script.</summary>
	public static bool HasScripts( PlayerLook look ) => EffectsOf( look ).Any( x => x.Script is not null ) || (look?.Weapons ?? new List<WeaponDesign>()).Any( x => x.Script is not null );

	// Builds each scripted effect of effects in place (one on a weapon stays on it, without a key); true when one has to
	// wait its turn
	private static bool BuildEach( List<Effect> effects, PlayerLook look, Func<Effect, ScriptBuild> build, bool onWeapon )
	{
		var pending = false;
		for ( var i = 0; i < (effects?.Count ?? 0); i++ )
		{
			var stored = effects[ i ];
			if ( stored.Script is null )
				continue;

			var made = build( stored );
			pending |= made is null;
			if ( made is { Succeeded: true } && Shown( stored, made ) is { } shown )
			{
				foreach ( var shape in shown.Shapes ?? new List<DrawnShape>() )
				{
					if ( !look.DrawnShapes.Exists( x => x.Id == shape.Id ) )
						look.DrawnShapes.Add( shape );
				}

				shown.Shapes = null;
				if ( onWeapon )
				{
					shown.Key = null;
					shown.Anchor.Kind = stored.Anchor.Kind;
					shown.Anchor.Joint = stored.Anchor.Joint;
				}

				effects[ i ] = shown;
				continue;
			}

			stored.Script = null;
			stored.Parameters = null;
		}

		return pending;
	}

	/// <summary>
	/// <paramref name="look"/> without running any script: each effect shows what its look kept. What a client shows where
	/// the host switched scripts off, or of a script too long for the host's limit (<paramref name="mostBytes"/>). This very
	/// look when nothing changes, else a copy.
	/// </summary>
	public static PlayerLook WithoutScripts( PlayerLook look, int mostBytes = 0 )
	{
		if ( !EffectsOf( look ).Any( x => TooLong( x, mostBytes ) ) && !(look?.Weapons ?? new List<WeaponDesign>()).Any( x => TooLong( x.Script, mostBytes ) || TooLong( x.Skin?.Script, mostBytes ) )
			&& !(look?.DrawnShapes ?? new List<DrawnShape>()).Any( x => TooLong( x.Script, mostBytes ) ) )
			return look;

		var copy = look.Sanitized();
		foreach ( var effect in EffectsOf( copy ) )
		{
			if ( TooLong( effect, mostBytes ) )
			{
				effect.Script = null;
				effect.Parameters = null;
			}
		}

		foreach ( var design in copy.Weapons )
		{
			if ( TooLong( design.Script, mostBytes ) )
				design.Script = null;

			// A skin's own script gives way to its stock motion
			if ( design.Skin is { } skin && TooLong( skin.Script, mostBytes ) )
				skin.Script = null;
		}

		// A sticker's own script too: its layers keep their own colours
		for ( var i = 0; i < copy.DrawnShapes.Count; i++ )
		{
			if ( TooLong( copy.DrawnShapes[ i ].Script, mostBytes ) )
				copy.DrawnShapes[ i ] = copy.DrawnShapes[ i ].Scripted( null );
		}

		return copy;
	}

	private static bool TooLong( Effect effect, int mostBytes ) => TooLong( effect.Script, mostBytes );

	private static bool TooLong( string script, int mostBytes ) => script is not null && ScriptLimits.Utf8Bytes( script ) > mostBytes;

	// Every effect of a look: its body's, then each weapon's
	private static IEnumerable<Effect> EffectsOf( PlayerLook look )
	{
		if ( look is null )
			yield break;

		foreach ( var effect in look.Effects ?? new List<Effect>() )
			yield return effect;

		foreach ( var design in look.Weapons ?? new List<WeaponDesign>() )
		{
			foreach ( var effect in design.Effects ?? new List<Effect>() )
				yield return effect;
		}
	}
}
