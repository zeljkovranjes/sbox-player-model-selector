using PlayerModels.Engine;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// A tile's picture of a model: the engine's thumbnail, or, for a mounted model whose own shader doesn't load, its
/// picture with stand-in materials once that is drawn (<see cref="StandInIcons"/>).
/// </summary>
public sealed class ModelIcon : Panel
{
	/// <summary>The model pictured.</summary>
	[Parameter] public string Model { get; set; }

	/// <summary>The engine's picture of it, a <c>thumb:</c> path.</summary>
	[Parameter] public string Icon { get; set; }

	private string _shown;
	private bool _settled;

	public override void Tick()
	{
		base.Tick();

		if ( Icon != _shown )
		{
			_shown = Icon;
			_settled = false;
			Style.SetBackgroundImage( Icon );
		}

		if ( _settled || !StandInIcons.TryGet( Model, out var icon ) )
			return;

		_settled = true;
		if ( icon is not null )
			Style.SetBackgroundImage( icon );
	}
}
