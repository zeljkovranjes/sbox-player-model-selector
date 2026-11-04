using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace PlayerModels.Core;

/// <summary>
/// One thing a player shared for a model (an armature, a look) as it is kept: the name and description they gave it,
/// and the thing itself as its own JSON. Read from anyone, so <see cref="Parse"/> keeps it within bounds.
/// </summary>
public sealed class SharePost
{
	/// <summary>The longest name: enough to tell looks apart in a list.</summary>
	public const int MaxNameLength = 32;

	/// <summary>The longest description.</summary>
	public const int MaxDescriptionLength = 128;

	/// <summary>
	/// The most a record holds, in UTF-8 bytes: the Workshop's own limit on a record's value. A post past it isn't
	/// published, and one read past it (hand-made) isn't one.
	/// </summary>
	public const int MaxRecordBytes = 65536;

	// Past any real armature, look, effect or shape, items included, with room in a record for the JSON around it
	private const int MaxItemLength = 32 * 1024;
	private const int MaxKindLength = 32;
	private const int MaxModelLength = 512;

	/// <summary>What it is: "armature" or "look".</summary>
	public string Kind { get; set; } = "";

	/// <summary>The model it was made for.</summary>
	public string Model { get; set; } = "";

	/// <summary>What its author called it.</summary>
	public string Name { get; set; } = "";

	/// <summary>What its author said about it; empty for nothing.</summary>
	public string Description { get; set; } = "";

	/// <summary>The thing shared, as its own JSON.</summary>
	public string Item { get; set; } = "";

	/// <summary>The post as JSON.</summary>
	public string Serialize()
	{
		return JsonSerializer.Serialize( this );
	}

	/// <summary>
	/// A post read back from <see cref="Serialize"/>, its name and description tidied (see <see cref="Tidy"/>), or null
	/// when <paramref name="json"/> isn't one: unreadable, without a kind, model or item, or past the limits a real one
	/// keeps to.
	/// </summary>
	public static SharePost Parse( string json )
	{
		// A character is at least a byte: longer than a record holds is refused before it is read
		if ( string.IsNullOrWhiteSpace( json ) || json.Length > MaxRecordBytes )
			return null;

		SharePost post;
		try
		{
			post = JsonSerializer.Deserialize<SharePost>( json );
		}
		catch ( JsonException )
		{
			return null;
		}

		if ( post is null || !IsWithin( post.Kind, MaxKindLength ) || !IsWithin( post.Model, MaxModelLength ) || !IsWithin( post.Item, MaxItemLength ) )
			return null;

		post.Name = Tidy( post.Name, MaxNameLength );
		post.Description = Tidy( post.Description, MaxDescriptionLength );
		return post;
	}

	/// <summary>Why <paramref name="json"/> (a post as it is published) can't be: past what a record holds; null when it can.</summary>
	public static string TooBig( string json )
	{
		var bytes = Encoding.UTF8.GetByteCount( json ?? "" );
		return bytes <= MaxRecordBytes && (json ?? "").Length > 0 && Parse( json ) is not null
			? null
			: $"Too big to publish: {bytes / 1024f:0.0} KB of the {MaxRecordBytes / 1024} KB a share holds.";
	}

	/// <summary>
	/// <paramref name="text"/> as a name or description shows: on one line (line breaks and other control characters
	/// become spaces), trimmed, and cut to <paramref name="max"/> characters as a reader counts them.
	/// </summary>
	public static string Tidy( string text, int max )
	{
		if ( string.IsNullOrWhiteSpace( text ) )
			return "";

		var line = new string( text.Select( x => char.IsControl( x ) ? ' ' : x ).ToArray() ).Trim();
		var characters = new StringInfo( line );
		return characters.LengthInTextElements <= max ? line : characters.SubstringByTextElements( 0, max ).TrimEnd();
	}

	private static bool IsWithin( string text, int max )
	{
		return !string.IsNullOrWhiteSpace( text ) && text.Length <= max;
	}
}
