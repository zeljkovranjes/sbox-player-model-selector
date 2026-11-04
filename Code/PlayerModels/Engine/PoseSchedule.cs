using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// How often each body is posed. Posing is the selector's cost for every player, every frame, so with many players only
/// those near the camera are posed every frame; further away a pose a few frames old doesn't show, and out of view a body
/// is still posed now and then for the shadow it casts into view. The body moves with its player every frame either way.
/// The same distances say how much of a body's effects show (see <see cref="EffectsShareFor"/>).
/// </summary>
internal sealed class PoseSchedule : GameObjectSystem<PoseSchedule>
{
	// Always every frame within this of the camera: your own body, behind or under the camera in first person
	private const float NearDistance = 750f;

	// Beyond the near distance, in view: 30 poses a second up to this, 15 past it
	private const float MidDistance = 2000f;
	private const float MidInterval = 1f / 30f;
	private const float FarInterval = 1f / 15f;

	// Out of view
	private const float UnseenInterval = 1f / 10f;

	// A weapon skin's moving colours are worked out and uploaded this often for a body posed near, at mid distance and far;
	// out of view not at all (it shows as it last was)
	private const float NearSkinInterval = 1f / 60f;
	private const float MidSkinInterval = 1f / 30f;
	private const float FarSkinInterval = 1f / 10f;

	// Effects in view between the near and mid distances show this much of their sprites; past it, none
	private const float MidEffectsShare = 0.5f;

	// A body's bounds are its player's, posed this frame; grown so one coming into view at the edge is posed in time
	private const float ViewMargin = 32f;

	private Frustum _view;
	private Vector3 _eye;
	private bool _hasCamera;

	public PoseSchedule( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, ReadCamera, "PoseSchedule.ReadCamera" );
	}

	/// <summary>
	/// Seconds between poses for a body within <paramref name="bounds"/> (in the world): 0 for every frame, as it is
	/// without a camera.
	/// </summary>
	internal float IntervalFor( BBox bounds )
	{
		if ( !_hasCamera )
			return 0f;

		var distance = bounds.Center.Distance( _eye );
		if ( distance < NearDistance )
			return 0f;

		if ( !_view.IsInside( bounds.Grow( ViewMargin ), true ) )
			return UnseenInterval;

		return distance < MidDistance ? MidInterval : FarInterval;
	}

	/// <summary>
	/// Seconds between fills of the weapon skin colours of a body posed every <paramref name="poseInterval"/> seconds (see
	/// <see cref="IntervalFor"/>): 60 a second near, 30 at mid distance, 10 far, and never out of view.
	/// </summary>
	internal static float SkinIntervalFor( float poseInterval ) => poseInterval switch
	{
		<= 0f => NearSkinInterval,
		MidInterval => MidSkinInterval,
		FarInterval => FarSkinInterval,
		_ => float.PositiveInfinity,
	};

	/// <summary>
	/// How much of the effects of a body within <paramref name="bounds"/> (in the world) show, 0 to 1: all of them near
	/// the camera, fewer further away, none past the mid distance, and all without a camera. <paramref name="seen"/> says
	/// whether it is in view, <paramref name="distance"/> how far from the camera it is.
	/// </summary>
	internal float EffectsShareFor( BBox bounds, out bool seen, out float distance )
	{
		seen = true;
		distance = 0f;
		if ( !_hasCamera )
			return 1f;

		distance = bounds.Center.Distance( _eye );
		seen = distance < NearDistance || _view.IsInside( bounds.Grow( ViewMargin ), true );
		return distance < NearDistance ? 1f : distance < MidDistance ? MidEffectsShare : 0f;
	}

	// Once a frame, after the game moved the camera and before the bodies are posed
	private void ReadCamera()
	{
		var camera = Scene.Camera;
		_hasCamera = camera.IsValid();
		if ( !_hasCamera )
			return;

		_eye = camera.WorldPosition;
		_view = camera.GetFrustum();
	}
}
