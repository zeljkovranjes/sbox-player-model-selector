using System;
using System.Collections.Generic;
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
	}

	private IReadOnlyList<WeaponDesign> _designs = Array.Empty<WeaponDesign>();
	private IReadOnlyList<DrawnShape> _shapes = Array.Empty<DrawnShape>();
	private bool _changed;

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

	/// <summary>The design dressed now; null for none.</summary>
	internal WeaponDesign Design => _design;

	/// <summary>The weapon's world model dressed now, by path; null for none.</summary>
	internal string Weapon { get; private set; }

	/// <summary>The main renderer of the weapon dressed now (or held, with no design); null for none.</summary>
	internal ModelRenderer Renderer => _renderer;

	/// <summary>
	/// How a first-person weapon lies against its world model: a turn, scale and move from the world model's own space
	/// into the first-person model's, so props and stickers sit on it alike, and the world model's bounds, which props
	/// and stickers are fitted to.
	/// </summary>
	internal readonly record struct Mapping( Transform FromWorldModel, BBox WorldModelBounds );

	/// <summary>The weapon designs a look wears, and the drawn shapes it carries for their stickers.</summary>
	internal void Apply( IReadOnlyList<WeaponDesign> designs, IReadOnlyList<DrawnShape> shapes )
	{
		designs ??= Array.Empty<WeaponDesign>();
		if ( ReferenceEquals( designs, _designs ) && ReferenceEquals( shapes, _shapes ) )
			return;

		_designs = designs;
		_shapes = shapes ?? Array.Empty<DrawnShape>();
		_changed = true;
	}

	/// <summary>
	/// Dresses <paramref name="held"/> (a weapon's object, or null when nothing is held) as its design says, and wears its
	/// effects on <paramref name="rack"/>. A first-person weapon gives the world model it stands for
	/// (<paramref name="worldModel"/>: whose design it wears) and how it lies against it (<paramref name="mapping"/>);
	/// without <paramref name="extras"/> it is only skinned. Nothing happens while the weapon and its design stay the same.
	/// </summary>
	internal void Dress( GameObject held, EffectRack rack, string worldModel = null, Mapping? mapping = null, bool extras = true )
	{
		var renderer = held.IsValid() ? MainRenderer( held ) : null;
		var weapon = worldModel ?? renderer?.Model?.ResourcePath;
		var design = weapon is null ? null : DesignFor( weapon );
		if ( !_changed && held == _root && renderer == _renderer && ReferenceEquals( design, _design ) )
			return;

		_changed = false;
		Undress();
		_root = held.IsValid() ? held : null;
		_renderer = renderer;
		_design = design;
		Weapon = weapon;
		_startedAt = RealTime.Now;

		if ( design is not null && renderer.IsValid() )
		{
			Skin( held, renderer, design );
			if ( extras )
			{
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

			if ( stuck.Sticker.AnimationIs != Sticker.Motion.None )
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
			if ( renderer.Model is null || renderer.GameObject.Name == PropName || renderer is SkinnedModelRenderer { BoneMergeTarget: not null } )
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

	// Every renderer of the weapon drawn with its skin: its own materials, finished or skinned; a tint alone is the
	// renderer's tint, which keeps every material as it is
	private void Skin( GameObject held, ModelRenderer main, WeaponDesign design )
	{
		var painted = design.Skin is not null;
		var key = painted ? WeaponSkins.KeyOf( design ) : null;
		var frame = WeaponSkins.Frame.Of( main.Model.Bounds );
		foreach ( var renderer in held.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( renderer.Model is null || renderer.GameObject.Name == PropName )
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

	// Each prop as a model of its own on the weapon, once its model is loaded, fitted to the caps
	private async Task PropsAsync( ModelRenderer renderer, WeaponDesign design, Mapping? mapping, int request )
	{
		var bounds = mapping?.WorldModelBounds ?? renderer.Model.Bounds;
		var weapon = new WeaponBounds( bounds.Mins.x, bounds.Mins.y, bounds.Mins.z, bounds.Maxs.x, bounds.Maxs.y, bounds.Maxs.z );
		foreach ( var prop in design.Props ?? new List<WeaponProp>() )
		{
			if ( !PropCatalog.IsStock( prop.Model ) )
				continue;

			var model = await ModelLoader.LoadAsync( prop.Model );
			if ( request != _request || !renderer.IsValid() )
				return;

			var size = AccessoryCatalog.SizeOf( model );
			var turn = Rotation.From( prop.Pitch, prop.Yaw, prop.Roll );
			var middle = turn * (model?.Bounds.Center ?? Vector3.Zero);
			if ( model is null || prop.FittedTo( size, WeaponBounds.Point( middle.x, middle.y, middle.z ), weapon ) is not { } fitted )
				continue;

			var place = new Transform( new Vector3( fitted.Forward, fitted.Left, fitted.Up ), Rotation.From( fitted.Pitch, fitted.Yaw, fitted.Roll ), fitted.Scale );
			if ( mapping is { } map )
			{
				var nudge = new Transform( new Vector3( fitted.NudgeForward, fitted.NudgeLeft, fitted.NudgeUp ), Rotation.From( fitted.NudgePitch, fitted.NudgeYaw, fitted.NudgeRoll ) );
				place = map.FromWorldModel.ToWorld( place ).ToWorld( nudge );
			}

			using ( renderer.Scene.Push() )
			{
				var go = new GameObject( false, PropName );
				go.NetworkMode = NetworkMode.Never;
				go.Flags |= GameObjectFlags.NotSaved;
				go.SetParent( renderer.GameObject, false );
				go.LocalTransform = place;
				var made = go.AddComponent<ModelRenderer>();
				made.Model = model;
				made.RenderType = renderer.RenderType;
				go.Enabled = true;
				_props.Add( made );
			}
		}
	}

	// Each sticker as a decal pressed onto the weapon from its side, its picture from the atlas or its drawn shape
	private void Stick( ModelRenderer renderer, WeaponDesign design, Mapping? mapping )
	{
		var bounds = mapping?.WorldModelBounds ?? renderer.Model.Bounds;
		var weapon = new WeaponBounds( bounds.Mins.x, bounds.Mins.y, bounds.Mins.z, bounds.Maxs.x, bounds.Maxs.y, bounds.Maxs.z );
		var scale = mapping?.FromWorldModel.Scale.x ?? 1f;
		using ( renderer.Scene.Push() )
		{
			foreach ( var sticker in design.Stickers ?? new List<WeaponSticker>() )
			{
				var fitted = sticker.FittedTo( weapon );
				var place = new Transform( new Vector3( fitted.Forward, fitted.Left, fitted.Up ), Rotation.From( fitted.Pitch, fitted.Yaw, fitted.Roll ) * Rotation.FromPitch( 90f ) );
				if ( mapping is { } map )
					place = map.FromWorldModel.ToWorld( place );

				var go = new GameObject( false, StickerName );
				go.NetworkMode = NetworkMode.Never;
				go.Flags |= GameObjectFlags.NotSaved;
				go.SetParent( renderer.GameObject, false );
				go.LocalTransform = place.WithScale( 1f );

				var stuck = new Stuck { Sticker = fitted, Drawn = Find( fitted.DrawnPicture ) };
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
		switch ( sticker.AnimationIs )
		{
			case Sticker.Motion.Rainbow:
				return new ColorHsv( (scaled / MillisecondsPerSecond * RainbowTurns + i / (float)count) % 1f * 360f, 1f, 1f, 1f ).ToColor();

			case Sticker.Motion.Pulse:
			{
				var own = Color.Parse( sticker.Color ) ?? Color.White;
				var level = 1f - PulseDepth * (0.5f - 0.5f * MathF.Cos( scaled / MillisecondsPerSecond * PulseBeats * MathF.Tau ));
				return new Color( own.r * level, own.g * level, own.b * level, 1f );
			}

			case Sticker.Motion.Cyln:
				return palette.Length >= 2 && ColorCycle.IsScannerHighlight( scaled, i, count ) ? palette[ 0 ] : palette[ ^1 ];

			default:
			{
				var kind = sticker.AnimationIs == Sticker.Motion.Cycl ? ColorCycle.Kind.Cycl : ColorCycle.Kind.Rain;
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

		foreach ( var prop in _props )
		{
			if ( prop.IsValid() )
				prop.GameObject.Destroy();
		}

		foreach ( var stuck in _stickers )
		{
			if ( stuck.Decal.IsValid() )
				stuck.Decal.GameObject.Destroy();
		}

		_swapped.Clear();
		_tinted.Clear();
		_props.Clear();
		_stickers.Clear();
	}
}
