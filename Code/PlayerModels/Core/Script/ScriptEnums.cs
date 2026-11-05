using System;
using System.Linq;

namespace PlayerModels.Core.Script;

/// <summary>
/// The named choices scripts pick from, each the data's own enum by the same names (so <c>Rhythm.Pulse</c> is saved as
/// the visual editor saves it): what a layer is, the joints, where sprites come from, the way a layer faces, colour
/// modes, rhythms, when a layer shows, what an effect comes from, how a sticker's colour moves, and which way sprites face.
/// </summary>
public static class ScriptEnums
{
	[Sandbox.SkipHotload]
	public static readonly ScriptEnum Kind = new( "Kind", Enum.GetNames<LayerKind>(), "What a layer is: sprites, a light, or a trail it leaves." );
	[Sandbox.SkipHotload]
	public static readonly ScriptEnum Joint = new( "Joint", Enum.GetNames<Effect.Joint>(), "A joint of the body an effect or layer comes from (Joint.Ground: the ground under it, turned the way it faces)." );
	[Sandbox.SkipHotload]
	public static readonly ScriptEnum ComesFrom = new( "ComesFrom", Enum.GetNames<LayerShape>(), "Where a layer's sprites come from around its place (\"Comes from\")." );
	[Sandbox.SkipHotload]
	public static readonly ScriptEnum Facing = new( "Facing", Enum.GetNames<LayerFacing>(), "The way a layer faces from its joint: where a cone opens, a line runs, a shape looks." );
	[Sandbox.SkipHotload]
	public static readonly ScriptEnum ColorMode = new( "ColorMode", Enum.GetNames<LayerColorMode>(), "How a layer's sprites are coloured: fading, a World at War cycle, or the palette in turn." );
	[Sandbox.SkipHotload]
	public static readonly ScriptEnum Rhythm = new( "Rhythm", Enum.GetNames<LayerRhythm>(), "How a layer changes over time." );
	[Sandbox.SkipHotload]
	public static readonly ScriptEnum When = new( "When", Enum.GetNames<LayerWhen>(), "When a layer shows: always, while something is so, or for a moment when something happens." );
	[Sandbox.SkipHotload]
	public static readonly ScriptEnum Anchor = new( "Anchor", Enum.GetNames<AnchorKind>(), "What an effect comes from: a joint, an item worn, or the weapon held." );
	[Sandbox.SkipHotload]
	public static readonly ScriptEnum Animation = new( "Animation", Enum.GetNames<Sticker.Motion>(), "How a sticker's (or emblem layer's) colour moves." );
	[Sandbox.SkipHotload]
	public static readonly ScriptEnum Align = new( "Align", Enum.GetNames<LayerAlign>(), "Which way a layer's sprites face: the camera, a swing's plane, flat on the ground, the way the layer faces, or out from its place." );

	[Sandbox.SkipHotload]
	public static readonly ScriptEnum Finish = new( "Finish", FinishNames(), "What a weapon is made of: its own materials (None), or one of the stock finishes." );
	[Sandbox.SkipHotload]
	public static readonly ScriptEnum Pattern = new( "Pattern", Enum.GetNames<WeaponSkin.Pattern>(), "What is laid over a weapon from every side: stripes, camo, checker, a gradient, carbon fibre or a repeated shape." );
	[Sandbox.SkipHotload]
	public static readonly ScriptEnum SkinAnimation = new( "SkinAnimation", Enum.GetNames<WeaponSkin.Motion>(), "How a weapon's colour moves: a rainbow flowing along it, World at War's colourings in bands, a pulse, a glow, a sheen or its palette going round." );
	[Sandbox.SkipHotload]
	public static readonly ScriptEnum StickerFinish = new( "StickerFinish", Enum.GetNames<Core.StickerFinish>(), "What a sticker is printed on: paper, holo, foil, glitter, gold or lenticular." );

	/// <summary>Every one of them.</summary>
	[Sandbox.SkipHotload]
	public static readonly ScriptEnum[] All = { Kind, Joint, ComesFrom, Facing, ColorMode, Rhythm, When, Anchor, Animation, Align, Finish, Pattern, SkinAnimation, StickerFinish };

	/// <summary>The finish a choice of <see cref="Finish"/> names in the data (<see cref="MaterialFinish"/>): null for None.</summary>
	public static string FinishOf( string member ) => member is null || member == NoFinish ? null : MaterialFinish.All.FirstOrDefault( x => x.Replace( " ", "" ) == member );

	/// <summary>The choice of <see cref="Finish"/> for a finish of the data; None for none.</summary>
	public static string FinishMember( string finish ) => MaterialFinish.IsKnown( finish ) ? finish.Replace( " ", "" ) : NoFinish;

	private const string NoFinish = "None";

	private static string[] FinishNames() => new[] { NoFinish }.Concat( MaterialFinish.All.Select( x => x.Replace( " ", "" ) ) ).ToArray();
}
