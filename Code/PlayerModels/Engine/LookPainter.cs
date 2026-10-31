using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
	// Words of body group names that are the body itself, not something worn on it
	private static readonly HashSet<string> BodyWords = new( StringComparer.OrdinalIgnoreCase )
	{
		"head", "chest", "torso", "body", "leg", "legs", "arm", "arms", "hand", "hands", "foot", "feet", "default", "base",
		"skin",
	};

	// What splits a group name into words: "weapon_front", "Head01", "reload-bow-clip"
	private static readonly Regex NameWords = new( "[^a-zA-Z]+" );

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

		// Checked here, on every client, so no player can make themselves invisible to the others
		var hidesAll = HidesEverything( renderer.Model, look );
		foreach ( var (group, choice) in look.BodyGroups )
			renderer.SetBodyGroup( group, Allowed( renderer.Model, group, choice, hidesAll ) );

		foreach ( var morph in previous.Morphs.Keys.Where( x => !look.Morphs.ContainsKey( x ) ) )
			renderer.Morphs.Set( morph, 0f );

		foreach ( var (morph, weight) in look.Morphs )
			renderer.Morphs.Set( morph, weight );

		// Only when it changes: a stock character's skin tone is a skin too, set by the clothing dresser
		if ( look.Skin != previous.Skin )
			renderer.MaterialGroup = HasSkin( renderer.Model, look.Skin ) ? look.Skin : null;

		renderer.Tint = string.IsNullOrEmpty( look.Tint ) ? Color.White : Color.Parse( look.Tint ) ?? Color.White;
		_parts.Apply( renderer, look.Parts, HasSkin( renderer.Model, look.Skin ) ? look.Skin : null );

		// A skin brings materials of its own, which may need stand-ins of their own
		StandInMaterials.Apply( renderer );

		return true;
	}

	/// <summary>Whether <paramref name="model"/> has the skin (material group) <paramref name="skin"/>.</summary>
	internal static bool HasSkin( Model model, string skin )
	{
		return model is not null && !string.IsNullOrEmpty( skin ) && model.GetMaterialGroupIndex( skin ) >= 0;
	}

	/// <summary>
	/// Whether the body group <paramref name="group"/> may be hidden: an accessory (a hat, a weapon, a pet) may, the body
	/// itself (a citizen's Head or Legs, a hero's "default" body) never, told by the words of its name.
	/// </summary>
	internal static bool CanHide( string group )
	{
		return !string.IsNullOrEmpty( group ) && !NameWords.Split( group ).Any( BodyWords.Contains );
	}

	// The choice a look's choice is shown as: one that shows something, or hides a group that may be hidden; anything
	// else is the model's own default
	private static int Allowed( Model model, string group, int choice, bool hidesAll )
	{
		var part = model?.Parts.Get( group );
		if ( part is null || choice <= 0 || choice >= part.Choices.Count )
			return 0;

		if ( part.Choices[ choice ].Mask != 0 )
			return choice;

		return CanHide( group ) && !hidesAll ? choice : 0;
	}

	// Whether the look's choices would leave every body group empty: a model whose body has a name not known for one is
	// still never hidden whole
	private static bool HidesEverything( Model model, PlayerLook look )
	{
		var parts = model?.Parts.All;
		if ( parts is null || parts.Count == 0 )
			return false;

		return parts.All( part =>
		{
			var choice = look.BodyGroups.TryGetValue( part.Name, out var chosen ) && chosen >= 0 && chosen < part.Choices.Count ? chosen : 0;
			return part.Choices.Count == 0 || part.Choices[ choice ].Mask == 0;
		} );
	}
}
