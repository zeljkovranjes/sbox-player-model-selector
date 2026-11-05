using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

// The weapon panel's changes made to a weapon design script in place, as ScriptEdit makes the visual editor's to an
// effect's: a field set by a literal at the top has that literal rewritten, one not set yet gets a statement after its
// owner's others, and a sticker, prop, hidden part or effect added or taken away is a block added at the end or the
// statements about it taken out. A field worked out by code is left alone and reported with its line
public static partial class ScriptEdit
{
	/// <summary>
	/// Makes <paramref name="source"/> (built as <paramref name="build"/> into <paramref name="before"/>) build
	/// <paramref name="after"/>: every changed field edited in place, stickers, props, hidden parts and effects added or
	/// taken away (one taken away at a time). Nothing is changed when any edit can't be made.
	/// </summary>
	public static EditResult ApplyWeapon( string source, ScriptBuild build, WeaponDesign before, WeaponDesign after, Func<string, string> shapeName = null )
	{
		if ( build?.Design is null )
			return new EditResult( source, "The script has errors: fix them in code first" );

		// One taken away: only its statements go, so its place's later items keep theirs
		if ( Removed( before.Stickers, after.Stickers, x => Data( x ) ) is { } sticker )
			return RemoveItem( source, build, build.StickerSite( sticker ), ScriptBuild.StickerOwner + sticker );

		if ( Removed( before.Props, after.Props, x => Data( x ) ) is { } prop )
			return RemoveItem( source, build, build.PropSite( prop ), ScriptBuild.PropOwner + prop );

		if ( Removed( before.Effects, after.Effects, x => Data( x ) ) is { } effect )
			return RemoveItem( source, build, build.EffectSite( effect ), ScriptBuild.EffectOwnerBase + effect );

		var hiddenBefore = before.HiddenParts ?? new List<string>();
		var hiddenAfter = after.HiddenParts ?? new List<string>();
		for ( var i = 0; i < hiddenBefore.Count; i++ )
		{
			if ( !hiddenAfter.Contains( hiddenBefore[ i ] ) )
				return RemoveItem( source, build, build.PartSite( i ), int.MinValue );
		}

		if ( (after.Stickers?.Count ?? 0) < (before.Stickers?.Count ?? 0) || (after.Props?.Count ?? 0) < (before.Props?.Count ?? 0)
			|| (after.Effects?.Count ?? 0) < (before.Effects?.Count ?? 0) )
			return new EditResult( source, "Take things off one at a time" );

		var changes = new List<Splice>();
		foreach ( var field in ScriptCodegen.DesignFields() )
		{
			var owner = ScriptProperties.Of( field ).Owner == TypeKind.Weapon ? "Weapon" : "Skin";
			if ( FieldChange( source, build, before, after, field, 0, ScriptBuild.WeaponOwner, owner, shapeName, changes, out var line ) is { } problem )
				return new EditResult( source, problem, line );
		}

		if ( Items( source, build, before.Stickers, after.Stickers, ScriptCodegen.StickerFields, ScriptBuild.StickerOwner, build.StickerSite, before, after, shapeName, changes, out var stickerLine ) is { } stickers )
			return new EditResult( source, stickers, stickerLine );

		if ( Items( source, build, before.Props, after.Props, ScriptCodegen.PropFields, ScriptBuild.PropOwner, build.PropSite, before, after, shapeName, changes, out var propLine ) is { } props )
			return new EditResult( source, props, propLine );

		if ( Items( source, build, before.Effects, after.Effects, ScriptCodegen.EffectFields, ScriptBuild.EffectOwnerBase, build.EffectSite, before, after, shapeName, changes, out var effectLine ) is { } effects )
			return new EditResult( source, effects, effectLine );

		var edited = Apply( source, changes );
		var taken = TakenNames( edited );
		var added = new System.Text.StringBuilder();
		Added( added, after, PropertyId.StickerPicture, before.Stickers?.Count ?? 0, after.Stickers?.Count ?? 0, taken, shapeName );
		Added( added, after, PropertyId.PropModel, before.Props?.Count ?? 0, after.Props?.Count ?? 0, taken, shapeName );
		foreach ( var part in hiddenAfter )
		{
			if ( !hiddenBefore.Contains( part ) )
				added.Append( "Weapon.HidePart( " ).Append( ScriptLiteral.Text( part ) ).Append( " )\n" );
		}

		Added( added, after, PropertyId.EffectName, before.Effects?.Count ?? 0, after.Effects?.Count ?? 0, taken, shapeName );
		if ( added.Length == 0 )
			return new EditResult( edited );

		var head = edited.TrimEnd( '\n', ' ', '\t', '\r' );
		return new EditResult( (head.Length > 0 ? head + "\n\n" : "") + added );
	}

	// The fields of each item both have changed in place, or why one can't be
	private static string Items<T>( string source, ScriptBuild build, List<T> before, List<T> after, PropertyId[] fields, int ownerBase, Func<int, ScriptSite> site,
		WeaponDesign was, WeaponDesign now, Func<string, string> shapeName, List<Splice> changes, out int line )
	{
		line = 0;
		for ( var i = 0; i < Math.Min( before?.Count ?? 0, after?.Count ?? 0 ); i++ )
		{
			var name = site( i )?.LocalName;
			foreach ( var field in fields )
			{
				if ( FieldChange( source, build, was, now, field, i, ownerBase + i, name, shapeName, changes, out line ) is { } problem )
					return problem;
			}
		}

		return null;
	}

	private static string FieldChange( string source, ScriptBuild build, WeaponDesign before, WeaponDesign after, PropertyId field, int index, int owner, string name,
		Func<string, string> shapeName, List<Splice> changes, out int line )
	{
		line = 0;
		var wanted = ScriptLiteral.Of( field, after, index, shapeName );
		if ( wanted == ScriptLiteral.Of( field, before, index, shapeName ) )
			return null;

		var from = build.SourceOf( field, owner );
		if ( wanted is null )
		{
			// Cleared (a tint taken off): the statement setting it goes
			if ( from.Origin == FieldOrigin.Literal )
			{
				changes.Add( new Splice( LineStart( source, from.Site.StatementStart ), LineEnd( source, from.Site.StatementEnd ), "", changes.Count ) );
				return null;
			}

			line = from.Line;
			return "Clearing that is done in code";
		}

		var insertAt = name is null ? -1 : WeaponInsertionPoint( build, owner );
		return Change( source, build, from, wanted, $"{name}.{ScriptProperties.Of( field ).Name}", insertAt, changes, out line );
	}

	// Where a new statement about owner goes: after the last top-level statement about it (making it, or setting its fields)
	private static int WeaponInsertionPoint( ScriptBuild build, int owner )
	{
		var made = owner >= ScriptBuild.EffectOwnerBase ? build.EffectSite( owner - ScriptBuild.EffectOwnerBase )
			: owner >= ScriptBuild.PropOwner ? build.PropSite( owner - ScriptBuild.PropOwner )
			: owner >= ScriptBuild.StickerOwner ? build.StickerSite( owner - ScriptBuild.StickerOwner ) : null;
		var at = made is { IsTopLevel: true } ? made.StatementEnd : -1;
		foreach ( var ((target, _), site) in build.Sites )
		{
			var statement = build.Program.Sites[ site ];
			if ( target == owner && statement.IsTopLevel )
				at = Math.Max( at, statement.StatementEnd );
		}

		return owner == ScriptBuild.WeaponOwner && at < 0 ? 0 : at;
	}

	// The statement making an item and those about it taken out; refused when the script uses its name anywhere else
	private static EditResult RemoveItem( string source, ScriptBuild build, ScriptSite made, int owner )
	{
		if ( made is not { IsTopLevel: true } )
			return new EditResult( source, "This isn't made by a plain statement at the top of the script: take it off in code" );

		var statements = new List<ScriptSite> { made };
		foreach ( var ((target, _), site) in build.Sites )
		{
			if ( target == owner )
				statements.Add( build.Program.Sites[ site ] );
		}

		if ( made.LocalName is { } local )
		{
			var tree = Parser.Parse( source );
			foreach ( var token in tree.Tokens )
			{
				if ( token.Kind != TokenKind.Name || source.Substring( token.Start, token.Length ) != local )
					continue;

				if ( !statements.Exists( x => x.IsTopLevel && token.Start >= x.StatementStart && token.Start < x.StatementEnd ) )
					return new EditResult( source, $"'{local}' is used by the script here: take it off in code", tree.Lines.Position( token.Start ).Line );
			}
		}

		var changes = new List<Splice>();
		foreach ( var statement in statements )
			changes.Add( new Splice( LineStart( source, statement.StatementStart ), LineEnd( source, statement.StatementEnd ), "", 0 ) );

		return new EditResult( Apply( source, changes ) );
	}

	// The index of the one item of before missing from after, the rest the same in order; null when there isn't one
	private static int? Removed<T>( List<T> before, List<T> after, Func<T, string> data )
	{
		var (was, now) = (before?.Count ?? 0, after?.Count ?? 0);
		if ( now != was - 1 )
			return null;

		for ( var k = 0; k < was; k++ )
		{
			var same = true;
			for ( var i = 0; i < now && same; i++ )
				same = data( before[ i < k ? i : i + 1 ] ) == data( after[ i ] );

			if ( same )
				return k;
		}

		return null;
	}

	private static string Data( object item ) => System.Text.Json.JsonSerializer.Serialize( item, item.GetType() );

	// Each item added at the end of after written out as its own block, with a name not taken
	private static void Added( System.Text.StringBuilder text, WeaponDesign after, PropertyId kind, int from, int to, HashSet<string> taken, Func<string, string> shapeName )
	{
		for ( var i = from; i < to; i++ )
		{
			var block = ScriptCodegen.WeaponItem( after, kind, i, "item", shapeName );
			var made = block.IndexOf( "local item = ", StringComparison.Ordinal );
			var name = made < 0 ? null : Unique( taken, Suggested( block ) );
			text.Append( name is null ? block : block.Replace( "local item = ", $"local {name} = " ).Replace( "\nitem.", $"\n{name}." ) ).Append( '\n' );
		}
	}

	// The name codegen gives an item of its own: a sticker by its picture, a prop by its model, an effect by its name
	private static string Suggested( string block )
	{
		var call = block.IndexOf( "( ", StringComparison.Ordinal );
		var end = block.IndexOf( " )", Math.Max( 0, call ), StringComparison.Ordinal );
		if ( call < 0 || end < 0 )
			return "item";

		var inner = block[ (call + 2)..end ].Trim( '"' );
		var slash = inner.LastIndexOfAny( new[] { '/', '.' } );
		var word = inner.Contains( '/' ) ? inner[ (inner.LastIndexOf( '/' ) + 1).. ].Replace( ".vmdl", "" ) : slash >= 0 ? inner[ (slash + 1).. ] : inner;
		var quoted = inner.IndexOf( "( \"", StringComparison.Ordinal );
		if ( quoted >= 0 )
			word = inner[ (quoted + 3).. ].TrimEnd( '"' );

		return ScriptCodegen.Identifier( word, "item" );
	}

	private static HashSet<string> TakenNames( string source )
	{
		var taken = new HashSet<string> { "Weapon", "Skin" };
		var tree = Parser.Parse( source );
		foreach ( var token in tree.Tokens )
		{
			if ( token.Kind == TokenKind.Name )
				taken.Add( source.Substring( token.Start, token.Length ) );
		}

		return taken;
	}

	private static string Unique( HashSet<string> taken, string name )
	{
		var unique = name;
		for ( var i = 2; !taken.Add( unique ); i++ )
			unique = name + i;

		return unique;
	}
}
