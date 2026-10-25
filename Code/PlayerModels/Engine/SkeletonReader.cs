using System.Collections.Generic;
using HumanoidRetargeter.Core.Maths;
using HumanoidRetargeter.Core.Skeleton;
using Sandbox;
using SkeletonModel = HumanoidRetargeter.Core.Skeleton.Skeleton;

namespace PlayerModels.Engine;

/// <summary>
/// Reads an s&amp;box model's bind pose as a Humanoid Retargeter skeleton, and converts transforms between the two.
/// </summary>
internal static class SkeletonReader
{
	// The retargeter works in centimetres, s&box models in inches.
	private const float CentimetresPerInch = 2.54f;

	/// <summary>The model's bones with their bind pose, each relative to its parent.</summary>
	internal static SkeletonModel Read( Model model )
	{
		var bones = model.Bones.AllBones;
		var definitions = new List<BoneDefinition>( bones.Count );

		foreach ( var bone in bones )
		{
			// The engine gives each bone relative to the model; the retargeter wants it relative to its parent.
			var parent = bone.Parent;
			var local = parent is null ? bone.LocalTransform : parent.LocalTransform.ToLocal( bone.LocalTransform );

			definitions.Add( new BoneDefinition( bone.Name, parent?.Name, ToCore( local ) ) );
		}

		return SkeletonModel.Create( definitions );
	}

	/// <summary>An engine transform as a retargeter one, in centimetres. Scale is dropped.</summary>
	internal static XForm ToCore( Transform transform )
	{
		var position = transform.Position * CentimetresPerInch;
		var rotation = transform.Rotation;

		return new XForm(
			new System.Numerics.Vector3( position.x, position.y, position.z ),
			new System.Numerics.Quaternion( rotation.x, rotation.y, rotation.z, rotation.w ) );
	}

	/// <summary>A retargeter transform as an engine one, in inches.</summary>
	internal static Transform ToEngine( XForm transform )
	{
		var position = new Vector3( transform.Pos.X, transform.Pos.Y, transform.Pos.Z ) / CentimetresPerInch;
		var rotation = new Rotation( transform.Rot.X, transform.Rot.Y, transform.Rot.Z, transform.Rot.W );

		return new Transform( position, rotation, 1f );
	}
}
