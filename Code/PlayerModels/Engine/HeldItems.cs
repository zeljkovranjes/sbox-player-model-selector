using System.Collections.Generic;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// What the hidden citizen holds (a weapon's world model on its hand bone), moved into the picked model's hand. The
/// thing itself is moved, not a copy, so everything that starts from it follows: the muzzle, the physgun's beam, muzzle
/// flashes, glows. It is moved each frame by however far the model's hand is from the citizen's and keeps its own aim,
/// so a gun still points where the player aims. Only objects that exist on this client alone are moved; the weapon's
/// networked objects are left where the game put them.
/// </summary>
internal sealed class HeldItems
{
	private sealed class Held
	{
		// The object hanging off the citizen's bone, and that bone
		public GameObject Root { get; init; }
		public string SourceBone { get; init; }
		public string TargetBone { get; init; }

		// Where the game holds it, relative to the bone: set once, when the weapon attaches it
		public Transform GameLocal { get; init; }
	}

	private readonly List<Held> _held = new();

	/// <summary>Starts moving anything new the citizen holds.</summary>
	internal void Find( SkinnedModelRenderer citizen, PoseTransfer transfer, GameObject body )
	{
		foreach ( var renderer in citizen.GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInDescendants ) )
		{
			// The citizen itself and the clothes merged onto it are hidden, not held
			if ( renderer == citizen || renderer is SkinnedModelRenderer { BoneMergeTarget: not null } || IsUnder( renderer.GameObject, body ) )
				continue;

			var root = RootOf( renderer.GameObject, citizen );
			if ( root is null || root.NetworkMode != NetworkMode.Never || IsHeld( root ) )
				continue;

			if ( !transfer.TryMapBone( root.Parent.Name, out var sourceBone, out var targetBone ) )
				continue;

			_held.Add( new Held
			{
				Root = root,
				SourceBone = sourceBone,
				TargetBone = targetBone,
				GameLocal = root.LocalTransform
			} );
		}
	}

	/// <summary>Moves each held thing into the model's hand, aimed the way the citizen aims it right now.</summary>
	internal void Place( SkinnedModelRenderer citizen, SkinnedModelRenderer model )
	{
		for ( var i = _held.Count - 1; i >= 0; i-- )
		{
			var held = _held[ i ];

			// Dropped, removed, or taken off the bone
			if ( !held.Root.IsValid() || !held.Root.Parent.IsValid() )
			{
				_held.RemoveAt( i );
				continue;
			}

			// Always from where the game holds it, never from last frame's move, so the offset can't add up
			held.Root.LocalTransform = held.GameLocal;

			if ( !citizen.TryGetBoneTransform( held.SourceBone, out var hand ) || !model.TryGetBoneTransform( held.TargetBone, out var modelHand ) )
				continue;

			var where = held.Root.WorldTransform;
			held.Root.WorldTransform = where.WithPosition( where.Position + (modelHand.Position - hand.Position) );
		}
	}

	/// <summary>Puts everything back where the game holds it.</summary>
	internal void Release()
	{
		foreach ( var held in _held )
		{
			if ( held.Root.IsValid() )
				held.Root.LocalTransform = held.GameLocal;
		}

		_held.Clear();
	}

	private bool IsHeld( GameObject root )
	{
		foreach ( var held in _held )
		{
			if ( held.Root == root )
				return true;
		}

		return false;
	}

	// The object hanging directly off one of the citizen's bone objects, above this one
	private static GameObject RootOf( GameObject go, SkinnedModelRenderer citizen )
	{
		for ( var current = go; current?.Parent is not null && current.Parent != citizen.GameObject; current = current.Parent )
		{
			if ( citizen.Model.Bones.HasBone( current.Parent.Name ) && !citizen.Model.Bones.HasBone( current.Name ) )
				return current;
		}

		return null;
	}

	private static bool IsUnder( GameObject go, GameObject ancestor )
	{
		for ( var current = go; current is not null; current = current.Parent )
		{
			if ( current == ancestor )
				return true;
		}

		return false;
	}
}
