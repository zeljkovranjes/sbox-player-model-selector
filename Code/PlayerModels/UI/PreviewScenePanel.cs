using System.Threading.Tasks;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// The panel a preview's scene is drawn on, destroying that scene the frame after the panel is deleted. A screen being
/// destroyed deletes its panels in the middle of the engine's component callbacks; a scene destroyed there takes its
/// objects out of the scene before their own OnDisabled and OnDestroy have run, and those throw a null reference
/// (<c>SkinnedModelRenderer.OnDisabledInternal</c>, <c>CameraComponent.OnDestroy</c>).
/// </summary>
internal sealed class PreviewScenePanel : ScenePanel
{
	public override void OnDeleted() => _ = ReleaseNextFrame();

	private async Task ReleaseNextFrame()
	{
		// Cancelled when the game is closing: the scene's finalizer destroys it instead
		await GameTask.Yield();
		base.OnDeleted();
	}
}
