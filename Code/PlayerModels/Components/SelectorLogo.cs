using Sandbox;

namespace PlayerModels.Components;

/// <summary>
/// The Player Model Selector's logo, outside a game: the entity's thumbnail, its icon in the spawn menu and on sbox.game,
/// is rendered from the prefab. In a game the selector shows nothing in the world. Made from code and never saved.
/// </summary>
[Title( "Selector Logo" ), Icon( "image" )]
public sealed class SelectorLogo : Component, Component.ExecuteInEditor
{
	private const string ObjectName = "Logo";
	private const string LogoTexture = "entity/player_model_selector_icon.png";
	private const float LogoSize = 64f;

	private GameObject _logo;

	protected override void OnEnabled()
	{
		// Thumbnails are rendered outside play, from the prefab or a scene it is cloned into; a spawned one is in a game
		if ( Game.IsPlaying && !Scene.IsEditor && Scene is not PrefabScene )
			return;

		_logo = new GameObject( false, ObjectName );
		_logo.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.Hidden | GameObjectFlags.NotNetworked;
		_logo.SetParent( GameObject, false );
		_logo.LocalPosition = Vector3.Up * LogoSize * 0.5f;

		// Faces whatever looks at it, unlit
		var renderer = _logo.AddComponent<SpriteRenderer>();
		renderer.Sprite = Sprite.FromTexture( Texture.Load( LogoTexture ) );
		renderer.Size = LogoSize;
		renderer.Billboard = SpriteRenderer.BillboardMode.Always;
		renderer.Lighting = false;

		// A sprite has no bounds of its own, and the thumbnail is framed by the bounds: a box the logo's size gives them
		var frame = _logo.AddComponent<BoxCollider>();
		frame.Scale = LogoSize;
		frame.IsTrigger = true;
		frame.Static = true;

		_logo.Enabled = true;
	}

	protected override void OnDisabled()
	{
		if ( _logo.IsValid() )
			_logo.Destroy();

		_logo = null;
	}
}
