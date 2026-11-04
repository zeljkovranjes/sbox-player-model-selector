using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// One property of a layer, an effect, a shape, an emblem layer or a weapon design's parts: its name and type, the range a write is held to
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
	[Sandbox.SkipHotload]
	private static readonly ScriptType Particle = ScriptType.FunctionOf( new[] { ScriptType.Particle }, ScriptType.Void );
	[Sandbox.SkipHotload]
	private static readonly ScriptType Colors = ScriptType.ListOf( ScriptType.Color );
	[Sandbox.SkipHotload]
	private static readonly ScriptType Pictures = ScriptType.ListOf( ScriptType.Picture );

	[Sandbox.SkipHotload]
	public static readonly IReadOnlyList<ScriptProperty> All = new ScriptProperty[]
	{
		new( PropertyId.LayerKind, TypeKind.Layer, "Kind", ScriptEnums.Kind.Type, "What the layer is: Sprite(), Light(), Trail(), Beam() or Ghost() made it.", ReadOnly: true, ExampleResult: "Kind.Sprite" ),
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
		new( PropertyId.LayerKeep, TypeKind.Layer, "Keep", ScriptType.Number, "How many sprites it keeps out all the time (at most 100), each with its own p.Index from 0, spread evenly along where they come from in that order and never fading with age (their Life loops every Lifetime): a formation its particle function moves, like runes in a ring or orbs following. 0: sprites come and go by Amount and Lifetime. For a beam, the points it is drawn through.", 0, EffectLayer.MaxKeep, ExampleValue: "12", ExampleResult: "12" ),
		new( PropertyId.LayerAlign, TypeKind.Layer, "Align", ScriptEnums.Align.Type, "Which way its sprites face: the camera (most do); the plane of a swing (a slash lies along the sword's sweep); flat on the ground, turned the way the body faces (footprints); the way the layer faces (Facing: a portal's disc); or out from its place, like tiles on a ball. p.Rotation turns them within that plane.", ExampleValue: "Align.Ground", ExampleResult: "Align.Ground" ),
		new( PropertyId.LayerPictures, TypeKind.Layer, "Pictures", Pictures, "The pictures its sprites take, one each (at most 16): in turn as they appear, or as its particle function picks with p.Frame. Pictures or shapes; LooksLike when there are none.", ExampleValue: "{ Picture.Heart, Shapes.Star5 }", ExampleResult: "{ Picture.Heart, Shapes.Star5 }" ),
		new( PropertyId.LayerDrag, TypeKind.Layer, "Drag", ScriptType.Number, "While its sprites follow the body (Follow), how far they lag behind it: 0 right on it, 1 left behind as a trail is, between a little behind (snow that stays on a runner yet drifts).", 0, 1, ExampleValue: "0.4", ExampleResult: "0.4" ),
		new( PropertyId.LayerAtHit, TypeKind.Layer, "AtHit", ScriptType.Bool, "Whether it shows where the last shot or swing hit, facing out of what it hit (impact sparks), rather than at its place: for a layer shown When.OnShoot or When.OnHit.", ExampleValue: "true", ExampleResult: "true" ),
		new( PropertyId.LayerSweep, TypeKind.Layer, "Sweep", ScriptType.Number, "How far each sprite turns over its life, in degrees, negative the other way: a slash sweeping round with the swing.", -EffectLayer.MaxSweep, EffectLayer.MaxSweep, ExampleValue: "120", ExampleResult: "120" ),
		new( PropertyId.LayerSlashArc, TypeKind.Layer, "SlashArc", ScriptType.Number, "How far round a slash (Picture.Slash) reaches, in degrees.", EffectLayer.MinSlashArc, EffectLayer.MaxSlashArc, ExampleValue: "160", ExampleResult: "160" ),
		new( PropertyId.LayerSlashThickness, TypeKind.Layer, "SlashThickness", ScriptType.Number, "How thick a slash (Picture.Slash) is at its middle, a share of its radius.", EffectLayer.MinSlashWidth, EffectLayer.MaxSlashWidth, ExampleValue: "0.3", ExampleResult: "0.3" ),
		new( PropertyId.LayerParticle, TypeKind.Layer, "Particle", Particle, "The function run for each of its sprites (a beam's points) each frame: function layer.Particle( p ) ... end." ),

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
		new( PropertyId.ShapeMirror, TypeKind.Shape, "Mirror", ScriptType.Bool, "Whether an emblem (a shape of layers) is drawn again flipped left to right, as one half of a symmetric picture.", ExampleValue: "true", ExampleResult: "true" ),
		new( PropertyId.ShapeFinish, TypeKind.Shape, "Finish", ScriptEnums.StickerFinish.Type, "What a sticker is printed on: paper, holo, foil, glitter, gold, or lenticular (two looks by the angle, see Side).", ExampleValue: "StickerFinish.Holo", ExampleResult: "StickerFinish.Holo" ),
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
		new( PropertyId.EmblemEraser, TypeKind.EmblemLayer, "Eraser", ScriptType.Bool, "Whether it is an eraser: it draws nothing and cuts its shape out of every layer below it.", ExampleValue: "true", ExampleResult: "true" ),
		new( PropertyId.EmblemSide, TypeKind.EmblemLayer, "Side", ScriptType.Number, "Which look of a lenticular sticker it is in: 0 both, 1 the first, 2 the second.", 0, 2, ExampleValue: "2", ExampleResult: "2" ),

		new( PropertyId.WeaponFinish, TypeKind.Weapon, "Finish", ScriptEnums.Finish.Type, "What the weapon is made of: its own materials (Finish.None) or a stock finish, under its tint and skin.", ExampleValue: "Finish.Gold", ExampleResult: "Finish.Gold" ),
		new( PropertyId.WeaponTint, TypeKind.Weapon, "Tint", ScriptType.Color, "The colour the weapon is tinted (white when it isn't).", 0, 1, ExampleValue: "Color.Parse( \"#ff8800\" )", ExampleResult: "#ff8800" ),
		new( PropertyId.SkinPattern, TypeKind.WeaponSkin, "Pattern", ScriptEnums.Pattern.Type, "What is laid over the weapon from every side (no UVs needed).", ExampleValue: "Pattern.Stripes", ExampleResult: "Pattern.Stripes" ),
		new( PropertyId.SkinPatternColor, TypeKind.WeaponSkin, "PatternColor", ScriptType.Color, "The pattern's first colour.", 0, 1, ExampleValue: "Color.Black", ExampleResult: "#000000" ),
		new( PropertyId.SkinPatternColorEnd, TypeKind.WeaponSkin, "PatternColorEnd", ScriptType.Color, "The pattern's second colour.", 0, 1, ExampleValue: "Color.Yellow", ExampleResult: "#ffff00" ),
		new( PropertyId.SkinPatternScale, TypeKind.WeaponSkin, "PatternScale", ScriptType.Number, "How large the pattern is: 1 as made.", WeaponSkin.MinScale, WeaponSkin.MaxScale, ExampleValue: "0.5", ExampleResult: "0.5" ),
		new( PropertyId.SkinPatternTurn, TypeKind.WeaponSkin, "PatternTurn", ScriptType.Number, "How far the pattern is turned, in degrees.", 0, 180, ExampleValue: "45", ExampleResult: "45" ),
		new( PropertyId.SkinPatternScroll, TypeKind.WeaponSkin, "PatternScroll", ScriptType.Number, "How fast the pattern scrolls along the weapon, its size a second; negative the other way.", -WeaponSkin.MaxScroll, WeaponSkin.MaxScroll, ExampleValue: "0.5", ExampleResult: "0.5" ),
		new( PropertyId.SkinPatternSpin, TypeKind.WeaponSkin, "PatternSpin", ScriptType.Number, "How fast the pattern spins, turns a second; negative the other way.", -WeaponSkin.MaxSpin, WeaponSkin.MaxSpin, ExampleValue: "0.25", ExampleResult: "0.25" ),
		new( PropertyId.SkinPatternShape, TypeKind.WeaponSkin, "PatternShape", ScriptType.Picture, "The shape Pattern.Shapes repeats: a standard shape, a picture or one the script made (a star unless set).", ExampleValue: "Shapes.Star6", ExampleResult: "Shapes.Star6" ),
		new( PropertyId.SkinAnimation, TypeKind.WeaponSkin, "Animation", ScriptEnums.SkinAnimation.Type, "How the weapon's colour moves (each a stock skin script).", ExampleValue: "SkinAnimation.Rainbow", ExampleResult: "SkinAnimation.Rainbow" ),
		new( PropertyId.SkinSpeed, TypeKind.WeaponSkin, "Speed", ScriptType.Number, "How fast its colour moves: 1 is the clan tags' own pace.", 0, WeaponSkin.MaxSpeed, ExampleValue: "2", ExampleResult: "2" ),
		new( PropertyId.SkinBands, TypeKind.WeaponSkin, "Bands", ScriptType.Number, "How many bands its colour moves along the weapon in.", WeaponSkin.MinBands, WeaponSkin.MaxBands, ExampleValue: "12", ExampleResult: "12" ),
		new( PropertyId.SkinPalette, TypeKind.WeaponSkin, "Palette", Colors, "The colours its motion goes through; the game's five (or its own for a pulse, glow or sheen: white) when none are given.", ExampleValue: "{ Color.Red, Color.Blue }", ExampleResult: "{ #ff0000, #0000ff }" ),
		new( PropertyId.StickerPicture, TypeKind.WeaponSticker, "Picture", ScriptType.Picture, "What the sticker shows: a picture, a standard shape or a shape or sticker of the player's own.", ExampleValue: "Picture.Heart", ExampleResult: "Picture.Heart" ),
		new( PropertyId.StickerColor, TypeKind.WeaponSticker, "Color", ScriptType.Color, "Its colour (a sticker of the player's own keeps its own colours).", 0, 1, ExampleValue: "Color.Red", ExampleResult: "#ff0000" ),
		new( PropertyId.StickerAnimation, TypeKind.WeaponSticker, "Animation", ScriptEnums.Animation.Type, "How its colour moves.", ExampleValue: "Animation.Rainbow", ExampleResult: "Animation.Rainbow" ),
		new( PropertyId.StickerPalette, TypeKind.WeaponSticker, "Palette", Colors, "The colours its colour cycle goes through.", ExampleValue: "{ Color.Cyan, Color.Magenta }", ExampleResult: "{ #00ffff, #ff00ff }" ),
		new( PropertyId.StickerSpeed, TypeKind.WeaponSticker, "AnimationSpeed", ScriptType.Number, "How fast its colour moves: 1 is the game's own pace.", 0, Sticker.MaxSpeed, ExampleValue: "2", ExampleResult: "2" ),
		new( PropertyId.StickerPosition, TypeKind.WeaponSticker, "Position", ScriptType.Vector3, "Where its middle is: forwards along the barrel (X), to the left (Y) and up (Z) from the weapon's origin; it is pressed onto the first surface it meets there.", -WeaponProp.MostOffset, WeaponProp.MostOffset, ExampleValue: "Vector3( 8, 0, 7 )", ExampleResult: "Vector3( 8, 0, 7 )" ),
		new( PropertyId.StickerRotation, TypeKind.WeaponSticker, "Rotation", ScriptType.Vector3, "Which way it is pressed on, as pitch, yaw and roll in degrees from pressing down from above (roll -90 or 90 presses it onto a side).", -Accessory.MaxTurn, Accessory.MaxTurn, ExampleValue: "Vector3( 0, 0, -90 )", ExampleResult: "Vector3( 0, 0, -90 )" ),
		new( PropertyId.StickerSize, TypeKind.WeaponSticker, "Size", ScriptType.Number, "How wide it is, in units.", WeaponSticker.MinSize, WeaponSticker.MaxSize, ExampleValue: "4", ExampleResult: "4" ),
		new( PropertyId.StickerFinish, TypeKind.WeaponSticker, "Finish", ScriptEnums.StickerFinish.Type, "What it is printed on.", ExampleValue: "StickerFinish.Holo", ExampleResult: "StickerFinish.Holo" ),
		new( PropertyId.StickerScrape, TypeKind.WeaponSticker, "Scrape", ScriptType.Number, "How worn it is, 0 new to 1 scraped away in patches (the same on every machine).", 0, 1, ExampleValue: "0.3", ExampleResult: "0.3" ),
		new( PropertyId.StickerBorder, TypeKind.WeaponSticker, "Border", ScriptType.Bool, "Whether it has a thin white die-cut border.", ExampleValue: "true", ExampleResult: "true" ),
		new( PropertyId.PropModel, TypeKind.WeaponProp, "Model", ScriptType.String, "The prop's model, by path (one of the stock props).", ExampleValue: "\"models/citizen_props/hotdog01.vmdl\"", ExampleResult: "\"models/citizen_props/hotdog01.vmdl\"" ),
		new( PropertyId.PropPart, TypeKind.WeaponProp, "Part", ScriptType.String, "The part of the weapon it is on (its magazine, slide...), by name: it follows that part through every animation; the whole weapon for \"\".", ExampleValue: "\"magazine\"", ExampleResult: "\"magazine\"" ),
		new( PropertyId.PropReplaces, TypeKind.WeaponProp, "Replaces", ScriptType.Bool, "Whether it takes its part's place: the part is hidden, and the prop may be as large as the part.", ExampleValue: "true", ExampleResult: "true" ),
		new( PropertyId.PropPosition, TypeKind.WeaponProp, "Position", ScriptType.Vector3, "Where its middle sits: forwards, left and up from its part's middle (or the weapon's origin); kept within the weapon's bounds grown by 8 units.", -WeaponProp.MostOffset, WeaponProp.MostOffset, ExampleValue: "Vector3( 2, 0, 10 )", ExampleResult: "Vector3( 2, 0, 10 )" ),
		new( PropertyId.PropRotation, TypeKind.WeaponProp, "Rotation", ScriptType.Vector3, "How it is turned: pitch, yaw and roll in degrees.", -Accessory.MaxTurn, Accessory.MaxTurn, ExampleValue: "Vector3( 0, 90, 0 )", ExampleResult: "Vector3( 0, 90, 0 )" ),
		new( PropertyId.PropScale, TypeKind.WeaponProp, "Scale", ScriptType.Number, "Its size: 1 as made; its longest side is held to 16 units (or its part's size).", WeaponProp.MinScale, WeaponProp.MostScale, ExampleValue: "0.8", ExampleResult: "0.8" ),
		new( PropertyId.PropNudge, TypeKind.WeaponProp, "Nudge", ScriptType.Vector3, "How far it is moved on the first-person weapon from where it is worked out to be: forwards, left and up, at most 4 units.", -WeaponProp.MaxNudge, WeaponProp.MaxNudge, ExampleValue: "Vector3( 0, 0, 1 )", ExampleResult: "Vector3( 0, 0, 1 )" ),
		new( PropertyId.PropNudgeRotation, TypeKind.WeaponProp, "NudgeRotation", ScriptType.Vector3, "How far it is turned on the first-person weapon: pitch, yaw and roll, at most 45 degrees each.", -WeaponProp.MaxNudgeTurn, WeaponProp.MaxNudgeTurn, ExampleValue: "Vector3( 0, 15, 0 )", ExampleResult: "Vector3( 0, 15, 0 )" ),
		new( PropertyId.EffectWhen, TypeKind.Effect, "When", ScriptEnums.When.Type, "When every layer of a weapon's effect shows: always, while something is so, or on a weapon event (When.OnShoot, OnReload, OnEquip, OnHolster, OnHit).", ExampleValue: "When.OnReload", ExampleResult: "When.OnReload" ),
	};

	/// <summary>Where each layer property starts in a layer's live block, and how big a block is.</summary>
	[Sandbox.SkipHotload]
	public static readonly int[] LiveOffset = LiveLayout( out LiveBlockSize );
	[Sandbox.SkipHotload]
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
