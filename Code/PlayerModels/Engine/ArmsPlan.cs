using System.Threading.Tasks;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// What showing a worn model's own arms in first person takes: the model, loaded, and the plan that poses it from a
/// view model's hands (which have the citizen's bones). Made once per model and bone picks.
/// </summary>
internal sealed class ArmsPlan
{
	/// <summary>The model and bone picks this plan is for (see <see cref="KeyOf"/>).</summary>
	internal string Key { get; }

	/// <summary>The worn model.</summary>
	internal Model Model { get; }

	/// <summary>The plan posing its arms.</summary>
	internal PoseTransfer Transfer { get; }

	private ArmsPlan( string key, Model model, PoseTransfer transfer )
	{
		Key = key;
		Model = model;
		Transfer = transfer;
	}

	/// <summary>The model and bone picks of <paramref name="look"/> as one string; empty for none.</summary>
	internal static string KeyOf( PlayerLook look )
	{
		return look is null || look.IsDefault ? "" : $"{look.Model}|{look.BonesKey()}";
	}

	/// <summary>The plan for the model <paramref name="look"/> wears, or null when its arms can't be posed.</summary>
	internal static async Task<ArmsPlan> LoadAsync( PlayerLook look )
	{
		var model = await ModelLoader.LoadAsync( look.Model );
		var citizen = await ModelLoader.LoadAsync( ModelLoader.CitizenModel );
		if ( model is null || citizen is null )
			return null;

		var transfer = PoseTransfer.TryCreate( citizen, model, look.Bones, out _ );
		return transfer is null ? null : new ArmsPlan( KeyOf( look ), model, transfer );
	}
}
