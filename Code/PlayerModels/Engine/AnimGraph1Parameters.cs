using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Reads and sets a citizen's AnimGraph1 parameters. Engines with AnimGraph2 mark these calls obsolete, but Sandbox's
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
#pragma warning restore CS0618
}
