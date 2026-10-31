using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.Components;

// Host: the picks players send. Each is taken at most every PickInterval, and an sbox.game package only once sbox.game
// says a body can be one; a client can't flood the server or wear what the window leaves out.
public sealed partial class PlayerModelSelector
{
	// Host: a pick is taken from each player at most this often. One sent sooner waits, replaced by any sent after it,
	// so the last one always lands, but no player can flood everyone with looks to sync and models to pose.
	private const float PickInterval = 0.5f;
	private readonly Dictionary<Guid, RealTimeSince> _lastPicks = new();

	// Host: each player's picks counted, so an sbox.game pick still being vetted is dropped if a newer one came meanwhile
	private readonly Dictionary<Guid, int> _pickCounts = new();
	private readonly Dictionary<Guid, (string Text, bool FromHost)> _waitingPicks = new();

	[Rpc.Host]
	private void PickOnHost( string text )
	{
		// Refused before it is read: a look this long isn't one this addon writes
		if ( text is not null && text.Length > PlayerLook.MaxTextLength )
			return;

		var id = KeyOf( Rpc.Caller );
		var fromHost = Rpc.Caller is not { IsHost: false };

		if ( _lastPicks.TryGetValue( id, out var since ) && since < PickInterval )
		{
			_waitingPicks[ id ] = (text, fromHost);
			return;
		}

		TakePick( id, text, fromHost );
	}

	// Host: the picks that waited out their player's interval
	private void TakeWaitingPicks()
	{
		if ( _waitingPicks.Count == 0 )
			return;

		foreach ( var (id, pick) in _waitingPicks.ToArray() )
		{
			if ( _lastPicks.TryGetValue( id, out var since ) && since < PickInterval )
				continue;

			_waitingPicks.Remove( id );
			TakePick( id, pick.Text, pick.FromHost );
		}
	}

	private void TakePick( Guid id, string text, bool fromHost )
	{
		_lastPicks[ id ] = 0;
		var count = _pickCounts[ id ] = _pickCounts.GetValueOrDefault( id ) + 1;
		var look = PlayerLook.Parse( text );

		// Bones picked by hand only count from the host while the host keeps players from picking them
		if ( BlockArmature && !fromHost )
			look = WithoutBones( look );

		if ( look.IsDefault )
		{
			Picks.Remove( id );
			return;
		}

		if ( !Offers( look.Model ) )
			return;

		// A package goes on once sbox.game says it is one a body can be: no client can pick what the window leaves out. One
		// new to the server is downloaded by everyone, so each player brings in only so many (see DownloadLimit).
		if ( CloudModels.IsPackageIdent( look.Model ) )
		{
			if ( TakeServerDownload( id, look.Model, fromHost ) )
				_ = PutOnVettedAsync( id, look, count );

			return;
		}

		Picks[ id ] = look.Serialize();
	}

	private async Task PutOnVettedAsync( Guid id, PlayerLook look, int count )
	{
		if ( !await CloudModels.VetAsync( look.Model ) || !IsValid || _pickCounts.GetValueOrDefault( id ) != count )
			return;

		ShareDownload( look.Model );
		Picks[ id ] = look.Serialize();
	}
}
