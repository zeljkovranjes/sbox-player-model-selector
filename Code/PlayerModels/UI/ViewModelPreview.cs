using System.Threading.Tasks;
using PlayerModels.Components;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;

namespace PlayerModels.UI;

/// <summary>
/// The View Model preview: first-person arms the way the game shows them, bone-merged onto Sandbox's physgun view model
/// playing its idle, seen from the view origin; or the worn model's own arms posed from it, when those are used.
/// </summary>
public sealed class ViewModelPreview : Panel
{
	private const string WeaponModel = "models/weapons/sbox_physgun/v_physgun.vmdl";
	private const float FieldOfView = 48f;

	// The preview is tall and narrow while a view model is framed for a wide screen, with the weapon off to the right:
	// from a little behind the eye, turn towards the weapon so both hands and the weapon fit
	private static readonly Vector3 EyeOffset = new( -6f, 0f, 0f );
	private static readonly Angles TurnToWeapon = new( 5f, -11f, 0f );

	// A worn model's shoulders sit around the eye, where the game's camera is; looking from behind the eye, the preview
	// would be inside them. Everything nearer than the eye is clipped instead, as the game's camera leaves it behind.
	private const float NearClip = 1f;
	private static readonly float BodyArmsNearClip = -EyeOffset.x + 1f;

	private readonly ScenePanel _scene;
	private readonly Label _message;
	private SkinnedModelRenderer _arms;
	private CameraComponent _camera;

	private string _shown;
	private int _request;

	// The worn model's arms: the plan wanted (see ArmsPlan.KeyOf), the plan once loaded, and the arms shown
	private string _bodyKey = "";
	private ArmsPlan _plan;
	private ModelArms _bodyArms;

	/// <summary>The look whose model's own arms are shown on the weapon instead of the arms, or null for the arms.</summary>
	public PlayerLook BodyArms { get; set; }

	public ViewModelPreview()
	{
		_scene = AddChild<ScenePanel>( "render" );
		_message = Add.Label( "", "message" );

		BuildScene();
	}

	/// <summary>Shows <paramref name="model"/> as first-person arms, or Sandbox's own when it is null or empty.</summary>
	public async Task ShowAsync( string model )
	{
		model ??= "";
		if ( model == _shown )
			return;

		_shown = model;
		var request = ++_request;
		var arms = await ModelLoader.LoadAsync( model == "" ? ViewModelArms.DefaultArms : model );

		// Another one was asked for while this loaded
		if ( request != _request || !_arms.IsValid() )
			return;

		SetMessage( arms is null ? "These arms couldn't be loaded." : null );
		if ( arms is not null )
		{
			_arms.Model = arms;
			StandInMaterials.Apply( _arms );
		}
	}

	public override void Tick()
	{
		base.Tick();

		// A hot reload keeps the plan but not state new code added to it: it is made again
		var key = ArmsPlan.KeyOf( BodyArms );
		if ( key != _bodyKey || _plan is { Transfer.IsIntact: false } )
		{
			_bodyKey = key;
			_plan = null;
			RemoveBodyArms();

			if ( key != "" )
				_ = LoadPlanAsync( BodyArms );
		}

		if ( _plan is not null && !_bodyArms.IsValid() && _arms.IsValid() && ModelArms.CanStandIn( _arms, _plan ) )
		{
			using ( _scene.RenderScene.Push() )
				_bodyArms = ModelArms.Attach( _arms, _plan );
		}

		// The preview's scene isn't run by the game: the arms are posed from here
		if ( _bodyArms.IsValid() )
		{
			_bodyArms.ApplyLook( BodyArms );
			_bodyArms.Tick();

			// Arms that took themselves off
			if ( !_bodyArms.GameObject.Enabled )
				RemoveBodyArms();
		}

		if ( _camera.IsValid() )
			_camera.ZNear = _bodyArms.IsValid() ? BodyArmsNearClip : NearClip;
	}

	private async Task LoadPlanAsync( PlayerLook look )
	{
		var key = ArmsPlan.KeyOf( look );
		var plan = await ArmsPlan.LoadAsync( look );

		// Wanted something else while this loaded
		if ( key == _bodyKey )
			_plan = plan;
	}

	// Removed at once: the preview's scene never gets to removing objects
	private void RemoveBodyArms()
	{
		if ( _bodyArms.IsValid() )
		{
			var arms = _bodyArms.GameObject;
			_bodyArms.TakeOff();
			arms.DestroyImmediate();
		}

		_bodyArms = null;
	}

	private void BuildScene()
	{
		using ( _scene.RenderScene.Push() )
		{
			// The view model's origin is the eye: look down its forward axis from near there
			var camera = new GameObject( true, "Camera" ).AddComponent<CameraComponent>();
			camera.WorldPosition = EyeOffset;
			camera.WorldRotation = TurnToWeapon;
			camera.BackgroundColor = Color.Transparent;
			camera.FieldOfView = FieldOfView;
			camera.ZNear = NearClip;
			camera.ZFar = 512;
			_camera = camera;

			var sunObject = new GameObject( true, "Sun" );
			sunObject.WorldRotation = Rotation.From( 50, 150, 0 );
			var sun = sunObject.AddComponent<DirectionalLight>();
			sun.LightColor = new Color( 1.0f, 0.96f, 0.9f ) * 1.6f;
			sun.SkyColor = new Color( 0.35f, 0.38f, 0.45f );

			var weaponObject = new GameObject( true, "Weapon" );
			var weapon = weaponObject.AddComponent<SkinnedModelRenderer>();
			weapon.Model = Model.Load( WeaponModel );

			var armsObject = new GameObject( weaponObject, true, "Arms" );
			_arms = armsObject.AddComponent<SkinnedModelRenderer>();
			_arms.BoneMergeTarget = weapon;
		}

		_ = ShowAsync( "" );
	}

	private void SetMessage( string message )
	{
		_message.Text = message ?? "";
		_message.SetClass( "visible", !string.IsNullOrEmpty( message ) );
	}
}
