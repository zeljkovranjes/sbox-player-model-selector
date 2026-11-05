using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// One layer of an emblem a script builds (<c>shape.AddLayer( Shapes.Star5 )</c>): the shape it draws (a standard shape's
/// name or a drawn shape's reference, as a layer names them), moved, turned, scaled (each way), flipped, coloured, with
/// its opacity, outline and animated colour, an eraser or a lenticular sticker's look. Plain data for the emblem editor
/// and renderer to take (see <see cref="ToLayer"/>); the stack's order is the list's.
/// </summary>
public sealed class EmblemPart
{
	/// <summary>The most layers an emblem has.</summary>
	public const int MaxPerEmblem = 32;

	public string Shape { get; set; } = nameof( StandardShape.Circle );
	public float X { get; set; }
	public float Y { get; set; }
	public float Rotation { get; set; }
	public float ScaleX { get; set; } = 1f;
	public float ScaleY { get; set; } = 1f;
	public bool FlipX { get; set; }
	public bool FlipY { get; set; }
	public string Color { get; set; } = "#ffffff";
	public float Opacity { get; set; } = 1f;
	public string OutlineColor { get; set; } = "#000000";
	public float OutlineWidth { get; set; }
	public string Animation { get; set; }
	public List<string> Palette { get; set; }
	public float AnimationSpeed { get; set; } = 1f;
	public bool Hidden { get; set; }
	public bool Eraser { get; set; }
	public int Side { get; set; }

	/// <summary>
	/// It as a layer of an emblem (see <see cref="EmblemLayer"/>), its shape as <see cref="Shape"/> names it; the emblem's
	/// sanitizer holds it to its bounds.
	/// </summary>
	public EmblemLayer ToLayer() => new()
	{
		Shape = Shape, X = X, Y = Y, Rotation = Rotation, ScaleX = ScaleX, ScaleY = ScaleY, FlipX = FlipX, FlipY = FlipY, Color = Color,
		Opacity = Opacity, OutlineColor = OutlineColor, OutlineWidth = OutlineWidth, Animation = Animation, Palette = Palette,
		AnimationSpeed = AnimationSpeed, Hidden = Hidden, Eraser = Eraser, Side = Side,
	};

	// The shape it draws while building, by handle, named once the build is done
	internal int ShapeHandle;
}
