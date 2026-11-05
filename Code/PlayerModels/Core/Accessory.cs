using System;
using System.Text.Json.Serialization;

namespace PlayerModels.Core;

/// <summary>
/// A cosmetic worn on a model, stock or from the Workshop: which item, where it sits and how big, and how it is customised.
/// Most clothes follow the whole body; what sits on one joint (glasses, a hat) is pinned to it instead, and can be turned
/// too. Size and distance are capped by the size of the model wearing it, which only a client with both loaded knows, so
/// every client caps it for itself (see <see cref="FittedTo"/>).
/// </summary>
public sealed class Accessory
{
	/// <summary>The joints an item can be pinned to.</summary>
	public enum Joint { Head, Neck, Chest, Hips, LeftHand, RightHand }

	/// <summary>The most items one look wears.</summary>
	public const int MaxWorn = 8;

	/// <summary>The smallest an item goes, whatever is wearing it.</summary>
	public const float MinScale = 0.25f;

	/// <summary>How much tighter clothes that follow the body can go: size there is how far they stand off it.</summary>
	public const float MinFollowScale = 0.9f;

	/// <summary>How much looser those can go.</summary>
	public const float MaxFollowScale = 1.25f;

	/// <summary>The most an item turns each way around each axis, in degrees.</summary>
	public const float MaxTurn = 180f;

	// The largest an item goes, whatever is wearing it, and how far from its joint: only there to bound what is read
	// from anyone, the caps below are the real ones
	private const float MostScale = 4f;
	private const float MostOffset = 64f;

	// An item is no longer than half the model wearing it is tall, and no further from its joint than a quarter of that:
	// big enough to be a choice, too small to hide the player or float away from them
	private const float MaxShareOfBody = 0.5f;
	private const float MaxReachOfBody = 0.25f;

	/// <summary>How tall the citizen stands: items and effects are made for it, so a model this tall wears them at their own size.</summary>
	internal const float CitizenHeight = 72f;

	private const int MaxItemLength = 256;

	/// <summary>The item: a stock clothing resource's path, or a Workshop package's ident.</summary>
	public string Item { get; set; } = "";

	/// <summary>The <see cref="Joint"/> it is pinned to, by name.</summary>
	public string Pin { get; set; } = nameof( Joint.Head );

	/// <summary>How far it is moved from where the joint holds it: forwards, to the model's left, and up.</summary>
	public float Forward { get; set; }

	/// <inheritdoc cref="Forward"/>
	public float Left { get; set; }

	/// <inheritdoc cref="Forward"/>
	public float Up { get; set; }

	/// <summary>How it is turned, in degrees.</summary>
	public float Pitch { get; set; }

	/// <inheritdoc cref="Pitch"/>
	public float Yaw { get; set; }

	/// <inheritdoc cref="Pitch"/>
	public float Roll { get; set; }

	/// <summary>Its size: 1 is the size it was made.</summary>
	public float Scale { get; set; } = 1f;

	/// <summary>How the item itself is customised (colours, finish, body groups, morphs); its model is the item.</summary>
	public PlayerLook Style { get; set; } = new();

	/// <summary>The joint an item of <paramref name="group"/> (see <see cref="GroupOf"/>) is pinned to at first.</summary>
	public static Joint StartingJoint( string group ) => group == "Neck" ? Joint.Neck : Joint.Head;

	/// <summary>
	/// What an item of clothing <paramref name="category"/> is offered as, or null for a body's skin, which isn't worn.
	/// Anything new lands in "Other": every item is offered.
	/// </summary>
	public static string GroupOf( string category )
	{
		if ( string.IsNullOrEmpty( category ) || category == "Skin" )
			return null;

		if ( category.StartsWith( "Glasses", StringComparison.Ordinal ) || category == "Eyewear" )
			return "Eyewear";

		if ( category.StartsWith( "Hat", StringComparison.Ordinal ) || category == "Headwear" )
			return "Hats";

		if ( category.StartsWith( "FacialHair", StringComparison.Ordinal ) || category == "Facial" )
			return "Facial hair";

		if ( category.StartsWith( "Hair", StringComparison.Ordinal ) )
			return "Hair";

		if ( category.StartsWith( "Head", StringComparison.Ordinal ) || category is "EarringStud" or "Piercing" )
			return "Head";

		if ( category.StartsWith( "Necklace", StringComparison.Ordinal ) )
			return "Neck";

		if ( category.StartsWith( "Makeup", StringComparison.Ordinal ) )
			return "Makeup";

		return category switch
		{
			"TShirt" or "Shirt" or "Tops" or "Hoodie" or "Sweatshirt" or "Knitwear" => "Tops",
			"Jacket" or "Coat" or "Cardigan" or "Gilet" or "Vest" => "Jackets",
			"Dress" or "Skirt" or "Suit" or "Costume" => "Outfits",
			"Trousers" or "Jeans" or "Shorts" or "Bottoms" => "Bottoms",
			"Boots" or "Heels" or "Shoes" or "Trainers" or "Slippers" or "Sandals" or "Footwear" or "Socks" => "Footwear",
			"Gloves" => "Gloves",
			"WristWatch" or "Wristwear" or "Ring" => "Wrist",
			"Underwear" or "Bra" => "Underwear",
			_ => "Other",
		};
	}

	/// <summary>Whether items of <paramref name="group"/> sit on one joint; the rest bend with the whole body.</summary>
	public static bool IsPinned( string group ) => group is "Eyewear" or "Hats" or "Hair" or "Facial hair" or "Head" or "Neck";

	/// <summary>
	/// The most an item <paramref name="itemSize"/> long may be scaled on a model <paramref name="bodyHeight"/> tall. Never
	/// below <see cref="MinScale"/>, however small the model.
	/// </summary>
	public static float MaxScaleFor( float bodyHeight, float itemSize )
	{
		if ( bodyHeight <= 0f || itemSize <= 0f )
			return 1f;

		return Math.Clamp( bodyHeight * MaxShareOfBody / itemSize, MinScale, MostScale );
	}

	/// <summary>How far from its joint an item may be moved, along each axis, on a model <paramref name="bodyHeight"/> tall.</summary>
	public static float MaxOffsetFor( float bodyHeight )
	{
		return bodyHeight <= 0f ? 0f : Math.Min( bodyHeight * MaxReachOfBody, MostOffset );
	}

	/// <summary>The size an item starts at on a model <paramref name="bodyHeight"/> tall: in proportion to the citizen it was made for.</summary>
	public static float StartingScale( float bodyHeight, float itemSize )
	{
		var scale = bodyHeight <= 0f ? 1f : bodyHeight / CitizenHeight;
		return Math.Clamp( scale, MinScale, MaxScaleFor( bodyHeight, itemSize ) );
	}

	/// <summary>
	/// A copy within the caps of a model <paramref name="bodyHeight"/> tall: for an item <paramref name="itemSize"/> long,
	/// or for clothes that follow the body (<paramref name="follows"/>), whose size stays near what they were made.
	/// </summary>
	public Accessory FittedTo( float bodyHeight, float itemSize, bool follows = false )
	{
		var copy = Sanitized();
		var reach = MaxOffsetFor( bodyHeight );
		copy.Forward = Math.Clamp( copy.Forward, -reach, reach );
		copy.Left = Math.Clamp( copy.Left, -reach, reach );
		copy.Up = Math.Clamp( copy.Up, -reach, reach );
		copy.Scale = follows
			? Math.Clamp( copy.Scale, MinFollowScale, MaxFollowScale )
			: Math.Clamp( copy.Scale, MinScale, MaxScaleFor( bodyHeight, itemSize ) );
		return copy;
	}

	/// <summary>Whether the item comes from the Workshop: a package's ident has no path.</summary>
	[JsonIgnore]
	public bool IsWorkshop => Item is not null && !Item.Contains( '/' );

	/// <summary>
	/// A copy within bounds, or null without an item: numbers finite and limited, the joint a real one, and the style
	/// only what an item can have (no body shape, bones or items of its own).
	/// </summary>
	public Accessory Sanitized()
	{
		var item = Item?.Trim() ?? "";
		if ( item.Length == 0 || item.Length > MaxItemLength )
			return null;

		var style = new PlayerLook
		{
			Model = item,
			BodyGroups = Style?.BodyGroups,
			Morphs = Style?.Morphs,
			Skin = Style?.Skin,
			Tint = Style?.Tint,
			Parts = Style?.Parts,
			Finish = Style?.Finish,
			PartFinishes = Style?.PartFinishes,
		};

		return new Accessory
		{
			Item = item,
			Pin = Enum.IsDefined( typeof( Joint ), Pin ?? "" ) ? Pin : nameof( Joint.Head ),
			Forward = Limited( Forward, MostOffset, 0f ),
			Left = Limited( Left, MostOffset, 0f ),
			Up = Limited( Up, MostOffset, 0f ),
			Pitch = Limited( Pitch, MaxTurn, 0f ),
			Yaw = Limited( Yaw, MaxTurn, 0f ),
			Roll = Limited( Roll, MaxTurn, 0f ),
			Scale = float.IsFinite( Scale ) ? Math.Clamp( Scale, MinScale, MostScale ) : 1f,
			Style = style.Sanitized(),
		};
	}

	/// <summary>Whether <paramref name="other"/> is the same item, pinned the same and customised the same.</summary>
	public bool SameAs( Accessory other )
	{
		return other is not null && other.Item == Item && other.Pin == Pin
			&& other.Forward == Forward && other.Left == Left && other.Up == Up
			&& other.Pitch == Pitch && other.Yaw == Yaw && other.Roll == Roll && other.Scale == Scale
			&& (Style?.SameCustomisationAs( other.Style ) ?? other.Style is null);
	}

	private static float Limited( float value, float most, float otherwise )
	{
		return float.IsFinite( value ) ? Math.Clamp( value, -most, most ) : otherwise;
	}
}
