using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PlayerModels.Core;

/// <summary>What a layer of an effect is: sprites (flames, sparks, hearts), a light, or a trail left behind as it moves.</summary>
public enum LayerKind { Sprite, Light, Trail }

/// <summary>
/// Where a layer's sprites come from, around its place: one point, a ball, a flat ring, all around the body (an upright
/// ball as tall as the body, wherever the layer sits), a box, a cone opening the way the layer faces, a line (to another
/// joint, or across the way it faces), a standard shape (see <see cref="StandardShape"/>), or along the last shot (from
/// the layer's place to where the shot hit, or as far as it went: a bullet tracer).
/// </summary>
public enum LayerShape { Point, Sphere, Ring, AroundBody, Box, Cone, Line, Shape, Shot }

/// <summary>
/// What a sprite looks like: the curated pictures every client draws for itself from lines (see <see cref="EffectShape"/>).
/// Saved by name, so pictures and shapes added later join without changing what is saved; a standard shape's name (see
/// <see cref="StandardShape"/>) is a picture too.
/// </summary>
public enum LayerPicture { Glow, Spark, Flame, Smoke, Sparkle, Heart, Snowflake, Droplet, Slash }

/// <summary>
/// Which way a layer's sprites face: the camera, as most do, or the plane a swing sweeps through (worked out from where
/// the player aims and the way the weapon's tip moves when they swing), so a slash lies along the swing.
/// </summary>
public enum LayerAlign { Camera, Swing }

/// <summary>
/// How a layer changes over time: not at all, brighter and dimmer, flickering, through every colour, turning round its
/// joint (so a layer set off from it circles it), or wobbling about its place.
/// </summary>
public enum LayerRhythm { Steady, Pulse, Flicker, Rainbow, Spin, Jiggle }

/// <summary>
/// How a layer's sprites are coloured: each fading from its colour to its end colour; one by one in the order they
/// appear, by one of World at War's clan tag animations (see <see cref="ColorCycle"/>) over a palette; or each one of the
/// palette's colours in turn, kept for its life (confetti).
/// </summary>
public enum LayerColorMode { Fixed, Rain, Cycl, Cyln, Mix }

/// <summary>The way a layer faces from its joint: where a cone opens, a line runs and a shape looks.</summary>
public enum LayerFacing { Forward, Up, Down, Back, Left, Right }

/// <summary>
/// When a layer shows: always; while its wearer moves, stands on the ground, is in the air, crouches or holds a weapon;
/// or for a moment when something happens (the events effects share, by the names scripts will use): they jump, land,
/// get hurt, start or stop moving, crouch or stand up, shoot or swing (attack), hit something hard, reload, draw a weapon
/// or put it away, their shot or swing hits something, they die or spawn again, or a foot steps (on a foot, that foot).
/// </summary>
public enum LayerWhen
{
	Always, Moving, Grounded, InAir, Crouching, HoldingWeapon,
	OnJump, OnLand, OnHurt, OnStartMoving, OnStopMoving, OnCrouch, OnUncrouch, OnShoot, OnImpact,
	OnReload, OnEquip, OnHolster, OnHit, OnDeath, OnRespawn, OnFootstep,
}

/// <summary>
/// One layer of an <see cref="Effect"/>: sprites coming from a shape on the body, a light there, or a trail it leaves.
/// Sizes and distances are for the citizen; a body of another size gets them in proportion. Saved under short names,
/// without what is at its default, so a look full of effects stays small. Kinds, shapes and pictures are saved by name: a
/// layer of a kind this version doesn't know (added later) is dropped when it is read, never the look.
/// </summary>
public sealed class EffectLayer
{
	/// <summary>How far a layer's shape reaches (its radius), and how far it can sit from its joint, along each axis.</summary>
	public const float MaxSpread = 48f;
	public const float MaxOffset = 24f;

	/// <summary>The size of a sprite, across, or how wide a trail is.</summary>
	public const float MinSize = 0.1f;
	public const float MaxSize = 48f;

	/// <summary>How far a light reaches.</summary>
	public const float MinReach = 8f;
	public const float MaxReach = 400f;

	/// <summary>How fast sprites leave their shape, and how hard they fall (rise when negative), in units a second.</summary>
	public const float MaxSpeed = 200f;
	public const float MaxGravity = 400f;

	/// <summary>How long a sprite lives, or how long a trail stays behind, in seconds.</summary>
	public const float MinLifetime = 0.1f;
	public const float MaxLifetime = 5f;

	/// <summary>How many sprites a second, and how bright a light is.</summary>
	public const float MaxRate = 100f;
	public const float MaxBrightness = 4f;

	/// <summary>How many times a second a rhythm beats (or turns, for <see cref="LayerRhythm.Spin"/>).</summary>
	public const float MaxRhythmSpeed = 10f;

	/// <summary>How wide a cone opens, in degrees from its middle.</summary>
	public const float MaxAngle = 45f;

	/// <summary>How many sprites appear at once when the layer's moment comes (see <see cref="When"/>).</summary>
	public const float MaxBurst = 100f;

	/// <summary>How long a layer shows after its moment, in seconds.</summary>
	public const float MaxShowFor = 5f;

	/// <summary>The most colours a palette of the player's own has.</summary>
	public const int MaxPalette = 32;

	/// <summary>How far a sprite turns over its life, either way, in degrees.</summary>
	public const float MaxSweep = 720f;

	/// <summary>How far round a slash reaches, and how thick it is (a share of its radius).</summary>
	public const float MinSlashArc = 30f;
	public const float MaxSlashArc = 330f;
	public const float MinSlashWidth = 0.05f;
	public const float MaxSlashWidth = 0.6f;
	public const float SlashArc = 150f;
	public const float SlashWidth = 0.25f;

	private const string White = "#ffffff";

	/// <summary>What the layer is, a <see cref="LayerKind"/> by name.</summary>
	[JsonPropertyName( "k" )]
	public string Kind { get; set; } = nameof( LayerKind.Sprite );

	/// <summary>Whether it is switched off: kept with its effect, but not shown.</summary>
	[JsonPropertyName( "off" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public bool Off { get; set; }

	/// <summary>The <see cref="Effect.Joint"/> it comes from, by name, in place of its effect's; null for its effect's.</summary>
	[JsonPropertyName( "j" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Joint { get; set; }

	/// <summary>Its <see cref="LayerShape"/> by name; null for a point.</summary>
	[JsonPropertyName( "s" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Shape { get; set; }

	/// <summary>How far the shape reaches from the layer's place.</summary>
	[JsonPropertyName( "r" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Spread { get; set; }

	/// <summary>How far it sits from its joint: forwards, to the body's left, and up.</summary>
	[JsonPropertyName( "f" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Forward { get; set; }

	/// <inheritdoc cref="Forward"/>
	[JsonPropertyName( "l" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Left { get; set; }

	/// <inheritdoc cref="Forward"/>
	[JsonPropertyName( "u" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Up { get; set; }

	/// <summary>Its <see cref="LayerFacing"/> by name; null for forwards.</summary>
	[JsonPropertyName( "fc" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Facing { get; set; }

	/// <summary>How wide a cone opens, in degrees from its middle.</summary>
	[JsonPropertyName( "an" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Angle { get; set; }

	/// <summary>The <see cref="Effect.Joint"/> a line runs to, by name; null for a line across the way it faces.</summary>
	[JsonPropertyName( "jt" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string ToJoint { get; set; }

	/// <summary>
	/// The <see cref="StandardShape"/> sprites come from, by name, for a layer from a shape; or a drawn shape the look
	/// carries (see <see cref="DrawnShape.Reference"/>).
	/// </summary>
	[JsonPropertyName( "fm" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Formation { get; set; }

	/// <summary>Whether sprites come from all over the shape (all through a solid) rather than along its outline (its surface).</summary>
	[JsonPropertyName( "fi" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public bool Fill { get; set; }

	/// <summary>
	/// Its sprites' <see cref="LayerPicture"/> or <see cref="StandardShape"/> by name, or a drawn shape the look carries
	/// (see <see cref="DrawnShape.Reference"/>); null for a glow.
	/// </summary>
	[JsonPropertyName( "p" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Picture { get; set; }

	/// <summary>Its colour as <c>#rrggbb</c>: a sprite's when it appears, the light's, or a trail's where it starts.</summary>
	[JsonPropertyName( "c" )]
	public string Color { get; set; } = White;

	/// <summary>A sprite's colour as it fades away (a trail's at its end), as <c>#rrggbb</c>; null to keep <see cref="Color"/>.</summary>
	[JsonPropertyName( "c2" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string ColorEnd { get; set; }

	/// <summary>Its <see cref="LayerColorMode"/> by name; null for fixed colours.</summary>
	[JsonPropertyName( "cm" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string ColorMode { get; set; }

	/// <summary>
	/// The colours a colour cycle goes through, as <c>#rrggbb</c>, any number of them; null for the game's own (see
	/// <see cref="CycleColours"/>).
	/// </summary>
	[JsonPropertyName( "pal" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<string> Palette { get; set; }

	/// <summary>A sprite's size when it appears, how far the light reaches, or how wide a trail is where it starts.</summary>
	[JsonPropertyName( "a" )]
	public float Size { get; set; } = 2f;

	/// <summary>A sprite's size as it fades away (a trail's width at its end); null to keep <see cref="Size"/>.</summary>
	[JsonPropertyName( "b" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public float? SizeEnd { get; set; }

	/// <summary>How fast sprites leave the shape, outwards from its middle.</summary>
	[JsonPropertyName( "v" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Speed { get; set; }

	/// <summary>How hard sprites fall; negative rises.</summary>
	[JsonPropertyName( "g" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Gravity { get; set; }

	/// <summary>How long a sprite lives, or how long a trail stays behind, in seconds.</summary>
	[JsonPropertyName( "t" )]
	public float Lifetime { get; set; } = 1f;

	/// <summary>How many sprites appear a second, or how bright the light is.</summary>
	[JsonPropertyName( "n" )]
	public float Amount { get; set; } = 10f;

	/// <summary>Whether sprites move with the body once out; otherwise they stay where they appeared, trailing behind.</summary>
	[JsonPropertyName( "fo" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public bool Follow { get; set; }

	/// <summary>Whether sprites (or a trail) cover what is behind them (smoke, hearts) instead of glowing over it.</summary>
	[JsonPropertyName( "so" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public bool Solid { get; set; }

	/// <summary>Its <see cref="LayerRhythm"/> by name; null for steady.</summary>
	[JsonPropertyName( "rh" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Rhythm { get; set; }

	/// <summary>How many times a second the rhythm beats.</summary>
	[JsonPropertyName( "rs" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float RhythmSpeed { get; set; }

	/// <summary>How much the rhythm changes it, 0 to 1.</summary>
	[JsonPropertyName( "ra" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float RhythmAmount { get; set; }

	/// <summary>When it shows, a <see cref="LayerWhen"/> by name; null for always.</summary>
	[JsonPropertyName( "wh" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string When { get; set; }

	/// <summary>How many sprites appear at once when its moment comes (see <see cref="When"/>).</summary>
	[JsonPropertyName( "bu" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Burst { get; set; }

	/// <summary>How long it shows after its moment, in seconds: sprites keep coming, a light shines, a trail is drawn.</summary>
	[JsonPropertyName( "sf" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float ShowFor { get; set; }

	/// <summary>Whether it shows only as much as its place moves: none standing still, fully at a run or a swing.</summary>
	[JsonPropertyName( "fs" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public bool FadeWithSpeed { get; set; }

	/// <summary>
	/// Whether it shows where the last shot hit, facing out of what it hit (impact sparks), rather than at its place;
	/// only for a layer shown when a shot is fired or hits.
	/// </summary>
	[JsonPropertyName( "ah" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public bool AtHit { get; set; }

	/// <summary>Its <see cref="LayerAlign"/> by name; null for facing the camera.</summary>
	[JsonPropertyName( "al" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public string Align { get; set; }

	/// <summary>How far each sprite turns over its life, in degrees (a slash sweeping round); 0 for none.</summary>
	[JsonPropertyName( "sw" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float Sweep { get; set; }

	/// <summary>How far round a <see cref="LayerPicture.Slash"/> reaches, in degrees.</summary>
	[JsonPropertyName( "sa" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float SlashArcDegrees { get; set; }

	/// <summary>How thick a <see cref="LayerPicture.Slash"/> is at its middle, a share of its radius.</summary>
	[JsonPropertyName( "sk" ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public float SlashThickness { get; set; }

	/// <summary>Which way its sprites face.</summary>
	[JsonIgnore]
	public LayerAlign AlignIs => Parsed( Align, LayerAlign.Camera );

	/// <summary>A slash's reach and thickness as drawn: its own, or the usual ones.</summary>
	[JsonIgnore]
	public (float Arc, float Width) SlashShape => (SlashArcDegrees > 0f ? SlashArcDegrees : SlashArc, SlashThickness > 0f ? SlashThickness : SlashWidth);

	/// <summary>What the layer is; only right on a layer within bounds (see <see cref="Sanitized"/>).</summary>
	[JsonIgnore]
	public LayerKind KindIs => Parsed( Kind, LayerKind.Sprite );

	/// <summary>Where its sprites come from.</summary>
	[JsonIgnore]
	public LayerShape ShapeIs => Parsed( Shape, LayerShape.Point );

	/// <summary>What its sprites look like, when it is one of the curated pictures (see <see cref="PictureShape"/> for a shape).</summary>
	[JsonIgnore]
	public LayerPicture PictureIs => Parsed( Picture, LayerPicture.Glow );

	/// <summary>The standard shape its sprites look like, or null for a curated picture.</summary>
	[JsonIgnore]
	public StandardShape? PictureShape => Picture is not null && !Known<LayerPicture>( Picture ) && Known<StandardShape>( Picture )
		? Parsed( Picture, StandardShape.Circle )
		: null;

	/// <summary>How it changes over time.</summary>
	[JsonIgnore]
	public LayerRhythm RhythmIs => Parsed( Rhythm, LayerRhythm.Steady );

	/// <summary>How its sprites are coloured.</summary>
	[JsonIgnore]
	public LayerColorMode ColorModeIs => Parsed( ColorMode, LayerColorMode.Fixed );

	/// <summary>The way it faces.</summary>
	[JsonIgnore]
	public LayerFacing FacingIs => Parsed( Facing, LayerFacing.Forward );

	/// <summary>The standard shape its sprites come from, for a layer from a shape (a circle for a drawn one).</summary>
	[JsonIgnore]
	public StandardShape FormationIs => Parsed( Formation, StandardShape.Circle );

	/// <summary>The id of the drawn shape its sprites come from, or look like; null for none.</summary>
	[JsonIgnore]
	public string DrawnFormation => DrawnShape.IsReference( Formation ) ? Formation[ 1.. ] : null;

	/// <inheritdoc cref="DrawnFormation"/>
	[JsonIgnore]
	public string DrawnPicture => DrawnShape.IsReference( Picture ) ? Picture[ 1.. ] : null;

	/// <summary>When it shows.</summary>
	[JsonIgnore]
	public LayerWhen WhenIs => Parsed( When, LayerWhen.Always );

	/// <summary>Whether it shows for a moment (see <see cref="When"/>) rather than while something is so.</summary>
	[JsonIgnore]
	public bool IsMoment => WhenIs >= LayerWhen.OnJump;

	/// <summary>
	/// The colours its colour cycle goes through: its own palette, or the game's five, and for
	/// <see cref="LayerColorMode.Cyln"/> the highlight then the rest (the game's red and white unless its palette has two).
	/// </summary>
	public IReadOnlyList<string> CycleColours()
	{
		if ( ColorModeIs == LayerColorMode.Cyln )
			return Palette is { Count: >= 2 } ? Palette : new[] { ColorCycle.ScannerHighlight, ColorCycle.ScannerRest };

		return Palette is { Count: > 0 } ? Palette : ColorCycle.GamePalette;
	}

	/// <summary>
	/// How many of its sprites are out at once at most, at <paramref name="amount"/> times its own amount: what it makes
	/// over a lifetime, and a moment's burst on top; a light, a trail or a layer switched off has none.
	/// </summary>
	public float LiveSprites( float amount = 1f ) => KindIs == LayerKind.Sprite && !Off ? (Amount * Lifetime + Burst) * amount : 0f;

	/// <summary>
	/// A copy within bounds, or null for a layer of a kind (or from a joint, shape, picture, rhythm, colour mode, facing or
	/// moment) this version doesn't know: numbers finite and limited, colours valid hex colours (a palette keeps only those,
	/// at most <see cref="MaxPalette"/>), and what is at its default left out. A drawn shape it names is named as
	/// <paramref name="drawn"/> says the look carries it; one the look doesn't carry (or with no <paramref name="drawn"/>)
	/// is a shape it doesn't know.
	/// </summary>
	public EffectLayer Sanitized( Func<string, string> drawn = null )
	{
		var formation = DrawnShape.IsReference( Formation ) ? drawn?.Invoke( Formation ) : Formation;
		var picture = DrawnShape.IsReference( Picture ) ? drawn?.Invoke( Picture ) : Picture;
		if ( formation is null && Formation is not null || picture is null && Picture is not null )
			return null;

		if ( !Known<LayerKind>( Kind ) || Joint is not null && !Known<Effect.Joint>( Joint ) || Shape is not null && !Known<LayerShape>( Shape )
			|| picture is not null && !Known<LayerPicture>( picture ) && !Known<StandardShape>( picture ) && !DrawnShape.IsReference( picture )
			|| Rhythm is not null && !Known<LayerRhythm>( Rhythm ) || ColorMode is not null && !Known<LayerColorMode>( ColorMode )
			|| Facing is not null && !Known<LayerFacing>( Facing ) || ToJoint is not null && !Known<Effect.Joint>( ToJoint )
			|| formation is not null && !Known<StandardShape>( formation ) && !DrawnShape.IsReference( formation )
			|| When is not null && !Known<LayerWhen>( When ) || Align is not null && !Known<LayerAlign>( Align ) )
			return null;

		var palette = new List<string>();
		foreach ( var colourInPalette in Palette ?? new() )
		{
			if ( palette.Count < MaxPalette && PlayerLook.IsColour( colourInPalette ) )
				palette.Add( colourInPalette.ToLowerInvariant() );
		}

		var isLight = Kind == nameof( LayerKind.Light );
		var colour = PlayerLook.IsColour( Color ) ? Color.ToLowerInvariant() : White;
		var colourEnd = PlayerLook.IsColour( ColorEnd ) ? ColorEnd.ToLowerInvariant() : null;
		var size = isLight ? Limited( Size, MinReach, MaxReach, MinReach ) : Limited( Size, MinSize, MaxSize, MinSize );
		var sizeEnd = SizeEnd is { } end && !isLight ? Limited( end, MinSize, MaxSize, size ) : (float?)null;
		var isMoment = When is not null && Parsed( When, LayerWhen.Always ) >= LayerWhen.OnJump;

		return new EffectLayer
		{
			Kind = Kind,
			Off = Off,
			Joint = Joint,
			Shape = Shape == nameof( LayerShape.Point ) ? null : Shape,
			Spread = Limited( Spread, 0f, MaxSpread, 0f ),
			Forward = Limited( Forward, -MaxOffset, MaxOffset, 0f ),
			Left = Limited( Left, -MaxOffset, MaxOffset, 0f ),
			Up = Limited( Up, -MaxOffset, MaxOffset, 0f ),
			Facing = Facing == nameof( LayerFacing.Forward ) ? null : Facing,
			Angle = Limited( Angle, 0f, MaxAngle, 0f ),
			ToJoint = ToJoint,
			Formation = formation,
			Fill = Fill,
			Picture = picture == nameof( LayerPicture.Glow ) ? null : picture,
			Color = colour,
			ColorEnd = colourEnd == colour ? null : colourEnd,
			ColorMode = ColorMode == nameof( LayerColorMode.Fixed ) ? null : ColorMode,
			Palette = palette.Count > 0 ? palette : null,
			Size = size,
			SizeEnd = sizeEnd == size ? null : sizeEnd,
			Speed = Limited( Speed, 0f, MaxSpeed, 0f ),
			Gravity = Limited( Gravity, -MaxGravity, MaxGravity, 0f ),
			Lifetime = Limited( Lifetime, MinLifetime, MaxLifetime, 1f ),
			Amount = isLight ? Limited( Amount, 0f, MaxBrightness, 1f ) : Limited( Amount, 0f, MaxRate, 0f ),
			Follow = Follow,
			Solid = Solid,
			Rhythm = Rhythm == nameof( LayerRhythm.Steady ) ? null : Rhythm,
			RhythmSpeed = Limited( RhythmSpeed, 0f, MaxRhythmSpeed, 0f ),
			RhythmAmount = Limited( RhythmAmount, 0f, 1f, 0f ),
			When = When == nameof( LayerWhen.Always ) ? null : When,
			Burst = isMoment && !isLight ? MathF.Round( Limited( Burst, 0f, MaxBurst, 0f ) ) : 0f,
			ShowFor = isMoment ? Limited( ShowFor, 0f, MaxShowFor, 0f ) : 0f,
			FadeWithSpeed = FadeWithSpeed,
			AtHit = AtHit && When is nameof( LayerWhen.OnShoot ) or nameof( LayerWhen.OnHit ),
			Align = Align == nameof( LayerAlign.Camera ) ? null : Align,
			Sweep = Limited( Sweep, -MaxSweep, MaxSweep, 0f ),
			SlashArcDegrees = picture == nameof( LayerPicture.Slash ) && SlashArcDegrees != 0f ? Limited( SlashArcDegrees, MinSlashArc, MaxSlashArc, SlashArc ) : 0f,
			SlashThickness = picture == nameof( LayerPicture.Slash ) && SlashThickness != 0f ? Limited( SlashThickness, MinSlashWidth, MaxSlashWidth, SlashWidth ) : 0f,
		};
	}

	private static bool Known<TEnum>( string name ) where TEnum : struct, Enum
	{
		return name is not null && Enum.IsDefined( typeof( TEnum ), name );
	}

	private static TEnum Parsed<TEnum>( string name, TEnum otherwise ) where TEnum : struct, Enum
	{
		return name is not null && Enum.TryParse<TEnum>( name, false, out var value ) && Enum.IsDefined( typeof( TEnum ), value ) ? value : otherwise;
	}

	private static float Limited( float value, float least, float most, float otherwise )
	{
		return float.IsFinite( value ) ? Math.Clamp( value, least, most ) : otherwise;
	}
}
