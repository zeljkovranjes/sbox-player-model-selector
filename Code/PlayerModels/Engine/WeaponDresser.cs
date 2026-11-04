using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Dresses the weapon a body holds as its player designed it (see <see cref="WeaponDesign"/>), found by the weapon's
/// world model: its materials finished, tinted and skinned (see <see cref="WeaponSkins"/>), props on it (stock models
/// only, see <see cref="PropCatalog"/>, each fitted to the caps), stickers pressed onto its surface as decals, and its
/// effects worn beside the body's (see <see cref="EffectRack.ApplyWeapon"/>). Only the weapon held is dressed, once when
/// it changes; everything it adds hangs off the weapon, so it moves with it for nothing. Taking it off puts the weapon
/// back as the game made it. The same dresser dresses a first-person weapon (a different mesh), mapping props and
/// stickers onto it (see <see cref="Mapping"/>). Each frame only a moving sticker's colour changes: no allocations.
/// </summary>
internal sealed class WeaponDresser
{
	private const string PropName = "Weapon prop";
	private const string StickerName = "Weapon sticker";

	// A sticker presses at least this deep (as deep as it is wide when wider): deep enough to wrap over a barrel's
	// curve, shallow enough to stay off the hand holding it; and fades on surfaces this steep to it
	private const float StickerDepth = 2.5f;
	private const float StickerAttenuation = 0.6f;

	// A sticker's moving colour: a rainbow turns round the wheel this often a second, a pulse beats this often and deep
	private const float RainbowTurns = 0.5f;
	private const float PulseBeats = 1f;
	private const float PulseDepth = 0.5f;
	private const float MillisecondsPerSecond = 1000f;

	private sealed class Stuck
	{
		public Decal Decal;
		public WeaponSticker Sticker;
		public DrawnShape Drawn;
		public int Generation = -1;
		public Color[] Palette;

		// Read once when stuck, not each frame: reading the motion from its name boxes it
		public Sticker.Motion Motion;
		public Color Own;
	}

	private IReadOnlyList<WeaponDesign> _designs = Array.Empty<WeaponDesign>();
	private IReadOnlyList<DrawnShape> _shapes = Array.Empty<DrawnShape>();
	private string _designsKey = "";
	private bool _changed;
	private bool _extras = true;

	// What is dressed now: the weapon's object and main renderer, its design, the materials swapped on it (to put back)
	// and the tint it had, the props and stickers on it
	private GameObject _root;
	private ModelRenderer _renderer;
	private WeaponDesign _design;
	private readonly List<(ModelRenderer Renderer, int Index)> _swapped = new();
	private readonly List<(ModelRenderer Renderer, Color Tint)> _tinted = new();
	private readonly List<ModelRenderer> _props = new();
	private readonly List<Stuck> _stickers = new();
	private int _request;
	private float _startedAt;

	// The body groups the weapon had before parts were hidden, to put back; and whether any were
	private ulong _bodyGroups;
	private bool _hidParts;

	/// <summary>The design dressed now; null for none.</summary>
	internal WeaponDesign Design => _design;

	/// <summary>The weapon's world model dressed now, by path; null for none.</summary>
	internal string Weapon { get; private set; }

	/// <summary>The main renderer of the weapon dressed now (or held, with no design); null for none.</summary>
	internal ModelRenderer Renderer => _renderer;

	/// <summary>
	/// How a first-person weapon lies against its world model (<paramref name="WorldModel"/>): a turn, scale and move from
	/// the world model's own space into the first-person model's (<paramref name="FromWorldModel"/>), so props and
	/// stickers sit on it alike; they are fitted to the world model, as everyone else sees them.
	/// </summary>
	internal readonly record struct Mapping( Transform FromWorldModel, Model WorldModel )
	{
		// What a first-person model's weapon moves with as it sways and kicks: everything on it hangs off this bone
		private const string RootBone = "weapon_root";
		private const string MuzzleName = "muzzle";

		/// <summary>The bone of <paramref name="firstPerson"/> the whole weapon moves with; null when it has none.</summary>
		internal static string RootOf( Model firstPerson ) => firstPerson?.Bones.GetBone( RootBone ) is not null ? RootBone : null;

		/// <summary>
		/// How <paramref name="firstPerson"/> lies against <paramref name="worldModel"/>: their muzzles together where both
		/// mark one, else their middles, sized by their lengths.
		/// </summary>
		internal static Mapping Between( Model worldModel, Model firstPerson )
		{
			var scale = AccessoryCatalog.SizeOf( firstPerson ) / MathF.Max( AccessoryCatalog.SizeOf( worldModel ), 0.01f );
			scale = float.IsFinite( scale ) && scale > 0f ? scale : 1f;
			if ( MuzzleOf( worldModel ) is { } from && MuzzleOf( firstPerson ) is { } to )
			{
				var turn = to.Rotation * from.Rotation.Inverse;
				return new Mapping( new Transform( to.Position - turn * (from.Position * scale), turn, scale ), worldModel );
			}

			return new Mapping( new Transform( firstPerson.Bounds.Center - worldModel.Bounds.Center * scale, Rotation.Identity, scale ), worldModel );
		}

		private static Transform? MuzzleOf( Model model )
		{
			if ( model?.Attachments?.Get( MuzzleName ) is not { } attachment )
				return null;

			return attachment.Bone is { } bone ? bone.LocalTransform.ToWorld( attachment.LocalTransform ) : attachment.LocalTransform;
		}
	}

	/// <summary>The weapon designs a look wears, and the drawn shapes it carries for their stickers.</summary>
	internal void Apply( IReadOnlyList<WeaponDesign> designs, IReadOnlyList<DrawnShape> shapes )
	{
		designs ??= Array.Empty<WeaponDesign>();
		shapes ??= Array.Empty<DrawnShape>();

		// By what they say rather than which lists they are: an editor changes a look's designs in place. Asked only when
		// a look changes (in the game) or a preview's does
		var key = designs.Count == 0 ? "" : JsonSerializer.Serialize( designs ) + shapes.Count;
		_designs = designs;
		_shapes = shapes;
		if ( key == _designsKey )
			return;

		_designsKey = key;
		_changed = true;
	}

	/// <summary>
	/// Dresses <paramref name="held"/> (a weapon's object, or null when nothing is held) as its design says, and wears its
	/// effects on <paramref name="rack"/>. A first-person weapon gives its renderer (<paramref name="main"/>: its arms are
	/// under it too), the world model it stands for (<paramref name="worldModel"/>: whose design it wears) and how it lies
	/// against it (<paramref name="mapping"/>); without <paramref name="extras"/> it is only skinned. Nothing happens
	/// while the weapon and its design stay the same.
	/// </summary>
	internal void Dress( GameObject held, EffectRack rack, string worldModel = null, Mapping? mapping = null, bool extras = true, ModelRenderer main = null )
	{
		// The same weapon as before, with the same designs to choose from, is dressed already: asked of every body twice a
		// second, so it costs nothing
		if ( !_changed && held == _root && (held is null || _renderer.IsValid()) && (worldModel is null || worldModel == Weapon) && extras == _extras )
			return;

		var renderer = main.IsValid() ? main : held.IsValid() ? MainRenderer( held ) : null;
		var weapon = worldModel ?? renderer?.Model?.ResourcePath;
		var design = weapon is null ? null : DesignFor( weapon );

		_changed = false;
		_extras = extras;
		Undress();
		_root = held.IsValid() ? held : null;
		_renderer = renderer;
		_design = design;
		Weapon = weapon;
		_startedAt = RealTime.Now;

		if ( design is not null && renderer.IsValid() )
		{
			Skin( main.IsValid() ? main.GameObject : held, renderer, design, main.IsValid() );
			if ( extras )
			{
				HideParts( renderer, design );
				_ = PropsAsync( renderer, design, mapping, ++_request );
				Stick( renderer, design, mapping );
			}
		}

		rack?.ApplyWeapon( extras ? design?.Effects : null );
	}

	/// <summary>Colours moving stickers, and keeps props drawn the way the weapon is (hidden in first person). Every frame: no allocations.</summary>
	internal void Animate()
	{
		if ( _stickers.Count == 0 && _props.Count == 0 )
			return;

		if ( !_renderer.IsValid() )
			return;

		var shadows = _renderer.RenderType;
		for ( var i = 0; i < _props.Count; i++ )
		{
			if ( _props[ i ].IsValid() && _props[ i ].RenderType != shadows )
				_props[ i ].RenderType = shadows;
		}

		var ms = (long)((RealTime.Now - _startedAt) * MillisecondsPerSecond);
		for ( var i = 0; i < _stickers.Count; i++ )
		{
			var stuck = _stickers[ i ];
			if ( !stuck.Decal.IsValid() )
				continue;

			if ( stuck.Drawn is not null && stuck.Generation != ShapeTextures.Generation )
				UseDrawn( stuck );

			if ( stuck.Motion != Sticker.Motion.None )
				stuck.Decal.ColorTint = ColourOf( stuck, ms, i );
		}
	}

	/// <summary>Puts the weapon back as the game made it and lets go of what was added to it.</summary>
	internal void Release()
	{
		Undress();
		_root = null;
		_renderer = null;
		_design = null;
		Weapon = null;
	}

	private WeaponDesign DesignFor( string weapon )
	{
		for ( var i = 0; i < _designs.Count; i++ )
		{
			if ( _designs[ i ].Weapon == weapon )
				return _designs[ i ];
		}

		return null;
	}

	/// <summary>The renderer of the weapon itself under <paramref name="held"/>: its largest, never a prop added to it.</summary>
	internal static ModelRenderer MainRenderer( GameObject held )
	{
		ModelRenderer main = null;
		var largest = -1f;
		foreach ( var renderer in held.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( !IsWeapon( renderer ) )
				continue;

			var size = renderer.Model.Bounds.Size.Length;
			if ( size > largest )
			{
				largest = size;
				main = renderer;
			}
		}

		return main;
	}

	// Whether a renderer under a weapon's object draws the weapon: not a prop added to it, nor what is merged onto it
	private static bool IsWeapon( ModelRenderer renderer ) => renderer.Model is not null && renderer.GameObject.Name != PropName
		&& renderer is not SkinnedModelRenderer { BoneMergeTarget: not null };

	// Every renderer of the weapon drawn with its skin: its own materials, finished or skinned; a tint alone is the
	// renderer's tint, which keeps every material as it is
	private void Skin( GameObject held, ModelRenderer main, WeaponDesign design, bool mainAlone )
	{
		var painted = design.Skin is not null;
		var key = painted ? WeaponSkins.KeyOf( design ) : null;
		var frame = WeaponSkins.Frame.Of( main.Model.Bounds );
		foreach ( var renderer in held.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			// What is merged onto the weapon is someone else's: a first-person weapon's arms
			if ( !IsWeapon( renderer ) || mainAlone && renderer != main )
				continue;

			if ( !painted && design.Tint is { } tint )
			{
				_tinted.Add( (renderer, renderer.Tint) );
				renderer.Tint = Color.Parse( tint ) ?? Color.White;
			}

			if ( !painted && design.Finish is null )
				continue;

			var materials = renderer.Model.Materials;
			for ( var i = 0; i < materials.Length; i++ )
			{
				var made = painted
					? WeaponSkins.For( materials[ i ], design, key, frame, _shapes )
					: FinishMaterials.Make( design.Finish, materials[ i ] );
				if ( made is null )
					continue;

				renderer.Materials.SetOverride( i, made );
				_swapped.Add( (renderer, i) );
			}
		}
	}

	// The parts the design hides, and those its props take the place of, through their body groups; a part the weapon
	// can't hide shows
	private void HideParts( ModelRenderer renderer, WeaponDesign design )
	{
		_bodyGroups = renderer.BodyGroups;
		foreach ( var part in PartsHidden( design ) )
		{
			var empty = WeaponParts.EmptyChoice( renderer.Model, part );
			if ( empty < 0 )
				continue;

			renderer.SetBodyGroup( WeaponParts.NameIn( renderer.Model, part ), empty );
			_hidParts = true;
		}
	}

	private static IEnumerable<string> PartsHidden( WeaponDesign design )
	{
		foreach ( var part in design.HiddenParts ?? new List<string>() )
			yield return part;

		foreach ( var prop in design.Props ?? new List<WeaponProp>() )
		{
			if ( prop.Replaces )
				yield return prop.Part;
		}
	}

	// Each prop as a model of its own on the weapon, once its model is loaded, fitted to the caps: on its part (from the
	// part's middle) or on the weapon, and on a first-person weapon hung off the bone that moves that part, so it follows
	// the part's animation (a magazine coming out on a reload)
	private async Task PropsAsync( ModelRenderer renderer, WeaponDesign design, Mapping? mapping, int request )
	{
		var worldModel = mapping?.WorldModel ?? renderer.Model;
		var weapon = BoundsOf( worldModel.Bounds );
		foreach ( var prop in design.Props ?? new List<WeaponProp>() )
		{
			if ( !PropCatalog.IsStock( prop.Model ) )
				continue;

			var model = await ModelLoader.LoadAsync( prop.Model );
			if ( request != _request || !renderer.IsValid() )
				return;

			// Where a prop is, is where its middle is: from its part's middle, or the weapon's origin
			var part = prop.Part is null ? null : WeaponParts.BoundsOf( worldModel, prop.Part );
			var from = part?.Center ?? Vector3.Zero;
			var size = AccessoryCatalog.SizeOf( model );
			var partSize = part is { } box ? MathF.Max( box.Size.x, MathF.Max( box.Size.y, box.Size.z ) ) : 0f;
			if ( model is null || prop.FittedTo( size, WeaponBounds.Point( from.x, from.y, from.z ), weapon, partSize ) is not { } fitted )
				continue;

			var turn = Rotation.From( fitted.Pitch, fitted.Yaw, fitted.Roll );
			var middle = from + new Vector3( fitted.Forward, fitted.Left, fitted.Up );
			var place = new Transform( middle - turn * (model.Bounds.Center * fitted.Scale), turn, fitted.Scale );
			var parent = renderer.GameObject;
			if ( mapping is { } map )
			{
				var nudge = new Transform( new Vector3( fitted.NudgeForward, fitted.NudgeLeft, fitted.NudgeUp ), Rotation.From( fitted.NudgePitch, fitted.NudgeYaw, fitted.NudgeRoll ) );
				place = map.FromWorldModel.ToWorld( place ).ToWorld( nudge );
				(parent, place) = OnBone( renderer, prop.Part, place );
			}

			using ( renderer.Scene.Push() )
			{
				var go = new GameObject( false, PropName );
				go.NetworkMode = NetworkMode.Never;
				go.Flags |= GameObjectFlags.NotSaved;
				go.SetParent( parent, false );
				go.LocalTransform = place;
				var made = go.AddComponent<ModelRenderer>();
				made.Model = model;
				made.RenderType = renderer.RenderType;
				go.Enabled = true;
				_props.Add( made );
			}
		}
	}

	// A place on a first-person weapon (in its model's own space) as hung off the bone that moves part (or, for none,
	// the whole weapon), relative to that bone's rest; the model itself when it has no such bone
	private static (GameObject Parent, Transform Place) OnBone( ModelRenderer renderer, string part, Transform place )
	{
		var model = renderer.Model;
		var bone = (part is null ? null : WeaponParts.BoneOf( model, part )) ?? Mapping.RootOf( model );
		if ( bone is null || renderer is not SkinnedModelRenderer skinned || skinned.GetBoneObject( bone ) is not { } boneObject )
			return (renderer.GameObject, place);

		return (boneObject, model.Bones.GetBone( bone ).LocalTransform.ToLocal( place ));
	}

	private static WeaponBounds BoundsOf( BBox bounds ) => new( bounds.Mins.x, bounds.Mins.y, bounds.Mins.z, bounds.Maxs.x, bounds.Maxs.y, bounds.Maxs.z );

	// Each sticker as a decal pressed onto the weapon from its side, its picture from the atlas or its drawn shape
	private void Stick( ModelRenderer renderer, WeaponDesign design, Mapping? mapping )
	{
		var weapon = BoundsOf( (mapping?.WorldModel ?? renderer.Model).Bounds );
		var scale = mapping?.FromWorldModel.Scale.x ?? 1f;
		using ( renderer.Scene.Push() )
		{
			foreach ( var sticker in design.Stickers ?? new List<WeaponSticker>() )
			{
				var fitted = sticker.FittedTo( weapon );
				var place = new Transform( new Vector3( fitted.Forward, fitted.Left, fitted.Up ), Rotation.From( fitted.Pitch, fitted.Yaw, fitted.Roll ) * Rotation.FromPitch( 90f ) );
				var parent = renderer.GameObject;
				if ( mapping is { } map )
					(parent, place) = OnBone( renderer, null, map.FromWorldModel.ToWorld( place ) );

				var go = new GameObject( false, StickerName );
				go.NetworkMode = NetworkMode.Never;
				go.Flags |= GameObjectFlags.NotSaved;
				go.SetParent( parent, false );
				go.LocalTransform = place.WithScale( 1f );

				var stuck = new Stuck { Sticker = fitted, Drawn = Find( fitted.DrawnPicture ), Motion = fitted.AnimationIs, Own = Color.Parse( fitted.Color ?? "" ) ?? Color.White };
				var size = fitted.Size * scale;
				var decal = go.AddComponent<Decal>();
				decal.Decals = new List<DecalDefinition>
				{
					new() { ColorTexture = TextureOf( stuck ), Width = size, Height = size, Tint = Color.White, ColorMix = 1f },
				};
				decal.Depth = MathF.Max( StickerDepth * scale, size );
				decal.AttenuationAngle = StickerAttenuation;
				decal.SortLayer = (uint)_stickers.Count;
				decal.ColorTint = stuck.Drawn?.Sticker is not null ? Color.White : Color.Parse( fitted.Color ) ?? Color.White;
				stuck.Decal = decal;
				stuck.Palette = PaletteOf( fitted );
				go.Enabled = true;
				_stickers.Add( stuck );
			}
		}
	}

	private DrawnShape Find( string id )
	{
		if ( id is null )
			return null;

		foreach ( var shape in _shapes )
		{
			if ( shape.Id == id )
				return shape;
		}

		return null;
	}

	// A drawn shape's picture once drawn (a circle meanwhile), or the atlas's picture or standard shape
	private static Texture TextureOf( Stuck stuck )
	{
		if ( stuck.Drawn is null )
			return EffectAtlas.TextureOf( stuck.Sticker.Picture );

		stuck.Generation = ShapeTextures.Generation;
		return ShapeTextures.TextureOf( stuck.Drawn ) ?? EffectAtlas.TextureOf( StandardShape.Circle );
	}

	// Asked again only when the drawn shapes' pictures change
	private static void UseDrawn( Stuck stuck )
	{
		var texture = TextureOf( stuck );
		var definition = stuck.Decal.Decals is { Count: > 0 } decals ? decals[ 0 ] : null;
		if ( definition is not null && definition.ColorTexture != texture )
			definition.ColorTexture = texture;
	}

	private static Color[] PaletteOf( WeaponSticker sticker )
	{
		var colours = sticker.CycleColours();
		var palette = new Color[ colours.Count ];
		for ( var i = 0; i < colours.Count; i++ )
			palette[ i ] = Color.Parse( colours[ i ] ) ?? Color.White;

		return palette;
	}

	// A moving sticker's colour ms milliseconds in, the i-th sticker of the weapon: World at War's clan tag colourings
	// count the stickers as their elements, so stickers along a weapon wipe and scroll in turn
	private Color ColourOf( Stuck stuck, long ms, int i )
	{
		var sticker = stuck.Sticker;
		var scaled = (long)(ms * sticker.Speed);
		var palette = stuck.Palette;
		var count = _stickers.Count;
		switch ( stuck.Motion )
		{
			case Sticker.Motion.Rainbow:
				return new ColorHsv( (scaled / MillisecondsPerSecond * RainbowTurns + i / (float)count) % 1f * 360f, 1f, 1f, 1f ).ToColor();

			case Sticker.Motion.Pulse:
			{
				var own = stuck.Own;
				var level = 1f - PulseDepth * (0.5f - 0.5f * MathF.Cos( scaled / MillisecondsPerSecond * PulseBeats * MathF.Tau ));
				return new Color( own.r * level, own.g * level, own.b * level, 1f );
			}

			case Sticker.Motion.Cyln:
				return palette.Length >= 2 && ColorCycle.IsScannerHighlight( scaled, i, count ) ? palette[ 0 ] : palette[ ^1 ];

			default:
			{
				var kind = stuck.Motion == Sticker.Motion.Cycl ? ColorCycle.Kind.Cycl : ColorCycle.Kind.Rain;
				var index = ColorCycle.PaletteIndex( kind, scaled, i, count, palette.Length );
				return index >= 0 ? palette[ index ] : Color.White;
			}
		}
	}

	private void Undress()
	{
		_request++;
		foreach ( var (renderer, index) in _swapped )
		{
			if ( renderer.IsValid() )
				renderer.Materials.SetOverride( index, null );
		}

		foreach ( var (renderer, tint) in _tinted )
		{
			if ( renderer.IsValid() )
				renderer.Tint = tint;
		}

		if ( _hidParts && _renderer.IsValid() )
			_renderer.BodyGroups = _bodyGroups;

		_hidParts = false;

		// Right away: a preview's scene doesn't run the game loop a deferred destroy waits for
		foreach ( var prop in _props )
		{
			if ( prop.IsValid() )
				prop.GameObject.DestroyImmediate();
		}

		foreach ( var stuck in _stickers )
		{
			if ( stuck.Decal.IsValid() )
				stuck.Decal.GameObject.DestroyImmediate();
		}

		_swapped.Clear();
		_tinted.Clear();
		_props.Clear();
		_stickers.Clear();
	}
}
