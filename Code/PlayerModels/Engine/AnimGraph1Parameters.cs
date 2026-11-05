using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Reads and sets a citizen's AnimGraph1 parameters, and finds a model's AnimGraph1 graph. Engines with AnimGraph2 mark these calls obsolete, but Sandbox's
/// citizen still runs AnimGraph1 and older engines have nothing else, so the warning is kept to this one place.
/// </summary>
internal static class AnimGraph1Parameters
{
#pragma warning disable CS0618
	internal static bool ReadBool( this SkinnedModelRenderer renderer, string name ) => renderer.GetBool( name );

	internal static float ReadFloat( this SkinnedModelRenderer renderer, string name ) => renderer.GetFloat( name );

	internal static int ReadInt( this SkinnedModelRenderer renderer, string name ) => renderer.GetInt( name );

	internal static void Write( this SkinnedModelRenderer renderer, string name, bool value ) => renderer.Set( name, value );

	internal static void Write( this SkinnedModelRenderer renderer, string name, int value ) => renderer.Set( name, value );

	internal static void Write( this SkinnedModelRenderer renderer, string name, float value ) => renderer.Set( name, value );

	internal static void Write( this SkinnedModelRenderer renderer, string name, Vector3 value ) => renderer.Set( name, value );

	/// <summary>The AnimGraph1 graph <paramref name="model"/> comes with; null for none.</summary>
	internal static AnimationGraph Graph1( this Model model ) => model?.AnimGraph;

	/// <summary>Whether <paramref name="model"/>'s AnimGraph1 graph has a parameter called <paramref name="name"/>; false with no graph.</summary>
	internal static bool HasGraph1Parameter( this Model model, string name )
	{
		if ( model.Graph1() is not { } graph )
			return false;

		for ( var i = 0; i < graph.ParamCount; i++ )
		{
			if ( graph.GetParameterName( i ) == name )
				return true;
		}

		return false;
	}
#pragma warning restore CS0618
}
