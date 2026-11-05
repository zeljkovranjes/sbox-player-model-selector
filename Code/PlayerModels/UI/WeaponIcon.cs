using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// A picture of a weapon design (see <see cref="WeaponDesign"/>): its weapon in a little scene of its own, dressed as the
/// design says (skin, props, stickers; see <see cref="WeaponDresser"/>), seen from its side, its colours moving as they do
/// in the game. Drawn on this machine from the design, so a share needs no picture uploaded; and a weapon as it comes,
/// which the engine's thumbnails draw without its materials.
/// </summary>
public sealed class WeaponIcon : Panel
{
	// The camera looks from the side, this far back for the weapon's length, with a narrow lens so it fills the picture
	private const float FieldOfView = 30f;
	private const float DistanceForLength = 2.1f;
	private const string ReflectionCubemap = "textures/cubemaps/default.vtex";

	/// <summary>The design pictured.</summary>
	[Parameter] public WeaponDesign Design { get; set; }

	/// <summary>The weapon pictured as it comes, by its world model's path, while there is no <see cref="Design"/>.</summary>
	[Parameter] public string Weapon { get; set; }

	private ScenePanel _scene;
	private CameraComponent _camera;
	private GameObject _weapon;
	private readonly WeaponDresser _dresser = new();
	private WeaponDesign _shown;
	private string _shownWeapon;

	public WeaponIcon()
	{
		_scene = AddChild<PreviewScenePanel>( "render" );
		_scene.Style.Width = Length.Percent( 100 );
		_scene.Style.Height = Length.Percent( 100 );
		using ( _scene.RenderScene.Push() )
		{
			_camera = new GameObject( true, "Camera" ).AddComponent<CameraComponent>();
			_camera.BackgroundColor = new Color( 0.1f, 0.12f, 0.15f, 1f );
			_camera.FieldOfView = FieldOfView;
			_camera.ZNear = 1f;

			var sun = new GameObject( true, "Sun" ).AddComponent<DirectionalLight>();
			sun.WorldRotation = Rotation.From( 50, 150, 0 );
			sun.LightColor = new Color( 1f, 0.96f, 0.9f ) * 1.6f;
			sun.SkyColor = new Color( 0.35f, 0.38f, 0.45f );

			var reflections = new GameObject( true, "Reflections" ).AddComponent<EnvmapProbe>();
			reflections.Mode = EnvmapProbe.EnvmapProbeMode.CustomTexture;
			reflections.Texture = Texture.Load( ReflectionCubemap );
			reflections.Bounds = BBox.FromPositionAndSize( Vector3.Zero, 512f );
		}
	}

	public override void Tick()
	{
		base.Tick();

		if ( !ReferenceEquals( Design, _shown ) || Design is null && Weapon != _shownWeapon )
			Show( Design );

		_dresser.Animate();
	}

	private void Show( WeaponDesign design )
	{
		_shown = design;
		_shownWeapon = Weapon;
		_dresser.Release();
		if ( _weapon.IsValid() )
		{
			using ( _scene.RenderScene.Push() )
				_weapon.DestroyImmediate();
		}

		var path = design?.Weapon ?? Weapon;
		if ( string.IsNullOrEmpty( path ) || Model.Load( path ) is not { IsError: false } model )
			return;

		using ( _scene.RenderScene.Push() )
		{
			_weapon = new GameObject( true, "Weapon" );
			var renderer = _weapon.AddComponent<SkinnedModelRenderer>();
			renderer.Model = model;
			renderer.UseAnimGraph = false;
			if ( design is not null )
			{
				_dresser.Apply( new[] { design }, design.Shapes );
				_dresser.Dress( _weapon, null );
			}
		}

		var bounds = model.Bounds;
		_camera.WorldPosition = bounds.Center + Vector3.Right * (bounds.Size.Length * DistanceForLength) + Vector3.Up * (bounds.Size.z * 0.3f);
		_camera.WorldRotation = Rotation.LookAt( bounds.Center - _camera.WorldPosition );
	}

	public override void OnDeleted()
	{
		_dresser.Release();
		base.OnDeleted();
	}
}
