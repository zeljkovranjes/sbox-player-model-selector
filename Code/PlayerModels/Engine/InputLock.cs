using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Keeps the keyboard and mouse from the game while a window of ours is using them, so the player stands still behind
/// it. Held scene stage by scene stage, the way Sandbox's own free camera does it: the engine lifts suppression before
/// every stage, so setting it once does nothing.
/// </summary>
internal sealed class InputLock : GameObjectSystem<InputLock>, Component.ISceneStage
{
	/// <summary>Whether the game is kept from the input.</summary>
	internal bool IsHeld { get; set; }

	public InputLock( Scene scene ) : base( scene )
	{
	}

	void Component.ISceneStage.Start()
	{
		if ( IsHeld )
			Input.Suppressed = true;
	}

	void Component.ISceneStage.End()
	{
		if ( IsHeld )
			Input.Suppressed = false;
	}
}
