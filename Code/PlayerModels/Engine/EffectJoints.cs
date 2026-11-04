using System;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Where each joint effects come from is on one body (see <see cref="Effect.Joint"/>), worked out once a frame however
/// many layers come from it. A joint is the worn model's bone for the citizen's (as items find theirs, see
/// <see cref="AccessoryRack.TryGetJoint"/>), or the citizen's own when it wears no model, facing the way the body faces at
/// rest turned as the joint is now, and as big as the model is next to the citizen effects are made on. The eyes are where
/// the model marks them, or else in front of its head; the crown is as high over the head bone as the model reaches. A
/// joint the model doesn't have is stood in for: the feet under the hips, anything else by the hips, or by the body
/// itself. Runs every frame: no allocations.
/// </summary>
internal sealed class EffectJoints
{
	// Where a model marks its eyes: an attachment for between them, and an attachment or else a bone for each one
	private const string EyesAttachment = "eyes";
	private const string LeftEyeAttachment = "eye_L_forward";
	private const string RightEyeAttachment = "eye_R_forward";
	private const string LeftEyeBone = "eye_L";
	private const string RightEyeBone = "eye_R";

	// Where the eyes are on a model that doesn't mark them: in front of its head bone and above it, each this far to
	// the side, as on the citizen
	private static readonly Vector3 EyesFromHead = new( 3f, 0f, 4f );
	private const float EyeApart = 1.35f;

	// The crown is never further over the head bone than this share of the model's height
	private const float MostCrownAbove = 0.25f;

	// The size a model wears effects at next to the citizen stays within these, however tall it is
	private const float MinBodyScale = 0.25f;
	private const float MaxBodyScale = 4f;

	// Where the feet are on a model without foot bones: this far to each side of the hips, on the ground under them
	private const float FootApart = 4f;

	// Each joint's bone on the citizen, the worn model's bone for it, the citizen's bind turn undone, and the model's own
	// marks (a bone and the place on it)
	private readonly BoneCollection.Bone[] _sourceBones;
	private readonly string[] _targetBones;
	private readonly Rotation[] _unbind;
	private readonly string[] _pointBones;
	private readonly int[] _pointIndices;
	private readonly Vector3[] _pointOffsets;
	private readonly float _crownAbove;

	// Without a model the citizen's own bones are the joints, read straight off it
	private readonly bool _isCitizen;

	// Each joint as placed this frame (a bit per joint for found, another for missing)
	private readonly Transform[] _placed;
	private int _found;
	private int _missing;

	/// <summary>How big the model is next to the citizen effects are made on, by height.</summary>
	internal float BodyScale { get; }

	/// <summary>
	/// The joints of <paramref name="model"/>, worn over <paramref name="citizen"/> and posed by <paramref name="transfer"/>;
	/// or, without a transfer, the citizen's own (<paramref name="model"/> is the citizen then).
	/// </summary>
	internal EffectJoints( PoseTransfer transfer, Model citizen, Model model )
	{
		var joints = Enum.GetValues<Effect.Joint>();
		_isCitizen = transfer is null;
		_sourceBones = new BoneCollection.Bone[ joints.Length ];
		_targetBones = new string[ joints.Length ];
		_unbind = new Rotation[ joints.Length ];
		_pointBones = new string[ joints.Length ];
		_pointIndices = new int[ joints.Length ];
		_pointOffsets = new Vector3[ joints.Length ];
		_placed = new Transform[ joints.Length ];

		for ( var i = 0; i < joints.Length; i++ )
		{
			var bone = AccessoryRack.BoneOf( joints[ i ].ToString() );
			_sourceBones[ i ] = citizen.Bones.GetBone( bone );
			_targetBones[ i ] = _sourceBones[ i ] is null ? null : _isCitizen ? bone : transfer.TryMapBone( bone, out _, out var target ) ? target : null;
			_unbind[ i ] = (_sourceBones[ i ]?.LocalTransform.Rotation ?? Rotation.Identity).Inverse;
		}

		Mark( model, Effect.Joint.Eyes, EyesAttachment, null );
		Mark( model, Effect.Joint.LeftEye, LeftEyeAttachment, LeftEyeBone );
		Mark( model, Effect.Joint.RightEye, RightEyeAttachment, RightEyeBone );

		var height = AccessoryCatalog.HeightOf( model );
		BodyScale = Math.Clamp( height / Accessory.CitizenHeight, MinBodyScale, MaxBodyScale );

		// How far the model reaches over its head bone at rest, which differs a lot between rigs: a citizen's head bone is
		// at its jaw, others' near the top of the skull
		var head = _targetBones[ (int)Effect.Joint.Head ] is { } headBone ? model.Bones.GetBone( headBone ) : null;
		_crownAbove = head is null ? 0f : Math.Clamp( model.Bounds.Maxs.z - head.LocalTransform.Position.z, 0f, height * MostCrownAbove );
	}

	/// <summary>Forgets where the joints were: the body was posed again.</summary>
	internal void NextFrame()
	{
		_found = 0;
		_missing = 0;
	}

	/// <summary>
	/// Where <paramref name="joint"/> (an <see cref="Effect.Joint"/>) is this frame, as the model is posed, with the size
	/// effects are worn at as its scale. False while the model or citizen lacks it, or before it is first posed.
	/// </summary>
	internal bool TryGet( int joint, SkinnedModelRenderer citizen, SkinnedModelRenderer model, PoseTransfer transfer, out Transform placed )
	{
		var bit = 1 << joint;
		if ( (_found & bit) != 0 )
		{
			placed = _placed[ joint ];
			return true;
		}

		if ( (_missing & bit) != 0 || !Find( joint, citizen, model, transfer, out placed ) )
		{
			_missing |= bit;
			placed = default;
			return false;
		}

		_placed[ joint ] = placed;
		_found |= bit;
		return true;
	}

	// A joint the model marks with an attachment (or else a bone): the bone it is on and where on it, if it has one
	private void Mark( Model model, Effect.Joint joint, string attachment, string bone )
	{
		if ( model.Attachments?.Get( attachment ) is { Bone: { } on } marked )
		{
			_pointBones[ (int)joint ] = on.Name;
			_pointIndices[ (int)joint ] = on.Index;
			_pointOffsets[ (int)joint ] = marked.LocalTransform.Position;
		}
		else if ( bone is not null && model.Bones.GetBone( bone ) is { } marking )
		{
			_pointBones[ (int)joint ] = bone;
			_pointIndices[ (int)joint ] = marking.Index;
		}
	}

	private bool Find( int joint, SkinnedModelRenderer citizen, SkinnedModelRenderer model, PoseTransfer transfer, out Transform placed )
	{
		var scale = model.WorldScale.x * BodyScale;
		if ( !TryBone( joint, citizen, model, transfer, out var position, out var heading ) )
			return StandIn( joint, citizen, model, transfer, scale, out placed );

		if ( TryMarked( joint, citizen, model, transfer, out var marked ) )
			position = marked;
		else if ( joint == (int)Effect.Joint.Eyes )
			position += heading * (EyesFromHead * scale);
		else if ( joint is (int)Effect.Joint.LeftEye or (int)Effect.Joint.RightEye && TryGet( (int)Effect.Joint.Eyes, citizen, model, transfer, out var eyes ) )
			position = eyes.Position + heading * new Vector3( 0f, joint == (int)Effect.Joint.LeftEye ? EyeApart * scale : -EyeApart * scale, 0f );
		else if ( joint == (int)Effect.Joint.Crown )
			position += heading * new Vector3( 0f, 0f, _crownAbove * model.WorldScale.x );

		placed = new Transform( position, heading, scale );
		return true;
	}

	// Where the bone of a joint is posed now, turned as the citizen's would be
	private bool TryBone( int joint, SkinnedModelRenderer citizen, SkinnedModelRenderer model, PoseTransfer transfer, out Vector3 position, out Rotation heading )
	{
		position = default;
		heading = Rotation.Identity;
		if ( _targetBones[ joint ] is not { } targetBone )
			return false;

		if ( _isCitizen )
		{
			var world = citizen.BoneWorldTransforms;
			var index = _sourceBones[ joint ].Index;
			if ( index >= world.Length )
				return false;

			position = world[ index ].Position;
			heading = world[ index ].Rotation * _unbind[ joint ];
			return true;
		}

		if ( !AccessoryRack.TryGetJoint( citizen, model, transfer, _sourceBones[ joint ], targetBone, out var target, out var turn ) )
			return false;

		position = target.Position;
		heading = turn * _unbind[ joint ];
		return true;
	}

	// Where the model marks a joint (the eyes), posed now
	private bool TryMarked( int joint, SkinnedModelRenderer citizen, SkinnedModelRenderer model, PoseTransfer transfer, out Vector3 position )
	{
		position = default;
		if ( _pointBones[ joint ] is not { } pointBone )
			return false;

		if ( _isCitizen )
		{
			var world = citizen.BoneWorldTransforms;
			if ( _pointIndices[ joint ] >= world.Length )
				return false;

			position = world[ _pointIndices[ joint ] ].PointToWorld( _pointOffsets[ joint ] );
			return true;
		}

		if ( !transfer.TryGetPosedBone( model, pointBone, out var marked ) )
			return false;

		position = marked.PointToWorld( _pointOffsets[ joint ] );
		return true;
	}

	// A joint the model doesn't have: a foot on the ground under the hips, to its side; anything else the hips, or without
	// those the body where it stands
	private bool StandIn( int joint, SkinnedModelRenderer citizen, SkinnedModelRenderer model, PoseTransfer transfer, float scale, out Transform placed )
	{
		const int hips = (int)Effect.Joint.Hips;
		if ( joint == hips || !TryGet( hips, citizen, model, transfer, out var hipsPlaced ) )
		{
			placed = new Transform( model.WorldPosition, model.WorldRotation, scale );
			return true;
		}

		if ( joint is (int)Effect.Joint.LeftFoot or (int)Effect.Joint.RightFoot )
		{
			var side = joint == (int)Effect.Joint.LeftFoot ? FootApart : -FootApart;
			var under = hipsPlaced.Position + model.WorldRotation * new Vector3( 0f, side * scale, 0f );
			placed = new Transform( under.WithZ( model.WorldPosition.z ), model.WorldRotation, scale );
			return true;
		}

		placed = hipsPlaced;
		return true;
	}
}
