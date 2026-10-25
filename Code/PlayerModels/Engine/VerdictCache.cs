using System.Collections.Generic;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// What earlier checks said about models, kept in a file on this machine: each key to "" (passed) or why it didn't.
/// A check can mean downloading or loading a model, so it is done once, not every time the window opens.
/// </summary>
internal sealed class VerdictCache
{
	private readonly string _file;
	private Dictionary<string, string> _verdicts;

	internal VerdictCache( string file )
	{
		_file = file;
	}

	/// <summary>True passed, false didn't, null never checked.</summary>
	internal bool? Known( string key )
	{
		return Verdicts().TryGetValue( key, out var why ) ? string.IsNullOrEmpty( why ) : null;
	}

	/// <summary>Keeps a check's result: <paramref name="whyNot"/> is null when it passed.</summary>
	internal void Remember( string key, string whyNot )
	{
		Verdicts()[ key ] = whyNot ?? "";
		FileSystem.Data.WriteJson( _file, _verdicts );
	}

	private Dictionary<string, string> Verdicts()
	{
		return _verdicts ??= FileSystem.Data.ReadJsonOrDefault( _file, new Dictionary<string, string>() );
	}
}
