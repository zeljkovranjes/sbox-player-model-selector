using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// One property of a layer, an effect, a shape or an emblem layer: its name and type, the range a write is held to
/// (a light's own for its size and brightness), whether an event or particle function may change it while the effect
/// runs (<see cref="Live"/>), and a value for its example.
/// </summary>
public sealed record ScriptProperty(
	PropertyId Id, TypeKind Owner, string Name, ScriptType Type, string Doc, double Min = double.NaN, double Max = double.NaN,
	bool Live = false, bool ReadOnly = false, double LightMin = double.NaN, double LightMax = double.NaN,
	string ExampleValue = null, string ExampleResult = null );

/// <summary>
/// Every property scripts reach on layers, effects, shapes and emblem layers: one table that the library's entries, the
/// builder and the machine's live layer block (<see cref="LiveOffset"/>) are all made from.
/// </summary>
public static class ScriptProperties
{
	private static readonly ScriptType Particle = ScriptType.FunctionOf( new[] { ScriptType.Particle }, ScriptType.Void );
	private static readonly ScriptType Colors = ScriptType.ListOf( ScriptType.Color );

	public static readonly IReadOnlyList<ScriptProperty> All = new ScriptProperty[]
	{
		new( PropertyId.LayerKind, TypeKind.Layer, "Kind", ScriptEnums.Kind.Type, "What the layer is: Sprite(), Light() or Trail() made it.", ReadOnly: true, ExampleResult: "Kind.Sprite" ),
		new( PropertyId.LayerOff, TypeKind.Layer, "Off", ScriptType.Bool, "Whether it is switched off: kept, but not shown.", Live: true, ExampleValue: "true", ExampleResult: "true" ),
		new( PropertyId.LayerJoint, TypeKind.Layer, "Joint", ScriptEnums.Joint.Type, "The joint it comes from, in place of its effect's.", ExampleValue: "Joint.LeftHand", ExampleResult: "Joint.LeftHand" ),
		new( PropertyId.LayerComesFrom, TypeKind.Layer, "ComesFrom", ScriptEnums.ComesFrom.Type, "Where its sprites come from around its place (\"Comes from\").", ExampleValue: "ComesFrom.Sphere", ExampleResult: "ComesFrom.Sphere" ),
		new( PropertyId.LayerSpread, TypeKind.Layer, "Spread", ScriptType.Number, "How far that reaches (its radius).", 0, EffectLayer.MaxSpread, Live: true, ExampleValue: "6", ExampleResult: "6" ),
		new( PropertyId.LayerOffset, TypeKind.Layer, "Offset", ScriptType.Vector3, "How far it sits from its joint: forwards (X), to the body's left (Y) and up (Z).", -EffectLayer.MaxOffset, EffectLayer.MaxOffset, Live: true, ExampleValue: "Vector3( 3, 0, 1 )", ExampleResult: "Vector3( 3, 0, 1 )" ),
		new( PropertyId.LayerFacing, TypeKind.Layer, "Facing", ScriptEnums.Facing.Type, "The way it faces: where a cone opens, a line runs, a shape looks.", ExampleValue: "Facing.Up", ExampleResult: "Facing.Up" ),
		new( PropertyId.LayerAngle, TypeKind.Layer, "Angle", ScriptType.Number, "How wide a cone opens, in degrees from its middle.", 0, EffectLayer.MaxAngle, Live: true, ExampleValue: "30", ExampleResult: "30" ),
		new( PropertyId.LayerToJoint, TypeKind.Layer, "ToJoint", ScriptEnums.Joint.Type, "The joint a line runs to (ComesFrom.Line).", ExampleValue: "Joint.RightHand", ExampleResult: "Joint.RightHand" ),
		new( PropertyId.LayerFormation, TypeKind.Layer, "Formation", ScriptType.Shape, "The shape sprites come from (ComesFrom.Shape): a standard one or one the script made.", ExampleValue: "Shapes.Heart", ExampleResult: "Shapes.Heart" ),
		new( PropertyId.LayerFill, TypeKind.Layer, "Fill", ScriptType.Bool, "Whether sprites come from all over the shape rather than along its outline.", ExampleValue: "true", ExampleResult: "true" ),
		new( PropertyId.LayerLooksLike, TypeKind.Layer, "LooksLike", ScriptType.Picture, "What each sprite looks like (\"Looks like\"): a picture or a shape.", ExampleValue: "Picture.Flame", ExampleResult: "Picture.Flame" ),
		new( PropertyId.LayerColor, TypeKind.Layer, "Color", ScriptType.Color, "Its colour: a sprite's when it appears, the light's, a trail's where it starts.", 0, 1, Live: true, ExampleValue: "Color.Parse( \"#ff8800\" )", ExampleResult: "#ff8800" ),
		new( PropertyId.LayerColorEnd, TypeKind.Layer, "ColorEnd", ScriptType.Color, "A sprite's colour as it fades away (a trail's at its end).", 0, 1, Live: true, ExampleValue: "Color.Red", ExampleResult: "#ff0000" ),
		new( PropertyId.LayerColorMode, TypeKind.Layer, "ColorMode", ScriptEnums.ColorMode.Type, "How its sprites are coloured: fading, a World at War cycle over the palette, or the palette in turn.", ExampleValue: "ColorMode.Rain", ExampleResult: "ColorMode.Rain" ),
		new( PropertyId.LayerPalette, TypeKind.Layer, "Palette", Colors, "The colours a colour cycle goes through; the game's five when none are given.", ExampleValue: "{ Color.Red, Color.Blue }", ExampleResult: "{ #ff0000, #0000ff }" ),
		new( PropertyId.LayerSize, TypeKind.Layer, "Size", ScriptType.Number, "A sprite's size when it appears, how far a light reaches, how wide a trail starts.", EffectLayer.MinSize, EffectLayer.MaxSize, Live: true, LightMin: EffectLayer.MinReach, LightMax: EffectLayer.MaxReach, ExampleValue: "3", ExampleResult: "3" ),
		new( PropertyId.LayerSizeEnd, TypeKind.Layer, "SizeEnd", ScriptType.Number, "A sprite's size as it fades away (a trail's width at its end).", EffectLayer.MinSize, EffectLayer.MaxSize, Live: true, ExampleValue: "0.5", ExampleResult: "0.5" ),
		new( PropertyId.LayerSpeed, TypeKind.Layer, "Speed", ScriptType.Number, "How fast sprites leave their shape, outwards.", 0, EffectLayer.MaxSpeed, Live: true, ExampleValue: "20", ExampleResult: "20" ),
		new( PropertyId.LayerGravity, TypeKind.Layer, "Gravity", ScriptType.Number, "How hard sprites fall; negative rises.", -EffectLayer.MaxGravity, EffectLayer.MaxGravity, Live: true, ExampleValue: "-40", ExampleResult: "-40" ),
		new( PropertyId.LayerLifetime, TypeKind.Layer, "Lifetime", ScriptType.Number, "How long a sprite lives, or a trail stays behind, in seconds.", EffectLayer.MinLifetime, EffectLayer.MaxLifetime, Live: true, ExampleValue: "1.5", ExampleResult: "1.5" ),
		new( PropertyId.LayerAmount, TypeKind.Layer, "Amount", ScriptType.Number, "How many sprites appear a second, or how bright a light is.", 0, EffectLayer.MaxRate, Live: true, LightMin: 0, LightMax: EffectLayer.MaxBrightness, ExampleValue: "25", ExampleResult: "25" ),
		new( PropertyId.LayerFollow, TypeKind.Layer, "Follow", ScriptType.Bool, "Whether sprites move with the body once out, rather than staying where they appeared.", ExampleValue: "true", ExampleResult: "true" ),
		new( PropertyId.LayerSolid, TypeKind.Layer, "Solid", ScriptType.Bool, "Whether sprites cover what is behind them (smoke, hearts) instead of glowing over it.", ExampleValue: "true", ExampleResult: "true" ),
		new( PropertyId.LayerRhythm, TypeKind.Layer, "Rhythm", ScriptEnums.Rhythm.Type, "How it changes over time.", ExampleValue: "Rhythm.Pulse", ExampleResult: "Rhythm.Pulse" ),
		new( PropertyId.LayerRhythmSpeed, TypeKind.Layer, "RhythmSpeed", ScriptType.Number, "How many times a second the rhythm beats (turns, for Spin).", 0, EffectLayer.MaxRhythmSpeed, Live: true, ExampleValue: "2", ExampleResult: "2" ),
		new( PropertyId.LayerRhythmAmount, TypeKind.Layer, "RhythmAmount", ScriptType.Number, "How much the rhythm changes it.", 0, 1, Live: true, ExampleValue: "0.5", ExampleResult: "0.5" ),
		new( PropertyId.LayerWhen, TypeKind.Layer, "When", ScriptEnums.When.Type, "When it shows: always, while something is so, or for a moment when something happens.", ExampleValue: "When.OnLand", ExampleResult: "When.OnLand" ),
		new( PropertyId.LayerBurst, TypeKind.Layer, "Burst", ScriptType.Number, "How many sprites appear at once when its moment comes (When.On...).", 0, EffectLayer.MaxBurst, ExampleValue: "30", ExampleResult: "30" ),
		new( PropertyId.LayerShowFor, TypeKind.Layer, "ShowFor", ScriptType.Number, "How long it shows after its moment, in seconds.", 0, EffectLayer.MaxShowFor, ExampleValue: "0.5", ExampleResult: "0.5" ),
		new( PropertyId.LayerFadeWithSpeed, TypeKind.Layer, "FadeWithSpeed", ScriptType.Bool, "Whether it shows only as much as its place moves (none standing still).", ExampleValue: "true", ExampleResult: "true" ),
		new( PropertyId.LayerParticle, TypeKind.Layer, "Particle", Particle, "The function run for each of its sprites each frame: function layer.Particle( p ) ... end." ),

		new( PropertyId.EffectName, TypeKind.Effect, "Name", ScriptType.String, "What the effect is called.", ExampleValue: "\"Sparks\"", ExampleResult: "\"Sparks\"" ),
		new( PropertyId.EffectKey, TypeKind.Effect, "Key", ScriptType.Key, "The key that shows it for a moment each press (one key start every 2 minutes per player).", ExampleValue: "\"g\"", ExampleResult: "\"g\"" ),
		new( PropertyId.EffectOff, TypeKind.Effect, "Off", ScriptType.Bool, "Whether it is switched off: kept with the look, not shown.", ExampleValue: "true", ExampleResult: "true" ),
		new( PropertyId.EffectAnchor, TypeKind.Effect, "Anchor", ScriptEnums.Anchor.Type, "What it comes from: a joint, an item worn (Effect.Item) or the weapon held.", ExampleValue: "Anchor.HeldWeapon", ExampleResult: "Anchor.HeldWeapon" ),
		new( PropertyId.EffectJoint, TypeKind.Effect, "Joint", ScriptEnums.Joint.Type, "The joint it comes from (for an item or a weapon, the joint used while that isn't there).", ExampleValue: "Joint.Chest", ExampleResult: "Joint.Chest" ),
		new( PropertyId.EffectItem, TypeKind.Effect, "Item", ScriptType.String, "The worn item it comes from, for Anchor.Item.", ExampleValue: "\"models/hat.vmdl\"", ExampleResult: "\"models/hat.vmdl\"" ),
		new( PropertyId.EffectOffset, TypeKind.Effect, "Offset", ScriptType.Vector3, "How far it is moved from where it comes from: forwards, left and up.", -EffectLayer.MaxOffset, EffectLayer.MaxOffset, ExampleValue: "Vector3( 0, 0, 4 )", ExampleResult: "Vector3( 0, 0, 4 )" ),
		new( PropertyId.EffectHue, TypeKind.Effect, "Hue", ScriptType.Number, "The colour slider: how far round the colour wheel every colour is turned.", 0, 1, ExampleValue: "0.5", ExampleResult: "0.5" ),
		new( PropertyId.EffectSize, TypeKind.Effect, "Size", ScriptType.Number, "The size slider: -1 half, 0 as made, 1 twice the size.", -1, 1, ExampleValue: "0.5", ExampleResult: "0.5" ),
		new( PropertyId.EffectAmount, TypeKind.Effect, "Amount", ScriptType.Number, "The amount slider: -1 a quarter, 1 four times the sprites (and light).", -1, 1, ExampleValue: "-0.5", ExampleResult: "-0.5" ),
		new( PropertyId.EffectSpeed, TypeKind.Effect, "Speed", ScriptType.Number, "The speed slider: -1 half, 1 twice as fast.", -1, 1, ExampleValue: "0.25", ExampleResult: "0.25" ),

		new( PropertyId.ShapeFillColor, TypeKind.Shape, "FillColor", ScriptType.Color, "A sticker's fill colour (setting any sticker property makes the shape a sticker).", 0, 1, ExampleValue: "Color.Red", ExampleResult: "#ff0000" ),
		new( PropertyId.ShapeOutlineColor, TypeKind.Shape, "OutlineColor", ScriptType.Color, "A sticker's outline colour.", 0, 1, ExampleValue: "Color.Black", ExampleResult: "#000000" ),
		new( PropertyId.ShapeOutlineWidth, TypeKind.Shape, "OutlineWidth", ScriptType.Number, "How wide a sticker's outline is, on the shape's square (2 across).", 0, Sticker.MaxOutline, ExampleValue: "0.1", ExampleResult: "0.1" ),
		new( PropertyId.ShapeAnimation, TypeKind.Shape, "Animation", ScriptEnums.Animation.Type, "How a sticker's fill colour moves.", ExampleValue: "Animation.Rain", ExampleResult: "Animation.Rain" ),
		new( PropertyId.ShapePalette, TypeKind.Shape, "Palette", Colors, "The colours a sticker's colour cycle goes through.", ExampleValue: "{ Color.Red, Color.Yellow }", ExampleResult: "{ #ff0000, #ffff00 }" ),
		new( PropertyId.ShapeAnimationSpeed, TypeKind.Shape, "AnimationSpeed", ScriptType.Number, "How fast a sticker's animation runs: 1 is the game's own pace.", 0, Sticker.MaxSpeed, ExampleValue: "2", ExampleResult: "2" ),
		new( PropertyId.ShapePointCount, TypeKind.Shape, "PointCount", ScriptType.Number, "How many points the shape has so far (at most 500).", ReadOnly: true, ExampleResult: "0" ),

		new( PropertyId.EmblemShape, TypeKind.EmblemLayer, "Shape", ScriptType.Shape, "The shape the emblem layer draws.", ExampleValue: "Shapes.Heart", ExampleResult: "Shapes.Heart" ),
		new( PropertyId.EmblemPosition, TypeKind.EmblemLayer, "Position", ScriptType.Vector3, "Where its middle is on the emblem's square (X right, Y up, -1 to 1; Z unused).", -1, 1, ExampleValue: "Vector3( 0.5, -0.25, 0 )", ExampleResult: "Vector3( 0.5, -0.25, 0 )" ),
		new( PropertyId.EmblemRotation, TypeKind.EmblemLayer, "Rotation", ScriptType.Number, "How far it is turned, in degrees, anticlockwise.", -360, 360, ExampleValue: "45", ExampleResult: "45" ),
		new( PropertyId.EmblemScale, TypeKind.EmblemLayer, "Scale", ScriptType.Vector3, "How big it is across (X) and up (Y): 1 fills the square; Z unused.", 0.01, 4, ExampleValue: "Vector3( 0.5, 0.8, 1 )", ExampleResult: "Vector3( 0.5, 0.8, 1 )" ),
		new( PropertyId.EmblemFlipX, TypeKind.EmblemLayer, "FlipX", ScriptType.Bool, "Whether it is flipped left to right.", ExampleValue: "true", ExampleResult: "true" ),
		new( PropertyId.EmblemFlipY, TypeKind.EmblemLayer, "FlipY", ScriptType.Bool, "Whether it is flipped top to bottom.", ExampleValue: "true", ExampleResult: "true" ),
		new( PropertyId.EmblemColor, TypeKind.EmblemLayer, "Color", ScriptType.Color, "Its fill colour.", 0, 1, ExampleValue: "Color.Yellow", ExampleResult: "#ffff00" ),
		new( PropertyId.EmblemOpacity, TypeKind.EmblemLayer, "Opacity", ScriptType.Number, "How much it covers what is below it: 0 not at all, 1 fully.", 0, 1, ExampleValue: "0.75", ExampleResult: "0.75" ),
		new( PropertyId.EmblemOutlineColor, TypeKind.EmblemLayer, "OutlineColor", ScriptType.Color, "Its outline's colour.", 0, 1, ExampleValue: "Color.Black", ExampleResult: "#000000" ),
		new( PropertyId.EmblemOutlineWidth, TypeKind.EmblemLayer, "OutlineWidth", ScriptType.Number, "How wide its outline is; 0 for none.", 0, Sticker.MaxOutline, ExampleValue: "0.05", ExampleResult: "0.05" ),
		new( PropertyId.EmblemAnimation, TypeKind.EmblemLayer, "Animation", ScriptEnums.Animation.Type, "How its colour moves.", ExampleValue: "Animation.Rainbow", ExampleResult: "Animation.Rainbow" ),
		new( PropertyId.EmblemPalette, TypeKind.EmblemLayer, "Palette", Colors, "The colours its colour cycle goes through.", ExampleValue: "{ Color.Cyan, Color.Magenta }", ExampleResult: "{ #00ffff, #ff00ff }" ),
		new( PropertyId.EmblemAnimationSpeed, TypeKind.EmblemLayer, "AnimationSpeed", ScriptType.Number, "How fast its animation runs: 1 is the game's own pace.", 0, Sticker.MaxSpeed, ExampleValue: "0.5", ExampleResult: "0.5" ),
		new( PropertyId.EmblemHidden, TypeKind.EmblemLayer, "Hidden", ScriptType.Bool, "Whether it is hidden: kept, not drawn.", ExampleValue: "true", ExampleResult: "true" ),
	};

	/// <summary>Where each layer property starts in a layer's live block, and how big a block is.</summary>
	public static readonly int[] LiveOffset = LiveLayout( out LiveBlockSize );
	public static readonly int LiveBlockSize;

	/// <summary>The property <paramref name="id"/>.</summary>
	public static ScriptProperty Of( PropertyId id ) => All[ (int)id ];

	private static int[] LiveLayout( out int size )
	{
		var offsets = new int[ (int)PropertyId.Total ];
		size = 0;
		foreach ( var property in All )
		{
			if ( property.Owner != TypeKind.Layer )
				continue;

			offsets[ (int)property.Id ] = size;
			size += property.Type.Width;
		}

		return offsets;
	}
}
