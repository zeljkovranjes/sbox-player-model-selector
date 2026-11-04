using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

internal sealed partial class ScriptBuilder
{
	/// <summary>Reads a property of a layer, the effect, a shape or an emblem layer into <paramref name="into"/> at <paramref name="at"/>.</summary>
	public bool Get( PropertyId property, int handle, double[] into, int at )
	{
		if ( IsWeaponProperty( property ) )
			return GetWeapon( property, handle, into, at );

		if ( _design is not null && property is >= PropertyId.EffectName and <= PropertyId.EffectSpeed )
			return WeaponEffectOf( handle, property ) is { } carried && GetWeaponEffect( carried, property, into, at );

		if ( property <= PropertyId.LayerParticle )
			return LayerOf( handle ) is { } layer && GetLayer( property, handle, layer, into, at );

		if ( property <= PropertyId.EffectSpeed )
			return IsCurrent( handle ) && GetEffect( property, into, at );

		if ( property <= PropertyId.ShapePointCount )
			return DraftOf( handle ) is { } draft && GetShape( property, draft, into, at );

		return PartOf( handle ) is { } part && GetPart( property, part, into, at );
	}

	/// <summary>Writes a property from <paramref name="from"/> at <paramref name="at"/>, recording <paramref name="site"/> as where it was set.</summary>
	public bool Set( PropertyId property, int handle, double[] from, int at, int site )
	{
		if ( IsWeaponProperty( property ) )
		{
			if ( !SetWeapon( property, handle, from, at ) )
				return false;

			Record( WeaponOwnerOf( property, handle ), property, site );
			return true;
		}

		if ( _design is not null && property is >= PropertyId.EffectName and <= PropertyId.EffectSpeed )
		{
			if ( WeaponEffectOf( handle, property ) is not { } carried || !SetWeaponEffect( carried, property, from, at ) )
				return false;

			Record( ScriptBuild.EffectOwnerBase + _designEffects.IndexOf( handle ), property, site );
			return true;
		}

		if ( property <= PropertyId.LayerParticle )
		{
			if ( LayerOf( handle ) is not { } layer || !SetLayer( property, handle, layer, from, at ) )
				return false;

			Record( handle, property, site );
			return true;
		}

		if ( property <= PropertyId.EffectSpeed )
		{
			if ( !IsCurrent( handle ) || !SetEffect( property, from, at ) )
				return false;

			Record( ScriptBuild.EffectOwner, property, site );
			return true;
		}

		if ( property <= PropertyId.ShapePointCount )
			return DraftOf( handle ) is { } draft && SetShape( property, draft, from, at );

		return PartOf( handle ) is { } part && SetPart( property, part, from, at );
	}

	private void Record( int owner, PropertyId property, int site )
	{
		_sites[ (owner, property) ] = site;
		if ( _siteTarget.TryGetValue( site, out var target ) && target != (owner, property) )
			_sharedSites.Add( site );
		else
			_siteTarget[ site ] = (owner, property);
	}

	private EffectLayer LayerOf( int handle )
	{
		if ( handle >= 0 && handle < _layers.Count )
			return _layers[ handle ];

		Fail( "That isn't one of the effect's layers" );
		return null;
	}

	private bool IsCurrent( int handle ) => handle == Handles.CurrentEffect || Fail( "Only the effect this script makes can be read or changed" ) >= 0;

	private EmblemPart PartOf( int handle )
	{
		if ( handle >= 0 && handle < _parts.Count )
			return _parts[ handle ];

		Fail( "That isn't an emblem layer" );
		return null;
	}

	private bool GetEffect( PropertyId property, double[] into, int at )
	{
		var anchor = _effect.Anchor;
		switch ( property )
		{
			case PropertyId.EffectName: into[ at ] = StringIndex( _effect.Name ); break;
			case PropertyId.EffectKey: into[ at ] = Math.Max( Compiler.KeyIndex( _effect.Key ), 0 ); break;
			case PropertyId.EffectOff: into[ at ] = Flag( _effect.Off ); break;
			case PropertyId.EffectAnchor: into[ at ] = (int)anchor.KindIs; break;
			case PropertyId.EffectJoint: into[ at ] = Choice( ScriptEnums.Joint, anchor.Joint ); break;
			case PropertyId.EffectItem: into[ at ] = StringIndex( anchor.Item ); break;
			case PropertyId.EffectOffset: (into[ at ], into[ at + 1 ], into[ at + 2 ]) = (anchor.Forward, anchor.Left, anchor.Up); break;
			case PropertyId.EffectHue: into[ at ] = _effect.Hue; break;
			case PropertyId.EffectSize: into[ at ] = _effect.Size; break;
			case PropertyId.EffectAmount: into[ at ] = _effect.Amount; break;
			case PropertyId.EffectSpeed: into[ at ] = _effect.Speed; break;
		}

		return true;
	}

	private bool SetEffect( PropertyId property, double[] from, int at )
	{
		var anchor = _effect.Anchor;
		var number = (float)ScriptMath.Safe( from[ at ] );
		switch ( property )
		{
			case PropertyId.EffectName: _effect.Name = Text( from[ at ] ); break;
			case PropertyId.EffectKey:
			{
				var key = (int)from[ at ];
				_effect.Key = key >= 0 && key < Effect.Keys.Count ? Effect.Keys[ key ] : null;
				break;
			}

			case PropertyId.EffectOff: _effect.Off = from[ at ] != 0; break;
			case PropertyId.EffectAnchor:
				return Named( ScriptEnums.Anchor, from[ at ], out var kind ) && Assign( () => anchor.Kind = kind == nameof( AnchorKind.Joint ) ? null : kind );
			case PropertyId.EffectJoint: return Named( ScriptEnums.Joint, from[ at ], out var joint ) && Assign( () => anchor.Joint = joint );
			case PropertyId.EffectItem: anchor.Item = Text( from[ at ] ); break;
			case PropertyId.EffectOffset: (anchor.Forward, anchor.Left, anchor.Up) = Vector( from, at ); break;
			case PropertyId.EffectHue: _effect.Hue = number; break;
			case PropertyId.EffectSize: _effect.Size = number; break;
			case PropertyId.EffectAmount: _effect.Amount = number; break;
			case PropertyId.EffectSpeed: _effect.Speed = number; break;
		}

		return true;
	}

	private bool GetShape( PropertyId property, Draft draft, double[] into, int at )
	{
		var sticker = draft.Sticker ?? new Sticker();
		switch ( property )
		{
			case PropertyId.ShapeFillColor: ScriptMachine.WriteColor( sticker.Fill, into, at ); break;
			case PropertyId.ShapeOutlineColor: ScriptMachine.WriteColor( sticker.Outline, into, at ); break;
			case PropertyId.ShapeOutlineWidth: into[ at ] = sticker.OutlineWidth; break;
			case PropertyId.ShapeAnimation: into[ at ] = (int)sticker.AnimationIs; break;
			case PropertyId.ShapePalette: return WritePalette( sticker.Palette, into, at );
			case PropertyId.ShapeAnimationSpeed: into[ at ] = sticker.Speed; break;
			case PropertyId.ShapeMirror: into[ at ] = Flag( draft.Mirror ); break;
			case PropertyId.ShapeFinish: into[ at ] = (int)StickerFinishes.Of( draft.Finish ); break;
			case PropertyId.ShapePointCount: into[ at ] = PointsOf( draft ); break;
		}

		return true;
	}

	private bool SetShape( PropertyId property, Draft draft, double[] from, int at )
	{
		draft.Edited = true;
		switch ( property )
		{
			case PropertyId.ShapeMirror:
				draft.Mirror = from[ at ] != 0;
				return true;
			case PropertyId.ShapeFinish:
				return Named( ScriptEnums.StickerFinish, from[ at ], out var finish ) && Assign( () => draft.Finish = StickerFinishes.Sanitized( finish ) );
		}

		var sticker = draft.Sticker ??= new Sticker();
		switch ( property )
		{
			case PropertyId.ShapeFillColor: sticker.Fill = Hex( from, at ); break;
			case PropertyId.ShapeOutlineColor: sticker.Outline = Hex( from, at ); break;
			case PropertyId.ShapeOutlineWidth: sticker.OutlineWidth = (float)ScriptMath.Safe( from[ at ] ); break;
			case PropertyId.ShapeAnimation:
				return Named( ScriptEnums.Animation, from[ at ], out var motion ) && Assign( () => sticker.Animation = motion == nameof( Sticker.Motion.None ) ? null : motion );
			case PropertyId.ShapePalette: return Palette( from, at, out var palette ) && Assign( () => sticker.Palette = palette );
			case PropertyId.ShapeAnimationSpeed: sticker.Speed = (float)ScriptMath.Safe( from[ at ] ); break;
		}

		return true;
	}

	private bool GetPart( PropertyId property, EmblemPart part, double[] into, int at )
	{
		switch ( property )
		{
			case PropertyId.EmblemShape: into[ at ] = part.ShapeHandle; break;
			case PropertyId.EmblemPosition: (into[ at ], into[ at + 1 ], into[ at + 2 ]) = (part.X, part.Y, 0); break;
			case PropertyId.EmblemRotation: into[ at ] = part.Rotation; break;
			case PropertyId.EmblemScale: (into[ at ], into[ at + 1 ], into[ at + 2 ]) = (part.ScaleX, part.ScaleY, 1); break;
			case PropertyId.EmblemFlipX: into[ at ] = Flag( part.FlipX ); break;
			case PropertyId.EmblemFlipY: into[ at ] = Flag( part.FlipY ); break;
			case PropertyId.EmblemColor: ScriptMachine.WriteColor( part.Color, into, at ); break;
			case PropertyId.EmblemOpacity: into[ at ] = part.Opacity; break;
			case PropertyId.EmblemOutlineColor: ScriptMachine.WriteColor( part.OutlineColor, into, at ); break;
			case PropertyId.EmblemOutlineWidth: into[ at ] = part.OutlineWidth; break;
			case PropertyId.EmblemAnimation: into[ at ] = Choice( ScriptEnums.Animation, part.Animation ); break;
			case PropertyId.EmblemPalette: return WritePalette( part.Palette, into, at );
			case PropertyId.EmblemAnimationSpeed: into[ at ] = part.AnimationSpeed; break;
			case PropertyId.EmblemHidden: into[ at ] = Flag( part.Hidden ); break;
			case PropertyId.EmblemEraser: into[ at ] = Flag( part.Eraser ); break;
			case PropertyId.EmblemSide: into[ at ] = part.Side; break;
		}

		return true;
	}

	private bool SetPart( PropertyId property, EmblemPart part, double[] from, int at )
	{
		var definition = ScriptProperties.Of( property );
		var number = (float)Held( from[ at ], definition );
		switch ( property )
		{
			case PropertyId.EmblemShape:
				if ( StrokesOf( (int)from[ at ] ) is null )
					return false;

				part.ShapeHandle = (int)from[ at ];
				break;
			case PropertyId.EmblemPosition: (part.X, part.Y) = ((float)Held( from[ at ], definition ), (float)Held( from[ at + 1 ], definition )); break;
			case PropertyId.EmblemRotation: part.Rotation = number; break;
			case PropertyId.EmblemScale: (part.ScaleX, part.ScaleY) = ((float)Held( from[ at ], definition ), (float)Held( from[ at + 1 ], definition )); break;
			case PropertyId.EmblemFlipX: part.FlipX = from[ at ] != 0; break;
			case PropertyId.EmblemFlipY: part.FlipY = from[ at ] != 0; break;
			case PropertyId.EmblemColor: part.Color = Hex( from, at ); break;
			case PropertyId.EmblemOpacity: part.Opacity = number; break;
			case PropertyId.EmblemOutlineColor: part.OutlineColor = Hex( from, at ); break;
			case PropertyId.EmblemOutlineWidth: part.OutlineWidth = number; break;
			case PropertyId.EmblemAnimation:
				return Named( ScriptEnums.Animation, from[ at ], out var motion ) && Assign( () => part.Animation = motion == nameof( Sticker.Motion.None ) ? null : motion );
			case PropertyId.EmblemPalette: return Palette( from, at, out var palette ) && Assign( () => part.Palette = palette );
			case PropertyId.EmblemAnimationSpeed: part.AnimationSpeed = number; break;
			case PropertyId.EmblemHidden: part.Hidden = from[ at ] != 0; break;
			case PropertyId.EmblemEraser: part.Eraser = from[ at ] != 0; break;
			case PropertyId.EmblemSide: part.Side = (int)Math.Round( number ); break;
		}

		return true;
	}

	// Emblem layers have no sanitizer yet: their numbers are held to their ranges here
	private static double Held( double value, ScriptProperty definition ) =>
		double.IsNaN( definition.Min ) ? ScriptMath.Safe( value ) : Math.Clamp( ScriptMath.Safe( value ), definition.Min, definition.Max );

	private static bool Assign( Action assign )
	{
		assign();
		return true;
	}

	private static double Flag( bool value ) => value ? 1 : 0;

	private static int Choice( ScriptEnum choices, string name ) => Math.Max( name is null ? 0 : choices.IndexOf( name ), 0 );

	private bool Named( ScriptEnum choices, double value, out string name )
	{
		var index = (int)value;
		name = index >= 0 && index < choices.Members.Count ? choices.Members[ index ] : null;
		return name is not null || Fail( $"That isn't a {choices.Name}" ) >= 0;
	}

	private static (float, float, float) Vector( double[] from, int at ) =>
		((float)ScriptMath.Safe( from[ at ] ), (float)ScriptMath.Safe( from[ at + 1 ] ), (float)ScriptMath.Safe( from[ at + 2 ] ));

	private static string Hex( double[] from, int at ) => ScriptValue.ColorText( from[ at ], from[ at + 1 ], from[ at + 2 ], 1 );

	private int StringIndex( string text ) => Math.Max( Array.IndexOf( _machine.Strings, text ?? "" ), 0 );

	private bool Palette( double[] from, int at, out List<string> palette )
	{
		palette = null;
		var (start, count) = ((int)from[ at ], (int)from[ at + 1 ]);
		if ( count > EffectLayer.MaxPalette )
			return Fail( $"A palette has at most {EffectLayer.MaxPalette} colours" ) >= 0;

		palette = new List<string>( count );
		for ( var i = 0; i < count; i++ )
		{
			var colour = start + i * 4;
			palette.Add( ScriptValue.ColorText( _machine.Arena[ colour ], _machine.Arena[ colour + 1 ], _machine.Arena[ colour + 2 ], 1 ) );
		}

		return true;
	}

	// A palette read: its colours copied into the machine's lists, the game's five for none; false once the lists are full
	private bool WritePalette( List<string> own, double[] into, int at )
	{
		IReadOnlyList<string> colours = own is { Count: > 0 } ? own : ColorCycle.GamePalette;
		var start = _machine.TryAppendArena( colours.Count * 4 );
		if ( start < 0 )
			return Fail( ScriptMachine.ArenaFull ) >= 0;

		for ( var i = 0; i < colours.Count; i++ )
			ScriptMachine.WriteColor( colours[ i ], _machine.Arena, start + i * 4 );

		into[ at ] = start;
		into[ at + 1 ] = colours.Count;
		return true;
	}

	private int ShapeHandle( string name )
	{
		if ( name is null )
			return 0;

		if ( Enum.TryParse<StandardShape>( name, false, out var standard ) && Enum.IsDefined( standard ) )
			return (int)standard;

		return DraftHandle( name );
	}

	private int PictureHandle( string name )
	{
		if ( name is null )
			return Handles.PictureBase;

		if ( Enum.TryParse<LayerPicture>( name, false, out var picture ) && Enum.IsDefined( picture ) )
			return Handles.PictureBase + (int)picture;

		return ShapeHandle( name );
	}

	// The draft a layer's shape or picture names: by its mark while building, by its reference once made
	private int DraftHandle( string name )
	{
		if ( name.Length > 1 && name[ 0 ] == DraftMark && int.TryParse( name.AsSpan( 1 ), out var index ) )
			return Handles.DraftBase + index;

		for ( var i = 0; i < _drafts.Count; i++ )
		{
			if ( _drafts[ i ].Made?.Reference == name )
				return Handles.DraftBase + i;
		}

		return 0;
	}

	private bool ShapeName( int handle, out string name )
	{
		name = null;
		if ( handle >= 0 && handle < Handles.DraftBase && Enum.IsDefined( typeof( StandardShape ), handle ) )
		{
			name = ((StandardShape)handle).ToString();
			return true;
		}

		var index = handle - Handles.DraftBase;
		if ( index < 0 || index >= _drafts.Count )
			return Fail( "That isn't a shape" ) >= 0;

		name = _drafts[ index ].IsMade ? _drafts[ index ].Made.Reference : DraftMark + index.ToString();
		return true;
	}

	private bool PictureName( int handle, out string name )
	{
		var picture = handle - Handles.PictureBase;
		if ( picture >= 0 && Enum.IsDefined( typeof( LayerPicture ), picture ) )
		{
			name = ((LayerPicture)picture).ToString();
			return true;
		}

		return ShapeName( handle, out name );
	}
}
