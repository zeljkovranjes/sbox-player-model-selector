using System;
using System.Collections.Generic;
using System.Linq;
using HumanoidRetargeter.Core.Mapping;
using HumanoidRetargeter.Core.Maths;
using HumanoidRetargeter.Core.Solve;
using HumanoidRetargeter.Core.Target;
using Sandbox;
using SkeletonModel = HumanoidRetargeter.Core.Skeleton.Skeleton;

namespace PlayerModels.Engine;

/// <summary>
/// Copies the pose of an animated citizen onto another humanoid model every frame, through Humanoid Retargeter's
/// runtime retargeter. Made once per body (the mapping is the expensive part); <see cref="Apply"/> then only converts
/// and solves. One instance per body: the retargeter keeps scratch buffers.
/// </summary>
internal sealed partial class PoseTransfer
{
	private readonly RuntimePoseRetargeter _retargeter;

	// Source side, in the retargeter's bone order: the engine bone, and the engine bone of its parent (-1 for a root).
	private readonly int[] _sourceBones;
	private readonly int[] _sourceParents;

	// Target side, in the retargeter's bone order: the engine bone, and the retargeter index of its parent.
	private readonly BoneCollection.Bone[] _targetBones;
	private readonly int[] _targetParents;

	private readonly XForm[] _sourceLocals;
	private readonly XForm[] _targetLocals;
	private readonly Transform[] _targetPose;

	// Source bones with a humanoid role (by the retargeter's index) to the target bone with the same role
	private readonly Dictionary<int, int> _roleBones = new();

	// The feet, for keeping the model's soles on the citizen's ground: the citizen's engine bones and the target's
	// retargeter indices, each with its bind height above the model's origin
	private readonly int[] _sourceFeet;
	private readonly float[] _sourceFeetRest;
	private readonly int[] _targetFeet;
	private readonly float[] _targetFeetRest;

	// Body shape, by the target's retargeter index: the role each bone plays (if any), how far its offset from its
	// parent stretches and how much the mesh around it grows; and the whole model's scale
	private readonly BoneRole?[] _targetRoles;
	private readonly float[] _lengthScale;
	private readonly float[] _meshScale;
	private float _height = 1f;

	private bool _warned;

	/// <summary>False when a hot reload dropped state this plan needs; the body using it is then rebuilt.</summary>
	internal bool IsIntact => _retargeter is not null && _sourceBones is not null && _sourceParents is not null
		&& _targetBones is not null && _targetParents is not null && _sourceLocals is not null && _targetLocals is not null
		&& _targetPose is not null && _roleBones is not null
		&& _sourceFeet is not null && _sourceFeetRest is not null && _targetFeet is not null && _targetFeetRest is not null
		&& _targetRoles is not null && _lengthScale is not null && _meshScale is not null && ArmsIntact;

	private PoseTransfer( RuntimePoseRetargeter retargeter, Model source, Model target )
	{
		_retargeter = retargeter;

		var sourceSkeleton = retargeter.SourceSkeleton;
		_sourceBones = new int[ sourceSkeleton.Count ];
		_sourceParents = new int[ sourceSkeleton.Count ];
		for ( var i = 0; i < sourceSkeleton.Count; i++ )
		{
			var bone = source.Bones.GetBone( sourceSkeleton[ i ].Name );
			_sourceBones[ i ] = bone.Index;
			_sourceParents[ i ] = bone.Parent?.Index ?? -1;
		}

		var targetSkeleton = retargeter.TargetRig.Skeleton;
		_targetBones = new BoneCollection.Bone[ targetSkeleton.Count ];
		_targetParents = new int[ targetSkeleton.Count ];
		for ( var i = 0; i < targetSkeleton.Count; i++ )
		{
			_targetBones[ i ] = target.Bones.GetBone( targetSkeleton[ i ].Name );
			_targetParents[ i ] = targetSkeleton[ i ].ParentIndex;
		}

		_sourceLocals = new XForm[ sourceSkeleton.Count ];
		_targetLocals = new XForm[ targetSkeleton.Count ];
		_targetPose = new Transform[ targetSkeleton.Count ];

		foreach ( var (role, sourceBone) in retargeter.SourceMapping.RoleToBone )
		{
			if ( retargeter.TargetRig.BoneForRole( role ) is { } targetBone )
				_roleBones[ sourceBone ] = targetBone;
		}

		// BoneCollection keeps each bone's bind relative to the model, so its height is the bind height of the foot
		var sourceFeet = new List<int>();
		var sourceFeetRest = new List<float>();
		var targetFeet = new List<int>();
		var targetFeetRest = new List<float>();
		foreach ( var role in new[] { BoneRole.FootL, BoneRole.FootR } )
		{
			if ( retargeter.SourceMapping.RoleToBone.TryGetValue( role, out var sourceFoot ) )
			{
				var bone = source.Bones.GetBone( sourceSkeleton[ sourceFoot ].Name );
				sourceFeet.Add( bone.Index );
				sourceFeetRest.Add( bone.LocalTransform.Position.z );
			}

			if ( retargeter.TargetRig.BoneForRole( role ) is { } targetFoot )
			{
				targetFeet.Add( targetFoot );
				targetFeetRest.Add( _targetBones[ targetFoot ].LocalTransform.Position.z );
			}
		}

		_sourceFeet = sourceFeet.ToArray();
		_sourceFeetRest = sourceFeetRest.ToArray();
		_targetFeet = targetFeet.ToArray();
		_targetFeetRest = targetFeetRest.ToArray();

		_targetRoles = new BoneRole?[ targetSkeleton.Count ];
		foreach ( var role in Enum.GetValues<BoneRole>() )
		{
			if ( retargeter.TargetRig.BoneForRole( role ) is { } bone )
				_targetRoles[ bone ] = role;
		}

		_lengthScale = new float[ targetSkeleton.Count ];
		_meshScale = new float[ targetSkeleton.Count ];
		Shape( null );

		PrepareArms( source );
	}

	/// <summary>
	/// Shapes the body with the sliders of a look (see <see cref="Core.BodyShape"/>); null for the model as it comes.
	/// Returns the whole model's scale, which the renderer takes.
	/// </summary>
	internal float Shape( IReadOnlyDictionary<string, float> sliders )
	{
		for ( var i = 0; i < _targetRoles.Length; i++ )
		{
			var role = _targetRoles[ i ];
			_lengthScale[ i ] = role is { } lengthRole ? Core.BodyShape.LengthScale( lengthRole, sliders ) : 1f;
			_meshScale[ i ] = role is { } meshRole ? Core.BodyShape.MeshScale( meshRole, sliders ) : 1f;
		}

		_height = Core.BodyShape.Height( sliders );
		return _height;
	}

	/// <summary>
	/// The target bone that stands in for <paramref name="sourceBone"/>: the one with the same humanoid role as it or its
	/// nearest ancestor that has one (a citizen's hold_R finds the target's right hand). <paramref name="roleBone"/> is
	/// that source bone.
	/// </summary>
	internal bool TryMapBone( string sourceBone, out string roleBone, out string targetBone )
	{
		var skeleton = _retargeter.SourceSkeleton;
		var index = skeleton.IndexOf( sourceBone );

		while ( index >= 0 && !_roleBones.ContainsKey( index ) )
			index = skeleton[ index ].ParentIndex;

		if ( index < 0 )
		{
			roleBone = targetBone = null;
			return false;
		}

		roleBone = skeleton[ index ].Name;
		targetBone = _retargeter.TargetRig.Skeleton[ _roleBones[ index ] ].Name;
		return true;
	}

	/// <summary>
	/// Plans the transfer from <paramref name="source"/> (the citizen) to <paramref name="target"/>, or returns null
	/// with the reason in <paramref name="error"/> when the target is not a humanoid the retargeter can map.
	/// </summary>
	internal static PoseTransfer TryCreate( Model source, Model target, out string error )
	{
		return TryCreate( source, target, null, out error );
	}

	/// <summary>
	/// The same, with bones picked by hand (a <see cref="BoneRole"/> name to a bone of <paramref name="target"/>) for
	/// roles the automatic mapping couldn't fill. Roles it did fill are locked and keep their bones.
	/// </summary>
	internal static PoseTransfer TryCreate( Model source, Model target, IReadOnlyDictionary<string, string> bones, out string error )
	{
		if ( target.BoneCount == 0 )
		{
			error = "This model has no skeleton, so it can't be a player model.";
			return null;
		}

		try
		{
			var sourceSkeleton = SkeletonReader.Read( source );
			var sourceMapping = AutoMapper.Map( sourceSkeleton );
			var targetSkeleton = SkeletonReader.Read( target );
			var targetMapping = AutoMapper.Map( targetSkeleton );
			PickBones( targetMapping, targetSkeleton, bones );

			if ( MissingEssentials( targetMapping ) is { } missing )
			{
				error = $"Target skeleton could not be mapped as a humanoid: no bone for {missing} (hips, head, hands, and feet are required).";
				return null;
			}

			// The model stands where the citizen stands, so it faces the citizen's way even if its bind faces another
			var retargeter = new RuntimePoseRetargeter( sourceSkeleton, sourceMapping, TargetRig.FromSkeleton( targetSkeleton, targetMapping ),
				alignFacingToSource: true );

			error = null;
			return new PoseTransfer( retargeter, source, target );
		}
		catch ( Exception e ) when ( e is ArgumentException or InvalidOperationException )
		{
			error = $"This model's skeleton couldn't be read as a humanoid: {e.Message}";
			return null;
		}
	}

	/// <summary>The bone the automatic mapping gives each humanoid role on <paramref name="model"/>.</summary>
	internal static Dictionary<BoneRole, string> AutomaticBones( Model model )
	{
		var skeleton = SkeletonReader.Read( model );
		return AutoMapper.Map( skeleton ).RoleToBone.ToDictionary( x => x.Key, x => skeleton[ x.Value ].Name );
	}

	private static readonly BoneRole[] Essentials =
		{ BoneRole.Hips, BoneRole.Head, BoneRole.HandL, BoneRole.HandR, BoneRole.FootL, BoneRole.FootR };

	private static string MissingEssentials( MappingResult mapping )
	{
		var missing = Essentials.Where( x => !mapping.RoleToBone.ContainsKey( x ) ).ToArray();
		return missing.Length == 0 ? null : string.Join( ", ", missing );
	}

	// Bones picked by hand only fill roles the automatic mapping left empty, with bones no role uses yet. What the
	// automatic mapping found is locked: a look can't move or clear it, so a preset stays as set up here.
	private static void PickBones( MappingResult mapping, SkeletonModel skeleton, IReadOnlyDictionary<string, string> bones )
	{
		if ( bones is null )
			return;

		var locked = mapping.RoleToBone.Keys.ToHashSet();
		foreach ( var (roleName, boneName) in bones )
		{
			if ( !Enum.TryParse<BoneRole>( roleName, out var role ) || locked.Contains( role ) || string.IsNullOrEmpty( boneName ) )
				continue;

			var bone = skeleton.IndexOf( boneName );
			if ( bone >= 0 && !mapping.RoleToBone.ContainsValue( bone ) )
				mapping.RoleToBone[ role ] = bone;
		}
	}

	/// <summary>Poses <paramref name="target"/> the way <paramref name="source"/> is posed right now.</summary>
	internal void Apply( SkinnedModelRenderer source, SkinnedModelRenderer target )
	{
		var world = source.BoneWorldTransforms;
		if ( world.Length < source.Model.BoneCount )
			return;

		ReadSourcePose( world, source.WorldTransform );

		if ( !_retargeter.TryRetarget( _sourceLocals, _targetLocals, out var error ) )
		{
			// A bad frame (a ragdoll flung to infinity) shouldn't flood the console
			if ( !_warned )
				Log.Warning( $"Player Model Selector: {error}" );

			_warned = true;
			return;
		}

		ChainTargetPose();
		WriteTargetPose( target, GroundShift( world, source.WorldTransform ) );
	}

	// How far the model moves up so its planted foot stands where the citizen's planted foot stands. Each rig's lowest
	// foot is measured from its own bind height, so a model with longer or shorter legs keeps its soles on the ground,
	// and leaves it exactly as far as the citizen does when jumping.
	private float GroundShift( ReadOnlySpan<Transform> world, Transform origin )
	{
		if ( _sourceFeet.Length == 0 || _targetFeet.Length == 0 )
			return 0f;

		var sourceLift = float.MaxValue;
		for ( var i = 0; i < _sourceFeet.Length; i++ )
			sourceLift = MathF.Min( sourceLift, origin.ToLocal( world[ _sourceFeet[ i ] ] ).Position.z - _sourceFeetRest[ i ] );

		var targetLift = float.MaxValue;
		for ( var i = 0; i < _targetFeet.Length; i++ )
			targetLift = MathF.Min( targetLift, _targetPose[ _targetFeet[ i ] ].Position.z - _targetFeetRest[ i ] );

		// The renderer scales the model by its height, so a lift in the citizen's space is smaller in the model's
		return sourceLift / _height - targetLift;
	}

	// The source's bones relative to their parents, the roots relative to the renderer.
	private void ReadSourcePose( ReadOnlySpan<Transform> world, Transform origin )
	{
		for ( var i = 0; i < _sourceBones.Length; i++ )
		{
			var bone = world[ _sourceBones[ i ] ];
			var parent = _sourceParents[ i ];
			var local = parent < 0 ? origin.ToLocal( bone ) : world[ parent ].ToLocal( bone );

			_sourceLocals[ i ] = SkeletonReader.ToCore( local );
		}
	}

	// SetBoneTransform takes each bone relative to the model, so the parent-relative result is chained down first.
	// The retargeter orders bones parents first, so each parent is done before its children. Posing first-person arms,
	// the arm joints are moved onto the weapon's joints as they go, and the fingers follow.
	private void ChainTargetPose( bool pinToWeapon = false )
	{
		for ( var i = 0; i < _targetBones.Length; i++ )
		{
			var local = SkeletonReader.ToEngine( _targetLocals[ i ] );
			var parent = _targetParents[ i ];

			// A longer limb: the bone sits further from its parent, and its children follow
			if ( parent >= 0 )
				local = local.WithPosition( local.Position * _lengthScale[ i ] );

			_targetPose[ i ] = parent < 0 ? local : _targetPose[ parent ].ToWorld( local );

			if ( TryPinnedPosition( pinToWeapon, i, out var pinned ) )
				_targetPose[ i ] = _targetPose[ i ].WithPosition( pinned );
		}
	}

	// A thicker limb or bigger head is scaled here, after chaining, so the bone's children keep their size
	private void WriteTargetPose( SkinnedModelRenderer target, float shiftUp )
	{
		for ( var i = 0; i < _targetBones.Length; i++ )
		{
			var pose = _targetPose[ i ];
			pose = pose.WithPosition( pose.Position + Vector3.Up * shiftUp );

			if ( _meshScale[ i ] != 1f )
				pose = pose.WithScale( pose.Scale * _meshScale[ i ] );

			target.SetBoneTransform( _targetBones[ i ], pose );
		}
	}
}
