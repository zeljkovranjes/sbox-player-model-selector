using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlayerModels.Components;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;

namespace PlayerModels.UI;

/// <summary>
/// The Model window's preview: a citizen in a scene of its own, dressed as you, with a picked model posed on it the
/// same way the game does it. Shows the whole body; drag to spin it, scroll to zoom in towards the face. Built on the
/// engine menu's citizen preview.
/// </summary>
public sealed class ModelPreview : Panel
{
	// Third person: a narrow lens from far back, so the whole body fits the tall panel without distortion
	private const float ThirdPersonFieldOfView = 22f;
	private const float ThirdPersonDistance = 120f;
	private const float ThirdPersonHeight = 38f;

	private const float StartYaw = -20f;
	private const float DragDegreesPerPixel = 0.6f;

	// Mouse wheel zoom: 1 shows the whole body, MaxZoom is close enough for the face; each notch is ZoomStep
	private const float MaxZoom = 4f;
	private const float ZoomStep = 1.15f;

	// Pointing at a part: Sandbox's hover outline for body groups, a pulse to this colour for coloured parts
	private const string NoDrawMaterial = "materials/tools/toolsnodraw.vmat";
	private static readonly Color OutlineColor = new( 2.6f, 2.0f, 0.2f, 1f );
	private static readonly Color OutlineFill = new( 1f, 0.8f, 0.1f, 0.25f );
	private static readonly Color PulseColor = new( 1f, 0.85f, 0.2f );
	private const float PulsesPerSecond = 1.5f;

	// The Armature page's skeleton lines
	private static readonly Color SkeletonColor = new( 0.35f, 0.8f, 1f, 0.9f );

	/// <summary>How the worn model is customised; shown on it when it is the model in the preview.</summary>
	public PlayerLook Look { get; set; }

	/// <summary>The part of the shown model to point out, or null for none.</summary>
	public PartHighlight? Highlight { get; set; }

	/// <summary>Draws the shown model's bones over it, for picking bones on the Armature page.</summary>
	public bool ShowSkeleton { get; set; }

	/// <summary>The bone drawn lit up on the skeleton, or null.</summary>
	public string SelectedBone { get; set; }

	private ScenePanel _scene;
	private SkinnedModelRenderer _citizen;
	private CameraComponent _camera;
	private ModelBody _body;
	private SkinnedModelRenderer _unposed;
	private SkinnedModelRenderer _ghost;
	private HighlightOutline _outline;
	private readonly Label _loading;
	private readonly Label _message;
	private CancellationTokenSource _cancel;

	private string _shown;
	private string _shownBones;
	private int _request;
	private float _yaw = StartYaw;
	private float _zoom = 1f;
	private bool _dragging;
	private float _lastDragX;

	public ModelPreview()
	{
		_scene = AddChild<ScenePanel>( "render" );
		_loading = Add.Label( "Loading model...", "loading" );
		_message = Add.Label( "", "message" );

		BuildScene();
	}

	/// <summary>Shows <paramref name="model"/>, or the citizen dressed as you when it is null or empty.</summary>
	public async Task ShowAsync( string model )
	{
		model ??= "";
		if ( model == _shown )
			return;

		var request = ++_request;
		_shown = model;
		_shownBones = BonesFor( model );
		RemoveBody();
		SetMessage( null );

		if ( model == "" )
		{
			SetLoading( false );
			return;
		}

		SetLoading( true );
		var loaded = await ModelLoader.LoadAsync( model );

		// Another model was asked for while this one loaded
		if ( request != _request || !_citizen.IsValid() )
			return;

		SetLoading( false );

		if ( loaded is null )
		{
			SetMessage( "This model couldn't be loaded." );
			return;
		}

		// The model being customised is posed with the bones picked for it on the Armature page
		var bones = Look is not null && Look.Model == model ? Look.Bones : null;
		var transfer = PoseTransfer.TryCreate( _citizen.Model, loaded, bones, out var error );
		if ( transfer is null )
		{
			SetMessage( error );

			// Only the model being set up stands unposed for picking bones; a hovered one just says why it can't be worn
			if ( bones is not null )
				ShowUnposed( loaded );

			return;
		}

		// Stock characters are shown in your own outfit, as they'll be worn
		var outfit = ModelOutfit.IsStock( model ) ? ModelOutfit.For( model, ClothingContainer.CreateFromLocalUser() ) : null;

		using ( _scene.RenderScene.Push() )
			_body = ModelBody.Attach( _citizen, model, _shownBones, loaded, transfer, outfit );
	}

	// The bone picks the model is posed with: the customised look's for the model being customised, none otherwise
	private string BonesFor( string model )
	{
		return Look is not null && Look.Model == model ? Look.BonesKey() : "";
	}

	// A model that can't be posed yet stands in its own bind pose, so its bones can still be picked on the Armature page.
	// The citizen steps aside meanwhile, so only one model stands there.
	private void ShowUnposed( Model model )
	{
		_citizen.GameObject.Enabled = false;

		using ( _scene.RenderScene.Push() )
		{
			var unposedObject = new GameObject( true, "Unposed" );
			unposedObject.WorldRotation = Rotation.FromYaw( _yaw );
			_unposed = unposedObject.AddComponent<SkinnedModelRenderer>();
			_unposed.Model = model;
			_unposed.UseAnimGraph = false;
		}
	}

	public override void Tick()
	{
		base.Tick();

		if ( !_citizen.IsValid() || !_camera.IsValid() )
			return;

		UpdateSpin();
		_citizen.GameObject.WorldRotation = Rotation.FromYaw( _yaw );

		// Bones picked differently need a new pose plan (or make one possible at all)
		if ( _shown is not null && BonesFor( _shown ) != _shownBones )
		{
			ShowAgain();
			return;
		}

		if ( _unposed.IsValid() )
		{
			_unposed.GameObject.WorldRotation = Rotation.FromYaw( _yaw );
			DrawSkeleton( _unposed );
		}

		if ( _body.IsValid() )
		{
			// Customisation belongs to the model being customised; a hovered one shows as it comes
			if ( Look is not null && Look.Model == _body.ModelId )
				_body.ApplyLook( WithPulse( Look ) );

			_body.Tick();
			OutlineBodyGroup();
			DrawSkeleton( _body.Renderer );
		}
		else if ( _body is not null )
			ShowAgain();

		PlaceCamera();
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );

		if ( e.MouseButton != MouseButtons.Left )
			return;

		_dragging = true;
		_lastDragX = MousePosition.x;
	}

	// Wheel up zooms in. Not passed on: the wheel over the preview shouldn't scroll anything else
	public override void OnMouseWheel( Vector2 value )
	{
		_zoom = Math.Clamp( _zoom * MathF.Pow( ZoomStep, -value.y ), 1f, MaxZoom );
	}

	public override void OnDeleted()
	{
		_cancel?.Cancel();
		base.OnDeleted();
	}

	private void BuildScene()
	{
		_cancel = new CancellationTokenSource();

		using ( _scene.RenderScene.Push() )
		{
			var cameraObject = new GameObject( true, "Camera" );
			_camera = cameraObject.AddComponent<CameraComponent>();
			_camera.BackgroundColor = Color.Transparent;
			_camera.ZNear = 1;
			_camera.ZFar = 1024;

			// Draws the outline of the part being pointed at
			cameraObject.AddComponent<Highlight>();

			var sunObject = new GameObject( true, "Sun" );
			sunObject.WorldRotation = Rotation.From( 50, 150, 0 );

			var sun = sunObject.AddComponent<DirectionalLight>();
			sun.LightColor = new Color( 1.0f, 0.96f, 0.9f ) * 1.6f;
			sun.SkyColor = new Color( 0.35f, 0.38f, 0.45f );

			var bodyObject = new GameObject( true, "Citizen" );
			_citizen = bodyObject.AddComponent<SkinnedModelRenderer>();
			_citizen.Model = Model.Load( ModelLoader.CitizenModel );

			_ = DressAsync( _cancel.Token );
		}

		PlaceCamera();
	}

	// The default look is you: the citizen in your own avatar
	private async Task DressAsync( CancellationToken token )
	{
		try
		{
			using ( _scene.RenderScene.Push() )
			{
				var clothing = ClothingContainer.CreateFromLocalUser();
				clothing.Normalize();

				var dresser = Dresser.GetOrCreate( _citizen );
				dresser.UpdateAppearance( clothing );
				await dresser.ApplyAsync( clothing, token );
			}
		}
		catch ( OperationCanceledException )
		{
			// The window closed before the clothes arrived
		}
		catch ( Exception e )
		{
			Log.Warning( $"Player Model Selector: couldn't dress the preview: {e.Message}" );
		}
	}

	private void PlaceCamera()
	{
		// The shown model's own eyes, where zooming in heads, so a short or tall model is framed by its own head
		var eyes = (_body.IsValid() ? _body.Renderer?.GetAttachment( "eyes" ) : null) ?? _citizen.GetAttachment( "eyes" );

		// Zooming in moves closer and rises from the middle of the body to the eyes, so all the way in is the face
		var closeness = (_zoom - 1f) / (MaxZoom - 1f);
		var eyeHeight = eyes?.Position.z ?? ThirdPersonHeight;
		_camera.FieldOfView = ThirdPersonFieldOfView;
		_camera.WorldPosition = new Vector3( ThirdPersonDistance / _zoom, 0, MathX.Lerp( ThirdPersonHeight, eyeHeight, closeness ) );
		_camera.WorldRotation = Rotation.FromYaw( 180 );
	}

	// While the left button is held, the model turns with the cursor
	private void UpdateSpin()
	{
		if ( _dragging && !HasActive )
			_dragging = false;

		if ( !_dragging )
			return;

		var x = MousePosition.x;
		_yaw += (x - _lastDragX) * DragDegreesPerPixel;
		_lastDragX = x;
	}

	// A coloured part being pointed at pulses between its own colour and the highlight colour
	private PlayerLook WithPulse( PlayerLook look )
	{
		if ( Highlight?.Material is not { } part )
			return look;

		var own = look.Parts.TryGetValue( part, out var hex ) ? Color.Parse( hex ) ?? Color.White : Color.White;
		var pulse = (MathF.Sin( RealTime.Now * PulsesPerSecond * MathF.Tau ) + 1f) * 0.5f;

		var pulsing = look.WithModel( look.Model );
		pulsing.Parts[ part ] = Color.Lerp( own, PulseColor, pulse ).Hex[ ..7 ];
		return pulsing;
	}

	// Outlines the body group choice being pointed at. An invisible copy of the model showing only that choice's meshes
	// carries the outline, so a choice not picked yet is outlined where it would go.
	private void OutlineBodyGroup()
	{
		var mask = HighlightMask();
		if ( mask == 0 )
		{
			// Off entirely, not only its outline: the copy itself must never show as a second model
			if ( _ghost.IsValid() )
				_ghost.GameObject.Enabled = false;

			return;
		}

		if ( !_ghost.IsValid() || _ghost.BoneMergeTarget != _body.Renderer )
			MakeGhost();

		_ghost.GameObject.Enabled = true;
		_ghost.BodyGroups = mask;
		_outline.Enabled = true;
	}

	private ulong HighlightMask()
	{
		if ( Highlight?.BodyGroup is not { } group || _body.Renderer?.Model is not { } model )
			return 0;

		var part = model.BodyParts.FirstOrDefault( x => x.Name == group );
		var choice = Highlight.Value.Choice;

		return part is not null && choice >= 0 && choice < part.Choices.Count ? part.Choices[ choice ].Mask : 0;
	}

	private void MakeGhost()
	{
		RemoveGhost();

		using ( _scene.RenderScene.Push() )
		{
			var ghostObject = new GameObject( true, "Highlight" );
			_ghost = ghostObject.AddComponent<SkinnedModelRenderer>();
			_ghost.Model = _body.Renderer.Model;
			_ghost.BoneMergeTarget = _body.Renderer;
			_ghost.MaterialOverride = Material.Load( NoDrawMaterial );

			_outline = ghostObject.AddComponent<HighlightOutline>();
			_outline.OverrideTargets = true;
			_outline.Targets = new() { _ghost };
			// The body draws the same meshes in the same place, so the copy always counts as hidden: draw it as if seen
			_outline.Color = OutlineColor;
			_outline.ObscuredColor = OutlineColor;
			_outline.InsideColor = OutlineFill;
			_outline.InsideObscuredColor = OutlineFill;
			_outline.Width = 0.8f;
		}
	}

	private void RemoveGhost()
	{
		if ( _ghost.IsValid() )
		{
			using ( _scene.RenderScene.Push() )
				_ghost.GameObject.DestroyImmediate();
		}

		_ghost = null;
		_outline = null;
	}

	// The skeleton as a line from each bone to its parent, drawn over the model, with the picked bone lit up. Allocates a
	// transform array each frame, only while the Armature page is open.
	private void DrawSkeleton( SkinnedModelRenderer renderer )
	{
		if ( !ShowSkeleton || renderer?.Model is not { } model )
			return;

		var world = renderer.GetBoneTransforms( true );
		var overlay = _scene.RenderScene.DebugOverlay;

		foreach ( var bone in model.Bones.AllBones )
		{
			if ( bone.Index >= world.Length || IsHelper( bone.Name ) )
				continue;

			var selected = bone.Name == SelectedBone;
			if ( bone.Parent is { } parent && parent.Index < world.Length )
				overlay.Line( world[ parent.Index ].Position, world[ bone.Index ].Position, selected ? PulseColor : SkeletonColor, 0f, global::Transform.Zero, true );

			if ( selected )
				overlay.Sphere( new Sphere( world[ bone.Index ].Position, 1.2f ), PulseColor, 0f, global::Transform.Zero, true );
		}
	}

	// IK targets and attachment helpers hang off the root far from the body; drawn, they only clutter the skeleton
	private static bool IsHelper( string bone )
	{
		return bone.Contains( "ik", StringComparison.OrdinalIgnoreCase ) && (bone.Contains( "_ik", StringComparison.OrdinalIgnoreCase )
				|| bone.StartsWith( "ik", StringComparison.OrdinalIgnoreCase ) || bone.Contains( "ikrule", StringComparison.OrdinalIgnoreCase ))
			|| bone.Contains( "aim_matrix", StringComparison.OrdinalIgnoreCase )
			|| bone.Contains( "attach", StringComparison.OrdinalIgnoreCase );
	}

	// The same model again: the body took itself off (it couldn't pose after a hot reload), or the bone picks changed
	// while it was up. ShowAsync takes the old one off first.
	private void ShowAgain()
	{
		var model = _shown;
		_shown = null;
		_ = ShowAsync( model );
	}

	// Right away: this scene isn't the game's, and a deferred destroy would leave two models standing for a frame
	private void RemoveBody()
	{
		RemoveGhost();

		if ( _unposed.IsValid() )
		{
			using ( _scene.RenderScene.Push() )
				_unposed.GameObject.DestroyImmediate();
		}

		_unposed = null;

		// Every body on the citizen, not only the current one: this scene doesn't run the game loop, so a body that took
		// itself off (a deferred destroy, after a hot reload) would otherwise stay standing under the next one
		if ( _citizen.IsValid() )
		{
			_citizen.GameObject.Enabled = true;

			using ( _scene.RenderScene.Push() )
			{
				foreach ( var body in _citizen.GameObject.Children.Where( x => x.Name == ModelBody.ObjectName ).ToArray() )
					body.DestroyImmediate();
			}
		}

		_body = null;
	}

	private void SetLoading( bool loading )
	{
		_loading.SetClass( "visible", loading );
	}

	private void SetMessage( string message )
	{
		_message.Text = message ?? "";
		_message.SetClass( "visible", !string.IsNullOrEmpty( message ) );
	}
}
