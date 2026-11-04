using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PlayerModels.Core;

/// <summary>
/// One layer of an emblem (see <see cref="DrawnShape.Emblem"/>), like the layers of a game's emblem editor: a standard
/// shape (by name) or a drawn shape the emblem carries (by its <see cref="DrawnShape.Reference"/>: drawn by hand, text,
/// or one from the Workshop), moved, turned, scaled each way and flipped on the emblem's square, filled in a colour with
/// an outline of another, see-through as much as its opacity says, and its fill's colour moving if it is animated. An
/// eraser layer draws nothing: it cuts its shape out of every layer below it. Its fields are named and mean what the
/// effect scripts' emblem layers do, so a script builds one with nothing to change.
/// </summary>
public sealed class EmblemLayer
{
	/// <summary>The most layers an emblem has.</summary>
	public const int MaxPerEmblem = 32;

	/// <summary>How far from the middle a layer's middle may be, each way, on the emblem's square (2 across).</summary>
	public const float MaxReach = 1f;

	/// <summary>How small and how big a layer may be scaled, each way: 1 fills the square.</summary>
	public const float MinScale = 0.01f;
	public const float MaxScale = 4f;

	/// <summary>How far a layer may be turned, in degrees, either way.</summary>
	public const float MaxTurn = 360f;

	// Places, sizes and turns are kept to these steps, so a layer reads back as it was written and its emblem keeps its id
	private const float PlaceStep = 1000f;
	private const float TurnStep = 10f;

	private const string White = "#ffffff";
	private const string Black = "#000000";

	/// <summary>Its shape: a <see cref="StandardShape"/> by name, or a drawn shape the emblem carries by its reference.</summary>
	[JsonPropertyName( "sh" )]
	public string Shape { get; set; } = nameof( StandardShape.Circle );

	/// <summary>Where its middle is on the emblem's square: right and up, -1 to 1.</summary>
	[JsonPropertyName( "x" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float X { get; set; }

	/// <inheritdoc cref="X"/>
	[JsonPropertyName( "y" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Y { get; set; }

	/// <summary>How far it is turned, in degrees, anticlockwise.</summary>
	[JsonPropertyName( "r" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Rotation { get; set; }

	/// <summary>How big it is across and up: 1 fills the square.</summary>
	[JsonPropertyName( "sx" )]
	public float ScaleX { get; set; } = 1f;

	/// <inheritdoc cref="ScaleX"/>
	[JsonPropertyName( "sy" )]
	public float ScaleY { get; set; } = 1f;

	/// <summary>Whether it is flipped left to right, and top to bottom.</summary>
	[JsonPropertyName( "fx" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public bool FlipX { get; set; }

	/// <inheritdoc cref="FlipX"/>
	[JsonPropertyName( "fy" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public bool FlipY { get; set; }

	/// <summary>Its fill's colour, as <c>#rrggbb</c>; an animated fill takes its colours from its animation.</summary>
	[JsonPropertyName( "c" )]
	public string Color { get; set; } = White;

	/// <summary>How much it covers what is below it: 0 not at all, 1 fully.</summary>
	[JsonPropertyName( "op" )]
	public float Opacity { get; set; } = 1f;

	/// <summary>Its outline's colour, as <c>#rrggbb</c>.</summary>
	[JsonPropertyName( "oc" )]
	public string OutlineColor { get; set; } = Black;

	/// <summary>How wide its outline is, on the emblem's square; 0 for none.</summary>
	[JsonPropertyName( "ow" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float OutlineWidth { get; set; }

	/// <summary>How its fill's colour moves, a <see cref="Sticker.Motion"/> by name; null for none.</summary>
	[JsonPropertyName( "an" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Animation { get; set; }

	/// <summary>The colours its animation goes through, as <c>#rrggbb</c>; null for the game's.</summary>
	[JsonPropertyName( "pal" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<string> Palette { get; set; }

	/// <summary>How fast its animation runs: 1 is the game's own pace.</summary>
	[JsonPropertyName( "sp" )]
	public float AnimationSpeed { get; set; } = 1f;

	/// <summary>Whether it is hidden: kept, not drawn.</summary>
	[JsonPropertyName( "h" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public bool Hidden { get; set; }

	/// <summary>Whether it is locked in the editor: drawn, but not picked or moved on the canvas.</summary>
	[JsonPropertyName( "lk" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public bool Locked { get; set; }

	/// <summary>Which group it is in (layers of one are picked and moved together); 0 for none.</summary>
	[JsonPropertyName( "g" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public int Group { get; set; }

	/// <summary>Whether it is an eraser: it draws nothing, and cuts its shape out of every layer below it.</summary>
	[JsonPropertyName( "er" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public bool Eraser { get; set; }

	/// <summary>Which look of a lenticular sticker it is in: <see cref="BothLooks"/>, <see cref="FirstLook"/> or <see cref="SecondLook"/>.</summary>
	[JsonPropertyName( "sd" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public int Side { get; set; }

	/// <summary>The <see cref="Side"/>s a layer is in: both looks, the first, or the second.</summary>
	public const int BothLooks = 0;
	public const int FirstLook = 1;
	public const int SecondLook = 2;

	/// <summary>How its fill's colour moves; none for one this version doesn't know.</summary>
	[JsonIgnore]
	public Sticker.Motion AnimationIs => Animation is not null && Enum.TryParse<Sticker.Motion>( Animation, false, out var motion ) && Enum.IsDefined( typeof( Sticker.Motion ), motion )
		? motion
		: Sticker.Motion.None;

	/// <summary>The id of the drawn shape it is, or null for a standard one.</summary>
	[JsonIgnore]
	public string DrawnShapeId => DrawnShape.IsReference( Shape ) ? Shape[ 1.. ] : null;

	/// <summary>Its standard shape; null for a drawn one or a name this version doesn't know.</summary>
	[JsonIgnore]
	public StandardShape? StandardIs => Enum.TryParse<StandardShape>( Shape, false, out var shape ) && Enum.IsDefined( shape ) ? shape : null;

	/// <summary>The colours its animation goes through: its own palette or the game's five, the scanner's highlight then rest.</summary>
	public IReadOnlyList<string> CycleColours() => new Sticker { Animation = Animation, Palette = Palette }.CycleColours();

	/// <summary>
	/// Where a point of its shape (<paramref name="u"/>, <paramref name="v"/>, on the shape's own square) is on the emblem:
	/// flipped, scaled, turned about its middle, then moved, as the layer says.
	/// </summary>
	public (float X, float Y) Place( float u, float v )
	{
		var (sin, cos) = MathF.SinCos( Rotation * MathF.PI / 180f );
		var x = (FlipX ? -u : u) * ScaleX;
		var y = (FlipY ? -v : v) * ScaleY;
		return (x * cos - y * sin + X, x * sin + y * cos + Y);
	}

	/// <summary>Where a point on the emblem (<paramref name="x"/>, <paramref name="y"/>) is on the shape's own square: <see cref="Place"/> undone.</summary>
	public (float U, float V) Unplace( float x, float y )
	{
		var (sin, cos) = MathF.SinCos( Rotation * MathF.PI / 180f );
		var (dx, dy) = (x - X, y - Y);
		var u = (dx * cos + dy * sin) / NonZero( ScaleX );
		var v = (-dx * sin + dy * cos) / NonZero( ScaleY );
		return (FlipX ? -u : u, FlipY ? -v : v);
	}

	/// <summary>How much wider its lines are drawn on the emblem than on its own square.</summary>
	[JsonIgnore]
	public float WidthScale => MathF.Sqrt( MathF.Abs( ScaleX * ScaleY ) );

	/// <summary>A copy of it, to change apart from it.</summary>
	public EmblemLayer Copy() => new()
	{
		Shape = Shape, X = X, Y = Y, Rotation = Rotation, ScaleX = ScaleX, ScaleY = ScaleY, FlipX = FlipX, FlipY = FlipY, Color = Color, Opacity = Opacity,
		OutlineColor = OutlineColor, OutlineWidth = OutlineWidth, Animation = Animation, Palette = Palette is null ? null : new List<string>( Palette ),
		AnimationSpeed = AnimationSpeed, Hidden = Hidden, Locked = Locked, Group = Group, Eraser = Eraser, Side = Side,
	};

	/// <summary>
	/// A copy within bounds, or null for a shape this version doesn't know (or a drawn one <paramref name="drawn"/> says the
	/// emblem doesn't carry, giving the reference it does carry it by): numbers that aren't numbers put back to where they
	/// start, every number held to its range and steps, colours valid hex colours (a palette keeps only those, at most
	/// <see cref="EffectLayer.MaxPalette"/>, and only for a palette animation), and an animation this version doesn't know none.
	/// </summary>
	public EmblemLayer Sanitized( Func<string, string> drawn )
	{
		var shape = DrawnShape.IsReference( Shape ) ? drawn?.Invoke( Shape ) : StandardIs?.ToString();
		if ( shape is null )
			return null;

		var motion = AnimationIs;
		var palette = new List<string>();
		foreach ( var colour in Palette ?? new() )
		{
			if ( palette.Count < EffectLayer.MaxPalette && PlayerLook.IsColour( colour ) )
				palette.Add( colour.ToLowerInvariant() );
		}

		return new EmblemLayer
		{
			Shape = shape,
			X = Stepped( Held( X, 0f, -MaxReach, MaxReach ), PlaceStep ),
			Y = Stepped( Held( Y, 0f, -MaxReach, MaxReach ), PlaceStep ),
			Rotation = Stepped( Held( Rotation, 0f, -MaxTurn, MaxTurn ), TurnStep ),
			ScaleX = Stepped( Held( ScaleX, 1f, MinScale, MaxScale ), PlaceStep ),
			ScaleY = Stepped( Held( ScaleY, 1f, MinScale, MaxScale ), PlaceStep ),
			FlipX = FlipX,
			FlipY = FlipY,
			Color = PlayerLook.IsColour( Color ) ? Color.ToLowerInvariant() : White,
			Opacity = Stepped( Held( Opacity, 1f, 0f, 1f ), PlaceStep ),
			OutlineColor = PlayerLook.IsColour( OutlineColor ) ? OutlineColor.ToLowerInvariant() : Black,
			OutlineWidth = Stepped( Held( OutlineWidth, 0f, 0f, Sticker.MaxOutline ), PlaceStep ),
			Animation = motion == Sticker.Motion.None ? null : motion.ToString(),
			Palette = palette.Count > 0 && motion is Sticker.Motion.Rain or Sticker.Motion.Cycl or Sticker.Motion.Cyln ? palette : null,
			AnimationSpeed = Stepped( Held( AnimationSpeed, 1f, 0f, Sticker.MaxSpeed ), PlaceStep ),
			Hidden = Hidden,
			Locked = Locked,
			Group = Math.Clamp( Group, 0, MaxPerEmblem ),
			Eraser = Eraser,
			Side = Side is FirstLook or SecondLook ? Side : BothLooks,
		};
	}

	private static float Held( float value, float otherwise, float min, float max ) => float.IsFinite( value ) ? Math.Clamp( value, min, max ) : otherwise;

	private static float Stepped( float value, float steps ) => MathF.Round( value * steps ) / steps;

	// A scale is never 0 (see MinScale); one read before it was held is taken as the smallest
	private static float NonZero( float scale ) => MathF.Abs( scale ) < MinScale ? MathF.CopySign( MinScale, scale == 0f ? 1f : scale ) : scale;
}
