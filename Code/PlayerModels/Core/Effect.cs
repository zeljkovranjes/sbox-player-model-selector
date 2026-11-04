using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PlayerModels.Core.Script;

namespace PlayerModels.Core;

/// <summary>
/// Something worn that isn't a model: glowing eyes, fire in the hands, a halo. Its layers (see <see cref="EffectLayer"/>)
/// come from a joint, an item or the weapon held (see <see cref="EffectAnchor"/>), each layer from its effect's unless it
/// names a joint of its own, and four
/// sliders change all of them at once: colour, size, amount and speed. Lives in the look, so it saves, shares and
/// networks with it; every client builds it for itself, within the host's budget (see <see cref="EffectBudget"/>).
/// </summary>
public sealed class Effect
{
	/// <summary>
	/// What its key does (see <see cref="Key"/>): shows it for <see cref="TriggerSeconds"/> each press (and fires its
	/// script's <c>OnKey</c>), switches it on and off, or shows it while the key is held. Only starting it counts toward
	/// <see cref="TriggerCooldownSeconds"/>; switching it off or letting go is always free.
	/// </summary>
	public enum KeyAction { Fire, Toggle, Hold }

	/// <summary>
	/// The joints an effect can come from: those an item can be pinned to, the eyes (between them, and each one), the top
	/// of the head (the crown, however high over its head bone a model's head reaches), the feet, and the ground under the
	/// body (below its hips at its feet's height, turned the way it faces: a circle under the feet).
	/// </summary>
	public enum Joint { Head, Eyes, LeftEye, RightEye, Crown, Neck, Chest, Hips, LeftHand, RightHand, LeftFoot, RightFoot, Ground }

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
	[Sandbox.SkipHotload]
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
	[field: Sandbox.SkipHotload]
	public static IReadOnlyList<string> Keys { get; } = KeyNames();

	/// <summary>What its key does, a <see cref="KeyAction"/> by name; null to fire (show it for a moment each press).</summary>
	[JsonPropertyName( "km" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string KeyMode { get; set; }

	/// <summary>What its key does.</summary>
	[JsonIgnore]
	public KeyAction KeyActionIs => KeyMode is not null && Enum.TryParse<KeyAction>( KeyMode, false, out var action ) && Enum.IsDefined( typeof( KeyAction ), action )
		? action
		: KeyAction.Fire;

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

	/// <summary>
	/// The drawn shapes its layers name (see <see cref="DrawnShape"/>), carried with it while it is on its own: kept in My
	/// effects, shared or copied as a code. In a look the look carries them instead, each once (see
	/// <see cref="PlayerLook.DrawnShapes"/>), and this is null.
	/// </summary>
	[JsonPropertyName( "sh" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<DrawnShape> Shapes { get; set; }

	/// <summary>
	/// The script it is written in (see <see cref="ScriptCompiler"/>), minified as it is stored and sent; null for
	/// one only the visual editor made. Its layers and fields are what the script built, kept so a server with scripts
	/// switched off (or a script that doesn't compile) still shows them; a client that runs scripts builds it again.
	/// </summary>
	[JsonPropertyName( "sc" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Script { get; set; }

	/// <summary>
	/// The values chosen for its script's <c>[Parameter]</c> options, as text by parameter name (see
	/// <see cref="ScriptParameter.TryRead"/>); null for the script's own. Each client holds them to their parameter.
	/// </summary>
	[JsonPropertyName( "pm" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public Dictionary<string, string> Parameters { get; set; }

	/// <summary>The longest text a parameter's value is kept with: a palette of 32 colours fits.</summary>
	public const int MaxParameterLength = 512;

	/// <summary>The Workshop share it was used or forked from (see <see cref="ShareOrigin"/>); null for one made here.</summary>
	[JsonPropertyName( "ff" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public ShareOrigin ForkedFrom { get; set; }

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

	/// <summary>How many of its layers are lights, those switched off aside.</summary>
	public int Lights()
	{
		var lights = 0;
		foreach ( var layer in Layers )
		{
			if ( layer.KindIs == LayerKind.Light && !layer.Off )
				lights++;
		}

		return lights;
	}

	/// <summary>How many of its layers show, those switched off aside.</summary>
	public int LayersOn()
	{
		var on = 0;
		foreach ( var layer in Layers )
		{
			if ( !layer.Off )
				on++;
		}

		return on;
	}

	/// <summary>
	/// A copy on its own within bounds, with at most <paramref name="layers"/> layers, or null without any it can show:
	/// name and id capped (an id that isn't one becomes empty, for <see cref="Sanitized(IEnumerable{Effect}, ShapePool)"/>
	/// to give it one), sliders limited, its key one of <see cref="Keys"/> or none, only the layers this version knows, and
	/// the drawn shapes it carries read within bounds, those its layers name kept.
	/// </summary>
	public Effect Sanitized( int layers = MaxLayers )
	{
		var own = ShapePool.Of( Shapes );
		var copy = Sanitized( layers, own );
		if ( copy is not null && own.Used.Count > 0 )
			copy.Shapes = new List<DrawnShape>( own.Used );

		return copy;
	}

	// The same, its layers naming the drawn shapes of pool (a look's, which carries them itself, or its own)
	internal Effect Sanitized( int layers, ShapePool pool )
	{
		var kept = new List<EffectLayer>();
		foreach ( var layer in Layers ?? new() )
		{
			if ( kept.Count >= Math.Min( layers, MaxLayers ) )
				break;

			if ( layer?.Sanitized( pool.Resolve ) is { } sanitized )
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
			KeyMode = KeyMode is not null && KeyActionIs != KeyAction.Fire ? KeyActionIs.ToString() : null,
			Anchor = (Anchor ?? new EffectAnchor()).Sanitized(),
			Layers = kept,
			Hue = float.IsFinite( Hue ) ? Hue - MathF.Floor( Hue ) : 0f,
			Size = Slider( Size ),
			Amount = Slider( Amount ),
			Speed = Slider( Speed ),
			ForkedFrom = ForkedFrom?.Sanitized(),
			Script = Script is not null && ScriptLimits.Fits( Script ) ? Script : null,
			Parameters = Script is not null && ScriptLimits.Fits( Script ) ? SanitizedParameters( Parameters ) : null,
		};
	}

	// The parameter values kept with a script: by their lengths only (a client holds each to its parameter once the
	// script compiles), at most as many as a script has parameters; null for none
	private static Dictionary<string, string> SanitizedParameters( Dictionary<string, string> values )
	{
		if ( values is not { Count: > 0 } )
			return null;

		var kept = new Dictionary<string, string>();
		foreach ( var (name, value) in values )
		{
			if ( kept.Count >= ScriptParameter.MaxPerScript )
				break;

			if ( !string.IsNullOrEmpty( name ) && name.Length <= Lexer.MaxNameLength && value is not null && value.Length <= MaxParameterLength )
				kept[ name ] = value;
		}

		return kept.Count > 0 ? kept : null;
	}

	/// <summary>
	/// <paramref name="effects"/> within bounds: at most <see cref="MaxWorn"/> of them with <see cref="MaxLookLayers"/>
	/// layers between them, in order, each with an id of its own, their layers naming the drawn shapes of
	/// <paramref name="pool"/> (the look's, which carries them).
	/// </summary>
	internal static List<Effect> Sanitized( IEnumerable<Effect> effects, ShapePool pool )
	{
		var kept = new List<Effect>();
		var layers = MaxLookLayers;
		foreach ( var effect in effects ?? Array.Empty<Effect>() )
		{
			if ( kept.Count >= MaxWorn || layers <= 0 )
				break;

			if ( effect?.Sanitized( layers, pool ) is not { } sanitized )
				continue;

			if ( sanitized.Id == "" || kept.Exists( x => x.Id == sanitized.Id ) )
				sanitized.Id = NewId( kept );

			layers -= sanitized.Layers.Count;
			kept.Add( sanitized );
		}

		HoldScriptsTo( kept, ScriptLimits.MaxLookBytes );
		return kept;
	}

	/// <summary>
	/// Lets go of the scripts of <paramref name="effects"/> past <paramref name="bytes"/> together (stored, minified, in
	/// order): those effects keep what their scripts built, as if scripts were off.
	/// </summary>
	internal static void HoldScriptsTo( List<Effect> effects, int bytes )
	{
		var total = 0;
		foreach ( var effect in effects )
		{
			if ( effect.Script is null )
				continue;

			var size = ScriptLimits.Utf8Bytes( effect.Script );
			if ( total + size <= bytes )
			{
				total += size;
				continue;
			}

			effect.Script = null;
			effect.Parameters = null;
		}
	}

	/// <summary>The longest effect read from the Workshop or a code, as JSON: far past any real one.</summary>
	public const int MaxSharedLength = 32 * 1024;

	/// <summary>
	/// An effect shared as JSON (see <see cref="SharedForm"/>), within bounds and carrying its drawn shapes, or null when it
	/// isn't one: unreadable, too long, or with nothing to show.
	/// </summary>
	public static Effect ParseShared( string json )
	{
		if ( string.IsNullOrWhiteSpace( json ) || json.Length > MaxSharedLength || !json.TrimStart().StartsWith( '{' ) )
			return null;

		try
		{
			return JsonSerializer.Deserialize<Effect>( json )?.Sanitized();
		}
		catch ( JsonException )
		{
			return null;
		}
	}

	/// <summary>The effect as it is shared: within bounds, switched on, with the drawn shapes it carries.</summary>
	public string SharedForm()
	{
		var copy = Sanitized();
		if ( copy is null )
			return "";

		copy.Off = false;
		return JsonSerializer.Serialize( copy );
	}

	/// <summary>
	/// Whether <paramref name="other"/> is the same effect: the same layers, place, sliders and shapes, whatever it is
	/// called, whether it is on, its key and where it came from. The same one is never shared twice.
	/// </summary>
	public bool SameAs( Effect other )
	{
		return other is not null && Comparable( this ) == Comparable( other );
	}

	private static string Comparable( Effect effect )
	{
		var copy = effect.Sanitized();
		if ( copy is null )
			return "";

		copy.Id = "";
		copy.Name = "";
		copy.Off = false;
		copy.Key = null;
		copy.KeyMode = null;
		copy.ForkedFrom = null;
		copy.Shapes = null;
		return JsonSerializer.Serialize( copy );
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

/// <summary>What an effect comes from: a joint of the body, an item worn, or the weapon held.</summary>
public enum AnchorKind { Joint, Item, HeldWeapon }

/// <summary>
/// Where an effect comes from: a joint of the body (see <see cref="Effect.Joint"/>), an item worn (by the item itself,
/// so it follows it however the items are ordered), or the weapon held (its muzzle or tip), moved from there by an
/// offset. What isn't there stands in for itself: a joint a model doesn't have (the eyes by a point in front of the head,
/// the feet under the hips), an item no longer worn or a weapon not held by the joint it was on, so an effect always shows.
/// </summary>
public sealed class EffectAnchor
{
	/// <summary>The longest item an anchor names: as long as an item can be.</summary>
	public const int MaxItemLength = 256;

	/// <summary>
	/// The <see cref="Effect.Joint"/> it comes from, by name: for an item or a weapon, the joint it falls back to while
	/// that isn't there (the item's own, the hand).
	/// </summary>
	[JsonPropertyName( "j" )]
	public string Joint { get; set; } = nameof( Effect.Joint.Head );

	/// <summary>Its <see cref="AnchorKind"/> by name; null for a joint.</summary>
	[JsonPropertyName( "k" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Kind { get; set; }

	/// <summary>The item it comes from (see <see cref="Accessory.Item"/>), for an anchor on an item: the first one worn.</summary>
	[JsonPropertyName( "i" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Item { get; set; }

	/// <summary>How far it is moved from where it comes from: forwards, to the left, and up, in that place's own directions.</summary>
	[JsonPropertyName( "f" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Forward { get; set; }

	/// <inheritdoc cref="Forward"/>
	[JsonPropertyName( "l" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Left { get; set; }

	/// <inheritdoc cref="Forward"/>
	[JsonPropertyName( "u" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Up { get; set; }

	/// <summary>What it comes from; only right on an anchor within bounds (see <see cref="Sanitized"/>).</summary>
	[JsonIgnore]
	public AnchorKind KindIs => Kind is not null && Enum.TryParse<AnchorKind>( Kind, false, out var kind ) && Enum.IsDefined( typeof( AnchorKind ), kind ) ? kind : AnchorKind.Joint;

	/// <summary>
	/// A copy within bounds: a joint this version doesn't know is the head, a kind it doesn't know (or an item anchor
	/// without an item) is the joint, and the offset is limited like a layer's.
	/// </summary>
	public EffectAnchor Sanitized()
	{
		var item = Item?.Trim();
		var kind = Kind is not null && Enum.IsDefined( typeof( AnchorKind ), Kind ) && Kind != nameof( AnchorKind.Joint ) ? Kind : null;
		if ( kind == nameof( AnchorKind.Item ) && (string.IsNullOrEmpty( item ) || item.Length > MaxItemLength) )
			kind = null;

		return new EffectAnchor
		{
			Joint = Joint is not null && Enum.IsDefined( typeof( Effect.Joint ), Joint ) ? Joint : nameof( Effect.Joint.Head ),
			Kind = kind,
			Item = kind == nameof( AnchorKind.Item ) ? item : null,
			Forward = Offset( Forward ),
			Left = Offset( Left ),
			Up = Offset( Up ),
		};
	}

	private static float Offset( float value ) => float.IsFinite( value ) ? Math.Clamp( value, -EffectLayer.MaxOffset, EffectLayer.MaxOffset ) : 0f;
}
