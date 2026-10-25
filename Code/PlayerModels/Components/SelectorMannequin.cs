using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.Components;

/// <summary>
/// The stone citizen the Player Model Selector stands as in the world. Made from code on each machine and never saved: a
/// model the prefab names is packed into the published entity with everything it uses (160 MB for the citizen), though
/// every player already has the citizen. Runs in the editor too, so the entity's thumbnail shows it.
/// </summary>
[Title( "Selector Mannequin" ), Icon( "accessibility" )]
public sealed class SelectorMannequin : Component, Component.ExecuteInEditor
{
	private const string ObjectName = "Mannequin";
	private static readonly Color Stone = new( 0.82f, 0.82f, 0.85f );

	private GameObject _statue;

	protected override void OnEnabled()
	{
		_statue = new GameObject( false, ObjectName );
		_statue.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.Hidden | GameObjectFlags.NotNetworked;
		_statue.SetParent( GameObject, false );

		var renderer = _statue.AddComponent<SkinnedModelRenderer>();
		renderer.Model = Model.Load( ModelLoader.CitizenModel );
		renderer.Tint = Stone;

		_statue.Enabled = true;
	}

	protected override void OnDisabled()
	{
		if ( _statue.IsValid() )
			_statue.Destroy();

		_statue = null;
	}
}
