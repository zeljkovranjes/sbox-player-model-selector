using System;
using System.Collections.Generic;
using HumanoidRetargeter.Core.Mapping;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// First-person arms: the target's own arms posed from a weapon view model, whose skeleton has the citizen's arm and
/// finger bones (Sandbox's hands are bone-merged onto it). The citizen's pose is put together from the weapon (arms,
/// hands, fingers) and the citizen's bind (the rest of the body), retargeted like a body, then the arm joints are moved
/// to where the weapon's are so the model's hands hold it. Everything that isn't an arm is folded into the base of the
/// neck, between the shoulders, just behind the eye.
/// All of it is in the weapon's own space, which the target shares, so the arms never trail the weapon.
/// </summary>
internal sealed partial class PoseTransfer
{
	// Folded-away bones shrink to nothing; without arms to fold them between, they go this far behind the eye
	private const float HiddenScale = 0.001f;
	private const float HiddenDistance = 64f;

	// The share of the hand's roll about the forearm that the forearm bone itself takes
	private const float ForearmRollShare = 0.5f;

	// The joints that are moved onto the weapon's joints; the fingers keep the model's own lengths from the hand on
	private static readonly BoneRole[] PinnedRoles =
	{
		BoneRole.ClavicleL, BoneRole.ClavicleR, BoneRole.UpperArmL, BoneRole.UpperArmR,
		BoneRole.LowerArmL, BoneRole.LowerArmR, BoneRole.HandL, BoneRole.HandR
	};

	// Where the citizen's bind is lined up with the weapon's arms: the first of these the weapon has
	private static readonly BoneRole[] AnchorRoles = { BoneRole.ClavicleR, BoneRole.ClavicleL, BoneRole.UpperArmR, BoneRole.UpperArmL };

	// Source side, by the retargeter's index: the citizen's bind relative to the model, and the pose put together
	private Transform[] _sourceBind;
	private Transform[] _armsSourcePose;

	// Target side, by the retargeter's index: the source bone whose place a joint takes (-1 for none), whether the
	// bone is part of an arm, and the bones the arms start at
	private int[] _pinnedTo;
	private bool[] _isArm;
	private int[] _armRoots;
	private (int Forearm, int Hand)[] _forearms;

	// The weapon model the arm bones were matched on: each source bone's bone there (-1 for none), the turn from that
	// bone's rest frame to the citizen's, the anchor, the weapon's bone parents and its pose relative to the weapon
	private Model _weaponModel;
	private int[] _weaponBones;
	private Rotation[] _restTurns;
	private int[] _weaponParents;
	private Transform[] _weaponPose;
	private int _anchor = -1;

	/// <summary>
	/// How much the model is scaled for first-person arms so its arms are as long as the citizen's: the weapon's arm
	/// bones are the citizen's, so the model's hands then come out the citizen's size too.
	/// </summary>
	internal float ArmsScale { get; private set; } = 1f;

	private bool ArmsIntact => _sourceBind is not null && _armsSourcePose is not null && _pinnedTo is not null && _isArm is not null
		&& _armRoots is not null && _forearms is not null;

	private void PrepareArms( Model source )
	{
		var sourceSkeleton = _retargeter.SourceSkeleton;
		_sourceBind = new Transform[ sourceSkeleton.Count ];
		_armsSourcePose = new Transform[ sourceSkeleton.Count ];
		for ( var i = 0; i < sourceSkeleton.Count; i++ )
			_sourceBind[ i ] = source.Bones.GetBone( sourceSkeleton[ i ].Name ).LocalTransform;

		var targetCount = _targetBones.Length;
		_pinnedTo = new int[ targetCount ];
		Array.Fill( _pinnedTo, -1 );
		foreach ( var role in PinnedRoles )
		{
			if ( _retargeter.SourceMapping.RoleToBone.TryGetValue( role, out var sourceBone ) && _retargeter.TargetRig.BoneForRole( role ) is { } targetBone )
				_pinnedTo[ targetBone ] = sourceBone;
		}

		// An arm starts at its clavicle, or at the upper arm when the model has no clavicle; parents come first
		var armRoots = new List<int>();
		foreach ( var (clavicle, upperArm) in new[] { (BoneRole.ClavicleL, BoneRole.UpperArmL), (BoneRole.ClavicleR, BoneRole.UpperArmR) } )
		{
			if ( (_retargeter.TargetRig.BoneForRole( clavicle ) ?? _retargeter.TargetRig.BoneForRole( upperArm )) is { } root )
				armRoots.Add( root );
		}

		_armRoots = armRoots.ToArray();

		var forearms = new List<(int, int)>();
		foreach ( var (lower, hand) in new[] { (BoneRole.LowerArmL, BoneRole.HandL), (BoneRole.LowerArmR, BoneRole.HandR) } )
		{
			if ( _retargeter.TargetRig.BoneForRole( lower ) is { } forearm && _retargeter.TargetRig.BoneForRole( hand ) is { } handBone )
				forearms.Add( (forearm, handBone) );
		}

		_forearms = forearms.ToArray();
		_isArm = new bool[ targetCount ];
		for ( var i = 0; i < targetCount; i++ )
			_isArm[ i ] = armRoots.Contains( i ) || _targetParents[ i ] >= 0 && _isArm[ _targetParents[ i ] ];

		var citizenArm = ArmLength( role => _retargeter.SourceMapping.RoleToBone.TryGetValue( role, out var bone ) ? _sourceBind[ bone ].Position : null );
		var modelArm = ArmLength( role => _retargeter.TargetRig.BoneForRole( role ) is { } bone ? _targetBones[ bone ].LocalTransform.Position : null );
		ArmsScale = citizenArm > 0f && modelArm > 0f ? Math.Clamp( citizenArm / modelArm, 0.2f, 5f ) : 1f;
	}

	// Shoulder to elbow to wrist of the right arm, or the left one; 0 when neither is mapped
	private static float ArmLength( Func<BoneRole, Vector3?> position )
	{
		foreach ( var (upper, lower, hand) in new[] { (BoneRole.UpperArmR, BoneRole.LowerArmR, BoneRole.HandR), (BoneRole.UpperArmL, BoneRole.LowerArmL, BoneRole.HandL) } )
		{
			if ( position( upper ) is { } shoulder && position( lower ) is { } elbow && position( hand ) is { } wrist )
				return shoulder.Distance( elbow ) + elbow.Distance( wrist );
		}

		return 0f;
	}

	/// <summary>Whether <paramref name="weapon"/>'s skeleton has the citizen's arm bones, so it can pose first-person arms.</summary>
	internal bool CanPoseArmsFrom( Model weapon )
	{
		if ( weapon != _weaponModel )
			MatchWeaponBones( weapon );

		return _anchor >= 0;
	}

	/// <summary>
	/// Poses the arms of <paramref name="target"/> the way <paramref name="weapon"/> (a weapon view model) holds its arms
	/// right now, and folds the rest of it away. The target sits on the weapon, scaled by <see cref="ArmsScale"/>.
	/// </summary>
	internal void ApplyArms( SkinnedModelRenderer weapon, SkinnedModelRenderer target )
	{
		if ( !CanPoseArmsFrom( weapon.Model ) )
			return;

		ReadArmsPose( weapon );

		if ( !_retargeter.TryRetarget( _sourceLocals, _targetLocals, out var error ) )
		{
			if ( !_warned )
				Log.Warning( $"Player Model Selector: {error}" );

			_warned = true;
			return;
		}

		ChainTargetPose( pinToWeapon: true );
		WriteArmsPose( target );
	}

	private void MatchWeaponBones( Model weapon )
	{
		_weaponModel = weapon;
		_anchor = -1;

		if ( weapon is null )
			return;

		var sourceSkeleton = _retargeter.SourceSkeleton;
		_weaponBones = new int[ sourceSkeleton.Count ];
		_restTurns = new Rotation[ sourceSkeleton.Count ];
		for ( var i = 0; i < sourceSkeleton.Count; i++ )
		{
			var bone = weapon.Bones.HasBone( sourceSkeleton[ i ].Name ) ? weapon.Bones.GetBone( sourceSkeleton[ i ].Name ) : null;
			_weaponBones[ i ] = bone?.Index ?? -1;

			// The first-person arms rig names its bones like the citizen but rolls them differently at rest (the forearm
			// by about 16 degrees). The retargeter measures the pose against the citizen's rest, so a bone is turned onto
			// the citizen's frame first; left as it was, another model's wrists came out twisted. Both rests are given
			// relative to their model, and the two models face the same way.
			_restTurns[ i ] = bone is null ? Rotation.Identity : bone.LocalTransform.Rotation.Inverse * _sourceBind[ i ].Rotation;
		}

		_weaponParents = new int[ weapon.BoneCount ];
		foreach ( var bone in weapon.Bones.AllBones )
			_weaponParents[ bone.Index ] = bone.Parent?.Index ?? -1;

		_weaponPose = new Transform[ weapon.BoneCount ];

		foreach ( var role in AnchorRoles )
		{
			if ( _retargeter.SourceMapping.RoleToBone.TryGetValue( role, out var bone ) && _weaponBones[ bone ] >= 0 )
			{
				_anchor = bone;
				break;
			}
		}
	}

	// The citizen's pose relative to the weapon: the weapon's bones where it has them, the citizen's bind everywhere
	// else, moved to meet the weapon's arms at the anchor so the bones in between keep sensible offsets
	private void ReadArmsPose( SkinnedModelRenderer weapon )
	{
		// ponytail: allocates the weapon's bones once a frame (about 70); world-space bones would trail the weapon by a
		// frame when turning. Swap for a non-allocating read if the engine grows one.
		var locals = weapon.GetBoneTransforms( false );
		var count = Math.Min( locals.Length, _weaponPose.Length );
		for ( var i = 0; i < count; i++ )
			_weaponPose[ i ] = _weaponParents[ i ] < 0 ? locals[ i ] : _weaponPose[ _weaponParents[ i ] ].ToWorld( locals[ i ] );

		var shift = _weaponPose[ _weaponBones[ _anchor ] ].Position - _sourceBind[ _anchor ].Position;

		for ( var i = 0; i < _armsSourcePose.Length; i++ )
		{
			var weaponBone = _weaponBones[ i ];
			_armsSourcePose[ i ] = weaponBone >= 0 && weaponBone < count
				? _weaponPose[ weaponBone ].WithRotation( _weaponPose[ weaponBone ].Rotation * _restTurns[ i ] )
				: _sourceBind[ i ].WithPosition( _sourceBind[ i ].Position + shift );
		}

		var skeleton = _retargeter.SourceSkeleton;
		for ( var i = 0; i < _armsSourcePose.Length; i++ )
		{
			var parent = skeleton[ i ].ParentIndex;
			var local = parent < 0 ? _armsSourcePose[ i ] : _armsSourcePose[ parent ].ToLocal( _armsSourcePose[ i ] );

			_sourceLocals[ i ] = SkeletonReader.ToCore( local );
		}
	}

	// The weapon joint a target bone is moved onto, relative to the target (which sits on the weapon, scaled)
	private bool TryPinnedPosition( bool pinToWeapon, int bone, out Vector3 position )
	{
		if ( !pinToWeapon || _pinnedTo[ bone ] < 0 )
		{
			position = default;
			return false;
		}

		position = _armsSourcePose[ _pinnedTo[ bone ] ].Position / ArmsScale;
		return true;
	}

	// Between the shoulders: skin shared by an arm and the chest then pinches in at the shoulder, which is behind the
	// eye, instead of stretching across the view. The weapon's origin is the eye, looking down its forward axis.
	private Vector3 FoldPoint()
	{
		if ( _armRoots.Length == 0 )
			return Vector3.Backward * (HiddenDistance / ArmsScale);

		var sum = Vector3.Zero;
		foreach ( var root in _armRoots )
			sum += _targetPose[ root ].Position;

		return sum / _armRoots.Length;
	}

	// A weapon grip turns the hand far over (about 120 degrees on a rifle's front hand), which the first-person arms
	// spread along their forearm twist bones. Many models skin the forearm to the forearm bone alone, so all of that
	// turn wrung the skin at the wrist: the forearm bone takes part of it, about its own length. Bones are written
	// relative to the model, so the hand and the twist bones stay where they are.
	private void ShareHandRoll()
	{
		foreach ( var (forearm, hand) in _forearms )
		{
			var restTurn = _targetBones[ forearm ].LocalTransform.Rotation.Inverse * _targetBones[ hand ].LocalTransform.Rotation;
			var turn = _targetPose[ forearm ].Rotation.Inverse * _targetPose[ hand ].Rotation * restTurn.Inverse;
			var axis = _targetBones[ forearm ].LocalTransform.ToLocal( _targetBones[ hand ].LocalTransform ).Position.Normal;

			// The twist part of the turn about the forearm's length (swing-twist decomposition)
			var along = new Vector3( turn.x, turn.y, turn.z ).Dot( axis );
			var roll = 2f * MathF.Atan2( along, turn.w ) * (180f / MathF.PI);
			if ( roll > 180f ) roll -= 360f;
			if ( roll < -180f ) roll += 360f;

			var pose = _targetPose[ forearm ];
			_targetPose[ forearm ] = pose.WithRotation( pose.Rotation * Rotation.FromAxis( axis, roll * ForearmRollShare ) );
		}
	}

	private void WriteArmsPose( SkinnedModelRenderer target )
	{
		ShareHandRoll();
		var hidden = new Transform( FoldPoint(), Rotation.Identity, HiddenScale );

		for ( var i = 0; i < _targetBones.Length; i++ )
		{
			if ( !_isArm[ i ] )
			{
				target.SetBoneTransform( _targetBones[ i ], hidden );
				continue;
			}

			var pose = _targetPose[ i ];
			if ( _meshScale[ i ] != 1f )
				pose = pose.WithScale( pose.Scale * _meshScale[ i ] );

			target.SetBoneTransform( _targetBones[ i ], pose );
		}
	}
}
