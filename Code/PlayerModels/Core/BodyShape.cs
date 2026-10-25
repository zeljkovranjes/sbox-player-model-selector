using System;
using System.Collections.Generic;
using HumanoidRetargeter.Core.Mapping;

namespace PlayerModels.Core;

/// <summary>
/// The body shaping every model gets, whatever morphs it has: sliders that scale or lengthen the bones playing a
/// humanoid role. A slider runs from -1 to 1; 0 leaves the model as it is.
/// </summary>
public static class BodyShape
{
	/// <summary>A slider.</summary>
	public enum Part { Height, Head, Body, ArmLength, ArmThickness, Hands, LegLength, LegThickness, Feet }

	/// <summary>
	/// How much a slider at either end changes its part: 0.25 makes it a quarter bigger or smaller. Lengths stay modest
	/// so joints don't come apart; thickness and heads can go further.
	/// </summary>
	public static float MaxChange( Part part ) => part switch
	{
		Part.Height or Part.ArmLength or Part.LegLength => 0.25f,
		Part.Body => 0.35f,
		_ => 0.5f,
	};

	/// <summary>The size factor a slider gives its part: 1 at 0.</summary>
	public static float Factor( Part part, float slider )
	{
		return 1f + Math.Clamp( slider, -1f, 1f ) * MaxChange( part );
	}

	/// <summary>The whole model's scale.</summary>
	public static float Height( IReadOnlyDictionary<string, float> sliders )
	{
		return FactorOf( Part.Height, sliders );
	}

	/// <summary>How much the mesh around a bone with <paramref name="role"/> grows, without moving its children.</summary>
	public static float MeshScale( BoneRole role, IReadOnlyDictionary<string, float> sliders )
	{
		return role switch
		{
			BoneRole.Head => FactorOf( Part.Head, sliders ),
			BoneRole.Hips or BoneRole.Spine0 or BoneRole.Spine1 or BoneRole.Spine2 or BoneRole.Spine3 or BoneRole.Spine4
				=> FactorOf( Part.Body, sliders ),
			BoneRole.UpperArmL or BoneRole.UpperArmR or BoneRole.LowerArmL or BoneRole.LowerArmR
				=> FactorOf( Part.ArmThickness, sliders ),
			BoneRole.UpperLegL or BoneRole.UpperLegR or BoneRole.LowerLegL or BoneRole.LowerLegR
				=> FactorOf( Part.LegThickness, sliders ),
			BoneRole.FootL or BoneRole.FootR or BoneRole.ToeL or BoneRole.ToeR => FactorOf( Part.Feet, sliders ),
			_ when IsHandOrFinger( role ) => FactorOf( Part.Hands, sliders ),
			_ => 1f,
		};
	}

	/// <summary>
	/// How much the offset from its parent of a bone with <paramref name="role"/> stretches: an elbow further from the
	/// shoulder makes a longer arm.
	/// </summary>
	public static float LengthScale( BoneRole role, IReadOnlyDictionary<string, float> sliders )
	{
		return role switch
		{
			BoneRole.LowerArmL or BoneRole.LowerArmR or BoneRole.HandL or BoneRole.HandR => FactorOf( Part.ArmLength, sliders ),
			BoneRole.LowerLegL or BoneRole.LowerLegR or BoneRole.FootL or BoneRole.FootR => FactorOf( Part.LegLength, sliders ),
			BoneRole.ToeL or BoneRole.ToeR => FactorOf( Part.Feet, sliders ),
			_ when IsFinger( role ) => FactorOf( Part.Hands, sliders ),
			_ => 1f,
		};
	}

	private static float FactorOf( Part part, IReadOnlyDictionary<string, float> sliders )
	{
		return sliders is not null && sliders.TryGetValue( part.ToString(), out var slider ) ? Factor( part, slider ) : 1f;
	}

	private static bool IsHandOrFinger( BoneRole role )
	{
		return role is BoneRole.HandL or BoneRole.HandR || IsFinger( role );
	}

	private static bool IsFinger( BoneRole role )
	{
		var name = role.ToString();
		return name.StartsWith( "Thumb" ) || name.StartsWith( "Index" ) || name.StartsWith( "Middle" )
			|| name.StartsWith( "Ring" ) || name.StartsWith( "Pinky" );
	}
}
