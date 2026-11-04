namespace PlayerModels.Core.Script;

/// <summary>
/// The machine's built-in operations behind library functions and methods, by id. Those from <see cref="Sprite"/> up to
/// <see cref="LayerEmit"/> make things, so they only run while a script is built.
/// </summary>
public enum Intrinsic
{
	None,

	Abs, Sign, Floor, Ceiling, Round, Frac, Min, Max, Clamp, Lerp, Sin, Cos, Tan, Atan2, Sqrt, Pow, Exp, SmoothStep, Noise, Random,
	ColorNew, ColorParse, ColorFromHsv, ColorLerp,
	VectorNew, VectorLerp, VectorDot, VectorCross, VectorDistance, VectorLength, VectorNormal,

	Sprite, Light, Trail, Beam, Ghost, ListCreate, ShapeNew, ShapeText, ShapesMine, StickersMine, EmblemsMine, EffectsMine, EffectsPreset, EffectInclude,
	ShapePoint, ShapePoints, ShapeEndLine, ShapeEndOutline, ShapeLetter, ShapeAdd, ShapeFit, ShapeFade, ShapeAddLayer,
	WeaponAddSticker, WeaponAddProp, WeaponHidePart, WeaponAddEffect,

	LayerEmit, LayerEmitAt,
}

/// <summary>Which values of a property's owner, and of shapes and pictures, a handle is.</summary>
public static class Handles
{
	/// <summary>Where shapes made by the script start (a standard shape is its own index below).</summary>
	public const int DraftBase = 1000;

	/// <summary>Where the curated pictures start, as pictures (a shape is a picture too, by its own handle).</summary>
	public const int PictureBase = 2000;

	/// <summary>The effect being built, as an effect handle; the effects of <c>Effects.Mine</c> come after it.</summary>
	public const int CurrentEffect = 0;

	/// <summary>Whether <paramref name="intrinsic"/> makes something, so only runs while a script is built.</summary>
	public static bool IsBuildOnly( Intrinsic intrinsic ) => intrinsic is >= Intrinsic.Sprite and < Intrinsic.LayerEmit;
}
