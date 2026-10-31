using System;
using System.Collections.Generic;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.Components;

// sbox.game models new to the server: everyone here downloads each one a player puts on, so each player other than the
// host brings in only so many in a while, and every machine starts downloading one as soon as the host takes it.
public sealed partial class PlayerModelSelector
{
	/// <summary>
	/// How many sbox.game models new to the server each player other than the host may put on per
	/// <see cref="DownloadMinutes"/>. The host sets it on the Server page.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public int DownloadLimit { get; set; } = ServerRules.DefaultDownloadLimit;

	/// <summary>The minutes <see cref="DownloadLimit"/> counts over.</summary>
	[Sync( SyncFlags.FromHost )] public int DownloadMinutes { get; set; } = ServerRules.DefaultDownloadMinutes;

	// Host: the sbox.game models put on here this session, which every machine has or is downloading, and the new ones
	// each player brought in
	private readonly HashSet<string> _serverModels = new();
	private readonly Dictionary<Guid, DownloadQuota> _downloadQuotas = new();

	// This player's own new models, counted as the host counts them, so the grid can tell before downloading one; and
	// how long the host said to wait, when it refused one
	private readonly DownloadQuota _ownDownloads = new();
	private RealTimeUntil _refusedFor;

	private double DownloadWindow => DownloadMinutes * 60.0;

	/// <summary>Sets <see cref="DownloadLimit"/>. Host only.</summary>
	public void SetDownloadLimit( int limit )
	{
		ChangeRules( rules => rules.DownloadLimit = limit );
	}

	/// <summary>Sets <see cref="DownloadMinutes"/>. Host only.</summary>
	public void SetDownloadMinutes( int minutes )
	{
		ChangeRules( rules => rules.DownloadMinutes = minutes );
	}

	/// <summary>
	/// Seconds until this player may put on an sbox.game model they haven't downloaded yet; 0 when they may now. The host
	/// is never kept waiting.
	/// </summary>
	internal double DownloadWait()
	{
		if ( Networking.IsHost )
			return 0;

		var own = _ownDownloads.SecondsUntilFree( RealTime.Now, DownloadLimit, DownloadWindow );
		return Math.Max( own, Math.Max( 0, _refusedFor.Relative ) );
	}

	/// <summary>
	/// Counts an sbox.game model this player is about to download and put on. False, counting nothing, while they have
	/// to wait (see <see cref="DownloadWait"/>).
	/// </summary>
	internal bool TakeOwnDownload()
	{
		if ( Networking.IsHost )
			return true;

		return DownloadWait() <= 0 && _ownDownloads.TryTake( RealTime.Now, DownloadLimit, DownloadWindow );
	}

	// Host: whether a pick of the package model may go on: one new to the server counts against its player, unless that
	// is the host, and the player is told how long to wait when it can't
	private bool TakeServerDownload( Guid id, string model, bool fromHost )
	{
		if ( fromHost || _serverModels.Contains( model ) )
			return true;

		if ( !_downloadQuotas.TryGetValue( id, out var quota ) )
			_downloadQuotas[ id ] = quota = new DownloadQuota();

		if ( quota.TryTake( RealTime.Now, DownloadLimit, DownloadWindow ) )
			return true;

		if ( Networking.FindConnection( id ) is { } player )
		{
			using ( Rpc.FilterInclude( player ) )
				DownloadRefused( (float)quota.SecondsUntilFree( RealTime.Now, DownloadLimit, DownloadWindow ) );
		}

		return false;
	}

	// Host: a vetted model goes on; one new to the server is downloaded everywhere from now on
	private void ShareDownload( string model )
	{
		if ( _serverModels.Add( model ) )
			PreloadEverywhere( model );
	}

	[Rpc.Broadcast]
	private void DownloadRefused( float seconds )
	{
		if ( Rpc.Caller is not { IsHost: true } )
			return;

		_refusedFor = Math.Clamp( seconds, 0f, ServerRules.MaxDownloadMinutes * 60f );
	}

	// Every machine downloads a model someone put on, so it is there when they come into view. Only the host says what
	// everyone downloads, and only an sbox.game package.
	[Rpc.Broadcast]
	private void PreloadEverywhere( string model )
	{
		if ( Rpc.Caller is not { IsHost: true } || Application.IsDedicatedServer || !CloudModels.IsPackageIdent( model ) )
			return;

		_ = ModelLoader.LoadAsync( model );
	}
}
