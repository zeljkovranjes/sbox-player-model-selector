using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>A tile's picture of a standard shape (see <see cref="StandardShape"/>), as sprites show it (see <see cref="EffectAtlas"/>).</summary>
public sealed class StandardShapeIcon : Panel
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
