using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>What compiling a script gave: its program, or the errors (line:col, plain words) that stopped it.</summary>
public sealed class CompileResult
{
	public ScriptProgram Program { get; init; }
	public IReadOnlyList<ScriptError> Errors { get; init; } = System.Array.Empty<ScriptError>();
	public SyntaxTree Tree { get; init; }

	/// <summary>The names the script declares, with their types, even when it has errors (for completion and hover).</summary>
	public IReadOnlyList<ScriptSymbol> Symbols { get; init; } = System.Array.Empty<ScriptSymbol>();

	/// <summary>The script's size as it is stored (minified), in UTF-8 bytes; 0 when it didn't parse.</summary>
	public int StoredBytes { get; init; }

	public bool Succeeded => Program is not null;
}

/// <summary>
/// The way in for every script, from the editor, a look, or the Workshop: refuses what is too long before reading a
/// character of it, parses, holds the stored (minified) size to <see cref="ScriptLimits.MaxScriptBytes"/>, then type
/// checks and compiles. Never throws on anything a player can type or send.
/// </summary>
public static class ScriptCompiler
{
	/// <summary>
	/// Compiles <paramref name="source"/> as a script of <paramref name="kind"/>; the names it takes from the player's
	/// own library (<c>Shapes.Mine</c>, <c>Effects.Mine</c>) must be in <paramref name="library"/>.
	/// </summary>
	public static CompileResult Compile( string source, ScriptKind kind = ScriptKind.Effect, IPlayerLibrary library = null )
	{
		if ( source is null )
			return Refused( "There is no script" );

		if ( source.Length > ScriptLimits.MaxSourceLength )
			return Refused( "Script is too long: " + ScriptLimits.Describe( source.Length, ScriptLimits.MaxScriptBytes ) );

		var tree = Parser.Parse( source );
		if ( tree.Error is not null )
			return new CompileResult { Tree = tree, Errors = new[] { tree.Error } };

		var stored = Minifier.StoredBytes( tree );
		if ( stored > ScriptLimits.MaxScriptBytes )
			return new CompileResult { Tree = tree, StoredBytes = stored, Errors = new[] { tree.Lines.Error( "Script is too long: " + ScriptLimits.Describe( stored, ScriptLimits.MaxScriptBytes ), 0, 1 ) } };

		var program = Compiler.Compile( tree, kind, library, out var errors, out var symbols );
		return new CompileResult { Program = program, Tree = tree, StoredBytes = stored, Errors = errors, Symbols = symbols };
	}

	private static CompileResult Refused( string message ) => new() { Errors = new[] { new ScriptError( message, 0, 1, 1, 1 ) } };
}
