using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>What an edit made: the new source, or why it couldn't be made (and the line the reason is on).</summary>
public sealed record EditResult( string Source, string Problem = null, int Line = 0 )
{
	public bool Succeeded => Problem is null;
}

/// <summary>
/// The visual editor's changes made to a script in place, so the player's own code, functions and comments are never
/// lost: a field set by a literal at the top has that literal rewritten (a colour in the form it was written in); a
/// field the script doesn't set yet gets one statement added after its layer's others; a field worked out by code, or
/// given by a [Parameter], is left alone and reported with its line ("set by script"). Each edit is from one build of
/// the source, and building the edited source gives the edited effect.
/// </summary>
public static class ScriptEdit
{
	private readonly record struct Splice( int Start, int End, string Text, int Order );

	/// <summary>Sets layer <paramref name="layer"/>'s <paramref name="property"/> to what <paramref name="value"/> has.</summary>
	public static EditResult SetLayer( string source, ScriptBuild build, int layer, PropertyId property, EffectLayer value, Func<string, string> shapeName = null )
	{
		var changes = new List<Splice>();
		var problem = LayerChange( source, build, layer, property, ScriptLiteral.Of( property, value, shapeName ), changes, out var line );
		return problem is null ? new EditResult( Apply( source, changes ) ) : new EditResult( source, problem, line );
	}

	/// <summary>Sets the effect's own <paramref name="property"/> (name, key, anchor, sliders) to what <paramref name="value"/> has.</summary>
	public static EditResult SetEffect( string source, ScriptBuild build, PropertyId property, Effect value )
	{
		var changes = new List<Splice>();
		var problem = EffectChange( source, build, property, ScriptLiteral.Of( property, value ), changes, out var line );
		return problem is null ? new EditResult( Apply( source, changes ) ) : new EditResult( source, problem, line );
	}

	/// <summary>
	/// Makes <paramref name="source"/> (built as <paramref name="build"/> into <paramref name="before"/>) build
	/// <paramref name="after"/>: every changed field edited in place, layers added at the end written out. Layers are
	/// removed with <see cref="RemoveLayer"/> first. Nothing is changed when any edit can't be made.
	/// </summary>
	public static EditResult Apply( string source, ScriptBuild build, Effect before, Effect after, Func<string, string> shapeName = null )
	{
		if ( after.Layers.Count < before.Layers.Count )
			return new EditResult( source, "Remove layers one at a time with RemoveLayer" );

		var changes = new List<Splice>();
		for ( var property = PropertyId.EffectName; property <= PropertyId.EffectSpeed; property++ )
		{
			var wanted = ScriptLiteral.Of( property, after );
			if ( wanted != ScriptLiteral.Of( property, before ) && EffectChange( source, build, property, wanted, changes, out var line ) is { } problem )
				return new EditResult( source, problem, line );
		}

		for ( var layer = 0; layer < before.Layers.Count; layer++ )
		{
			var (was, now) = (before.Layers[ layer ], after.Layers[ layer ]);
			if ( was.KindIs != now.KindIs )
				return new EditResult( source, "A layer's kind can't change: remove it and add a new one" );

			foreach ( var definition in ScriptProperties.All )
			{
				if ( definition.Owner != TypeKind.Layer || definition.ReadOnly || definition.Id == PropertyId.LayerParticle )
					continue;

				var wanted = ScriptLiteral.Of( definition.Id, now, shapeName );
				if ( wanted != ScriptLiteral.Of( definition.Id, was, shapeName )
					&& LayerChange( source, build, layer, definition.Id, wanted, changes, out var line ) is { } problem )
					return new EditResult( source, problem, line );
			}
		}

		var edited = Apply( source, changes );
		for ( var layer = before.Layers.Count; layer < after.Layers.Count; layer++ )
			edited = AddLayer( edited, after.Layers[ layer ], shapeName ).Source;

		return new EditResult( edited );
	}

	/// <summary>Adds <paramref name="layer"/>, written out as a block after the script's other top-level statements (before its functions).</summary>
	public static EditResult AddLayer( string source, EffectLayer layer, Func<string, string> shapeName = null )
	{
		var tree = Parser.Parse( source );
		if ( tree.Error is not null )
			return new EditResult( source, "Fix the script's errors first", tree.Error.Line );

		var taken = new HashSet<string>();
		foreach ( var token in tree.Tokens )
		{
			if ( token.Kind == TokenKind.Name )
				taken.Add( source.Substring( token.Start, token.Length ) );
		}

		var name = ScriptCodegen.Identifier( layer.Picture ?? layer.Kind, "layer" );
		var unique = name;
		for ( var i = 2; taken.Contains( unique ); i++ )
			unique = name + i;

		var block = ScriptCodegen.Layer( unique, layer, shapeName );
		var at = source.Length;
		foreach ( var statement in tree.Root.Statements )
		{
			if ( statement is FunctionStmt { Target: NameExpr } )
			{
				at = LineStart( source, statement.Start );
				return new EditResult( source[ ..at ] + block + "\n" + source[ at.. ] );
			}
		}

		var head = source.TrimEnd( '\n', ' ', '\t', '\r' );
		return new EditResult( (head.Length > 0 ? head + "\n\n" : "") + block );
	}

	/// <summary>
	/// Removes layer <paramref name="layer"/>: the statement making it and those setting its fields. Refused when the
	/// script uses the layer anywhere else (a function, a loop), with that line.
	/// </summary>
	public static EditResult RemoveLayer( string source, ScriptBuild build, int layer )
	{
		var made = build.LayerSite( layer );
		if ( made is not { IsTopLevel: true, LocalName: not null } )
			return new EditResult( source, "This layer isn't made by a plain local name = Sprite() at the top of the script: remove it in code" );

		var statements = new List<ScriptSite> { made };
		foreach ( var ((owner, _), site) in build.Sites )
		{
			if ( owner == layer )
				statements.Add( build.Program.Sites[ site ] );
		}

		var tree = Parser.Parse( source );
		foreach ( var token in tree.Tokens )
		{
			if ( token.Kind != TokenKind.Name || source.Substring( token.Start, token.Length ) != made.LocalName )
				continue;

			if ( !statements.Exists( x => x.IsTopLevel && token.Start >= x.StatementStart && token.Start < x.StatementEnd ) )
				return new EditResult( source, $"'{made.LocalName}' is used by the script here: remove it in code", tree.Lines.Position( token.Start ).Line );
		}

		var changes = new List<Splice>();
		foreach ( var statement in statements )
			changes.Add( new Splice( LineStart( source, statement.StatementStart ), LineEnd( source, statement.StatementEnd ), "", 0 ) );

		return new EditResult( Apply( source, changes ) );
	}

	/// <summary>
	/// Writes <paramref name="value"/> (a chosen value's text, see <see cref="ScriptParameter.TryRead"/>) into the
	/// literal of parameter <paramref name="name"/>: "Save as default". A colour keeps the form it was written in.
	/// </summary>
	public static EditResult SaveAsDefault( string source, ScriptProgram program, string name, string value )
	{
		ScriptParameter parameter = null;
		foreach ( var candidate in program.Parameters )
		{
			if ( candidate.Name == name )
				parameter = candidate;
		}

		if ( parameter is null )
			return new EditResult( source, $"There is no parameter called '{name}'" );

		var strings = new List<string>( program.Strings );
		if ( !parameter.TryRead( value, strings, out var slots ) )
			return new EditResult( source, $"\"{value}\" isn't a value for '{parameter.Title}'", parameter.Line );

		var old = source.Substring( parameter.ValueStart, parameter.ValueEnd - parameter.ValueStart );
		var literal = parameter.Kind switch
		{
			ParameterKind.Number or ParameterKind.Integer => ScriptLiteral.Number( (float)slots[ 0 ] ),
			ParameterKind.Bool => ScriptLiteral.Bool( slots[ 0 ] != 0 ),
			ParameterKind.Color => ColorLiterals.At( old, 0 ) is { } colour
				? ColorLiterals.Write( old, colour.Form, slots[ 0 ], slots[ 1 ], slots[ 2 ], slots[ 3 ] )
				: ScriptLiteral.Colour( ScriptValue.ColorText( slots[ 0 ], slots[ 1 ], slots[ 2 ], slots[ 3 ] ) ),
			ParameterKind.Palette => ScriptLiteral.Palette( parameter.Text( slots, strings ).Split( ',' ) ),
			ParameterKind.Vector3 => ScriptLiteral.Vector( (float)slots[ 0 ], (float)slots[ 1 ], (float)slots[ 2 ] ),
			ParameterKind.Shape => "Shapes." + parameter.Text( slots, strings ),
			ParameterKind.Picture => (slots[ 0 ] >= Handles.PictureBase ? "Picture." : "Shapes.") + parameter.Text( slots, strings ),
			ParameterKind.Choice when parameter.Type.Kind == TypeKind.Enum => parameter.Type.Enum.Name + "." + parameter.Text( slots, strings ),
			_ => ScriptLiteral.Text( parameter.Text( slots, strings ) ),
		};

		return new EditResult( source[ ..parameter.ValueStart ] + literal + source[ parameter.ValueEnd.. ] );
	}

	private static string LayerChange( string source, ScriptBuild build, int layer, PropertyId property, string literal, List<Splice> changes, out int line )
	{
		line = 0;
		if ( literal is null )
			return "That value can't be written in this script (it isn't set, or uses a shape the script doesn't make)";

		var from = build.SourceOf( layer, property );
		var name = build.LayerSite( layer )?.LocalName;
		return Change( source, build, from, literal, $"{name}.{ScriptProperties.Of( property ).Name}", name is null ? -1 : InsertionPoint( build, layer ), changes, out line );
	}

	private static string EffectChange( string source, ScriptBuild build, PropertyId property, string literal, List<Splice> changes, out int line )
	{
		line = 0;
		if ( literal is null )
			return "Clearing that is done in code";

		var from = build.SourceOf( property );
		return Change( source, build, from, literal, "Effect." + ScriptProperties.Of( property ).Name, InsertionPoint( build, ScriptBuild.EffectOwner ), changes, out line );
	}

	private static string Change( string source, ScriptBuild build, FieldSource from, string literal, string target, int insertAt, List<Splice> changes, out int line )
	{
		line = from.Line;
		switch ( from.Origin )
		{
			case FieldOrigin.Literal:
			{
				var site = from.Site;
				var old = source.Substring( site.ValueStart, site.ValueEnd - site.ValueStart );
				if ( ColorLiterals.At( old, 0 ) is { Start: 0 } colour && colour.Length == old.Length && literal.StartsWith( "Color.Parse" ) )
				{
					ScriptMath.TryParseColor( literal[ 14..^3 ], out var r, out var g, out var b, out var a );
					literal = ColorLiterals.Write( old, colour.Form, r, g, b, a );
				}

				changes.Add( new Splice( site.ValueStart, site.ValueEnd, literal, changes.Count ) );
				return null;
			}

			case FieldOrigin.Default when insertAt >= 0:
				changes.Add( new Splice( insertAt, insertAt, $"\n{target} = {literal}", changes.Count ) );
				return null;
			case FieldOrigin.Default:
				return "This layer isn't made by a plain local name = Sprite() at the top of the script: set it in code";
			case FieldOrigin.Parameter:
				return $"Set by the parameter '{from.Site.Parameter}' (line {from.Line}): change the parameter";
			default:
				return $"Set by script (line {from.Line})";
		}
	}

	// Where a new statement for a layer (or the effect) goes: after the last top-level statement about it
	private static int InsertionPoint( ScriptBuild build, int owner )
	{
		var at = -1;
		if ( owner >= 0 && build.LayerSite( owner ) is { IsTopLevel: true } made )
			at = made.StatementEnd;

		foreach ( var ((target, _), site) in build.Sites )
		{
			var statement = build.Program.Sites[ site ];
			if ( target == owner && statement.IsTopLevel )
				at = Math.Max( at, statement.StatementEnd );
		}

		return owner == ScriptBuild.EffectOwner && at < 0 ? 0 : at;
	}

	private static int LineStart( string source, int at ) => at <= 0 ? 0 : source.LastIndexOf( '\n', at - 1 ) + 1;

	private static int LineEnd( string source, int at )
	{
		var end = source.IndexOf( '\n', at );
		return end < 0 ? source.Length : end + 1;
	}

	// Applies changes from the end back, so each one's place still holds; ones at the same place keep their order
	private static string Apply( string source, List<Splice> changes )
	{
		changes.Sort( ( a, b ) => a.Start != b.Start ? b.Start.CompareTo( a.Start ) : b.Order.CompareTo( a.Order ) );
		var text = source;
		foreach ( var change in changes )
		{
			var insertion = change.Start == 0 && change.End == 0 && change.Text.StartsWith( '\n' ) ? change.Text[ 1.. ] + "\n" : change.Text;
			text = text[ ..change.Start ] + insertion + text[ change.End.. ];
		}

		return text;
	}
}
