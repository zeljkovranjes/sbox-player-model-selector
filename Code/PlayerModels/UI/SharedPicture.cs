using PlayerModels.Core;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// A Workshop row's picture of what was shared, drawn on this machine from what it is (no picture is uploaded): an
/// effect as its tile shows it (see <see cref="EffectIcon"/>), a shape or sticker as bodies show it (see
/// <see cref="ShapeIcon"/>), on a dark ground so glows show; nothing for kinds without a picture.
/// </summary>
public sealed class SharedPicture : Panel
{
	/// <summary>What was shared.</summary>
	[Parameter] public object Item { get; set; }

	private object _shown;

	public override void Tick()
	{
		base.Tick();

		if ( ReferenceEquals( Item, _shown ) )
			return;

		_shown = Item;
		DeleteChildren( true );
		switch ( Item )
		{
			case Effect effect:
				AddChild( new EffectIcon { Effect = effect } ).AddClass( "fill" );
				break;

			case DrawnShape shape:
				AddChild( new ShapeIcon { Shape = shape } ).AddClass( "fill" );
				break;
		}
	}
}
