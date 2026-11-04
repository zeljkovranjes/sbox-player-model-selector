using System.Collections.Generic;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The textures weapon skins' moving colours are drawn from (see <see cref="SkinColours"/>): one for each different skin,
/// shared by every material of it and every player wearing it, filled once a frame by whichever weapon wearing it is
/// animated first and uploaded (a 64 by 4 float texture, 4 KB). Made full, so a weapon shows its colours from its first
/// frame. At most <see cref="MostKept"/> are kept; past that the least lately used go first.
/// </summary>
internal static class SkinTextures
{
	/// <summary>The most skins whose textures are kept.</summary>
	internal const int MostKept = 128;

	// Filled again at most this often, however many weapons wear it: once a frame at any rate a screen shows
	private const double FillStep = 1.0 / 240.0;

	/// <summary>One skin's colours and the texture they are uploaded to.</summary>
	internal sealed class Entry
	{
		public SkinColours Colours;
		public Texture Texture;
		public double Started;
		public double FilledAt;
		public double UsedAt;
	}

	private static readonly Dictionary<string, Entry> Kept = new();

	/// <summary>The texture of <paramref name="skin"/>'s moving colours, made (and filled) the first time; null when it doesn't move.</summary>
	internal static Entry For( WeaponSkin skin )
	{
		var key = SkinColours.KeyOf( skin );
		if ( key.Length == 0 )
			return null;

		var now = RealTime.NowDouble;
		if ( Kept.TryGetValue( key, out var kept ) && kept.Texture.IsValid() )
		{
			kept.UsedAt = now;
			return kept;
		}

		if ( SkinColours.Of( skin, ScriptPrograms.Cache, ScriptPrograms.Stack ) is not { } colours )
			return null;

		if ( Kept.Count >= MostKept )
			LetGoOldest();

		var texture = Texture.Create( SkinColours.Texels, SkinColours.Rows, ImageFormat.RGBA32323232F ).WithName( "weapon_skin_colours" ).WithDynamicUsage().Finish();
		texture.Update<float>( colours.Data );
		var entry = new Entry { Colours = colours, Texture = texture, Started = now, FilledAt = now, UsedAt = now };
		Kept[ key ] = entry;
		return entry;
	}

	/// <summary>Fills <paramref name="entry"/> for now and uploads it, unless it was this frame. Every frame: no allocations.</summary>
	internal static void Tick( Entry entry )
	{
		var now = RealTime.NowDouble;
		entry.UsedAt = now;
		if ( now - entry.FilledAt < FillStep || !entry.Texture.IsValid() )
			return;

		entry.FilledAt = now;
		entry.Colours.Fill( now - entry.Started );
		entry.Texture.Update<float>( entry.Colours.Data );
	}

	// The least lately used goes; a material still drawing from it gets the skin's texture again when it is next dressed
	private static void LetGoOldest()
	{
		string oldest = null;
		var at = double.MaxValue;
		foreach ( var (key, entry) in Kept )
		{
			if ( entry.UsedAt < at )
				(oldest, at) = (key, entry.UsedAt);
		}

		if ( oldest is not null )
			Kept.Remove( oldest );
	}
}
