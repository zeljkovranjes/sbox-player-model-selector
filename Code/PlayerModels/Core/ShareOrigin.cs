using System.Text.Json.Serialization;

namespace PlayerModels.Core;

/// <summary>
/// The Workshop share a copy came from, kept with it: an effect or a shape used or forked from a share is "based on" it
/// (its name and author shown, the credit kept when the copy is published again), and a host who blocks that share hides
/// every copy of it on their server, changed or not.
/// </summary>
public sealed class ShareOrigin
{
	/// <summary>What a share can be: an effect (or one layer of one), or a shape or sticker.</summary>
	public const string EffectKind = "effect";
	public const string ShapeKind = "shape";

	/// <summary>Which Workshop it is in: <see cref="EffectKind"/> or <see cref="ShapeKind"/>.</summary>
	[JsonPropertyName( "k" )]
	public string Kind { get; set; } = "";

	/// <summary>The share's record.</summary>
	[JsonPropertyName( "i" )]
	public long Id { get; set; }

	/// <summary>What its author called it.</summary>
	[JsonPropertyName( "n" )]
	public string Name { get; set; } = "";

	/// <summary>Who shared it, by Steam id.</summary>
	[JsonPropertyName( "a" )]
	public long Author { get; set; }

	/// <summary>
	/// The share as the host blocks it: its kind and record, which tell it from a share of the other kind with the same
	/// record number.
	/// </summary>
	[JsonIgnore]
	public string Key => KeyOf( Kind, Id );

	/// <summary>The key (see <see cref="Key"/>) of the share of <paramref name="kind"/> with record <paramref name="id"/>.</summary>
	public static string KeyOf( string kind, long id ) => $"{kind}:{id}";

	/// <summary>A copy within bounds (its name tidied), or null when it names no share: an unknown kind, no record or no author.</summary>
	public ShareOrigin Sanitized()
	{
		if ( Kind is not (EffectKind or ShapeKind) || Id <= 0 || Author <= 0 )
			return null;

		return new ShareOrigin { Kind = Kind, Id = Id, Name = SharePost.Tidy( Name, SharePost.MaxNameLength ), Author = Author };
	}
}
