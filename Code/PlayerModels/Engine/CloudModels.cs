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

	private static readonly VerdictCache Verdicts = new( "player_model_selector/verdicts.json" );

	// "org.ident": a package, never a path into the game's files
	private static readonly Regex PackageIdent = new( @"^[a-z0-9_\-]+\.[a-z0-9_\-]+$", RegexOptions.IgnoreCase );

	// The "(Playermodel)" / "[PLAYERMODEL]" / "(PM)" tags uploaders put in titles; the window already says what they are
	private static readonly Regex TitleTags = new( @"\s*[\(\[]\s*(player\s*model|pm)\s*[\)\]]\s*|\s+-\s*pm$", RegexOptions.IgnoreCase );

	/// <summary>Whether <paramref name="model"/> is an sbox.game package ident.</summary>
	internal static bool IsPackageIdent( string model )
	{
		return model is not null && PackageIdent.IsMatch( model );
	}

	/// <summary>The model packages a search on sbox.game returns, most popular first for the default search.</summary>
	internal static async Task<List<Found>> FindAsync( string query, int count )
	{
		var result = await Package.FindAsync( query, count, 0, default );

		return result.Packages
			.Where( x => x.TypeName == "model" && IsPackageIdent( x.FullIdent ) )
			.Select( x => new Found( x.FullIdent, Tidy( x.Title, x.FullIdent ), x.Updated ) )
			.ToList();
	}

	/// <summary>
	/// What an earlier check said about this version of the package: true wearable, false not, null never checked.
	/// </summary>
	internal static bool? Known( Found found )
	{
		return Verdicts.Known( KeyOf( found ) );
	}

	/// <summary>Checks <paramref name="found"/> (downloading it) and remembers the verdict. True when it can be worn.</summary>
	internal static async Task<bool> CheckAsync( Found found )
	{
		var why = await ModelCheck.WhyNotWearableAsync( found.Ident );
		Verdicts.Remember( KeyOf( found ), why );

		return why is null;
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
