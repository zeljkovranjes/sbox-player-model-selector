using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The items a body wears, each on an object of its own under the body and customised like a model of its own. The
/// items are made for the citizen, so each goes with the citizen's bones: they turn as that bone does on the citizen, and
/// sit where the worn model has it. What sits on one joint is carried by that joint whole; clothes have every bone posed
/// that way, so they bend with the model. Works on any rig.
/// </summary>
internal sealed class AccessoryRack
{
	private const string ObjectName = "Accessory";

	// A citizen's bone for each joint
	private static string BoneOf( string joint ) => joint switch
	{
		nameof( Accessory.Joint.Neck ) => "neck_0",
		nameof( Accessory.Joint.Chest ) => "spine_2",
		nameof( Accessory.Joint.Hips ) => "pelvis",
		nameof( Accessory.Joint.LeftHand ) => "hand_L",
		nameof( Accessory.Joint.RightHand ) => "hand_R",
		_ => "head",
	};

	// A bone of a worn piece of clothing. With a bone on the citizen it goes with it, offset from the nearest bone the
	// model has too (Anchor); a bone the citizen doesn't have (a coat's tail) is carried by its parent
	private readonly record struct FollowBone( BoneCollection.Bone Bone, int Source, int Anchor, BoneCollection.Bone Target, int Parent, Transform FromParent );

	private sealed class Worn
	{
		public GameObject Root;
		public SkinnedModelRenderer Renderer;
		public LookPainter Painter;
		public float Size;
		public string Pin;

		// The joint's bone on the citizen and on the model, and where the item has it at rest
		public string SourceBone;
		public string TargetBone;
		public Transform Rest;

		public Vector3 Offset;
		public Rotation Turn;
		public float Scale;

		// Where it was last placed: its joint (with its offset), the way that joint faces and the model's scale there
		public Vector3 Pivot;
		public Rotation Heading;
		public float AnchorScale = 1f;
		public bool Placed;

		// Clothes that follow the body: every bone, parents first, and a buffer for where they are this frame
		public bool Follows;
		public FollowBone[] Bones;
		public Transform[] Pose;
	}

	/// <summary>
	/// Where an item is: its pivot (its joint, moved by its offset), the way its joint faces (the axes its offset and turn
	/// are in), the model's scale there, its own size, whether it bends with the body, and its renderer.
	/// </summary>
	internal readonly record struct Placement( Vector3 Pivot, Rotation Heading, float Scale, float Size, bool Follows, SkinnedModelRenderer Renderer );

	private readonly List<Worn> _worn = new();
	private IReadOnlyList<Accessory> _wanted = Array.Empty<Accessory>();
	private string _items = "";
	private int _request;
	private bool _loading;

	/// <summary>
	/// Wears <paramref name="accessories"/> on <paramref name="parent"/>'s model, in place of what it wore, within the caps
	/// of that model's size. Anything that isn't a stock item is left off. <paramref name="changed"/> is told when the
	/// items themselves changed, once they are there.
	/// </summary>
	internal void Apply( IReadOnlyList<Accessory> accessories, GameObject parent, PoseTransfer transfer, Model citizen, Model model, Action changed )
	{
		_wanted = accessories ?? Array.Empty<Accessory>();

		var items = string.Join( '\n', _wanted.Select( x => x.Item ) );
		if ( items == _items )
		{
			if ( !_loading )
				Fit( transfer, citizen, model );

			return;
		}

		_items = items;
		Clear();
		_ = LoadAsync( parent, transfer, citizen, model, changed, ++_request );
	}

	/// <summary>How many items there are, by their places in the look; some may be missing (see <see cref="TryGetPlacement"/>).</summary>
	internal int Count => _loading ? 0 : _worn.Count;

	/// <summary>Where the item at <paramref name="index"/> in the look is now; false while it loads or when it couldn't be worn.</summary>
	internal bool TryGetPlacement( int index, out Placement placement )
	{
		placement = default;
		if ( _loading || index < 0 || index >= _worn.Count || _worn[ index ] is not { Placed: true } worn || !worn.Root.IsValid() )
			return false;

		var pivot = worn.Follows ? worn.Renderer.Bounds.Center : worn.Pivot;
		placement = new Placement( pivot, worn.Heading, worn.AnchorScale, worn.Size, worn.Follows, worn.Renderer );
		return true;
	}

	/// <summary>Places every item on its joint. Runs every frame, after the model is posed.</summary>
	internal void Place( SkinnedModelRenderer citizen, SkinnedModelRenderer model )
	{
		for ( var i = 0; i < _worn.Count; i++ )
		{
			var worn = _worn[ i ];
			if ( worn is null || !worn.Root.IsValid() )
				continue;

			if ( worn.Follows )
			{
				PoseClothes( worn, citizen, model );
				continue;
			}

			if ( worn.TargetBone is null )
				continue;

			if ( !citizen.TryGetBoneTransform( worn.SourceBone, out var source ) || !model.TryGetBoneTransform( worn.TargetBone, out var target ) )
				continue;

			// Takes the item's rest pose to where the joint is turned now. Forward, left and up stay the model's own
			// directions at rest whatever way the rig's bones point, so sliders mean the same on any model
			var heading = source.Rotation * worn.Rest.Rotation.Inverse;
			var turn = heading * worn.Turn;
			var scale = worn.Scale * target.Scale.x;
			var joint = target.Position + heading * (worn.Offset * target.Scale.x);

			worn.Root.WorldTransform = new Transform( joint - turn * (worn.Rest.Position * scale), turn, scale );
			worn.Pivot = joint;
			worn.Heading = heading;
			worn.AnchorScale = target.Scale.x;
			worn.Placed = true;
		}
	}

	// Runs every frame for each piece of clothing, over every bone: no allocations
	private static void PoseClothes( Worn worn, SkinnedModelRenderer citizen, SkinnedModelRenderer model )
	{
		var world = citizen.BoneWorldTransforms;
		var root = worn.Root.WorldTransform;
		var facing = citizen.WorldRotation;
		worn.Heading = facing;
		worn.AnchorScale = model.WorldScale.x;
		worn.Placed = true;

		for ( var i = 0; i < worn.Bones.Length; i++ )
		{
			var bone = worn.Bones[ i ];
			Transform pose;

			if ( bone.Source >= 0 && bone.Source < world.Length && model.TryGetBoneTransform( bone.Target, out var target ) )
			{
				var source = world[ bone.Source ];
				var along = (source.Position - world[ bone.Anchor ].Position + facing * worn.Offset) * target.Scale.x;
				pose = new Transform( target.Position + along, source.Rotation, worn.Scale * target.Scale.x );
			}
			else
			{
				pose = bone.Parent >= 0 ? worn.Pose[ bone.Parent ].ToWorld( bone.FromParent ) : root;
			}

			worn.Pose[ i ] = pose;
			worn.Renderer.SetBoneTransform( bone.Bone, root.ToLocal( pose ) );
		}
	}

	private async Task LoadAsync( GameObject parent, PoseTransfer transfer, Model citizen, Model model, Action changed, int request )
	{
		_loading = true;

		try
		{
			var models = new List<(AccessoryCatalog.Item Item, Model Model)>();
			foreach ( var accessory in _wanted.ToList() )
			{
				var item = await AccessoryCatalog.LoadAsync( accessory.Item );
				models.Add( (item, item is null ? null : await ModelLoader.LoadAsync( item.Model )) );
			}

			// Other items were asked for meanwhile, or the body is gone
			if ( request != _request || !parent.IsValid() )
				return;

			using ( parent.Scene.Push() )
			{
				foreach ( var (item, loaded) in models )
					_worn.Add( loaded is null ? null : Make( parent, loaded, item.Follows, transfer, citizen, model ) );
			}

			Fit( transfer, citizen, model );
			changed?.Invoke();
		}
		catch ( Exception e )
		{
			Log.Warning( $"Player Model Selector: couldn't put items on '{model?.Name}': {e.Message}" );
		}
		finally
		{
			if ( request == _request )
				_loading = false;
		}
	}

	private static Worn Make( GameObject parent, Model model, bool follows, PoseTransfer transfer, Model citizen, Model wearer )
	{
		var root = new GameObject( true, ObjectName );
		root.NetworkMode = NetworkMode.Never;
		root.Flags |= GameObjectFlags.NotSaved;
		root.SetParent( parent, false );

		var renderer = root.AddComponent<SkinnedModelRenderer>();
		renderer.Model = model;
		renderer.UseAnimGraph = false;

		var worn = new Worn { Root = root, Renderer = renderer, Painter = new LookPainter(), Size = AccessoryCatalog.SizeOf( model ), Follows = follows };
		if ( follows )
		{
			worn.Bones = FollowBones( model, transfer, citizen, wearer );
			worn.Pose = new Transform[ worn.Bones.Length ];
		}

		return worn;
	}

	private static FollowBone[] FollowBones( Model item, PoseTransfer transfer, Model citizen, Model wearer )
	{
		var all = item.Bones.AllBones;
		var bones = new FollowBone[ all.Count ];

		for ( var i = 0; i < all.Count; i++ )
		{
			var bone = all[ i ];
			var parent = bone.Parent?.Index ?? -1;
			var fromParent = bone.Parent?.LocalTransform.ToLocal( bone.LocalTransform ) ?? global::Transform.Zero;

			var source = citizen.Bones.GetBone( bone.Name );
			if ( source is not null && transfer.TryMapBone( bone.Name, out var anchor, out var target ) )
				bones[ i ] = new FollowBone( bone, source.Index, citizen.Bones.GetBone( anchor ).Index, wearer.Bones.GetBone( target ), parent, fromParent );
			else
				bones[ i ] = new FollowBone( bone, -1, -1, null, parent, fromParent );
		}

		return bones;
	}

	// Each item as it is wanted now, within the caps of this model's size
	private void Fit( PoseTransfer transfer, Model citizen, Model model )
	{
		var height = AccessoryCatalog.HeightOf( model );
		for ( var i = 0; i < _worn.Count && i < _wanted.Count; i++ )
		{
			if ( _worn[ i ] is not { } worn || !worn.Root.IsValid() )
				continue;

			var fitted = _wanted[ i ].FittedTo( height, worn.Size, worn.Follows );
			if ( !worn.Follows && fitted.Pin != worn.Pin )
				Pin( worn, fitted.Pin, transfer, citizen );

			worn.Offset = new Vector3( fitted.Forward, fitted.Left, fitted.Up );
			worn.Turn = Rotation.From( fitted.Pitch, fitted.Yaw, fitted.Roll );
			worn.Scale = fitted.Scale;
			worn.Painter.Apply( worn.Renderer, fitted.Style );
		}
	}

	private static void Pin( Worn worn, string joint, PoseTransfer transfer, Model citizen )
	{
		worn.Pin = joint;

		var bone = BoneOf( joint );
		worn.SourceBone = bone;
		worn.TargetBone = transfer.TryMapBone( bone, out _, out var target ) ? target : null;

		// An item's mesh sits where its own copy of the bone is; one without that bone was made on the citizen's
		var rest = worn.Renderer.Model.Bones.GetBone( bone ) ?? citizen.Bones.GetBone( bone );
		worn.Rest = rest?.LocalTransform ?? global::Transform.Zero;
	}

	private void Clear()
	{
		foreach ( var worn in _worn )
		{
			if ( worn is not null && worn.Root.IsValid() )
				worn.Root.Destroy();
		}

		_worn.Clear();
	}
}
