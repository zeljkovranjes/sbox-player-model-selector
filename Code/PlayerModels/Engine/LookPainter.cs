using System.Linq;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Puts what a look says about the surface of a model on a renderer: body group choices, morph weights, the skin, a
/// tint over the whole model and colours of single parts. One per renderer; whatever a new look no longer lists goes
/// back to the model's default. The body shape isn't here: it bends the pose, which the pose transfer does.
/// </summary>
internal sealed class LookPainter
{
	private readonly PartTints _parts = new();
	private string _applied;

	/// <summary>Forgets what was put on, so the next <see cref="Apply"/> puts all of it on again.</summary>
	internal void Forget()
	{
		_applied = null;
	}

	/// <summary>Puts <paramref name="look"/> on <paramref name="renderer"/>. False when it already wore it.</summary>
	internal bool Apply( SkinnedModelRenderer renderer, PlayerLook look )
	{
		var key = look.Serialize();
		if ( key == _applied )
			return false;

		var previous = PlayerLook.Parse( _applied );
		_applied = key;

		foreach ( var group in previous.BodyGroups.Keys.Where( x => !look.BodyGroups.ContainsKey( x ) ) )
			renderer.SetBodyGroup( group, 0 );

		// Only choices that show something, or the model's own default: no look can hide part of a model
		foreach ( var (group, choice) in look.BodyGroups )
			renderer.SetBodyGroup( group, ShowsSomething( renderer.Model, group, choice ) ? choice : 0 );

		foreach ( var morph in previous.Morphs.Keys.Where( x => !look.Morphs.ContainsKey( x ) ) )
			renderer.Morphs.Set( morph, 0f );

		foreach ( var (morph, weight) in look.Morphs )
			renderer.Morphs.Set( morph, weight );

		// Only when it changes: a stock character's skin tone is a skin too, set by the clothing dresser
		if ( look.Skin != previous.Skin )
			renderer.MaterialGroup = HasSkin( renderer.Model, look.Skin ) ? look.Skin : null;

		renderer.Tint = string.IsNullOrEmpty( look.Tint ) ? Color.White : Color.Parse( look.Tint ) ?? Color.White;
		_parts.Apply( renderer, look.Parts, HasSkin( renderer.Model, look.Skin ) ? look.Skin : null );

		return true;
	}

	/// <summary>Whether <paramref name="model"/> has the skin (material group) <paramref name="skin"/>.</summary>
	internal static bool HasSkin( Model model, string skin )
	{
		return model is not null && !string.IsNullOrEmpty( skin ) && model.GetMaterialGroupIndex( skin ) >= 0;
	}

	private static bool ShowsSomething( Model model, string group, int choice )
	{
		var part = model?.BodyParts.FirstOrDefault( x => x.Name == group );
		return part is not null && choice > 0 && choice < part.Choices.Count && part.Choices[ choice ].Mask != 0;
	}
}
