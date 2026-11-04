namespace PlayerModels.Core.Script;

/// <summary>
/// The properties of layers, effects, shapes, emblem layers and weapon designs a script reads and writes. A layer's come first, in the
/// order of its live block (what its event and particle functions change while it runs; see <see cref="LayerProperties"/>).
/// </summary>
public enum PropertyId
{
	LayerKind, LayerOff, LayerJoint, LayerComesFrom, LayerSpread, LayerOffset, LayerFacing, LayerAngle, LayerToJoint,
	LayerFormation, LayerFill, LayerLooksLike, LayerColor, LayerColorEnd, LayerColorMode, LayerPalette, LayerSize,
	LayerSizeEnd, LayerSpeed, LayerGravity, LayerLifetime, LayerAmount, LayerFollow, LayerSolid, LayerRhythm,
	LayerRhythmSpeed, LayerRhythmAmount, LayerWhen, LayerBurst, LayerShowFor, LayerFadeWithSpeed, LayerKeep, LayerAlign, LayerPictures,
	LayerDrag, LayerAtHit, LayerSweep, LayerSlashArc, LayerSlashThickness,
	LayerParticle,

	EffectName, EffectKey, EffectOff, EffectAnchor, EffectJoint, EffectItem, EffectOffset, EffectHue, EffectSize,
	EffectAmount, EffectSpeed,

	ShapeFillColor, ShapeOutlineColor, ShapeOutlineWidth, ShapeAnimation, ShapePalette, ShapeAnimationSpeed, ShapePointCount,

	EmblemShape, EmblemPosition, EmblemRotation, EmblemScale, EmblemFlipX, EmblemFlipY, EmblemColor, EmblemOpacity,
	EmblemOutlineColor, EmblemOutlineWidth, EmblemAnimation, EmblemPalette, EmblemAnimationSpeed, EmblemHidden,

	WeaponFinish, WeaponTint,
	SkinPattern, SkinPatternColor, SkinPatternColorEnd, SkinPatternScale, SkinPatternTurn, SkinPatternScroll, SkinPatternSpin, SkinPatternShape,
	SkinAnimation, SkinSpeed, SkinBands, SkinPalette,
	StickerPicture, StickerColor, StickerAnimation, StickerPalette, StickerSpeed, StickerPosition, StickerRotation, StickerSize, StickerFinish,
	StickerScrape, StickerBorder,
	PropModel, PropPart, PropReplaces, PropPosition, PropRotation, PropScale, PropNudge, PropNudgeRotation,
	EffectWhen,

	Total,
}
