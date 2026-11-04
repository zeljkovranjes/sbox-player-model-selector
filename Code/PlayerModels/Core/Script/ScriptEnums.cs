using System;

namespace PlayerModels.Core.Script;

/// <summary>
/// The named choices scripts pick from, each the data's own enum by the same names (so <c>Rhythm.Pulse</c> is saved as
/// the visual editor saves it): what a layer is, the joints, where sprites come from, the way a layer faces, colour
/// modes, rhythms, when a layer shows, what an effect comes from, how a sticker's colour moves, and which way sprites face.
/// </summary>
public static class ScriptEnums
{
	public static readonly ScriptEnum Kind = new( "Kind", Enum.GetNames<LayerKind>(), "What a layer is: sprites, a light, or a trail it leaves." );
	public static readonly ScriptEnum Joint = new( "Joint", Enum.GetNames<Effect.Joint>(), "A joint of the body an effect or layer comes from." );
	public static readonly ScriptEnum ComesFrom = new( "ComesFrom", Enum.GetNames<LayerShape>(), "Where a layer's sprites come from around its place (\"Comes from\")." );
	public static readonly ScriptEnum Facing = new( "Facing", Enum.GetNames<LayerFacing>(), "The way a layer faces from its joint: where a cone opens, a line runs, a shape looks." );
	public static readonly ScriptEnum ColorMode = new( "ColorMode", Enum.GetNames<LayerColorMode>(), "How a layer's sprites are coloured: fading, a World at War cycle, or the palette in turn." );
	public static readonly ScriptEnum Rhythm = new( "Rhythm", Enum.GetNames<LayerRhythm>(), "How a layer changes over time." );
	public static readonly ScriptEnum When = new( "When", Enum.GetNames<LayerWhen>(), "When a layer shows: always, while something is so, or for a moment when something happens." );
	public static readonly ScriptEnum Anchor = new( "Anchor", Enum.GetNames<AnchorKind>(), "What an effect comes from: a joint, an item worn, or the weapon held." );
	public static readonly ScriptEnum Animation = new( "Animation", Enum.GetNames<Sticker.Motion>(), "How a sticker's (or emblem layer's) colour moves." );
	public static readonly ScriptEnum Align = new( "Align", Enum.GetNames<LayerAlign>(), "Which way a layer's sprites face: the camera, a swing's plane, flat on the ground, the way the layer faces, or out from its place." );

	/// <summary>Every one of them.</summary>
	public static readonly ScriptEnum[] All = { Kind, Joint, ComesFrom, Facing, ColorMode, Rhythm, When, Anchor, Animation, Align };
}
