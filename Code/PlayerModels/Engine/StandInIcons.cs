using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Pictures of mounted models drawn with their stand-in materials (<see cref="StandInMaterials"/>), for the ones whose
/// own shader doesn't load: the engine's thumbnail draws those with the error material. Drawn the way the engine draws
/// its thumbnails, one model at a time (each is loaded), and saved on this machine, so a model is loaded to be pictured
/// once, not every session.
/// </summary>
internal static class StandInIcons
{
	private const int RenderSize = 512;
	private const int IconSize = 256;

	// The pictures drawn, as files in the addon's data folder, and for each model whether it needs one: the engine's
	// thumbnail is right for one that doesn't. Renamed whenever what needs a picture changes, so models judged under
	// older rules are judged again
	private const string IconFolder = "player_model_selector/icons";
	private static readonly TimedCache<bool> Needed = new( "player_model_selector/stand_in_icons.json", TimeSpan.FromDays( 30 ), 8192 );

	// Each model asked about: its picture, or null when its own shader loads and the engine's thumbnail is right
	private static readonly Dictionary<string, Texture> Icons = new();
	private static readonly HashSet<string> Asked = new();
	private static Task _drawing = Task.CompletedTask;

	/// <summary>
	/// Whether it is known yet how <paramref name="model"/> should be pictured: <paramref name="icon"/> is its picture
	/// with stand-ins, or null for the engine's own thumbnail. The first ask queues the model to be loaded and drawn.
	/// </summary>
	internal static bool TryGet( string model, out Texture icon )
	{
		icon = null;
		if ( !MountModels.IsMountPath( model ) )
			return true;

		if ( Icons.TryGetValue( model, out icon ) )
			return true;

		if ( Asked.Add( model ) )
			_drawing = DrawAfterAsync( _drawing, model );

		return false;
	}

	private static async Task DrawAfterAsync( Task before, string path )
	{
		await before;

		try
		{
			Icons[ path ] = await DrawAsync( path );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Player Model Selector: couldn't picture {path} with stand-in materials: {e.Message}" );
			Icons[ path ] = null;
		}
	}

	private static async Task<Texture> DrawAsync( string path )
	{
		var file = $"{IconFolder}/{path.Md5()}.png";
		if ( Needed.TryGet( path, out var needs ) )
		{
			if ( !needs )
				return null;

			if ( FileSystem.Data.FileExists( file ) )
			{
				using var saved = Bitmap.CreateFromBytes( await FileSystem.Data.ReadAllBytesAsync( file ) );
				if ( saved is not null )
					return saved.ToTexture();
			}
		}

		// Not kept when it can't be loaded: it may load next time
		var model = await ModelLoader.LoadAsync( path );
		if ( model is null )
			return null;

		needs = StandInMaterials.Needs( model );
		Needed.Set( path, needs );
		if ( !needs )
			return null;

		using var bitmap = new Bitmap( RenderSize, RenderSize );
		Draw( model, bitmap );

		using var icon = bitmap.Resize( IconSize, IconSize );
		FileSystem.Data.CreateDirectory( IconFolder );
		FileSystem.Data.WriteAllBytes( file, icon.ToPng() );
		return icon.ToTexture();
	}

	// The engine's own thumbnail scene (SceneUtility.RenderModelBitmap, which can't take stand-ins): the same camera
	// angle, lights and reflections, framing the whole model
	private static void Draw( Model model, Bitmap bitmap )
	{
		var scene = new Scene();
		try
		{
			using var scope = scene.Push();

			var camera = new GameObject( true, "camera" ).AddComponent<CameraComponent>();
			camera.BackgroundColor = Color.Transparent;
			camera.WorldRotation = new Angles( 20, 180 + 45, 0 );
			camera.FieldOfView = 50f;
			camera.ZNear = 0.1f;
			camera.ZFar = 15000f;

			new GameObject( true, "ambient" ).AddComponent<AmbientLight>().Color = Color.White * 0.2f;

			var sun = new GameObject( true, "sun" ).AddComponent<DirectionalLight>();
			sun.Shadows = true;
			sun.WorldRotation = new Angles( 60, 45, 0 );
			sun.LightColor = Color.White * 0.4f;

			var envmap = new GameObject( true, "envmap" ).AddComponent<EnvmapProbe>();
			envmap.Mode = EnvmapProbe.EnvmapProbeMode.CustomTexture;
			envmap.Texture = Texture.Load( "textures/cubemaps/default2.vtex" );
			envmap.Bounds = BBox.FromPositionAndSize( Vector3.Zero, 100000 );

			var body = new GameObject( true, "model" );
			var renderer = body.AddComponent<ModelRenderer>();
			renderer.Model = model;
			StandInMaterials.Apply( renderer );

			var bounds = body.GetBounds();
			var distance = bounds.Size.Length * 0.5f / MathF.Sin( camera.FieldOfView.DegreeToRadian() * 0.5f );
			camera.WorldPosition = bounds.Center + camera.WorldRotation.Forward * -distance;

			camera.RenderToBitmap( bitmap );
		}
		finally
		{
			scene.Destroy();
		}
	}
}
