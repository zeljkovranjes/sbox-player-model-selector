using System;
using System.Collections.Generic;
using System.Text.Json;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.UI;

// The code editor on a weapon's whole design (see WeaponScripts), in place of an effect: opened as its script (a design
// made in the weapon panel as the script it would be), each good build put on the design, the panel's changes written
// into the code in place, and the caret in a sticker's, prop's or effect's lines selecting it in the panel
public partial class CodeEditor
{
	/// <summary>The weapon (its world model) whose design is edited, in place of an effect; null for an effect.</summary>
	public string Weapon { get; set; }

	/// <summary>Called with the item of the design the caret moves into: its kind (see <see cref="WeaponItem"/>) and place.</summary>
	public Action<WeaponItem, int> CaretInWeaponItem { get; init; }

	/// <summary>What of a weapon's design an item is.</summary>
	public enum WeaponItem { Prop, Sticker, Effect }

	// A design script starts as this: building it changes nothing yet
	private const string WeaponStarter = """
		-- This weapon's design: its finish and tint, its skin, and the stickers, props and effects put on it
		Weapon.Finish = Finish.None
		""";

	private WeaponScripts.DesignLibrary _designLibrary;
	private (WeaponItem Kind, int Index) _caretItem = (WeaponItem.Prop, -1);

	private ScriptKind Kind => Sticker is { } part ? (part == StickerCode.Layers ? ScriptKind.Shape : ScriptKind.Sticker)
		: Weapon is null ? ScriptKind.Effect : Skin ? ScriptKind.Skin : ScriptKind.Weapon;

	// What the script may name: the player's own, and for a design the effects it carries, its weapon's parts and the props
	private IPlayerLibrary Names => Weapon is null ? _library : _designLibrary;

	private WeaponDesign DesignOf( PlayerLook look ) => Weapon is null ? null : look?.Weapons.Find( x => x.Weapon == Weapon );

	// The design's script formatted, or the design written as one
	private string OpenWeapon( PlayerLook look )
	{
		if ( Skin )
			return OpenSkin( look );

		var design = DesignOf( look );
		_designLibrary = new WeaponScripts.DesignLibrary( _library, design?.Effects, Offered() );
		_openedAs = design is null ? null : JsonSerializer.Serialize( design );
		_applied = _openedAs;
		_name = $"{WeaponDesign.NameOf( Weapon )} design";
		if ( design?.Script is { } script )
			return Formatter.Format( script ) ?? script;

		var made = design is null || design.IsEmpty ? WeaponStarter : ScriptCodegen.Weapon( design, look.DrawnShapes );
		return Formatter.Format( made ) ?? made;
	}

	// The parts of the weapon (it is held, so loaded) and the stock props, for completion in their strings
	private IEnumerable<LibraryItem> Offered()
	{
		var model = Model.Load( Weapon );
		if ( model is { IsValid: true, IsError: false } )
		{
			foreach ( var part in WeaponParts.Of( model ) )
				yield return new LibraryItem( part, LibraryItemKind.Part, "" );
		}

		foreach ( var prop in PropCatalog.All )
			yield return new LibraryItem( prop, LibraryItemKind.Prop, "" );
	}

	// The build on the weapon's design (made when it has none yet): its script minified, the shapes it drew carried by the look
	private void PutDesign( ScriptBuild build )
	{
		var look = LookNow?.Invoke();
		if ( look is null )
			return;

		var at = look.Weapons.FindIndex( x => x.Weapon == Weapon );
		if ( at < 0 && look.Weapons.Count >= WeaponDesign.MaxPerLook )
		{
			_note = $"A look has designs for up to {WeaponDesign.MaxPerLook} weapons: reset one to design this";
			return;
		}

		var stored = at >= 0 ? look.Weapons[ at ] : new WeaponDesign { Weapon = Weapon };
		if ( WeaponScripts.Shown( stored, build ) is not { } shown )
			return;

		shown.Script = Minifier.Minify( _source );
		foreach ( var shape in shown.Shapes ?? new List<DrawnShape>() )
		{
			if ( !look.Carry( shape ) )
				_note = $"A look carries at most {DrawnShape.MaxPerLook} drawn shapes: this design's aren't all kept";
		}

		shown.Shapes = null;
		if ( at >= 0 )
			look.Weapons[ at ] = shown;
		else
			look.Weapons.Add( shown );

		_applied = JsonSerializer.Serialize( shown );
		Changed?.Invoke();
	}

	// The weapon panel changed the design: written into the code in place, or put back saying why
	private void SyncDesign()
	{
		if ( Skin )
		{
			SyncSkin();
			return;
		}

		var look = LookNow?.Invoke();
		var design = DesignOf( look );
		if ( design is null || _entry is null )
			return;

		var now = JsonSerializer.Serialize( design );
		if ( now == _applied )
			return;

		// Its script changed elsewhere (undo, the Workshop): that is the text now
		if ( design.Script is { } script && script != Minifier.Minify( _source ) && _applied is not null
			&& JsonSerializer.Deserialize<WeaponDesign>( _applied )?.Script is { } before && before != script )
		{
			_entry.Rewrite( Formatter.Format( script ) ?? script );
			_source = _entry.Source;
			_applied = now;
			Compile( false );
			return;
		}

		var built = _errors.Count == 0 ? WeaponScripts.Shown( design, _build ) : null;
		var result = built is null ? new EditResult( _source, "The code has errors: fix them first" ) : ScriptEdit.ApplyWeapon( _source, _build, built, design, ShapeName );
		if ( result.Succeeded )
		{
			if ( result.Source != _source )
			{
				_entry.Rewrite( result.Source );
				_source = _entry.Source;
			}

			_applied = now;
			Compile( true );
			return;
		}

		// Not something the script lets be changed here: put back, saying why (and where)
		if ( built is not null )
		{
			built.Script = design.Script;
			look.Weapons[ look.Weapons.IndexOf( design ) ] = built;
			_applied = JsonSerializer.Serialize( built );
		}

		_note = result.Line > 0 ? $"{result.Problem} · line {result.Line}" : result.Problem;
		Changed?.Invoke();
		StateHasChanged();
	}

	// The design as it was when opened (or last saved): Auto-apply's changes let go
	private void PutBackDesign()
	{
		var look = LookNow?.Invoke();
		if ( look is null )
			return;

		var at = look.Weapons.FindIndex( x => x.Weapon == Weapon );
		if ( _openedAs is null )
		{
			if ( at >= 0 )
				look.Weapons.RemoveAt( at );
		}
		else if ( JsonSerializer.Deserialize<WeaponDesign>( _openedAs ) is { } opened )
		{
			if ( at >= 0 )
				look.Weapons[ at ] = opened;
			else
				look.Weapons.Add( opened );
		}

		Changed?.Invoke();
	}

	/// <summary>Puts the caret on the lines putting item <paramref name="index"/> of <paramref name="kind"/> on the weapon (the panel selected it).</summary>
	public void ShowWeaponItem( WeaponItem kind, int index )
	{
		if ( SiteOf( kind, index ) is not { } site || _program?.Source != _source )
			return;

		_caretItem = (kind, index);
		_entry.Select( CodeText.LineStart( _source, site.StatementStart ), CodeText.LineEnd( _source, site.StatementEnd ) );
		EnsureVisible( site.StatementStart );
	}

	private ScriptSite SiteOf( WeaponItem kind, int index ) => kind switch
	{
		WeaponItem.Prop => _build?.PropSite( index ),
		WeaponItem.Sticker => _build?.StickerSite( index ),
		_ => _build?.EffectSite( index ),
	};

	// The item the caret is in: the last put on at or before it, while the build is of the text now
	private void FollowCaretInDesign( int caret )
	{
		if ( _build?.Design is not { } design || _program?.Source != _source )
			return;

		var found = (Kind: WeaponItem.Prop, Index: -1);
		var at = -1;
		foreach ( var (kind, count) in new[] { (WeaponItem.Prop, design.Props?.Count ?? 0), (WeaponItem.Sticker, design.Stickers?.Count ?? 0), (WeaponItem.Effect, design.Effects?.Count ?? 0) } )
		{
			for ( var i = 0; i < count; i++ )
			{
				if ( SiteOf( kind, i ) is { IsTopLevel: true } site && site.StatementStart <= caret && site.StatementStart > at )
					(found, at) = ((kind, i), site.StatementStart);
			}
		}

		if ( found.Index >= 0 && found != _caretItem )
		{
			_caretItem = found;
			CaretInWeaponItem?.Invoke( found.Kind, found.Index );
		}
	}
}
