using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// How many new scripts each player's looks may have compiled on this machine: a player swapping looks as fast as the
/// host lets them still costs at most <see cref="PerSecond"/> compiles a second here (after a first
/// <see cref="Burst"/>, for a look full of new effects). A look whose scripts must wait shows what they built until its
/// turn comes; only the latest look matters by then. Scripts already compiled (see <see cref="ScriptCache"/>) cost nothing.
/// </summary>
public sealed class CompileBudget
{
	/// <summary>Compiles a second each player gets, and how many they can have at once.</summary>
	public const double PerSecond = 2;
	public const double Burst = 8;

	private readonly Dictionary<Guid, (double Left, double At)> _players = new();

	/// <summary>
	/// Takes one compile for <paramref name="player"/> at <paramref name="now"/> (seconds): false, taking nothing, when
	/// they have none left.
	/// </summary>
	public bool TryTake( Guid player, double now )
	{
		var (left, at) = _players.TryGetValue( player, out var held ) ? held : (Burst, now);
		left = Math.Min( Burst, left + Math.Max( 0, now - at ) * PerSecond );
		if ( left < 1 )
		{
			_players[ player ] = (left, now);
			return false;
		}

		_players[ player ] = (left - 1, now);
		return true;
	}

	/// <summary>Forgets <paramref name="player"/> (they left).</summary>
	public void Forget( Guid player ) => _players.Remove( player );
}
