using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PlayerModels.Core;

/// <summary>
/// Something worn that isn't a model: glowing eyes, fire in the hands, a halo. Its layers (see <see cref="EffectLayer"/>)
/// come from a joint (see <see cref="EffectAnchor"/>), each layer from its effect's unless it names its own, and four
/// sliders change all of them at once: colour, size, amount and speed. Lives in the look, so it saves, shares and
/// networks with it; every client builds it for itself, within the host's budget (see <see cref="EffectBudget"/>).
/// </summary>
public sealed class Effect
{
	/// <summary>
	/// The joints an effect can come from: those an item can be pinned to, the eyes (between them, and each one), the top
	/// of the head (the crown, however high over its head bone a model's head reaches) and the feet.
	/// </summary>
	public enum Joint { Head, Eyes, LeftEye, RightEye, Crown, Neck, Chest, Hips, LeftHand, RightHand, LeftFoot, RightFoot }

	/// <summary>The most effects one look wears, the most layers one effect has, and the most layers all of them have.</summary>
	public const int MaxWorn = 8;
	public const int MaxLayers = 8;
	public const int MaxLookLayers = 32;

	/// <summary>The longest name an effect has.</summary>
	public const int MaxNameLength = 32;

	/// <summary>How far each slider changes its effect at either end: twice or half the size and speed, four times or a quarter the amount.</summary>
	public const float SizeRange = 2f;
	public const float AmountRange = 4f;
	public const float SpeedRange = 2f;

	/// <summary>How long an effect with a key shows once its key is pressed, in seconds.</summary>
	public const float TriggerSeconds = 10f;

	/// <summary>
	/// How long a player waits between pressing effect keys, in seconds: fixed, for everyone, so no one can flood the
	/// server with effects. The host holds every player to it.
	/// </summary>
	public const float TriggerCooldownSeconds = 120f;

	/// <summary>The longest an id is.</summary>
	public const int MaxIdLength = 12;
	private static readonly Regex IdPattern = new( "^[a-z0-9]+$" );

	/// <summary>What tells it from the look's other effects, as long as it is worn (short, lower case letters and digits).</summary>
	[JsonPropertyName( "id" )]
	public string Id { get; set; } = "";

	/// <summary>What it is called.</summary>
	[JsonPropertyName( "n" )]
	public string Name { get; set; } = "";

	/// <summary>
	/// The key that shows it (see <see cref="Keys"/>), for <see cref="TriggerSeconds"/> each press; null for an effect
	/// that always shows.
	/// </summary>
	[JsonPropertyName( "key" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Key { get; set; }

	/// <summary>
	/// The keys an effect can be shown with, by the names the game's keyboard reads: letters, digits, the function keys and
	/// the side mouse buttons.
	/// </summary>
	public static IReadOnlyList<string> Keys { get; } = KeyNames();

	/// <summary>Whether it is switched off: kept with the look, but not shown.</summary>
	[JsonPropertyName( "off" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public bool Off { get; set; }

	/// <summary>Where it comes from.</summary>
	[JsonPropertyName( "a" )]
	public EffectAnchor Anchor { get; set; } = new();

	/// <summary>What it is made of.</summary>
	[JsonPropertyName( "l" )]
	public List<EffectLayer> Layers { get; set; } = new();

	/// <summary>How far round the colour wheel every colour is turned, 0 to 1: 0 is the colours it was made in.</summary>
	[JsonPropertyName( "h" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Hue { get; set; }

	/// <summary>The size slider, -1 to 1: 0 is the size it was made (see <see cref="SizeRange"/>).</summary>
	[JsonPropertyName( "z" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Size { get; set; }

	/// <summary>The amount slider, -1 to 1: how many sprites, how bright its lights (see <see cref="AmountRange"/>).</summary>
	[JsonPropertyName( "q" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Amount { get; set; }

	/// <summary>The speed slider, -1 to 1: how fast it moves and beats (see <see cref="SpeedRange"/>).</summary>
	[JsonPropertyName( "w" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Speed { get; set; }

	/// <summary>What <see cref="Size"/> multiplies sizes by.</summary>
	[JsonIgnore]
	public float SizeFactor => MathF.Pow( SizeRange, Size );

	/// <summary>What <see cref="Amount"/> multiplies how many sprites appear, and how bright lights are, by.</summary>
	[JsonIgnore]
	public float AmountFactor => MathF.Pow( AmountRange, Amount );

	/// <summary>What <see cref="Speed"/> multiplies how fast it moves and beats by.</summary>
	[JsonIgnore]
	public float SpeedFactor => MathF.Pow( SpeedRange, Speed );

	/// <summary>The joint <paramref name="layer"/> comes from: its own, or this effect's.</summary>
	public Joint JointOf( EffectLayer layer )
	{
		return Enum.TryParse<Joint>( layer?.Joint ?? Anchor?.Joint, false, out var joint ) ? joint : Joint.Head;
	}

	/// <summary>How many sprites of it are out at once, with its amount slider.</summary>
	public float LiveSprites()
	{
		var live = 0f;
		foreach ( var layer in Layers )
			live += layer.LiveSprites( AmountFactor );

		return live;
	}

	/// <summary>How many of its layers are lights.</summary>
	public int Lights()
	{
		var lights = 0;
		foreach ( var layer in Layers )
		{
			if ( layer.KindIs == LayerKind.Light )
				lights++;
		}

		return lights;
	}

	/// <summary>
	/// A copy within bounds, with at most <paramref name="layers"/> layers, or null without any it can show: name and id
	/// capped (an id that isn't one becomes empty, for <see cref="Sanitized(IEnumerable{Effect})"/> to give it one),
	/// sliders limited, its key one of <see cref="Keys"/> or none, and only the layers this version knows.
	/// </summary>
	public Effect Sanitized( int layers = MaxLayers )
	{
		var kept = new List<EffectLayer>();
		foreach ( var layer in Layers ?? new() )
		{
			if ( kept.Count >= Math.Min( layers, MaxLayers ) )
				break;

			if ( layer?.Sanitized() is { } sanitized )
				kept.Add( sanitized );
		}

		if ( kept.Count == 0 )
			return null;

		var id = Id?.Trim() ?? "";
		var name = Name?.Trim() ?? "";
		return new Effect
		{
			Id = id.Length <= MaxIdLength && IdPattern.IsMatch( id ) ? id : "",
			Name = name.Length <= MaxNameLength ? name : name[ ..MaxNameLength ],
			Off = Off,
			Key = KeyNamed( Key ),
			Anchor = (Anchor ?? new EffectAnchor()).Sanitized(),
			Layers = kept,
			Hue = float.IsFinite( Hue ) ? Hue - MathF.Floor( Hue ) : 0f,
			Size = Slider( Size ),
			Amount = Slider( Amount ),
			Speed = Slider( Speed ),
		};
	}

	/// <summary>
	/// <paramref name="effects"/> within bounds: at most <see cref="MaxWorn"/> of them with <see cref="MaxLookLayers"/>
	/// layers between them, in order, each with an id of its own.
	/// </summary>
	public static List<Effect> Sanitized( IEnumerable<Effect> effects )
	{
		var kept = new List<Effect>();
		var layers = MaxLookLayers;
		foreach ( var effect in effects ?? Array.Empty<Effect>() )
		{
			if ( kept.Count >= MaxWorn || layers <= 0 )
				break;

			if ( effect?.Sanitized( layers ) is not { } sanitized )
				continue;

			if ( sanitized.Id == "" || kept.Exists( x => x.Id == sanitized.Id ) )
				sanitized.Id = NewId( kept );

			layers -= sanitized.Layers.Count;
			kept.Add( sanitized );
		}

		return kept;
	}

	/// <summary>An id none of <paramref name="effects"/> has: "e1", "e2" and so on.</summary>
	public static string NewId( IReadOnlyList<Effect> effects )
	{
		for ( var i = 1; ; i++ )
		{
			var id = $"e{i}";
			if ( !Exists( effects, id ) )
				return id;
		}
	}

	private static bool Exists( IReadOnlyList<Effect> effects, string id )
	{
		for ( var i = 0; i < effects.Count; i++ )
		{
			if ( effects[ i ]?.Id == id )
				return true;
		}

		return false;
	}

	/// <summary>The key of <see cref="Keys"/> named <paramref name="name"/>, whatever its case; null for none of them.</summary>
	public static string KeyNamed( string name )
	{
		var trimmed = name?.Trim();
		if ( string.IsNullOrEmpty( trimmed ) )
			return null;

		foreach ( var key in Keys )
		{
			if ( string.Equals( key, trimmed, StringComparison.OrdinalIgnoreCase ) )
				return key;
		}

		return null;
	}

	private static string[] KeyNames()
	{
		var keys = new List<string>();
		for ( var letter = 'a'; letter <= 'z'; letter++ )
			keys.Add( letter.ToString() );

		for ( var digit = '0'; digit <= '9'; digit++ )
			keys.Add( digit.ToString() );

		for ( var function = 1; function <= 12; function++ )
			keys.Add( $"f{function}" );

		keys.Add( "mouse4" );
		keys.Add( "mouse5" );
		return keys.ToArray();
	}

	private static float Slider( float value )
	{
		return float.IsFinite( value ) ? Math.Clamp( value, -1f, 1f ) : 0f;
	}
}

/// <summary>
/// Where an effect comes from: a joint of the body (see <see cref="Effect.Joint"/>). A joint a model doesn't have is
/// stood in for (the eyes by a point in front of the head), so an effect always shows.
/// </summary>
public sealed class EffectAnchor
{
	/// <summary>The <see cref="Effect.Joint"/> it comes from, by name.</summary>
	[JsonPropertyName( "j" )]
	public string Joint { get; set; } = nameof( Effect.Joint.Head );

	/// <summary>A copy within bounds: a joint this version doesn't know is the head.</summary>
	public EffectAnchor Sanitized()
	{
		return new EffectAnchor { Joint = Joint is not null && Enum.IsDefined( typeof( Effect.Joint ), Joint ) ? Joint : nameof( Effect.Joint.Head ) };
	}
}
