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

		// The citizen has nothing to customise but its effects and weapon designs: those it keeps, and nothing else is kept
		// with it
		if ( look.IsDefault )
		{
			KeepPick( id, look.Effects.Count > 0 || look.Weapons.Count > 0 ? look.EffectsAlone().Serialize() : null );

			return;
		}

		if ( !Offers( look.Model ) )
		{
			Refuse( id, look.Model, PickRefusal.NotAllowed );
			return;
		}

		KeepAffordableItems( id, look, fromHost );

		// A package goes on once sbox.game says it is one a body can be: no client can pick what the window leaves out. One
		// new to the server is downloaded by everyone, so each player brings in only so many (see DownloadLimit).
		if ( CloudModels.IsPackageIdent( look.Model ) )
		{
			if ( TakeServerDownload( id, look.Model, fromHost ) )
				_ = PutOnVettedAsync( id, look, count );

			return;
		}

		KeepPick( id, look.Serialize() );
	}

	// Workshop items are downloaded by everyone who sees them, so what a player brings in counts like a model: past their
	// limit an item is left off, and they are told when they can try again
	private void KeepAffordableItems( Guid id, PlayerLook look, bool fromHost )
	{
		foreach ( var item in look.Accessories.Where( x => x.IsWorkshop ).ToList() )
		{
			if ( TakeServerDownload( id, item.Item, fromHost ) )
				_serverModels.Add( item.Item );
			else
				look.Accessories.Remove( item );
		}
	}

	private async Task PutOnVettedAsync( Guid id, PlayerLook look, int count )
	{
		var vetted = await CloudModels.VetAsync( look.Model );
		if ( !IsValid || _pickCounts.GetValueOrDefault( id ) != count )
			return;

		if ( !vetted )
		{
			Refuse( id, look.Model, PickRefusal.NotABody );
			return;
		}

		ShareDownload( look.Model );
		KeepPick( id, look.Serialize() );
	}

	/// <summary>Why the host didn't put on a model a player picked.</summary>
	public enum PickRefusal
	{
		/// <summary>The host doesn't allow it: blocked, or its kind (sbox.game, mounted games) switched off.</summary>
		NotAllowed,

		/// <summary>sbox.game didn't say it can be a body (hands, clothing, too few bones), or couldn't be reached.</summary>
		NotABody,
	}

	/// <summary>The model the host last didn't put on for this player, and why; null until it refuses one.</summary>
	internal string RefusedModel { get; private set; }

	/// <summary>Why <see cref="RefusedModel"/> wasn't put on.</summary>
	internal PickRefusal RefusedFor { get; private set; }

	/// <summary>Counts the host's refusals of this player's picks, for what shows them.</summary>
	internal int Refusals { get; private set; }

	// Host: a player's own pick (null: none), and the model the host chose for them, if it did, wearing its effects and
	// weapon designs as they change
	private void KeepPick( Guid id, string text )
	{
		if ( text is null )
			Picks.Remove( id );
		else
			Picks[ id ] = text;

		if ( HostPicks?.TryGetValue( id, out var chosen ) == true )
			HostPicks[ id ] = HostChoice( PlayerLook.Parse( chosen ).Model, text );
	}

	// Host: the model the host chose for a player, wearing that player's own effects and weapon designs (own: their pick)
	private static string HostChoice( string model, string own )
	{
		return PlayerLook.Parse( own ).EffectsAlone().WithModel( model ).Serialize();
	}

	// Host: tells the player who picked model why it didn't go on
	private void Refuse( Guid id, string model, PickRefusal reason )
	{
		if ( Connection.Find( id ) is not { } player )
			return;

		using ( Rpc.FilterInclude( player ) )
			PickRefused( model, reason );
	}

	[Rpc.Broadcast]
	private void PickRefused( string model, PickRefusal reason )
	{
		if ( Rpc.Caller is not { IsHost: true } || string.IsNullOrEmpty( model ) || model.Length > MaxRefusedLength || !Enum.IsDefined( reason ) )
			return;

		RefusedModel = model;
		RefusedFor = reason;
		Refusals++;
	}

	// The longest model a refusal names: longer than any path or ident this addon picks
	private const int MaxRefusedLength = 512;
}
