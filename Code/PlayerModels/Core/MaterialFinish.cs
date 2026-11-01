using System.Collections.Generic;
using System.Linq;

namespace PlayerModels.Core;

/// <summary>The stock finishes a model or one of its parts can be given, by name, in the order they are offered.</summary>
public static class MaterialFinish
{
	/// <summary>Every finish.</summary>
	public static readonly IReadOnlyList<string> All = new[] { "Gold", "Silver", "Chrome", "Copper", "Bronze", "Gloss", "Clay", "Matte black" };

	/// <summary>What a single part can be given as well: it isn't drawn at all (see <see cref="Hiding"/>).</summary>
	public const string Hidden = "Hidden";

	/// <summary>Whether <paramref name="name"/> is a finish.</summary>
	public static bool IsKnown( string name ) => name is not null && All.Contains( name );

	/// <summary>Whether a single part can be given <paramref name="name"/>.</summary>
	public static bool IsKnownForPart( string name ) => name == Hidden || IsKnown( name );
}
