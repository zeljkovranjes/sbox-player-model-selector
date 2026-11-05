using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

// A weapon design script's top: the weapon's finish and tint, its skin, the stickers and props put on it, the parts it
// hides and the effects coming from it, made into the same WeaponDesign the weapon panel makes (held to the same caps)
internal sealed partial class ScriptBuilder
{
	private readonly WeaponDesign _design;
	private readonly List<WeaponSticker> _stickers = new();
	private readonly List<WeaponProp> _props = new();
	private readonly List<string> _hidden = new();
	private readonly List<int> _designEffects = new();
	private readonly List<int> _stickerSites = new();
	private readonly List<int> _propSites = new();
	private readonly List<int> _partSites = new();
	private readonly List<int> _effectSites = new();

	private static bool IsWeaponProperty( PropertyId property ) => property >= PropertyId.WeaponFinish;

	// Where a weapon field's statement is recorded (see ScriptBuild.WeaponOwner): the weapon and its skin, a sticker, a prop
	// or an effect it carries, by its place in the design
	private int WeaponOwnerOf( PropertyId property, int handle ) => ScriptProperties.Of( property ).Owner switch
	{
		TypeKind.WeaponSticker => ScriptBuild.StickerOwner + handle,
		TypeKind.WeaponProp => ScriptBuild.PropOwner + handle,
		TypeKind.Effect => ScriptBuild.EffectOwnerBase + _designEffects.IndexOf( handle ),
		_ => ScriptBuild.WeaponOwner,
	};

	private int RunWeapon( Intrinsic op, double[] s, int sp )
	{
		if ( _design is null )
			return Fail( "Weapons are designed in weapon design scripts" );

		var site = (int)s[ sp - 1 ];
		switch ( op )
		{
			case Intrinsic.WeaponAddSticker:
			{
				if ( _stickers.Count >= WeaponDesign.MaxStickers )
					return Fail( $"A weapon has at most {WeaponDesign.MaxStickers} stickers" );

				if ( !PictureName( (int)s[ sp - 2 ], out var picture ) )
					return -1;

				_stickers.Add( new WeaponSticker { Picture = picture } );
				_stickerSites.Add( site );
				s[ sp - 2 ] = _stickers.Count - 1;
				return sp - 1;
			}

			case Intrinsic.WeaponAddProp:
			{
				if ( _props.Count >= WeaponDesign.MaxProps )
					return Fail( $"A weapon has at most {WeaponDesign.MaxProps} props" );

				var model = Text( s[ sp - 2 ] ).Trim();
				if ( !WeaponProp.IsModelPath( model ) )
					return Fail( $"\"{model}\" isn't a model: give a prop's path, ending in .vmdl" );

				_props.Add( new WeaponProp { Model = model } );
				_propSites.Add( site );
				s[ sp - 2 ] = _props.Count - 1;
				return sp - 1;
			}

			case Intrinsic.WeaponHidePart:
			{
				var part = Text( s[ sp - 2 ] ).Trim();
				if ( !WeaponDesign.IsPartName( part ) )
					return Fail( $"\"{part}\" isn't a part's name" );

				if ( _hidden.Count >= WeaponDesign.MaxHiddenParts )
					return Fail( $"A design hides at most {WeaponDesign.MaxHiddenParts} parts" );

				if ( !_hidden.Contains( part ) )
				{
					_hidden.Add( part );
					_partSites.Add( site );
				}

				return sp - 2;
			}

			default:
			{
				var handle = (int)s[ sp - 2 ];
				if ( handle <= Handles.CurrentEffect || handle >= _included.Count )
					return Fail( "Put on an effect from Effects.Preset( \"...\" ) or Effects.Mine( \"...\" )" );

				if ( _designEffects.Contains( handle ) )
					return Fail( "That effect is on the weapon already: name it again to put on a second" );

				if ( _designEffects.Count >= WeaponDesign.MaxEffects )
					return Fail( $"A weapon has at most {WeaponDesign.MaxEffects} effects" );

				var layers = _included[ handle ].Layers.Count;
				foreach ( var other in _designEffects )
					layers += _included[ other ].Layers.Count;

				if ( layers > WeaponDesign.MaxLayers )
					return Fail( $"A weapon's effects have at most {WeaponDesign.MaxLayers} layers between them" );

				_designEffects.Add( handle );
				_effectSites.Add( site );
				return sp - 1;
			}
		}
	}

	private WeaponSticker StickerOf( int handle )
	{
		if ( handle >= 0 && handle < _stickers.Count )
			return _stickers[ handle ];

		Fail( "That isn't one of the weapon's stickers" );
		return null;
	}

	private WeaponProp PropOf( int handle )
	{
		if ( handle >= 0 && handle < _props.Count )
			return _props[ handle ];

		Fail( "That isn't one of the weapon's props" );
		return null;
	}

	// An effect the design puts on the weapon: what a weapon panel lets be changed on it, nothing more
	private Effect WeaponEffectOf( int handle, PropertyId property )
	{
		if ( !_designEffects.Contains( handle ) )
		{
			Fail( "Change an effect after putting it on the weapon: local flash = Weapon.AddEffect( ... )" );
			return null;
		}

		if ( !ScriptLibrary.OnWeaponEffects( property ) )
		{
			Fail( $"A weapon's effect comes from the weapon, without a key: its {ScriptProperties.Of( property ).Name} can't be changed" );
			return null;
		}

		return _included[ handle ];
	}

	private bool GetWeapon( PropertyId property, int handle, double[] into, int at )
	{
		var skin = _design?.Skin ?? new WeaponSkin();
		switch ( ScriptProperties.Of( property ).Owner )
		{
			case TypeKind.Weapon or TypeKind.WeaponSkin when _design is null:
				return Fail( "Weapons are designed in weapon design scripts" ) >= 0;
			case TypeKind.Weapon:
				if ( property == PropertyId.WeaponFinish )
					into[ at ] = Choice( ScriptEnums.Finish, ScriptEnums.FinishMember( _design.Finish ) );
				else
					ScriptMachine.WriteColor( _design.Tint, into, at );

				return true;
			case TypeKind.WeaponSkin: return GetSkin( property, skin, into, at );
			case TypeKind.WeaponSticker: return StickerOf( handle ) is { } sticker && GetSticker( property, sticker, into, at );
			case TypeKind.WeaponProp: return PropOf( handle ) is { } prop && GetProp( property, prop, into, at );
			default:
				if ( WeaponEffectOf( handle, property ) is not { } effect )
					return false;

				into[ at ] = Choice( ScriptEnums.When, effect.Layers.Count > 0 ? effect.Layers[ 0 ].When : null );
				return true;
		}
	}

	private bool SetWeapon( PropertyId property, int handle, double[] from, int at )
	{
		var owner = ScriptProperties.Of( property ).Owner;
		if ( _design is null && owner is TypeKind.Weapon or TypeKind.WeaponSkin )
			return Fail( "Weapons are designed in weapon design scripts" ) >= 0;

		switch ( owner )
		{
			case TypeKind.Weapon when property == PropertyId.WeaponFinish:
				return Named( ScriptEnums.Finish, from[ at ], out var finish ) && Assign( () => _design.Finish = ScriptEnums.FinishOf( finish ) );
			case TypeKind.Weapon:
				_design.Tint = Hex( from, at );
				return true;
			case TypeKind.WeaponSkin: return SetSkin( property, _design.Skin ??= new WeaponSkin(), from, at );
			case TypeKind.WeaponSticker: return StickerOf( handle ) is { } sticker && SetSticker( property, sticker, from, at );
			case TypeKind.WeaponProp: return PropOf( handle ) is { } prop && SetProp( property, prop, from, at );
			default:
				if ( WeaponEffectOf( handle, property ) is not { } effect || !Named( ScriptEnums.When, from[ at ], out var when ) )
					return false;

				WeaponDesign.ShowWhen( effect, when );
				return true;
		}
	}

	/// <summary>Sets a weapon effect's own field (its name, switch or a slider) on an effect a design carries.</summary>
	private bool SetWeaponEffect( Effect effect, PropertyId property, double[] from, int at )
	{
		var number = (float)ScriptMath.Safe( from[ at ] );
		switch ( property )
		{
			case PropertyId.EffectName: effect.Name = Text( from[ at ] ); break;
			case PropertyId.EffectOff: effect.Off = from[ at ] != 0; break;
			case PropertyId.EffectHue: effect.Hue = number; break;
			case PropertyId.EffectSize: effect.Size = number; break;
			case PropertyId.EffectAmount: effect.Amount = number; break;
			case PropertyId.EffectSpeed: effect.Speed = number; break;
		}

		return true;
	}

	private bool GetWeaponEffect( Effect effect, PropertyId property, double[] into, int at )
	{
		switch ( property )
		{
			case PropertyId.EffectName: into[ at ] = StringIndex( effect.Name ); break;
			case PropertyId.EffectOff: into[ at ] = Flag( effect.Off ); break;
			case PropertyId.EffectHue: into[ at ] = effect.Hue; break;
			case PropertyId.EffectSize: into[ at ] = effect.Size; break;
			case PropertyId.EffectAmount: into[ at ] = effect.Amount; break;
			case PropertyId.EffectSpeed: into[ at ] = effect.Speed; break;
		}

		return true;
	}

	private bool GetSkin( PropertyId property, WeaponSkin skin, double[] into, int at )
	{
		switch ( property )
		{
			case PropertyId.SkinPattern: into[ at ] = Choice( ScriptEnums.Pattern, skin.PatternIs.ToString() ); break;
			case PropertyId.SkinPatternColor: ScriptMachine.WriteColor( skin.PatternColor ?? "#000000", into, at ); break;
			case PropertyId.SkinPatternColorEnd: ScriptMachine.WriteColor( skin.PatternColorEnd, into, at ); break;
			case PropertyId.SkinPatternScale: into[ at ] = skin.PatternScale; break;
			case PropertyId.SkinPatternTurn: into[ at ] = skin.PatternTurn; break;
			case PropertyId.SkinPatternScroll: into[ at ] = skin.PatternScroll; break;
			case PropertyId.SkinPatternSpin: into[ at ] = skin.PatternSpin; break;
			case PropertyId.SkinPatternShape: into[ at ] = PictureHandle( skin.PatternShape ?? nameof( StandardShape.Star5 ) ); break;
			case PropertyId.SkinAnimation: into[ at ] = Choice( ScriptEnums.SkinAnimation, skin.AnimationIs.ToString() ); break;
			case PropertyId.SkinSpeed: into[ at ] = skin.Speed; break;
			case PropertyId.SkinBands: into[ at ] = skin.Bands; break;
			case PropertyId.SkinPalette: return WritePalette( skin.Palette, into, at );
		}

		return true;
	}

	private bool SetSkin( PropertyId property, WeaponSkin skin, double[] from, int at )
	{
		var number = (float)ScriptMath.Safe( from[ at ] );
		switch ( property )
		{
			case PropertyId.SkinPattern:
				return Named( ScriptEnums.Pattern, from[ at ], out var pattern ) && Assign( () => skin.PatternName = pattern == nameof( WeaponSkin.Pattern.None ) ? null : pattern );
			case PropertyId.SkinPatternColor: skin.PatternColor = Hex( from, at ); break;
			case PropertyId.SkinPatternColorEnd: skin.PatternColorEnd = Hex( from, at ); break;
			case PropertyId.SkinPatternScale: skin.PatternScale = number; break;
			case PropertyId.SkinPatternTurn: skin.PatternTurn = number; break;
			case PropertyId.SkinPatternScroll: skin.PatternScroll = number; break;
			case PropertyId.SkinPatternSpin: skin.PatternSpin = number; break;
			case PropertyId.SkinPatternShape: return PictureName( (int)from[ at ], out var shape ) && Assign( () => skin.PatternShape = shape );
			case PropertyId.SkinAnimation:
				return Named( ScriptEnums.SkinAnimation, from[ at ], out var motion ) && Assign( () => skin.Animation = motion == nameof( WeaponSkin.Motion.None ) ? null : motion );
			case PropertyId.SkinSpeed: skin.Speed = number; break;
			case PropertyId.SkinBands: skin.Bands = (int)Math.Round( Math.Clamp( number, WeaponSkin.MinBands, WeaponSkin.MaxBands ) ); break;
			case PropertyId.SkinPalette: return Palette( from, at, out var palette ) && Assign( () => skin.Palette = palette );
		}

		return true;
	}

	private bool GetSticker( PropertyId property, WeaponSticker sticker, double[] into, int at )
	{
		switch ( property )
		{
			case PropertyId.StickerPicture: into[ at ] = PictureHandle( sticker.Picture ); break;
			case PropertyId.StickerColor: ScriptMachine.WriteColor( sticker.Color, into, at ); break;
			case PropertyId.StickerAnimation: into[ at ] = Choice( ScriptEnums.Animation, sticker.AnimationIs.ToString() ); break;
			case PropertyId.StickerPalette: return WritePalette( sticker.Palette, into, at );
			case PropertyId.StickerSpeed: into[ at ] = sticker.Speed; break;
			case PropertyId.StickerPosition: (into[ at ], into[ at + 1 ], into[ at + 2 ]) = (sticker.Forward, sticker.Left, sticker.Up); break;
			case PropertyId.StickerRotation: (into[ at ], into[ at + 1 ], into[ at + 2 ]) = (sticker.Pitch, sticker.Yaw, sticker.Roll); break;
			case PropertyId.StickerSize: into[ at ] = sticker.Size; break;
			case PropertyId.StickerFinish: into[ at ] = (int)StickerFinishes.Of( sticker.Finish ); break;
			case PropertyId.StickerScrape: into[ at ] = sticker.Scrape; break;
			case PropertyId.StickerBorder: into[ at ] = Flag( sticker.Border ); break;
		}

		return true;
	}

	private bool SetSticker( PropertyId property, WeaponSticker sticker, double[] from, int at )
	{
		var number = (float)ScriptMath.Safe( from[ at ] );
		switch ( property )
		{
			case PropertyId.StickerPicture: return PictureName( (int)from[ at ], out var picture ) && Assign( () => sticker.Picture = picture );
			case PropertyId.StickerColor: sticker.Color = Hex( from, at ); break;
			case PropertyId.StickerAnimation:
				return Named( ScriptEnums.Animation, from[ at ], out var motion ) && Assign( () => sticker.Animation = motion == nameof( Sticker.Motion.None ) ? null : motion );
			case PropertyId.StickerPalette: return Palette( from, at, out var palette ) && Assign( () => sticker.Palette = palette );
			case PropertyId.StickerSpeed: sticker.Speed = number; break;
			case PropertyId.StickerPosition: (sticker.Forward, sticker.Left, sticker.Up) = Vector( from, at ); break;
			case PropertyId.StickerRotation: (sticker.Pitch, sticker.Yaw, sticker.Roll) = Vector( from, at ); break;
			case PropertyId.StickerSize: sticker.Size = number; break;
			case PropertyId.StickerFinish:
				return Named( ScriptEnums.StickerFinish, from[ at ], out var finish ) && Assign( () => sticker.Finish = StickerFinishes.Sanitized( finish ) );
			case PropertyId.StickerScrape: sticker.Scrape = number; break;
			case PropertyId.StickerBorder: sticker.Border = from[ at ] != 0; break;
		}

		return true;
	}

	private bool GetProp( PropertyId property, WeaponProp prop, double[] into, int at )
	{
		switch ( property )
		{
			case PropertyId.PropModel: into[ at ] = StringIndex( prop.Model ); break;
			case PropertyId.PropPart: into[ at ] = StringIndex( prop.Part ?? "" ); break;
			case PropertyId.PropReplaces: into[ at ] = Flag( prop.Replaces ); break;
			case PropertyId.PropPosition: (into[ at ], into[ at + 1 ], into[ at + 2 ]) = (prop.Forward, prop.Left, prop.Up); break;
			case PropertyId.PropRotation: (into[ at ], into[ at + 1 ], into[ at + 2 ]) = (prop.Pitch, prop.Yaw, prop.Roll); break;
			case PropertyId.PropScale: into[ at ] = prop.Scale; break;
			case PropertyId.PropNudge: (into[ at ], into[ at + 1 ], into[ at + 2 ]) = (prop.NudgeForward, prop.NudgeLeft, prop.NudgeUp); break;
			case PropertyId.PropNudgeRotation: (into[ at ], into[ at + 1 ], into[ at + 2 ]) = (prop.NudgePitch, prop.NudgeYaw, prop.NudgeRoll); break;
		}

		return true;
	}

	private bool SetProp( PropertyId property, WeaponProp prop, double[] from, int at )
	{
		switch ( property )
		{
			case PropertyId.PropModel:
			{
				var model = Text( from[ at ] ).Trim();
				if ( !WeaponProp.IsModelPath( model ) )
					return Fail( $"\"{model}\" isn't a model: give a prop's path, ending in .vmdl" ) >= 0;

				prop.Model = model;
				break;
			}

			case PropertyId.PropPart:
			{
				var part = Text( from[ at ] ).Trim();
				if ( part.Length > 0 && !WeaponDesign.IsPartName( part ) )
					return Fail( $"\"{part}\" isn't a part's name" ) >= 0;

				prop.Part = part.Length > 0 ? part : null;
				break;
			}

			case PropertyId.PropReplaces: prop.Replaces = from[ at ] != 0; break;
			case PropertyId.PropPosition: (prop.Forward, prop.Left, prop.Up) = Vector( from, at ); break;
			case PropertyId.PropRotation: (prop.Pitch, prop.Yaw, prop.Roll) = Vector( from, at ); break;
			case PropertyId.PropScale: prop.Scale = (float)ScriptMath.Safe( from[ at ] ); break;
			case PropertyId.PropNudge: (prop.NudgeForward, prop.NudgeLeft, prop.NudgeUp) = Vector( from, at ); break;
			case PropertyId.PropNudgeRotation: (prop.NudgePitch, prop.NudgeYaw, prop.NudgeRoll) = Vector( from, at ); break;
		}

		return true;
	}

	// The design made: its stickers' and pattern's drawn shapes resolved (and carried), its effects copied from what the
	// script named, within the same bounds as any design. Its weapon is the caller's to say
	private ScriptBuild FinishWeapon()
	{
		var shapes = new List<DrawnShape>();
		var emblems = new Dictionary<string, IReadOnlyList<EmblemPart>>();
		foreach ( var sticker in _stickers )
		{
			if ( !Resolve( sticker.Picture, shapes, emblems, out var picture ) )
				return Failed( Error );

			sticker.Picture = picture;
		}

		if ( _design.Skin is { } skin )
		{
			if ( !Resolve( skin.PatternShape, shapes, emblems, out var pattern ) )
				return Failed( Error );

			skin.PatternShape = pattern;
		}

		var effects = new List<Effect>();
		foreach ( var handle in _designEffects )
		{
			var effect = _included[ handle ];
			effect.Id = $"w{effects.Count + 1}";
			effect.Key = null;
			effect.Anchor ??= new EffectAnchor();
			effect.Anchor.Kind = nameof( AnchorKind.HeldWeapon );
			effects.Add( effect );
		}

		_design.Stickers = _stickers.Count > 0 ? _stickers : null;
		_design.Props = _props.Count > 0 ? _props : null;
		_design.HiddenParts = _hidden.Count > 0 ? _hidden : null;
		_design.Effects = effects.Count > 0 ? effects : null;
		_design.Shapes = shapes.Count > 0 ? shapes : null;

		// Held to the bounds a look holds a design to, on a stand-in weapon (the design's own is the caller's)
		_design.Weapon = WeaponDesign.AnyWeapon;
		var made = _design.IsEmpty ? new WeaponDesign() : _design.Sanitized() ?? new WeaponDesign();
		made.Weapon = "";
		return new ScriptBuild
		{
			Design = made, Emblems = emblems, Program = _program, Sites = _sites, SharedSites = _sharedSites,
			StickerSites = _stickerSites.ToArray(), PropSites = _propSites.ToArray(), PartSites = _partSites.ToArray(), EffectSites = _effectSites.ToArray(),
		};
	}
}
