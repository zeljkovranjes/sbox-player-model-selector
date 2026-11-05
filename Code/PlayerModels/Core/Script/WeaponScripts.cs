using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PlayerModels.Core.Script;

/// <summary>
/// A weapon design as its script (see <see cref="ScriptKind.Weapon"/>): built into the same <see cref="WeaponDesign"/>
/// the weapon panel makes, the effects it names found among the player's own and those the design carries, and told
/// apart from the stock presets so its script names a preset as one. Allocates: per edit or look change, never per frame.
/// </summary>
public static class WeaponScripts
{
	/// <summary>
	/// The player's library (<paramref name="inner"/>) with the effects a design carries (<paramref name="carried"/>) in
	/// it too, first: so a design names its own effects by name on every machine, whoever else has them.
	/// </summary>
	public sealed class DesignLibrary : IPlayerLibrary
	{
		private readonly IPlayerLibrary _inner;
		private readonly IReadOnlyList<Effect> _carried;

		/// <summary>
		/// <paramref name="inner"/> with <paramref name="carried"/> first, and <paramref name="offered"/> too (the parts of the
		/// weapon held and the stock props, which completion offers while typing).
		/// </summary>
		public DesignLibrary( IPlayerLibrary inner, IReadOnlyList<Effect> carried, IEnumerable<LibraryItem> offered = null )
		{
			_inner = inner;
			_carried = carried ?? Array.Empty<Effect>();
			var items = new List<LibraryItem>();
			foreach ( var effect in _carried )
				items.Add( new LibraryItem( effect.Name ?? "", LibraryItemKind.Effect, effect.Id ?? "" ) );

			items.AddRange( inner?.Items ?? Array.Empty<LibraryItem>() );
			items.AddRange( offered ?? Array.Empty<LibraryItem>() );
			Items = items;
		}

		public IReadOnlyList<LibraryItem> Items { get; }

		public Effect Effect( string name )
		{
			foreach ( var effect in _carried )
			{
				if ( (effect.Name ?? "") == name )
					return effect.Sanitized();
			}

			return _inner?.Effect( name );
		}

		public DrawnShape Shape( string name, LibraryItemKind kind ) => _inner?.Shape( name, kind );
	}

	/// <summary>
	/// <paramref name="script"/> built as a design (for the weapon of <paramref name="stored"/>, whose effects it may name),
	/// compiled by <paramref name="cache"/> naming <paramref name="library"/>; a failed build when it doesn't compile.
	/// </summary>
	public static ScriptBuild Build( string script, WeaponDesign stored, ScriptCache cache, IPlayerLibrary library, ScriptStack stack = null )
	{
		var named = new DesignLibrary( library, stored?.Effects );
		var compiled = cache.Compile( script ?? "", ScriptKind.Weapon, named );
		if ( !compiled.Succeeded )
			return ScriptBuild.Failed( compiled.Errors.Count > 0 ? compiled.Errors[ 0 ] : new ScriptError( "The script didn't compile", 0, 1, 1, 1 ) );

		return new ScriptMachine( compiled.Program ).Build( named, stack );
	}

	/// <summary>
	/// What <paramref name="stored"/> shows as once its script built <paramref name="build"/>: the build's design on the
	/// stored one's weapon, with its script, origin and its skin's own script; null when the build has nothing.
	/// </summary>
	public static WeaponDesign Shown( WeaponDesign stored, ScriptBuild build )
	{
		if ( build?.Design is not { } made || stored is null )
			return null;

		var copy = JsonSerializer.Deserialize<WeaponDesign>( JsonSerializer.Serialize( made ) );
		copy.Weapon = stored.Weapon;
		copy.ForkedFrom = stored.ForkedFrom;
		copy.Script = stored.Script;

		// The skin's own script is kept beside the design's, as an effect's id is: the design's script doesn't write it
		if ( stored.Skin?.Script is { } skin )
		{
			copy.Skin ??= new WeaponSkin();
			copy.Skin.Script = skin;
		}

		return copy;
	}

	/// <summary>
	/// <paramref name="script"/> (a design's, minified) changed so it builds <paramref name="after"/>, the design it built
	/// being <paramref name="before"/>: the change written in place (see <see cref="ScriptEdit.ApplyWeapon"/>), minified;
	/// null when the script no longer builds or the change can only be made in code (the design then keeps what it shows,
	/// without a script). For the weapon panel's changes to a design with a script while its code isn't open.
	/// </summary>
	public static string Follow( string script, WeaponDesign before, WeaponDesign after, IPlayerLibrary library )
	{
		var source = Formatter.Format( script ) ?? script;
		var build = Build( source, before, new ScriptCache(), library );
		if ( !build.Succeeded || Shown( before, build ) is not { } built )
			return null;

		var edit = ScriptEdit.ApplyWeapon( source, build, built, after );
		return edit.Succeeded ? Minifier.Minify( edit.Source ) : null;
	}

	/// <summary>
	/// The stock preset <paramref name="effect"/> (one on a weapon) is, as it would be put on with its sliders, switch and
	/// When changed; null when it is something else (a preset changed in other ways, or the player's own).
	/// </summary>
	public static Effect PresetOf( Effect effect )
	{
		foreach ( var preset in EffectPresets.All )
		{
			if ( preset.Name != effect.Name )
				continue;

			var copy = preset.Sanitized();
			if ( copy is null )
				continue;

			if ( ScriptLiteral.UniformWhen( effect ) is { } when )
				WeaponDesign.ShowWhen( copy, when );

			(copy.Off, copy.Hue, copy.Size, copy.Amount, copy.Speed) = (effect.Off, effect.Hue, effect.Size, effect.Amount, effect.Speed);
			if ( Comparable( copy ) == Comparable( effect ) )
				return preset;
		}

		return null;
	}

	// An effect on a weapon as what makes it the same: its layers and sliders, not its id, anchor or key
	private static string Comparable( Effect effect )
	{
		var copy = effect.Sanitized();
		if ( copy is null )
			return "";

		copy.Id = "";
		copy.Key = null;
		copy.KeyMode = null;
		copy.ForkedFrom = null;
		copy.Anchor = new EffectAnchor();
		foreach ( var layer in copy.Layers )
			layer.When = layer.WhenIs == LayerWhen.Always ? null : layer.When;

		return JsonSerializer.Serialize( copy );
	}
}
