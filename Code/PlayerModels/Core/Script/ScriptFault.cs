namespace PlayerModels.Core.Script;

/// <summary>Why a run stopped early. A stopped run changes nothing more; its outputs fall back to the host's own.</summary>
public enum FaultKind
{
	None, Budget, CallDepth, IndexOutOfRange, BuildOnly, NotLive, NoReturn, Build, Internal,
}

/// <summary>
/// The fault that stopped a machine's last run, kept as numbers (nothing is formatted while running); <see cref="Describe"/>
/// turns it into words at line:col for the editor.
/// </summary>
public struct ScriptFault
{
	public FaultKind Kind;
	public int Position;
	public int Index;
	public int Count;

	/// <summary>A message set by the builder (an effect with too many layers, say).</summary>
	public string Message;

	public bool IsFault => Kind != FaultKind.None;

	/// <summary>The fault in plain words, at its line and column in <paramref name="program"/>'s source.</summary>
	public ScriptError Describe( ScriptProgram program )
	{
		if ( Kind == FaultKind.None || program is null )
			return null;

		var text = Kind switch
		{
			FaultKind.Budget => "The script ran too long and was stopped (an endless loop, or too much work for one run)",
			FaultKind.CallDepth => $"Calls went too deep (more than {ScriptLimits.MaxCallDepth}) and the script was stopped",
			FaultKind.IndexOutOfRange => $"Item {Index} doesn't exist: the list has {Count} item{(Count == 1 ? "" : "s")} (0 to {Count - 1})",
			FaultKind.BuildOnly => "This only works while the effect is built (at the top of the script), not while it runs",
			FaultKind.NotLive => "This property can only be set while the effect is built",
			FaultKind.NoReturn => "The function ended without giving back its value",
			FaultKind.Build => Message,
			_ => "The script stopped on an internal error",
		};

		return program.Lines.Error( text, Position, 1 );
	}
}
