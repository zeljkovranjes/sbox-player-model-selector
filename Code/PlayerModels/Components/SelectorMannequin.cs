using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.Components;

/// <summary>
/// The stone citizen the Player Model Selector stands as in the world. Made from code on each machine and never saved: a
/// model the prefab names is packed into the published entity with everything it uses (160 MB for the citizen), though
/// every player already has the citizen. Outside a game it is the addon's logo instead: the entity's thumbnail, its icon
/// in the spawn menu and on sbox.game, is rendered from the prefab.
/// </summary>
[Title( "Selector Mannequin" ), Icon( "accessibility" )]
public sealed class SelectorMannequin : Component, Component.ExecuteInEditor
{
	private const string ObjectName = "Mannequin";
	private const string LogoTexture = "entity/player_model_selector_icon.png";
	private const float LogoSize = 64f;
	private static readonly Color Stone = new( 0.82f, 0.82f, 0.85f );

	private GameObject _statue;

	protected override void OnEnabled()
	{
		_statue = new GameObject( false, ObjectName );
		_statue.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.Hidden | GameObjectFlags.NotNetworked;
		_statue.SetParent( GameObject, false );

		// Thumbnails are rendered outside play, from the prefab or a scene it is cloned into; a spawned one is in a game
		if ( !Game.IsPlaying || Scene.IsEditor || Scene is PrefabScene )
			AddLogo();
		else
			AddCitizen();

		_statue.Enabled = true;
	}

	private void AddCitizen()
	{
		var renderer = _statue.AddComponent<SkinnedModelRenderer>();
		renderer.Model = Model.Load( ModelLoader.CitizenModel );
		renderer.Tint = Stone;
	}

	// Faces whatever looks at it, unlit, standing where the citizen would
	private void AddLogo()
	{
		var renderer = _statue.AddComponent<SpriteRenderer>();
		renderer.Sprite = Sprite.FromTexture( Texture.Load( LogoTexture ) );
		renderer.Size = LogoSize;
		renderer.Billboard = SpriteRenderer.BillboardMode.Always;
		renderer.Lighting = false;
		_statue.LocalPosition = Vector3.Up * LogoSize * 0.5f;
	}

	protected override void OnDisabled()
	{
		if ( _statue.IsValid() )
			_statue.Destroy();

		_statue = null;
	}
}
