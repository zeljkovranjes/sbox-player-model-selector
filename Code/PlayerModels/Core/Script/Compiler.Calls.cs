using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

public sealed partial class Compiler
{
	private ScriptType Call( CallExpr call, bool allowVoid )
	{
		var type = CallOf( call );
		if ( type.Kind == TypeKind.Void && !allowVoid )
		{
			Error( "This gives back nothing, so it can't be used as a value", call );
			return ScriptType.Error;
		}

		return type;
	}

	private ScriptType CallOf( CallExpr call )
	{
		switch ( call.Callee )
		{
			case NameExpr name:
			{
				if ( Lookup( name.Name ) is { } symbol )
				{
					if ( symbol.Kind == SymbolKind.Function )
					{
						if ( symbol.Type is null )
						{
							Error( $"'{name.Name}' calls itself, so give it a return type: function {name.Name}( ... ): number", name );
							return ScriptType.Error;
						}

						return UserCall( symbol.Type, call, symbol.Function, name.Name );
					}

					if ( symbol.Type.Kind == TypeKind.Function )
					{
						Load( symbol );
						return UserCall( symbol.Type, call, -1, name.Name );
					}

					Error( $"'{name.Name}' is {Article( symbol.Type )}, not a function", name );
					return ScriptType.Error;
				}

				if ( ScriptLibrary.Global( name.Name ) is { Kind: EntryKind.Function } function )
					return LibraryCall( function, call, null );

				if ( ScriptLibrary.Event( name.Name ) is not null )
					Error( $"{name.Name} is called by the game, not by the script: put shared code in a function of your own", name );
				else
					Error( $"There is no function '{name.Name}'{Suggest( name.Name, Visible() )}", name );

				return ScriptType.Error;
			}

			case MemberExpr member:
			{
				if ( NamespaceOf( member.Target ) is { } space )
				{
					var entry = ScriptLibrary.InNamespace( space, member.Name );
					if ( entry?.Kind == EntryKind.Function )
						return LibraryCall( entry, call, null );

					Error( entry is null
						? $"'{space}.{member.Name}' doesn't exist{Suggest( member.Name, MemberNames( ScriptLibrary.NamespaceMembers( space ) ) )}"
						: $"{space}.{member.Name} isn't a function", member );

					return ScriptType.Error;
				}

				var owner = Value( member.Target );
				if ( owner.Kind == TypeKind.Error )
					return ScriptType.Error;

				var method = ScriptLibrary.Member( owner.Kind, member.Name );
				if ( method?.Kind == EntryKind.Method )
					return LibraryCall( method, call, owner );

				if ( method is { Kind: EntryKind.Property } && method.Type.Kind == TypeKind.Function )
					Error( $"{method.Name} is run by the game for each sprite, not called by the script", member );
				else if ( method is not null )
					Error( $"{method.Name} isn't a function", member );
				else
					UnknownMember( owner, member );

				return ScriptType.Error;
			}

			default:
			{
				var type = Value( call.Callee );
				if ( type.Kind == TypeKind.Function )
					return UserCall( type, call, -1, "the function" );

				if ( type.Kind != TypeKind.Error )
					Error( $"{Article( type )} can't be called", call.Callee );

				return ScriptType.Error;
			}
		}
	}

	// A call to one of the script's own functions: directly, or a function value already pushed (direct -1)
	private ScriptType UserCall( ScriptType function, CallExpr call, int direct, string name )
	{
		var parameters = function.Parameters;
		foreach ( var parameter in parameters )
		{
			if ( parameter.Kind is TypeKind.Particle or TypeKind.Shot )
			{
				Error( $"'{name}' is run by the game, not called by the script", call );
				return ScriptType.Error;
			}
		}

		if ( call.Arguments.Count != parameters.Count )
		{
			Error( $"'{name}' takes {Count( parameters.Count, "argument" )}, but {call.Arguments.Count} {(call.Arguments.Count == 1 ? "was" : "were")} given", call );
			return function.Return;
		}

		var width = 0;
		for ( var i = 0; i < parameters.Count; i++ )
		{
			var type = Value( call.Arguments[ i ], parameters[ i ] );
			if ( !type.FitsInto( parameters[ i ] ) )
				Error( $"Argument {i + 1} of '{name}' should be {Article( parameters[ i ] )}, not {Article( type )}", call.Arguments[ i ] );

			width += parameters[ i ].Width;
		}

		_hasCalls = true;
		if ( direct >= 0 )
			Emit( OpCode.Call, direct, width );
		else
		{
			Emit( OpCode.CallValue, width );
			Pop( 1 );
		}

		Pop( width );
		Push( function.Return.Width );
		return function.Return;
	}

	private static string Count( int count, string what ) => count == 1 ? $"1 {what}" : $"{count} {what}s";

	// A call to a library function or method (its receiver already pushed)
	private ScriptType LibraryCall( LibraryEntry entry, CallExpr call, ScriptType receiver )
	{
		if ( !Allowed( entry, call.Callee ) )
			return entry.Type;

		if ( entry.Op == Intrinsic.ListCreate )
			return ListCreate( entry, call );

		var parameters = entry.Parameters;
		var given = call.Arguments.Count;
		if ( given < entry.Required || given > parameters.Count )
		{
			var wanted = entry.Required == parameters.Count ? Count( parameters.Count, "argument" ) : $"{entry.Required} to {parameters.Count} arguments";
			Error( $"{entry.Name} takes {wanted}, but {given} {(given == 1 ? "was" : "were")} given: {entry.Signature()}", call );
			return entry.Type;
		}

		var width = receiver?.Width ?? 0;
		for ( var i = 0; i < parameters.Count; i++ )
		{
			var parameter = parameters[ i ];
			if ( i < given )
			{
				var type = Value( call.Arguments[ i ], parameter.Type );
				if ( !type.FitsInto( parameter.Type ) )
					Error( $"Argument {i + 1} ('{parameter.Name}') of {entry.Name} should be {Article( parameter.Type )}, not {Article( type )}", call.Arguments[ i ] );
			}
			else
				PushConstant( parameter.Default );

			width += parameter.Type.Width;
		}

		CheckNamedArgument( entry, call );
		if ( Makes( entry.Op ) )
		{
			PushConstant( new double[] { Site( call, LocalNameOf( call ) ) } );
			width++;
		}

		Emit( OpCode.Intrinsic, (int)entry.Op );
		Pop( width );
		Push( entry.Type.Width );
		return entry.Type;
	}

	// List.Create( count, value ): a list of whatever the value is, so its type comes from the call
	private ScriptType ListCreate( LibraryEntry entry, CallExpr call )
	{
		if ( call.Arguments.Count != 2 )
		{
			Error( $"{entry.Name} takes 2 arguments, but {call.Arguments.Count} {(call.Arguments.Count == 1 ? "was" : "were")} given: {entry.Signature()}", call );
			return ScriptType.ListOf( ScriptType.Error );
		}

		var count = Value( call.Arguments[ 0 ], ScriptType.Number );
		if ( count.Kind is not (TypeKind.Number or TypeKind.Error) )
			Error( $"Argument 1 ('count') of {entry.Name} should be a number, not {Article( count )}", call.Arguments[ 0 ] );

		var item = Value( call.Arguments[ 1 ] );
		if ( !item.IsStorable )
		{
			Error( $"A list can't hold {Article( item )}", call.Arguments[ 1 ] );
			item = ScriptType.Error;
		}

		PushConstant( new double[] { item.Width } );
		Emit( OpCode.Intrinsic, (int)entry.Op );
		Pop( 2 + item.Width );
		Push( 2 );
		return ScriptType.ListOf( item );
	}

	// The intrinsics that make a layer, a shape or an emblem layer: the site making it goes with them
	private static bool Makes( Intrinsic op ) => op is Intrinsic.Sprite or Intrinsic.Light or Intrinsic.Trail or Intrinsic.Beam or Intrinsic.Ghost
		or Intrinsic.WeaponAddSticker or Intrinsic.WeaponAddProp or Intrinsic.WeaponHidePart or Intrinsic.WeaponAddEffect
		or Intrinsic.ShapeNew
		or Intrinsic.ShapeText or Intrinsic.ShapesMine or Intrinsic.StickersMine or Intrinsic.EmblemsMine or Intrinsic.ShapeAddLayer;

	// The local a top-level `local name = <call>` keeps what the call makes in
	private string LocalNameOf( CallExpr call ) =>
		_statement is LocalStmt local && local.Value == call && F.IsMain && F.BlockDepth == 0 ? local.Name : null;

	// What a script takes by name (a preset, the player's own shapes and effects) must exist when it compiles
	private void CheckNamedArgument( LibraryEntry entry, CallExpr call )
	{
		var kind = entry.Op switch
		{
			Intrinsic.ShapesMine => LibraryItemKind.Shape,
			Intrinsic.StickersMine => LibraryItemKind.Sticker,
			Intrinsic.EmblemsMine => LibraryItemKind.Emblem,
			Intrinsic.EffectsMine => LibraryItemKind.Effect,
			_ => (LibraryItemKind)(-1),
		};

		if ( entry.Op != Intrinsic.EffectsPreset && kind < 0 || call.Arguments.Count != 1 )
			return;

		if ( call.Arguments[ 0 ] is not StringExpr name )
		{
			Error( $"Name it in quotes, like {entry.Name}( \"name\" ): what a script uses must exist when it compiles", call.Arguments[ 0 ] );
			return;
		}

		var names = new List<string>();
		if ( entry.Op == Intrinsic.EffectsPreset )
		{
			foreach ( var preset in EffectPresets.All )
				names.Add( preset.Name );
		}
		else
		{
			foreach ( var item in _library?.Items ?? Array.Empty<LibraryItem>() )
			{
				if ( item.Kind == kind )
					names.Add( item.Name );
			}
		}

		if ( names.Contains( name.Value ) )
			return;

		if ( entry.Op == Intrinsic.EffectsPreset )
		{
			Error( $"There is no preset called \"{name.Value}\"{Suggest( name.Value, names )}", name );
			return;
		}

		var what = kind.ToString().ToLowerInvariant();
		var where = kind == LibraryItemKind.Effect ? "My effects" : "My shapes";
		var close = Suggestions.Closest( name.Value, names ) is { } guess ? $" — did you mean '{guess}'?" : ".";
		var make = kind == LibraryItemKind.Effect ? "Save it from the Effects tab" : "Make it in the shape creator";
		Error( $"No {what} called '{name.Value}' in {where}{close} {make} or get it from the Workshop", name );
	}

	/// <summary>
	/// Whether <paramref name="entry"/> can be read (or written) where the compiler is: a layer is made only at the top
	/// of an effect, an input read only while it runs, and so on. Says why not when it can't.
	/// </summary>
	private bool Allowed( LibraryEntry entry, Node at, bool write = false )
	{
		var where = write ? entry.WriteWhere : entry.Where;
		var here = F.Place;
		if ( (where & here) != 0 )
			return true;

		var name = entry.Name;
		string message;
		if ( write && entry.Owner == "Layer" && (entry.Where & here) != 0 )
			message = $"{name} can only be set while the effect is built (at the top of the script); while it runs, change things like Color, Size, Amount or Off";
		else if ( (where & ScriptPlace.EffectBuild) != 0 && here == ScriptPlace.EffectRun )
			message = $"'{name}' can only be used while the effect is built (at the top of the script), not while it runs";
		else if ( where == ScriptPlace.StickerRun )
			message = $"'{name}' is for sticker scripts" + (entry.Name == "i" ? ": in a particle function use p.Index" : "");
		else if ( where == ScriptPlace.Element )
			message = $"'{name}' is for sticker and weapon skin scripts" + (entry.Name == "i" ? ": in a particle function use p.Index" : "");
		else if ( where == ScriptPlace.SkinRun )
			message = $"'{name}' is for weapon skin scripts";
		else if ( where == ScriptPlace.WeaponBuild )
			message = $"'{name}' is for weapon design scripts";
		else if ( _kind == ScriptKind.Weapon && (where & ScriptPlace.EffectBuild) != 0 )
			message = $"'{name}' makes an effect: in a weapon design, put one on with Weapon.AddEffect( Effects.Preset( \"...\" ) )";
		else if ( (where & ScriptPlace.Run) != 0 && (here & ScriptPlace.Build) != 0 && _kind == ScriptKind.Effect )
			message = $"'{name}' changes while the effect runs: use it inside a function, like function OnUpdate( dt ) ... end";
		else if ( (where & ScriptPlace.Effect) == where )
			message = $"'{name}' is for effect scripts";
		else if ( where == ScriptPlace.ShapeBuild )
			message = $"'{name}' is the shape a shape script makes: elsewhere use Shapes.New( \"name\" )";
		else if ( (where & ScriptPlace.Build) != 0 && (here & ScriptPlace.Build) == 0 )
			message = entry.Category is "Shape" or "Emblem"
				? $"'{name}' makes shapes, so it can only be used where a shape or effect is built"
				: $"'{name}' can only be used where a shape or effect is built (at the top of its script)";
		else
			message = $"'{name}' can't be used here";

		Error( message, at );
		return false;
	}
}
