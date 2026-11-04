using System;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// A ghost layer (see <see cref="LayerKind.Ghost"/>): see-through copies of what its effect comes from (the body, the
/// weapon held or an item worn) left behind where it was, posed as it was, fading over the layer's lifetime:
/// afterimages. At most <see cref="EffectLayer.MaxGhostRate"/> a second and <see cref="EffectLayer.MaxGhosts"/> out at
/// once, kept and reused, so they cost nothing new as they come and go. Made only by effect layers, never saved: public
/// only as the scene's components are.
/// </summary>
public sealed class EffectGhosts : Component, Component.ExecuteInEditor
{
	// What ghosts are drawn with: white, lit by themselves and see-through, tinted the layer's colour
	private const string GhostMaterial = "materials/dev/primary_white_emissive_trans.vmat";

	// How solid a ghost is as it appears; it fades to nothing over its life
	private const float StartAlpha = 0.5f;

	private static Material _material;

	private sealed class Ghost
	{
		public GameObject Root;
		public SkinnedModelRenderer Renderer;
		public float Born = float.NegativeInfinity;
	}

	private readonly Ghost[] _ghosts = new Ghost[ (int)EffectLayer.MaxGhosts ];
	private Color _colour = Color.White;
	private float _rate;
	private float _lifetime = 1f;
	private float _share = 1f;
	private float _next;

	/// <summary>Sets it up as <paramref name="data"/> says, in <paramref name="colour"/>, with its effect's amount and speed.</summary>
	internal void Configure( EffectLayer data, Color colour, float amount, float speed )
	{
		_colour = colour;
		_rate = Math.Min( data.Amount * amount, EffectLayer.MaxGhostRate );
		_lifetime = Math.Max( data.Lifetime / Math.Max( speed, 0.01f ), EffectLayer.MinLifetime );
		_next = 0f;
		HideAll();
	}

	/// <summary>Leaves ghosts while <paramref name="share"/> is above nothing, as often as that share of its rate.</summary>
	internal void Share( float share )
	{
		_share = share;
	}

	/// <summary>
	/// Leaves a ghost of <paramref name="of"/> when its rate says, while <paramref name="wanted"/>, and fades those out:
	/// shown this much (<paramref name="shown"/>, 0 to 1). Every frame: no allocations once its ghosts are made.
	/// </summary>
	internal void Place( Transform place, float now, bool wanted, float shown, ModelRenderer of )
	{
		if ( wanted && shown > 0f && _share > 0f && _rate > 0f && now >= _next )
		{
			Leave( of, now );
			_next = now + 1f / (_rate * _share);
		}

		for ( var i = 0; i < _ghosts.Length; i++ )
		{
			var ghost = _ghosts[ i ];
			if ( ghost is null || !ghost.Root.IsValid() || !ghost.Root.Enabled )
				continue;

			var life = (now - ghost.Born) / _lifetime;
			if ( life >= 1f )
			{
				ghost.Root.Enabled = false;
				continue;
			}

			ghost.Renderer.Tint = _colour.WithAlpha( StartAlpha * (1f - life) * shown );
		}
	}

	/// <summary>Leaves <paramref name="count"/> ghosts of <paramref name="of"/> at once (its script's <c>Emit</c>), as many as it keeps at most.</summary>
	internal void Emit( int count, Transform place, ModelRenderer of )
	{
		var now = RealTime.Now;
		for ( var i = 0; i < Math.Min( count, _ghosts.Length ); i++ )
			Leave( of, now );
	}

	// A ghost of what the effect comes from, where it is and posed as it is now, in the oldest place free
	private void Leave( ModelRenderer of, float now )
	{
		if ( !of.IsValid() || of.Model is null )
			return;

		var ghost = Oldest();
		if ( !ghost.Root.IsValid() )
			Make( ghost );

		var renderer = ghost.Renderer;
		if ( renderer.Model != of.Model )
			renderer.Model = of.Model;

		ghost.Root.WorldTransform = of.WorldTransform;
		ghost.Root.Enabled = true;
		ghost.Born = now;
		renderer.Tint = _colour.WithAlpha( StartAlpha );
		if ( of is SkinnedModelRenderer body )
			CopyPose( body, renderer, ghost.Root.WorldTransform );
	}

	// The body's pose, bone by bone, as it is now: held until the ghost is left again
	private static void CopyPose( SkinnedModelRenderer from, SkinnedModelRenderer to, Transform root )
	{
		var bones = from.Model.Bones.AllBones;
		for ( var i = 0; i < bones.Count; i++ )
		{
			var bone = bones[ i ];
			if ( from.TryGetBoneTransform( bone, out var world ) )
				to.SetBoneTransform( bone, root.ToLocal( world ) );
		}
	}

	private Ghost Oldest()
	{
		Ghost oldest = null;
		for ( var i = 0; i < _ghosts.Length; i++ )
		{
			_ghosts[ i ] ??= new Ghost();
			if ( oldest is null || _ghosts[ i ].Born < oldest.Born )
				oldest = _ghosts[ i ];
		}

		return oldest;
	}

	// The ghost's own object, in the world rather than under the body (it stays where it was left)
	private void Make( Ghost ghost )
	{
		_material ??= Material.Load( GhostMaterial );
		using ( Scene.Push() )
		{
			ghost.Root = new GameObject( false, "Effect ghost" );
			ghost.Root.NetworkMode = NetworkMode.Never;
			ghost.Root.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.Hidden;
			ghost.Renderer = ghost.Root.AddComponent<SkinnedModelRenderer>();
			ghost.Renderer.UseAnimGraph = false;
			ghost.Renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
			ghost.Renderer.MaterialOverride = _material;
		}
	}

	private void HideAll()
	{
		foreach ( var ghost in _ghosts )
		{
			if ( ghost?.Root.IsValid() == true )
				ghost.Root.Enabled = false;
		}
	}

	protected override void OnDisabled() => HideAll();

	protected override void OnDestroy()
	{
		foreach ( var ghost in _ghosts )
		{
			if ( ghost?.Root.IsValid() == true )
				ghost.Root.Destroy();
		}
	}
}
