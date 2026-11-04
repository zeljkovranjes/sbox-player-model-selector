using System;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Where each joint effects come from is on one body (see <see cref="Effect.Joint"/>), worked out once a frame however
/// many layers come from it. A joint is the worn model's bone for the citizen's (as items find theirs, see
/// <see cref="AccessoryRack.TryGetJoint"/>), facing the way the body faces at rest turned as the joint is now, and as big
/// as the model is next to the citizen effects are made on. The eyes are where the model marks them, or else in front of
/// its head; the crown is as high over the head bone as the model reaches. Runs every frame: no allocations.
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

	// Each joint's bone on the citizen, the worn model's bone for it, the citizen's bind turn undone, and the model's own
	// marks (a bone and the place on it)
	private readonly BoneCollection.Bone[] _sourceBones;
	private readonly string[] _targetBones;
	private readonly Rotation[] _unbind;
	private readonly string[] _pointBones;
	private readonly Vector3[] _pointOffsets;
	private readonly float _crownAbove;

	// Each joint as placed this frame (a bit per joint for found, another for missing)
	private readonly Transform[] _placed;
	private int _found;
	private int _missing;

	/// <summary>How big the model is next to the citizen effects are made on, by height.</summary>
	internal float BodyScale { get; }

	/// <summary>The joints of <paramref name="model"/>, worn over <paramref name="citizen"/> and posed by <paramref name="transfer"/>.</summary>
	internal EffectJoints( PoseTransfer transfer, Model citizen, Model model )
	{
		var joints = Enum.GetValues<Effect.Joint>();
		_sourceBones = new BoneCollection.Bone[ joints.Length ];
		_targetBones = new string[ joints.Length ];
		_unbind = new Rotation[ joints.Length ];
		_pointBones = new string[ joints.Length ];
		_pointOffsets = new Vector3[ joints.Length ];
		_placed = new Transform[ joints.Length ];

		for ( var i = 0; i < joints.Length; i++ )
		{
			var bone = AccessoryRack.BoneOf( joints[ i ].ToString() );
			_sourceBones[ i ] = citizen.Bones.GetBone( bone );
			_targetBones[ i ] = _sourceBones[ i ] is not null && transfer.TryMapBone( bone, out _, out var target ) ? target : null;
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
			_pointOffsets[ (int)joint ] = marked.LocalTransform.Position;
		}
		else if ( bone is not null && model.Bones.HasBone( bone ) )
		{
			_pointBones[ (int)joint ] = bone;
		}
	}

	private bool Find( int joint, SkinnedModelRenderer citizen, SkinnedModelRenderer model, PoseTransfer transfer, out Transform placed )
	{
		placed = default;
		if ( _targetBones[ joint ] is not { } targetBone
			|| !AccessoryRack.TryGetJoint( citizen, model, transfer, _sourceBones[ joint ], targetBone, out var target, out var turn ) )
			return false;

		var heading = turn * _unbind[ joint ];
		var scale = model.WorldScale.x * BodyScale;
		var position = target.Position;

		if ( _pointBones[ joint ] is { } pointBone && transfer.TryGetPosedBone( model, pointBone, out var marked ) )
			position = marked.PointToWorld( _pointOffsets[ joint ] );
		else if ( joint == (int)Effect.Joint.Eyes )
			position += heading * (EyesFromHead * scale);
		else if ( joint is (int)Effect.Joint.LeftEye or (int)Effect.Joint.RightEye && TryGet( (int)Effect.Joint.Eyes, citizen, model, transfer, out var eyes ) )
			position = eyes.Position + heading * new Vector3( 0f, joint == (int)Effect.Joint.LeftEye ? EyeApart * scale : -EyeApart * scale, 0f );
		else if ( joint == (int)Effect.Joint.Crown )
			position += heading * new Vector3( 0f, 0f, _crownAbove * model.WorldScale.x );

		placed = new Transform( position, heading, scale );
		return true;
	}
}
