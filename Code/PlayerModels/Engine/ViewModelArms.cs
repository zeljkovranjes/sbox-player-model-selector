using System.Linq;
using System.Threading.Tasks;

namespace PlayerModels.Engine;

/// <summary>
/// First-person arms: which models can stand in for Sandbox's own. Sandbox's weapon view models animate a fixed set of
/// arm and finger bones that the arms are bone-merged onto, so arms only qualify when they have those bones by name, and
/// a whole body (which has them too) is turned away.
/// </summary>
internal static class ViewModelArms
{
	/// <summary>Sandbox's own first-person arms, worn when nothing else is picked.</summary>
	internal const string DefaultArms = "models/first_person/v_first_person_arms_human.vmdl";

	// The bones Sandbox's weapon animations move; arms without them would hang still
	private static readonly string[] ArmBones = { "arm_upper_L", "arm_lower_L", "hand_L", "arm_upper_R", "arm_lower_R", "hand_R" };

	// A model with legs is a body, and would wrap around the camera in first person
	private static readonly string[] LegBones = { "leg_upper_L", "leg_upper_R" };

	private static readonly VerdictCache Verdicts = new( "player_model_selector/viewmodel_verdicts.json" );

	/// <summary>What an earlier check said: true usable as arms, false not, null never checked.</summary>
	internal static bool? Known( string model )
	{
		return Verdicts.Known( model );
	}

	/// <summary>Checks <paramref name="model"/> (loading it) and remembers the verdict. True when it can be worn as arms.</summary>
	/// <summary>Remembers what checking <paramref name="model"/> found: why it can't be arms, or null when it can.</summary>
	internal static void Remember( string model, string why )
	{
		Verdicts.Remember( model, why );
	}

	internal static async Task<bool> CheckAsync( string model )
	{
		var why = await WhyNotArmsAsync( model );
		Verdicts.Remember( model, why );

		return why is null;
	}

	/// <summary>Why <paramref name="model"/> can't be worn as first-person arms, or null when it can.</summary>
	internal static async Task<string> WhyNotArmsAsync( string model )
	{
		var arms = await ModelLoader.LoadAsync( model );
		if ( arms is null )
			return "These arms couldn't be loaded.";

		if ( LegBones.Any( arms.Bones.HasBone ) )
			return "This is a whole body, not first-person arms.";

		var missing = ArmBones.Where( x => !arms.Bones.HasBone( x ) ).ToArray();
		return missing.Length == 0
			? null
			: $"Sandbox's weapons can't move these arms: they have no {string.Join( ", ", missing )}.";
	}
}
