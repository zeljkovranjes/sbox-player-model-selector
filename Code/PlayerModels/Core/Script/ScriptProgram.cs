using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>A compiled function: where its code starts, its parameters' and frame's size, and how deep its values stack.</summary>
internal readonly record struct FunctionInfo( int Start, int ParamWidth, int FrameSize, int MaxEval, string Name );

/// <summary>
/// A statement that sets a property while a script is built: where the statement and its value are in the source,
/// whether the value is a literal the visual editor can rewrite in place, whether it is at the script's top (not in a
/// loop, an if or a function), for one making a layer or shape the local it is kept in, and for a value that is a
/// [Parameter], that parameter's name.
/// </summary>
public sealed record ScriptSite( int StatementStart, int StatementEnd, int ValueStart, int ValueEnd, bool IsLiteral, bool IsTopLevel, string LocalName,
	string Parameter = null );

/// <summary>A name a script declares, its type and where it can be seen, for completion and hover.</summary>
public sealed record ScriptSymbol( string Name, ScriptType Type, string Kind, int DeclaredAt, int ScopeStart, int ScopeEnd );

/// <summary>A key written in a script (<c>"g"</c> where a key is wanted): where, and which key.</summary>
public readonly record struct KeyLiteral( int Start, int Length, string Key );

/// <summary>
/// A script compiled once (when a look arrives, cached by its text) into compact bytecode, immutable and shared by every
/// machine running it. Holds what the machine needs, and for the editor the source, its symbols and keys.
/// </summary>
public sealed class ScriptProgram
{
	public ScriptKind Kind { get; init; }
	public string Source { get; init; }
	public LineMap Lines { get; init; }

	internal int[] Code { get; init; }
	internal int[] Positions { get; init; }
	internal double[] Constants { get; init; }
	internal string[] Strings { get; init; }
	internal FunctionInfo[] Functions { get; init; }
	internal int GlobalSlots { get; init; }
	internal double[] Arena { get; init; }
	internal int[] EventFunctions { get; init; }
	internal bool HasCalls { get; init; }

	/// <summary>The property-setting statements, by the site number each SetProp carries.</summary>
	public IReadOnlyList<ScriptSite> Sites { get; init; }

	/// <summary>Properties of the script's layers set by its event or particle functions: (global slot of the layer, property, site).</summary>
	internal IReadOnlyList<(int Global, PropertyId Property, int Site)> RunningSets { get; init; }

	/// <summary>The options it exposes with [Parameter], in the order they are declared.</summary>
	public IReadOnlyList<ScriptParameter> Parameters { get; init; } = Array.Empty<ScriptParameter>();

	/// <summary>The names it declares (for completion and hover).</summary>
	public IReadOnlyList<ScriptSymbol> Symbols { get; init; }

	/// <summary>The key literals in it (for the key icons).</summary>
	public IReadOnlyList<KeyLiteral> Keys { get; init; }

	/// <summary>Its script variables by name, with their slot and type (the last of a name wins).</summary>
	internal IReadOnlyDictionary<string, (int Slot, ScriptType Type)> GlobalNames { get; init; }

	/// <summary>Whether it handles <paramref name="scriptEvent"/>.</summary>
	public bool Handles( ScriptEvent scriptEvent ) => EventFunctions[ (int)scriptEvent ] >= 0;

	/// <summary>How many stack slots one run needs at most, for a <see cref="ScriptStack"/>.</summary>
	public int StackSlots
	{
		get
		{
			var most = 0;
			foreach ( var function in Functions )
				most = Math.Max( most, function.FrameSize + function.MaxEval );

			return HasCalls ? most * (ScriptLimits.MaxCallDepth + 1) : most;
		}
	}
}
