using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>A standard shape's picture (see <see cref="StandardShape"/>), as sprites show it: drawn on this machine, nothing loaded.</summary>
public sealed class ShapePicture : Panel
{
	/// <summary>The shape pictured.</summary>
	[Parameter] public StandardShape Shape { get; set; }

	private StandardShape? _shown;

	public override void Tick()
	{
		base.Tick();
		if ( _shown == Shape )
			return;

		_shown = Shape;
		Style.BackgroundImage = EffectAtlas.TextureOf( Shape );
	}
}
