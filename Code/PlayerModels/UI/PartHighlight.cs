namespace PlayerModels.UI;

/// <summary>
/// The part of the previewed model being pointed at from the Customize page: a choice of a body group (outlined), or a
/// coloured part, by material name (pulsing).
/// </summary>
public readonly record struct PartHighlight( string BodyGroup, int Choice, string Material )
{
	/// <summary>A body group's choice.</summary>
	public static PartHighlight Group( string group, int choice ) => new( group, choice, null );

	/// <summary>A part coloured on its own.</summary>
	public static PartHighlight Part( string material ) => new( null, -1, material );
}
