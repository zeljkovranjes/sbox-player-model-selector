using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.Components;

/// <summary>
/// Shows a picked model in place of a citizen body. The citizen keeps animating (Sandbox's animation, IK and ragdoll
/// physics all still drive it) but is hidden with its clothes, and the picked model is posed from it every frame.
/// A stock character is dressed in the player's outfit. Made on each client for each body, never networked: every
/// client works the pose out for itself.
/// </summary>
public sealed class ModelBody : Component
{
	// How often the citizen's renderers are hidden again, for clothing the dresser puts on after we started.
	private const float HideInterval = 0.5f;

	/// <summary>The citizen renderer whose pose drives the model.</summary>
	public SkinnedModelRenderer Source { get; private set; }

	/// <summary>The name of the object each body is made on, under the citizen it shows on.</summary>
	internal const string ObjectName = "Player Model";

	/// <summary>The entry's model this body shows.</summary>
	public string ModelId { get; private set; }

	/// <summary>The bones picked by hand it was posed with (see <see cref="PlayerLook.BonesKey"/>).</summary>
	internal string BonesKey { get; private set; } = "";

	/// <summary>The renderer showing the picked model.</summary>
	internal SkinnedModelRenderer Renderer => _renderer;

	private SkinnedModelRenderer _renderer;
	private PoseTransfer _transfer;
	private readonly List<ModelRenderer> _hidden = new();
	private readonly List<ModelRenderer> _own = new();
	private readonly HeldItems _held = new();
	private readonly CancellationTokenSource _cancel = new();
	private TimeSince _sinceHidden;
	private LookPainter _painter;

	/// <summary>
	/// Puts <paramref name="model"/> on <paramref name="source"/>'s body, in the scene that is active now, dressed in
	/// <paramref name="outfit"/> when there is one (stock characters only).
	/// </summary>
	internal static ModelBody Attach( SkinnedModelRenderer source, string modelId, string bonesKey, Model model, PoseTransfer transfer,
		ClothingContainer outfit )
	{
		var go = new GameObject( true, ObjectName );
		go.NetworkMode = NetworkMode.Never;
		go.Flags |= GameObjectFlags.NotSaved;
		go.SetParent( source.GameObject, false );
		go.LocalTransform = global::Transform.Zero;

		var renderer = go.AddComponent<SkinnedModelRenderer>();
		renderer.Model = model;
		renderer.UseAnimGraph = false;

		var body = go.AddComponent<ModelBody>();
		body.Source = source;
		body.ModelId = modelId;
		body.BonesKey = bonesKey ?? "";
		body._renderer = renderer;
		body._transfer = transfer;
		body.FindOwnRenderers();
		body.HideSource();
		body.FindHeldItems();
		body.Pose();

		if ( outfit is not null )
			_ = body.DressAsync( outfit );

		return body;
	}

	/// <summary>
	/// Customises the model as <paramref name="look"/> says: body group choices, morph weights, skin, colours and body
	/// shape. Whatever it no longer lists goes back to the model's default.
	/// </summary>
	internal void ApplyLook( PlayerLook look )
	{
		if ( !_renderer.IsValid() || look is null )
			return;

		// Made here rather than with the body: a hot reload keeps the component but not fields added since
		_painter ??= new LookPainter();
		if ( !_painter.Apply( _renderer, look ) )
			return;

		// The body shape bends the pose itself; only the height is the renderer's
		_renderer.GameObject.LocalScale = _transfer.Shape( look.Shape );
	}

	/// <summary>
	/// The body shown on this citizen renderer, if any. Any other body found under it is taken off: a citizen shows one
	/// body, whatever left another behind (one posed from a renderer it no longer has, a second one put on).
	/// </summary>
	internal static ModelBody On( SkinnedModelRenderer source )
	{
		ModelBody shown = null;
		foreach ( var child in source.GameObject.Children.ToList() )
		{
			if ( child.Components.Get<ModelBody>() is not { } body )
				continue;

			if ( shown is null && body.Source == source )
				shown = body;
			else
				body.TakeOff();
		}

		return shown;
	}

	/// <summary>
	/// Poses the model the way the citizen is posed right now, and hides anything the citizen put on since.
	/// Runs every frame on its own; a scene that isn't rendered by the game (a UI preview) calls it itself.
	/// </summary>
	internal void Tick()
	{
		if ( !Source.IsValid() || !IsIntact() )
		{
			TakeOff();
			return;
		}

		try
		{
			if ( _sinceHidden > HideInterval )
			{
				FindOwnRenderers();
				HideSource();
				FindHeldItems();
			}

			Pose();
		}
		catch ( Exception e )
		{
			// A body that can't pose would stand frozen; taken off, the next scan puts a fresh one on
			Log.Warning( $"Player Model Selector: '{ModelId}' stopped posing ({e.Message}); putting it on again." );
			TakeOff();
		}
	}

	// A hot reload keeps this body but not the state new code added to it, which then fails every frame
	private bool IsIntact()
	{
		return _renderer.IsValid() && _transfer is { IsIntact: true } && _held is not null && _own is not null
			&& _hidden is not null && _cancel is not null;
	}

	protected override void OnPreRender()
	{
		Tick();
	}

	private void Pose()
	{
		if ( !_renderer.IsValid() )
			return;

		// Follows the citizen's own visibility: the player controller turns it to shadows only in first person
		foreach ( var renderer in _own )
		{
			if ( renderer.IsValid() )
				renderer.RenderType = Source.RenderType;
		}

		_transfer.Apply( Source, _renderer );
		_held.Place( Source, _renderer );
	}

	/// <summary>
	/// Takes the model off now: what the citizen holds goes back in its hand and the citizen shows again this frame,
	/// before a body put on in its place starts. Removing the object alone does that only at the end of the frame.
	/// </summary>
	internal void TakeOff()
	{
		_cancel?.Cancel();
		_held?.Release();
		ShowSource();

		// Switched off at once, so it isn't drawn, posed or found again before it is removed
		GameObject.Enabled = false;
		GameObject.Destroy();
	}

	protected override void OnDestroy()
	{
		_cancel?.Cancel();
		_held?.Release();
		ShowSource();
	}

	private async Task DressAsync( ClothingContainer outfit )
	{
		var model = _renderer.Model;

		try
		{
			await ModelOutfit.DressAsync( _renderer, outfit, _cancel.Token );

			// The dresser takes the body from the outfit's skin; the pose transfer was planned for the picked model
			if ( _renderer.IsValid() && _renderer.Model != model )
				_renderer.Model = model;

			FindOwnRenderers();

			// The dresser sets body groups of its own; the look goes on again over them
			_painter?.Forget();
		}
		catch ( OperationCanceledException )
		{
			// Taken off before the clothes arrived
		}
		catch ( Exception e )
		{
			Log.Warning( $"Player Model Selector: couldn't dress '{ModelId}': {e.Message}" );
		}
	}

	// The picked model and the clothes put on it
	private void FindOwnRenderers()
	{
		_own.Clear();
		_own.AddRange( GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) );
	}

	// Hides the citizen and the clothes merged onto it, but keeps it animating: it is still what the game moves and aims.
	// What it holds is left to the held items.
	private void HideSource()
	{
		_sinceHidden = 0;

		foreach ( var renderer in Source.GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			var worn = renderer == Source || renderer is SkinnedModelRenderer s && s.BoneMergeTarget == Source;
			if ( worn && !_own.Contains( renderer ) )
				Hide( renderer );
		}
	}

	// Anything new in the citizen's hands moves to the model's hands
	private void FindHeldItems()
	{
		_held.Find( Source, _transfer, GameObject );
	}

	private void Hide( ModelRenderer renderer )
	{
		if ( !renderer.RenderOptions.Game )
			return;

		renderer.RenderOptions.Game = false;
		_hidden.Add( renderer );
	}

	private void ShowSource()
	{
		if ( _hidden is null )
		{
			ShowEverythingHidden();
			return;
		}

		foreach ( var renderer in _hidden )
		{
			if ( renderer.IsValid() )
				renderer.RenderOptions.Game = true;
		}

		_hidden.Clear();
	}

	// The list of what was hidden is gone (a hot reload), so everything under the citizen that isn't drawn is shown again;
	// the game itself never hides those this way
	private void ShowEverythingHidden()
	{
		if ( !Source.IsValid() )
			return;

		foreach ( var renderer in Source.GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !IsMine( renderer.GameObject ) )
				renderer.RenderOptions.Game = true;
		}
	}

	private bool IsMine( GameObject go )
	{
		for ( var current = go; current is not null; current = current.Parent )
		{
			if ( current == GameObject )
				return true;
		}

		return false;
	}
}
