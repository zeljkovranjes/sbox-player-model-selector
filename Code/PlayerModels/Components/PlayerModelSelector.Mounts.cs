using System;
using System.Collections.Generic;
using System.Linq;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.Components;

// The mounted games players have: each client tells the host, and the host can keep any of them off the server.
public sealed partial class PlayerModelSelector
{
	// Each player tells the host which games they have mounted, for the host to block any on the Server page: checked this
	// often, and sent only when it changed. The host reads at most this much of what a player says.
	private const float MountReportInterval = 5f;
	private const int MaxMountsReported = 64;
	private const int MaxMountTitle = 64;
	private TimeSince _sinceMountReport = MountReportInterval;
	private string _mountsReported;

	// Host: the mounted games each player said they have, by connection id: ident to title
	private readonly Dictionary<Guid, Dictionary<string, string>> _playerMounts = new();

	/// <summary>A mounted game players here have: its mount ident, title, and how many of them (the host included) have it.</summary>
	public sealed record MountedGame( string Ident, string Title, int Players );

	/// <summary>
	/// Host: the mounted games the players here have, as each of them last said, and every blocked one, so it can be
	/// allowed again. In title order.
	/// </summary>
	public List<MountedGame> PlayersMounts()
	{
		var here = Connection.All.Select( KeyOf ).Append( KeyOf( Connection.Local ) ).ToHashSet();
		foreach ( var gone in _playerMounts.Keys.Where( x => !here.Contains( x ) ).ToArray() )
			_playerMounts.Remove( gone );

		var games = _playerMounts.Values
			.SelectMany( x => x )
			.GroupBy( x => x.Key )
			.Select( x => new MountedGame( x.Key, x.First().Value, x.Count() ) )
			.ToList();

		foreach ( var ident in BlockedMounts ?? Enumerable.Empty<string>() )
		{
			if ( games.All( x => x.Ident != ident ) )
				games.Add( new MountedGame( ident, ident, 0 ) );
		}

		return games.OrderBy( x => x.Title, StringComparer.OrdinalIgnoreCase ).ToList();
	}

	/// <summary>Host: keeps everyone from wearing models of the mounted game <paramref name="ident"/>, or allows it again.</summary>
	public void SetMountBlocked( string ident, bool blocked )
	{
		if ( !MountModels.IsGameIdent( ident ) )
			return;

		ChangeRules( rules =>
		{
			rules.BlockedMounts.Remove( ident );
			if ( blocked )
				rules.BlockedMounts.Add( ident );
		} );
	}

	// Tells the host which games are mounted here, when that changed
	private void ReportMounts()
	{
		if ( _sinceMountReport < MountReportInterval )
			return;

		_sinceMountReport = 0;
		var text = string.Join( "\n", MountModels.Mounted().Select( x => $"{x.Ident}\t{x.Title}" ) );
		if ( text == _mountsReported )
			return;

		_mountsReported = text;
		MountsOnHost( text );
	}

	[Rpc.Host]
	private void MountsOnHost( string text )
	{
		var maxLength = MaxMountsReported * (32 + 1 + MaxMountTitle + 1);
		if ( text is null || text.Length > maxLength )
			return;

		var mounts = new Dictionary<string, string>();
		foreach ( var line in text.Split( '\n', StringSplitOptions.RemoveEmptyEntries ).Take( MaxMountsReported ) )
		{
			var parts = line.Split( '\t' );
			if ( parts.Length != 2 || !MountModels.IsGameIdent( parts[ 0 ] ) )
				continue;

			var title = parts[ 1 ].Trim();
			mounts[ parts[ 0 ] ] = title.Length is > 0 and <= MaxMountTitle ? title : parts[ 0 ];
		}

		_playerMounts[ KeyOf( Rpc.Caller ) ] = mounts;
	}
}
