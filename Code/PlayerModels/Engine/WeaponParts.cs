using System;
using System.Collections.Generic;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The parts of a weapon a design can hide, replace or put props on: its model's body groups past the first (the weapon
/// itself), such as the magazine, a rail or the sights, each found by name, with where it is (its meshes' bounds). A
/// first-person model has the same parts as bones of the same names that its animations move (the magazine coming out
/// on a reload), so what is on a part follows it there.
/// </summary>
internal static class WeaponParts
{
	// The most detailed meshes, which a part's bounds are worked out from
	private const int FirstLod = 1;

	/// <summary>The parts of <paramref name="model"/>, by name, in the model's order.</summary>
	internal static List<string> Of( Model model )
	{
		var parts = new List<string>();
		var all = model?.Parts?.All;
		if ( all is null )
			return parts;

		for ( var i = 1; i < all.Count; i++ )
		{
			if ( all[ i ].Choices.Count > 0 && !string.IsNullOrEmpty( all[ i ].Name ) )
				parts.Add( all[ i ].Name );
		}

		return parts;
	}

	/// <summary>Where <paramref name="part"/> of <paramref name="model"/> is, in the model's own space: its first choice's meshes; null without it.</summary>
	internal static BBox? BoundsOf( Model model, string part )
	{
		var mask = MaskOf( model, part );
		if ( mask == 0 )
			return null;

		BBox? bounds = null;
		foreach ( var mesh in model.MeshInfo.Meshes )
		{
			if ( (mesh.MeshGroupMask & mask) == 0 || (mesh.LodMask & FirstLod) == 0 )
				continue;

			var box = new BBox( mesh.BoundsMin, mesh.BoundsMax );
			bounds = bounds is { } so ? so.AddBBox( box ) : box;
		}

		return bounds;
	}

	/// <summary>
	/// What <paramref name="model"/> calls <paramref name="part"/>, whatever its case ("Magazine" on one model is
	/// "magazine" on its first-person one); null when it has no such part.
	/// </summary>
	internal static string NameIn( Model model, string part )
	{
		if ( model?.Parts?.All is not { } all || part is null )
			return null;

		foreach ( var group in all )
		{
			if ( string.Equals( group.Name, part, StringComparison.OrdinalIgnoreCase ) )
				return group.Name;
		}

		return null;
	}

	/// <summary>The choice of <paramref name="part"/> that shows nothing, which hides it; -1 when it has none (it can't be hidden).</summary>
	internal static int EmptyChoice( Model model, string part )
	{
		var name = NameIn( model, part );
		var group = name is null ? null : model.Parts.Get( name );
		if ( group is null )
			return -1;

		for ( var i = 0; i < group.Choices.Count; i++ )
		{
			if ( group.Choices[ i ].Mask == 0 )
				return i;
		}

		return -1;
	}

	/// <summary>Whether <paramref name="part"/> of <paramref name="model"/> can be hidden.</summary>
	internal static bool CanHide( Model model, string part ) => EmptyChoice( model, part ) >= 0;

	/// <summary>The bone of <paramref name="model"/> (a first-person model) that moves <paramref name="part"/>: named so, whatever its case; null for none.</summary>
	internal static string BoneOf( Model model, string part )
	{
		if ( model is null || string.IsNullOrEmpty( part ) )
			return null;

		foreach ( var bone in model.Bones.AllBones )
		{
			if ( string.Equals( bone.Name, part, StringComparison.OrdinalIgnoreCase ) )
				return bone.Name;
		}

		return null;
	}

	private static ulong MaskOf( Model model, string part )
	{
		var name = NameIn( model, part );
		var group = name is null ? null : model.Parts.Get( name );
		if ( group is null )
			return 0;

		foreach ( var choice in group.Choices )
		{
			if ( choice.Mask != 0 )
				return choice.Mask;
		}

		return 0;
	}
}
