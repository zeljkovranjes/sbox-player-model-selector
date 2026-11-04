using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlayerModels.Core;

/// <summary>
/// How a player customised one weapon: its finish and tint, a skin painted over it (see <see cref="WeaponSkin"/>), props
/// on it (see <see cref="WeaponProp"/>), stickers on it (see <see cref="WeaponSticker"/>) and effects coming from it
/// (always, or when it shoots, reloads, is drawn, put away or hits). Kept by the weapon's world model, so the same design
/// shows on the same weapon in any game, and in the look, so it saves, shares and networks with it; every client builds
/// only the design of the weapon a player holds. The caps are hard, whatever the host allows: a tampered look can't
/// bring a huge prop or a crowd of them.
/// </summary>
public sealed class WeaponDesign
{
	/// <summary>The most designs one look has, one a weapon.</summary>
	public const int MaxPerLook = 16;

	/// <summary>The most props, stickers and effects one weapon has, and the most layers its effects have between them.</summary>
	public const int MaxProps = 8;
	public const int MaxStickers = 8;
	public const int MaxEffects = 4;
	public const int MaxLayers = 8;

	/// <summary>
	/// The most a look's designs take together, as JSON: with the most effects, shapes and items a look has, it stays
	/// under <see cref="PlayerLook.MaxTextLength"/>. Designs past it are left out, last first.
	/// </summary>
	public const int MaxLookBytes = 20 * 1024;

	/// <summary>The most parts a design hides, and the longest a part's name is.</summary>
	public const int MaxHiddenParts = 16;
	public const int MaxPartLength = 64;

	/// <summary>The longest design read from the Workshop or a code, as JSON: past the longest a design within its caps can be.</summary>
	public const int MaxSharedLength = 32 * 1024;

	// Effects on a weapon are told apart from the body's by their ids: "w1" to "w4"
	private const string IdPrefix = "w";

	/// <summary>The weapon: its world model, by path.</summary>
	[JsonPropertyName( "w" )]
	public string Weapon { get; set; } = "";

	/// <summary>Its finish (see <see cref="MaterialFinish"/>); null for its own materials.</summary>
	[JsonPropertyName( "fi" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Finish { get; set; }

	/// <summary>The colour it is tinted, as <c>#rrggbb</c>; null for none.</summary>
	[JsonPropertyName( "t" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Tint { get; set; }

	/// <summary>What is painted over it; null for nothing.</summary>
	[JsonPropertyName( "sk" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public WeaponSkin Skin { get; set; }

	/// <summary>The models on it.</summary>
	[JsonPropertyName( "p" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<WeaponProp> Props { get; set; }

	/// <summary>The pictures stuck on it.</summary>
	[JsonPropertyName( "s" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<WeaponSticker> Stickers { get; set; }

	/// <summary>
	/// The parts of it hidden (a magazine, a rail: see <see cref="IsPartName"/>), by name; a part the weapon doesn't
	/// have, or can't hide, is passed over. Null for none.
	/// </summary>
	[JsonPropertyName( "hp" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<string> HiddenParts { get; set; }

	/// <summary>The effects coming from it: anchored on the weapon held, never on a key.</summary>
	[JsonPropertyName( "e" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<Effect> Effects { get; set; }

	/// <summary>
	/// The drawn shapes its stickers and effects name, carried with it while it is on its own (shared, or copied as a
	/// code); in a look the look carries them (see <see cref="PlayerLook.DrawnShapes"/>) and this is null.
	/// </summary>
	[JsonPropertyName( "sh" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<DrawnShape> Shapes { get; set; }

	/// <summary>The Workshop share it was used or forked from; null for one made here.</summary>
	[JsonPropertyName( "ff" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public ShareOrigin ForkedFrom { get; set; }

	/// <summary>Whether it changes anything about its weapon.</summary>
	[JsonIgnore]
	public bool IsEmpty => Finish is null && Tint is null && Skin is null && (Props?.Count ?? 0) == 0 && (Stickers?.Count ?? 0) == 0
		&& (Effects?.Count ?? 0) == 0 && (HiddenParts?.Count ?? 0) == 0;

	/// <summary>
	/// Whether <paramref name="name"/> could name a part of a weapon (a body group of its model, or a bone of its first-person
	/// model): not empty, not too long, on one line.
	/// </summary>
	public static bool IsPartName( string name )
	{
		if ( string.IsNullOrWhiteSpace( name ) || name.Length > MaxPartLength )
			return false;

		foreach ( var character in name )
		{
			if ( char.IsControl( character ) )
				return false;
		}

		return true;
	}

	/// <summary>The weapon's name, from its model's file: "w_m4a1.vmdl" is "M4a1".</summary>
	public static string NameOf( string weapon )
	{
		if ( string.IsNullOrWhiteSpace( weapon ) )
			return "Weapon";

		var start = weapon.LastIndexOfAny( new[] { '/', '\\' } ) + 1;
		var end = weapon.IndexOf( '.', start );
		var name = end < 0 ? weapon[ start.. ] : weapon[ start..end ];
		if ( name.StartsWith( "w_", StringComparison.OrdinalIgnoreCase ) )
			name = name[ 2.. ];

		name = name.Replace( '_', ' ' ).Trim();
		return name.Length == 0 ? "Weapon" : char.ToUpperInvariant( name[ 0 ] ) + name[ 1.. ];
	}

	/// <summary>
	/// A copy on its own within bounds (carrying the drawn shapes it names), or null when it isn't one: see
	/// <see cref="Sanitized(ShapePool)"/>.
	/// </summary>
	public WeaponDesign Sanitized()
	{
		var own = ShapePool.Of( Shapes );
		var copy = Sanitized( own );
		if ( copy is not null && own.Used.Count > 0 )
			copy.Shapes = new List<DrawnShape>( own.Used );

		return copy;
	}

	/// <summary>
	/// A copy within bounds, its stickers and effects naming the drawn shapes of <paramref name="pool"/>, or null without a
	/// weapon or with nothing to change about it: a finish this version knows, a tint that is a colour, at most
	/// <see cref="MaxProps"/> props, <see cref="MaxStickers"/> stickers and <see cref="MaxEffects"/> effects of
	/// <see cref="MaxLayers"/> layers between them, each within its own bounds, each effect on the weapon and without a key.
	/// </summary>
	internal WeaponDesign Sanitized( ShapePool pool )
	{
		var weapon = Weapon?.Trim();
		if ( !WeaponProp.IsModelPath( weapon ) )
			return null;

		var props = new List<WeaponProp>();
		foreach ( var prop in Props ?? new() )
		{
			if ( props.Count < MaxProps && prop?.Sanitized() is { } kept )
				props.Add( kept );
		}

		var stickers = new List<WeaponSticker>();
		foreach ( var sticker in Stickers ?? new() )
		{
			if ( stickers.Count < MaxStickers && sticker?.Sanitized( pool.Resolve ) is { } kept )
				stickers.Add( kept );
		}

		var effects = new List<Effect>();
		var layers = MaxLayers;
		foreach ( var effect in Effects ?? new() )
		{
			if ( effects.Count >= MaxEffects || layers <= 0 )
				break;

			if ( effect?.Sanitized( layers, pool ) is not { } kept )
				continue;

			kept.Id = IdPrefix + (effects.Count + 1);
			kept.Key = null;
			kept.Anchor.Kind = nameof( AnchorKind.HeldWeapon );
			kept.Anchor.Joint = nameof( Effect.Joint.RightHand );
			layers -= kept.Layers.Count;
			effects.Add( kept );
		}

		var hidden = new List<string>();
		foreach ( var part in HiddenParts ?? new() )
		{
			var name = part?.Trim();
			if ( hidden.Count < MaxHiddenParts && IsPartName( name ) && !hidden.Contains( name ) )
				hidden.Add( name );
		}

		var design = new WeaponDesign
		{
			Weapon = weapon,
			HiddenParts = hidden.Count > 0 ? hidden : null,
			Finish = MaterialFinish.IsKnown( Finish ) ? Finish : null,
			Tint = PlayerLook.IsColour( Tint ) ? Tint.ToLowerInvariant() : null,
			Skin = Skin?.Sanitized( pool.Resolve ),
			Props = props.Count > 0 ? props : null,
			Stickers = stickers.Count > 0 ? stickers : null,
			Effects = effects.Count > 0 ? effects : null,
			ForkedFrom = ForkedFrom?.Sanitized(),
		};

		return design.IsEmpty ? null : design;
	}

	/// <summary>
	/// <paramref name="designs"/> within bounds: at most <see cref="MaxPerLook"/> of them, one a weapon (the first kept),
	/// in order, together within <see cref="MaxLookBytes"/>, naming the drawn shapes of <paramref name="pool"/>.
	/// </summary>
	internal static List<WeaponDesign> Sanitized( IEnumerable<WeaponDesign> designs, ShapePool pool )
	{
		var kept = new List<WeaponDesign>();
		var bytes = 0;
		var read = 0;
		foreach ( var design in designs ?? Array.Empty<WeaponDesign>() )
		{
			// Read past the cap only a little: a look listing thousands costs no more than a few
			if ( kept.Count >= MaxPerLook || read++ >= MaxPerLook * 2 )
				break;

			if ( design?.Sanitized( pool ) is not { } sanitized || kept.Exists( x => x.Weapon == sanitized.Weapon ) )
				continue;

			bytes += Encoding.UTF8.GetByteCount( JsonSerializer.Serialize( sanitized ) );
			if ( bytes > MaxLookBytes )
				break;

			kept.Add( sanitized );
		}

		return kept;
	}

	/// <summary>A design shared as JSON (see <see cref="SharedForm"/>), within bounds and carrying its drawn shapes, or null when it isn't one.</summary>
	public static WeaponDesign ParseShared( string json )
	{
		if ( string.IsNullOrWhiteSpace( json ) || json.Length > MaxSharedLength || !json.TrimStart().StartsWith( '{' ) )
			return null;

		try
		{
			return JsonSerializer.Deserialize<WeaponDesign>( json )?.Sanitized();
		}
		catch ( JsonException )
		{
			return null;
		}
	}

	/// <summary>The design as it is shared: within bounds, with the drawn shapes it carries.</summary>
	public string SharedForm() => Sanitized() is { } copy ? JsonSerializer.Serialize( copy ) : "";

	/// <summary>Whether <paramref name="other"/> is the same design: the same weapon and everything on it, wherever it came from.</summary>
	public bool SameAs( WeaponDesign other )
	{
		return other is not null && Comparable( this ) == Comparable( other );
	}

	private static string Comparable( WeaponDesign design )
	{
		var copy = design.Sanitized();
		if ( copy is null )
			return "";

		copy.ForkedFrom = null;
		return JsonSerializer.Serialize( copy );
	}

	/// <summary>The ids of the drawn shapes its stickers, skin and effects name.</summary>
	public IEnumerable<string> DrawnShapeIds()
	{
		foreach ( var sticker in Stickers ?? new() )
		{
			if ( sticker.DrawnPicture is { } id )
				yield return id;
		}

		if ( DrawnShape.IsReference( Skin?.PatternShape ) )
			yield return Skin.PatternShape[ 1.. ];

		foreach ( var effect in Effects ?? new() )
		{
			foreach ( var id in effect.Layers.SelectMany( x => x.DrawnShapeIds() ) )
				yield return id;
		}
	}
}
