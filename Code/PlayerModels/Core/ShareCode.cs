using System;
using System.Text;

namespace PlayerModels.Core;

/// <summary>
/// Something shared for a model (an armature, a look), as JSON, written as one line of text to post anywhere and paste
/// back: <c>pms-</c>, its kind, a colon, then the JSON in URL-safe base64, which chat apps leave alone.
/// </summary>
public static class ShareCode
{
	/// <summary>The longest code read: far past any real one, so pasted text can't be made to carry much.</summary>
	public const int MaxLength = 48 * 1024;

	/// <summary>The code for <paramref name="json"/>, a <paramref name="kind"/> ("armature", "look").</summary>
	public static string Write( string kind, string json )
	{
		var base64 = Convert.ToBase64String( Encoding.UTF8.GetBytes( json ) );
		return PrefixOf( kind ) + base64.TrimEnd( '=' ).Replace( '+', '-' ).Replace( '/', '_' );
	}

	/// <summary>
	/// The JSON in <paramref name="text"/> (surrounding whitespace allowed) when it is a code for a <paramref name="kind"/>,
	/// otherwise null. Whether the JSON is one is for that kind to say.
	/// </summary>
	public static string Read( string kind, string text )
	{
		var prefix = PrefixOf( kind );
		text = text?.Trim();
		if ( string.IsNullOrEmpty( text ) || text.Length > MaxLength || !text.StartsWith( prefix, StringComparison.Ordinal ) )
			return null;

		var base64 = text[ prefix.Length.. ].Replace( '-', '+' ).Replace( '_', '/' );
		base64 = base64.PadRight( base64.Length + (4 - base64.Length % 4) % 4, '=' );

		try
		{
			return Encoding.UTF8.GetString( Convert.FromBase64String( base64 ) );
		}
		catch ( FormatException )
		{
			return null;
		}
	}

	private static string PrefixOf( string kind ) => $"pms-{kind}:";
}
