using System;
using PlayerModels.Core;
using Sandbox.Services;

namespace PlayerModels.Engine;

/// <summary>What players share with each other through the selector: armatures and looks, each in its own cloud tables.</summary>
internal static class Shared
{
	/// <summary>Armatures: the bones picked by hand for a model's missing body parts.</summary>
	internal static readonly SharedLibrary<SharedArmature> Armatures = new( "armature", "armatures", "armature_votes",
		SharedArmature.Parse, x => x.Serialize(), x => x.Model, ( a, b ) => a.SameAs( b ) );

	/// <summary>Looks: how a model is customised (colours, skin, body groups, morphs, body shape), without its armature.</summary>
	internal static readonly SharedLibrary<PlayerLook> Looks = new( "look", "looks", "look_votes",
		PlayerLook.ParseShared, x => x.Customisation().Serialize(), x => x.Model, ( a, b ) => a.SameCustomisationAs( b ) );

	/// <summary>
	/// Whether <paramref name="e"/> means Facepunch's service isn't answering, as opposed to refusing this player something
	/// (a 4xx, which <see cref="Why"/> words).
	/// </summary>
	internal static bool IsUnresponsive( Exception e )
	{
		return e is not DatabaseException { StatusCode: >= 400 and < 500 };
	}

	/// <summary>What to tell the player when sharing, listing or rating failed.</summary>
	internal static string Why( Exception e )
	{
		return e switch
		{
			DatabaseException { StatusCode: 401 } => "Sign in to Steam to share and rate.",
			DatabaseException { StatusCode: 403 } => "Sharing isn't open to you here.",
			DatabaseException { StatusCode: 429 } => "Too many changes at once. Try again in a moment.",
			_ => "Shared designs can't be reached right now.",
		};
	}
}
