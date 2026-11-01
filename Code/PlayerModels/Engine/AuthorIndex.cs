using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlayerModels.Core;
using Sandbox;
using Sandbox.Services;

namespace PlayerModels.Engine;

/// <summary>
/// What each player shared, of every kind and for every model, so one's other shares can be shown: a copy of each
/// post in a public cloud table (<see cref="Database"/>), grouped by its author. Anyone can put a record in any group,
/// so only the author's own are believed. Calls fail with the backend's own exception; <see cref="Shared.Why"/> words it.
/// </summary>
internal static class AuthorIndex
{
	private const string Table = "shared_by";

	// The most of one player's shares listed
	private const int MaxListed = 50;

	// shortcut: a player's own shares are looked through one page (100, the most a page holds), so past 100 taking one
	// down may leave it on their list; page through ListOwned if anyone ever shares that much
	private const int MaxOwned = 100;

	/// <summary>A post a player shared, when they published it, and which one it is (see <see cref="KeyOf"/>).</summary>
	internal sealed record Share( SharePost Post, DateTimeOffset Published, string Key );

	/// <summary>Which share a record of a kind is: the same for its copy here.</summary>
	internal static string KeyOf( string kind, long id ) => $"{kind}:{id}";

	/// <summary>Records <paramref name="post"/>, published as record <paramref name="id"/>, as this player's.</summary>
	internal static async Task RecordAsync( SharePost post, long id )
	{
		await Database.GetTable( Table ).Create( KeyOf( post.Kind, id ), post.Serialize(), GroupOf( Game.SteamId.Value ), isPublic: true );
	}

	/// <summary>Forgets this player's share of <paramref name="kind"/> published as record <paramref name="id"/>, if it is there.</summary>
	internal static async Task ForgetAsync( string kind, long id )
	{
		var key = KeyOf( kind, id );
		var own = await Database.GetTable( Table ).ListOwned( GroupOf( Game.SteamId.Value ), 0, MaxOwned );
		if ( own.Records.FirstOrDefault( x => x.Key == key ) is { } mine )
			await mine.Delete();
	}

	/// <summary>What <paramref name="author"/> shared, the newest first.</summary>
	internal static async Task<List<Share>> ListAsync( long author )
	{
		var page = await Database.GetTable( Table ).List( GroupOf( author ), 0, MaxListed );
		return page.Records
			.Where( x => x.OwnerSteamId == author )
			.Select( x => (Post: SharePost.Parse( x.Value ), x.CreatedAt, x.Key) )
			.Where( x => x.Post is not null )
			.Select( x => new Share( x.Post, x.CreatedAt, x.Key ) )
			.OrderByDescending( x => x.Published )
			.ToList();
	}

	private static string GroupOf( long author ) => $"by:{author}";
}
