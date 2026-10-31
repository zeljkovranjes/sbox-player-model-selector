using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Player models found on sbox.game, and which of them turned out to be wearable. Checking one means downloading it
/// (<see cref="ModelCheck"/>), so each verdict is kept on this machine (keyed ident@updated) and a model is only
/// checked again when its package is updated.
/// </summary>
internal static class CloudModels
{
	/// <summary>A model package from the search: its ident, a tidied title, and when it was last updated.</summary>
	internal sealed record Found( string Ident, string Title, DateTimeOffset Updated );

	/// <summary>One page of a search: what it found that may be offered, and how many results the search has in all.</summary>
	internal sealed record SearchPage( List<Found> Found, int Total );

	// Renamed whenever what passes changes, so models checked under older rules are checked again
	private static readonly VerdictCache Verdicts = new( "player_model_selector/body_verdicts_2.json" );

	/// <summary>The fewest bones a body can have: hips, spine and head, and three for each arm and leg.</summary>
	internal const int MinHumanBones = 15;

	// Packages tagged as one of these are parts of a body or things worn on one, never a body to be
	private static readonly HashSet<string> NotBodyTags = new( StringComparer.OrdinalIgnoreCase )
		{ "hand", "hands", "arm", "arms", "clothing", "citizen" };

	// Host: what sbox.game said about each package picked, by ident, for the session (dropped whole once this many are
	// kept) and on this machine for a day
	private const int MaxVetted = 1024;
	private static readonly Dictionary<string, Task<bool>> Vetted = new();
	private static readonly TimedCache<bool> SavedVets = new( "player_model_selector/body_vets.json", TimeSpan.FromDays( 1 ), 4096 );

	// Each page of each search, kept on this machine for an hour: starting or joining again doesn't search again
	private static readonly TimedCache<SearchPage> SavedSearches = new( "player_model_selector/searches.json", TimeSpan.FromHours( 1 ), 256 );

	// "org.ident": a package, never a path into the game's files
	private static readonly Regex PackageIdent = new( @"^[a-z0-9_\-]+\.[a-z0-9_\-]+$", RegexOptions.IgnoreCase );

	// The "(Playermodel)" / "[PLAYERMODEL]" / "(PM)" tags uploaders put in titles; the window already says what they are
	private static readonly Regex TitleTags = new( @"\s*[\(\[]\s*(player\s*model|pm)\s*[\)\]]\s*|\s+-\s*pm$", RegexOptions.IgnoreCase );

	/// <summary>Whether <paramref name="model"/> is an sbox.game package ident.</summary>
	internal static bool IsPackageIdent( string model )
	{
		return model is not null && PackageIdent.IsMatch( model );
	}

	/// <summary>
	/// A page of the model packages a search on sbox.game returns (<paramref name="count"/> results from
	/// <paramref name="skip"/>), most popular first for the default search. For <paramref name="bodies"/>, packages tagged
	/// as hands, arms, clothing or the citizen are left out; with <paramref name="requiredTag"/>, only packages tagged so
	/// are kept.
	/// </summary>
	internal static async Task<SearchPage> FindAsync( string query, int count, int skip, bool bodies, string requiredTag = null )
	{
		var key = $"{query}|{count}|{skip}|{bodies}|{requiredTag}";
		if ( SavedSearches.TryGet( key, out var saved ) && saved?.Found is not null )
			return saved;

		var result = await Package.FindAsync( query, count, skip, default );
		var found = result.Packages
			.Where( x => x.TypeName == "model" && IsPackageIdent( x.FullIdent ) )
			.Where( x => !bodies || IsBodyTagged( x.Tags ) )
			.Where( x => requiredTag is null || x.Tags?.Contains( requiredTag, StringComparer.OrdinalIgnoreCase ) == true )
			.Select( x => new Found( x.FullIdent, Tidy( x.Title, x.FullIdent ), x.Updated ) )
			.ToList();

		var page = new SearchPage( found, result.TotalCount );
		SavedSearches.Set( key, page );
		return page;
	}

	/// <summary>
	/// Host: whether the package <paramref name="ident"/> may be worn, from what sbox.game says of it: a model, not
	/// tagged as hands, arms, clothing or the citizen, and with enough bones for a body when it says how many. A client
	/// can't get past this by picking a package the window wouldn't offer. Asked once per package and kept.
	/// </summary>
	internal static Task<bool> VetAsync( string ident )
	{
		if ( Vetted.TryGetValue( ident, out var known ) )
			return known;

		if ( Vetted.Count >= MaxVetted )
			Vetted.Clear();

		var verdict = FetchVerdictAsync( ident );
		Vetted[ ident ] = verdict;
		return verdict;
	}

	private static async Task<bool> FetchVerdictAsync( string ident )
	{
		if ( SavedVets.TryGet( ident, out var saved ) )
			return saved;

		try
		{
			var package = await Package.FetchAsync( ident, false );
			var verdict = package is { TypeName: "model" } && IsBodyTagged( package.Tags ) && WhyNotBody( package ) is null;

			// Only an answer is kept: a package that couldn't be fetched is asked about again
			if ( package is not null )
				SavedVets.Set( ident, verdict );

			return verdict;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Player Model Selector: couldn't ask sbox.game about {ident}: {e.Message}" );
			return false;
		}
	}

	// The bones sbox.game says the model has; null when it doesn't say (older packages)
	private static int? BonesOf( Package package )
	{
		return package?.Metadata is Package.ModelMetaData model ? model.Bones : null;
	}

	// Whether sbox.game says the model is big enough to be a body; true when it doesn't say
	private static bool BodySized( Package package )
	{
		return package?.Metadata is not Package.ModelMetaData model || ModelCheck.IsBodySized( model.BoundsMax - model.BoundsMin );
	}

	private static bool EnoughBones( int? bones )
	{
		return bones is null || bones >= MinHumanBones;
	}

	private static bool IsBodyTagged( string[] tags )
	{
		return tags is null || !tags.Any( NotBodyTags.Contains );
	}

	/// <summary>
	/// What an earlier check said about this version of the package: true wearable, false not, null never checked.
	/// </summary>
	internal static bool? Known( Found found )
	{
		return Verdicts.Known( KeyOf( found ) );
	}

	/// <summary>
	/// What sbox.game says of <paramref name="found"/>, without downloading it: false (kept as its verdict) when it has
	/// too few bones or is too small to be a body, true when it may be one or sbox.game doesn't say.
	/// </summary>
	internal static async Task<bool> PreCheckAsync( Found found )
	{
		var why = WhyNotBody( await Package.FetchAsync( found.Ident, false ) );
		if ( why is null )
			return true;

		Verdicts.Remember( KeyOf( found ), why );
		return false;
	}

	/// <summary>Remembers what checking <paramref name="found"/> found elsewhere: why it can't be worn, or null when it can.</summary>
	internal static void Remember( Found found, string why )
	{
		Verdicts.Remember( KeyOf( found ), why );
	}

	/// <summary>
	/// Checks <paramref name="found"/> and remembers the verdict. True when it can be worn. One sbox.game says can't be a
	/// body is refused without a download (see <see cref="PreCheckAsync"/>); any other is downloaded and checked.
	/// </summary>
	internal static async Task<bool> CheckAsync( Found found )
	{
		var why = WhyNotBody( await Package.FetchAsync( found.Ident, false ) ) ?? await ModelCheck.WhyNotWearableAsync( found.Ident );

		Verdicts.Remember( KeyOf( found ), why );
		return why is null;
	}

	// Why sbox.game's own data says a package can't be a body: too few bones, or too small (clothing and hats are rigged
	// to the citizen's whole skeleton, so only their size tells them apart); null when it may be one
	private static string WhyNotBody( Package package )
	{
		var bones = BonesOf( package );
		if ( !EnoughBones( bones ) )
			return $"It has {bones} bones; a body needs at least {MinHumanBones}.";

		return BodySized( package ) ? null : "It is too small to be a body: a part or something worn.";
	}

	private static string KeyOf( Found found )
	{
		return $"{found.Ident}@{found.Updated.ToUnixTimeSeconds()}";
	}

	private static string Tidy( string title, string ident )
	{
		var tidy = TitleTags.Replace( title ?? "", " " ).Trim();
		return string.IsNullOrEmpty( tidy ) ? ident : tidy;
	}
}
