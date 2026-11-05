using System.Collections.Generic;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// What the hidden citizen holds (a weapon's world model on its hand bone), moved into the picked model's hand. The
/// thing itself is moved, not a copy, so everything that starts from it follows: the muzzle, the physgun's beam, muzzle
/// flashes, glows. It is moved each frame by however far the model's hand is from the citizen's and keeps its own aim,
/// so a gun still points where the player aims. Only objects that exist on this client alone are moved; the weapon's
/// networked objects are left where the game put them. Effects can come from the tip of what is held (see
/// <see cref="TryGetTip"/>), held by a model or by the citizen itself.
/// </summary>
internal sealed class HeldItems
{
	private sealed class Held
	{
		// The object hanging off the citizen's bone, and that bone (by name, and looked up once: a lookup by name allocates)
		public GameObject Root { get; init; }
		public string SourceBone { get; init; }
		public string TargetBone { get; init; }
		public BoneCollection.Bone Source { get; init; }

		// Where the game holds it, relative to the bone: set once, when the weapon attaches it
		public Transform GameLocal { get; init; }

		// Its tip, once looked for: the muzzle object the weapon marks, or else a point on it relative to it, and whether
		// that is a muzzle (a model attachment counts) rather than a worked-out end
		public bool TipFound;
		public GameObject Muzzle;
		public Transform TipLocal;
		public bool HasMuzzle;
	}

	// What marks a weapon's muzzle: an object (or a model attachment) named so
	private const string MuzzleName = "muzzle";

	private readonly List<Held> _held = new();

	// What was held when last looked for, by how many of its objects showed, and the bone object the first hung from
	// with how many children it had: a weapon drawn, put away or swapped changes one of them (see MayHaveChanged)
	private int _shownWhenFound = -1;
	private GameObject _hand;
	private int _handChildren = -1;

	/// <summary>
	/// Starts moving anything new the citizen holds into the model's hand; without a <paramref name="transfer"/> (the citizen
	/// wears no model) it is only kept track of, for its tip.
	/// </summary>
	internal void Find( SkinnedModelRenderer citizen, PoseTransfer transfer, GameObject body )
	{
		// What was dropped since: without a model nothing else lets it go
		for ( var i = _held.Count - 1; i >= 0; i-- )
		{
			if ( !_held[ i ].Root.IsValid() || !_held[ i ].Root.Parent.IsValid() )
				_held.RemoveAt( i );
		}

		FindNew( citizen, transfer, body );
		_shownWhenFound = Shown();
		for ( var i = 0; i < _held.Count; i++ )
		{
			if ( _held[ i ].Root.IsValid() && _held[ i ].Root.Parent.IsValid() )
			{
				_hand = _held[ i ].Root.Parent;
				break;
			}
		}

		// Holding nothing, the hand the game puts a weapon in is watched: the first one drawn is seen the frame it appears
		if ( !_hand.IsValid() && citizen.IsValid() )
			_hand = citizen.GetBoneObject( HoldBone );

		_handChildren = _hand.IsValid() ? _hand.Children.Count : -1;
	}

	// The bone of the citizen's right hand weapons are held by
	private const string HoldBone = "hold_R";

	/// <summary>
	/// Whether what the citizen holds may have changed since <see cref="Find"/>: something held went away or was hidden or
	/// shown, or the bone it hung from holds more or fewer things (a weapon drawn, put away or swapped). Cheap enough for
	/// every frame, so the weapon drawn is found and dressed the frame it appears: no allocations.
	/// </summary>
	internal bool MayHaveChanged() => _shownWhenFound >= 0 && (Shown() != _shownWhenFound || _hand.IsValid() && _hand.Children.Count != _handChildren);

	// How many of what is held show: valid, still hanging off the body and active
	private int Shown()
	{
		var shown = 0;
		for ( var i = 0; i < _held.Count; i++ )
		{
			var root = _held[ i ].Root;
			if ( root.IsValid() && root.Parent.IsValid() && root.Active )
				shown++;
		}

		return shown;
	}

	private void FindNew( SkinnedModelRenderer citizen, PoseTransfer transfer, GameObject body )
	{
		foreach ( var renderer in citizen.GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInDescendants ) )
		{
			// The citizen itself and the clothes merged onto it are hidden, not held
			if ( renderer == citizen || renderer is SkinnedModelRenderer { BoneMergeTarget: not null } || IsUnder( renderer.GameObject, body ) )
				continue;

			var root = RootOf( renderer.GameObject, citizen );
			if ( root is null || root.NetworkMode != NetworkMode.Never || IsHeld( root ) )
				continue;

			string sourceBone = null, targetBone = null;
			if ( transfer is not null && !transfer.TryMapBone( root.Parent.Name, out sourceBone, out targetBone ) )
				continue;

			_held.Add( new Held
			{
				Root = root,
				SourceBone = sourceBone,
				TargetBone = targetBone,
				Source = sourceBone is null ? null : citizen.Model.Bones.GetBone( sourceBone ),
				GameLocal = root.LocalTransform
			} );
		}
	}

	/// <summary>
	/// Keeps track of <paramref name="root"/> alone as what is held, as it is, never moved (a first-person weapon), for
	/// its tip; null holds nothing.
	/// </summary>
	internal void Hold( GameObject root )
	{
		if ( _held.Count == 1 && _held[ 0 ].Root == root )
			return;

		_held.Clear();
		if ( root.IsValid() )
			_held.Add( new Held { Root = root, GameLocal = root.LocalTransform } );
	}

	/// <summary>
	/// What <paramref name="citizen"/> holds now (the first thing: a weapon's world model), its object, never what the
	/// selector put on it; null when nothing is held. Looked for when asked: not for every frame.
	/// </summary>
	internal static GameObject HeldBy( SkinnedModelRenderer citizen )
	{
		if ( !citizen.IsValid() )
			return null;

		foreach ( var renderer in citizen.GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInDescendants ) )
		{
			if ( renderer == citizen || renderer is SkinnedModelRenderer { BoneMergeTarget: not null } || !renderer.Active )
				continue;

			if ( RootOf( renderer.GameObject, citizen ) is { NetworkMode: NetworkMode.Never } root )
				return root;
		}

		return null;
	}

	/// <summary>Moves each held thing into the model's hand, aimed the way the citizen aims it right now.</summary>
	internal void Place( SkinnedModelRenderer citizen, SkinnedModelRenderer model, PoseTransfer transfer )
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

			if ( held.Source is null || !citizen.TryGetBoneTransform( held.Source, out var hand ) || !transfer.TryGetPosedBone( model, held.TargetBone, out var modelHand ) )
				continue;

			var where = held.Root.WorldTransform;
			held.Root.WorldTransform = where.WithPosition( where.Position + (modelHand.Position - hand.Position) );
		}
	}

	/// <summary>The object of the first thing held (a weapon's world model), or null while nothing is held. No allocations.</summary>
	internal GameObject Holding
	{
		get
		{
			for ( var i = 0; i < _held.Count; i++ )
			{
				if ( _held[ i ].Root.IsValid() && _held[ i ].Root.Parent.IsValid() )
					return _held[ i ].Root;
			}

			return null;
		}
	}

	/// <summary>
	/// Whether the first thing held marks a muzzle, a gun's, rather than having its tip worked out (a crowbar, a sword):
	/// a swing reaches only so far. False while nothing is held.
	/// </summary>
	internal bool HoldsMuzzle => TryGetTip( out _ ) && FirstHeld() is { HasMuzzle: true };

	private Held FirstHeld()
	{
		for ( var i = 0; i < _held.Count; i++ )
		{
			if ( _held[ i ].Root.IsValid() && _held[ i ].Root.Parent.IsValid() )
				return _held[ i ];
		}

		return null;
	}

	/// <summary>
	/// Where the tip of the first thing held is now: its muzzle where the weapon marks one, or else the far end of its
	/// longest side. False while nothing is held. Every frame: no allocations once its tip is found.
	/// </summary>
	internal bool TryGetTip( out Transform tip )
	{
		for ( var i = 0; i < _held.Count; i++ )
		{
			var held = _held[ i ];
			if ( !held.Root.IsValid() || !held.Root.Parent.IsValid() )
				continue;

			if ( !held.TipFound )
				FindTip( held );

			tip = held.Muzzle.IsValid() ? held.Muzzle.WorldTransform : held.Root.WorldTransform.ToWorld( held.TipLocal );
			return true;
		}

		tip = default;
		return false;
	}

	/// <summary>What draws the first thing held (for its ghosts); false while nothing is held. Every frame: no allocations.</summary>
	internal bool TryGetRenderer( out ModelRenderer renderer )
	{
		for ( var i = 0; i < _held.Count; i++ )
		{
			if ( _held[ i ].Root.IsValid() && _held[ i ].Root.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) is { } found )
			{
				renderer = found;
				return true;
			}
		}

		renderer = null;
		return false;
	}

	// Looked for once: an object named for the muzzle under it, a model attachment of that name, or the far end of its
	// model's longest side
	private static void FindTip( Held held )
	{
		held.TipFound = true;
		held.TipLocal = global::Transform.Zero;
		foreach ( var child in held.Root.GetAllObjects( true ) )
		{
			if ( child.Name.Contains( MuzzleName, System.StringComparison.OrdinalIgnoreCase ) )
			{
				held.Muzzle = child;
				held.HasMuzzle = true;
				return;
			}
		}

		if ( held.Root.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) is not { Model: { } model } renderer )
			return;

		var tip = global::Transform.Zero;
		if ( model.Attachments?.Get( MuzzleName ) is { } attachment )
		{
			tip = attachment.Bone is { } bone ? bone.LocalTransform.ToWorld( attachment.LocalTransform ) : attachment.LocalTransform;
			held.HasMuzzle = true;
		}
		else
		{
			var bounds = model.Bounds;
			var size = bounds.Size;
			var axis = size.x >= size.y && size.x >= size.z ? Vector3.Forward : size.y >= size.z ? Vector3.Left : Vector3.Up;
			tip = new Transform( bounds.Center + axis * (Vector3.Dot( size, axis ) * 0.5f) );
		}

		held.TipLocal = held.Root.WorldTransform.ToLocal( renderer.WorldTransform.ToWorld( tip ) );
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
