namespace PlayerModels.Core.Script;

/// <summary>
/// The scratch a machine runs on: value slots and call frames. Runs never overlap on one thread, so a host keeps one and
/// passes it to every run (100 players' scripts share it); a machine run without one makes its own once.
/// </summary>
public sealed class ScriptStack
{
	/// <summary>Slots a stack has unless asked for more: deep enough for any script within the caps.</summary>
	public const int DefaultSlots = 16 * 1024;

	internal readonly double[] Slots;
	internal readonly int[] Frames = new int[ 3 * (ScriptLimits.MaxCallDepth + 1) ];

	public ScriptStack( int slots = DefaultSlots ) => Slots = new double[ System.Math.Max( slots, 16 ) ];
}
