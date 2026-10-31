using System;
using System.Collections.Generic;
using HumanoidRetargeter.Core.Mapping;
using HumanoidRetargeter.Core.Target;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// A head left out of the skeleton's hierarchy, a root of its own as some Garry's Mod ports have it: the neck carries it
/// as if it hung from it. Otherwise it floats where the solve leaves it, apart from the body.
/// </summary>
internal sealed partial class PoseTransfer
{
	// A head left out of the hierarchy, a root of its own (as some Garry's Mod ports have it): the bone that carries it as
	// if it hung from it, the head's bind relative to that bone, and the bones under the head, parents first
	private int _detachedHead = -1;
	private int _headCarrier = -1;
	private Transform _headOnCarrier;
	private int[] _underHead = Array.Empty<int>();

	private void FindDetachedHead( TargetRig rig )
	{
		if ( rig.BoneForRole( BoneRole.Head ) is not { } head || _targetParents[ head ] >= 0 )
			return;

		// The neck, or the highest spine bone there is
		var carrier = rig.BoneForRole( BoneRole.Neck );
		foreach ( var role in new[] { BoneRole.Spine4, BoneRole.Spine3, BoneRole.Spine2, BoneRole.Spine1, BoneRole.Spine0 } )
			carrier ??= rig.BoneForRole( role );

		if ( carrier is not { } found )
			return;

		_detachedHead = head;
		_headCarrier = found;
		_headOnCarrier = _targetBones[ found ].LocalTransform.ToLocal( _targetBones[ head ].LocalTransform );

		var underHead = new List<int>();
		for ( var i = 0; i < _targetParents.Length; i++ )
		{
			if ( IsUnder( i, head ) )
				underHead.Add( i );
		}

		_underHead = underHead.ToArray();
	}

	private bool IsUnder( int bone, int ancestor )
	{
		for ( var parent = _targetParents[ bone ]; parent >= 0; parent = _targetParents[ parent ] )
		{
			if ( parent == ancestor )
				return true;
		}

		return false;
	}

	// The detached head goes where its carrier holds it, keeping the turn the solve gave it (it floated where the solve
	// left it, apart from the neck); the bones under it follow
	private void CarryDetachedHead()
	{
		if ( _detachedHead < 0 )
			return;

		var placed = _targetPose[ _headCarrier ].ToWorld( _headOnCarrier );
		_targetPose[ _detachedHead ] = _targetPose[ _detachedHead ].WithPosition( placed.Position );

		foreach ( var bone in _underHead )
		{
			var local = SkeletonReader.ToEngine( _targetLocals[ bone ] );
			_targetPose[ bone ] = _targetPose[ _targetParents[ bone ] ].ToWorld( local.WithPosition( local.Position * _lengthScale[ bone ] ) );
		}
	}
}
