using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>A layer's fields as script values, read and written while building.</summary>
internal sealed partial class ScriptBuilder
{
	private bool GetLayer( PropertyId property, int index, EffectLayer layer, double[] into, int at )
	{
		switch ( property )
		{
			case PropertyId.LayerKind: into[ at ] = (int)layer.KindIs; break;
			case PropertyId.LayerOff: into[ at ] = Flag( layer.Off ); break;
			case PropertyId.LayerJoint: into[ at ] = Choice( ScriptEnums.Joint, layer.Joint ?? _effect.Anchor.Joint ); break;
			case PropertyId.LayerComesFrom: into[ at ] = (int)layer.ShapeIs; break;
			case PropertyId.LayerSpread: into[ at ] = layer.Spread; break;
			case PropertyId.LayerOffset: (into[ at ], into[ at + 1 ], into[ at + 2 ]) = (layer.Forward, layer.Left, layer.Up); break;
			case PropertyId.LayerFacing: into[ at ] = (int)layer.FacingIs; break;
			case PropertyId.LayerAngle: into[ at ] = layer.Angle; break;
			case PropertyId.LayerToJoint: into[ at ] = Choice( ScriptEnums.Joint, layer.ToJoint ); break;
			case PropertyId.LayerFormation: into[ at ] = ShapeHandle( layer.Formation ); break;
			case PropertyId.LayerFill: into[ at ] = Flag( layer.Fill ); break;
			case PropertyId.LayerLooksLike: into[ at ] = PictureHandle( layer.Picture ); break;
			case PropertyId.LayerColor: ScriptMachine.WriteColor( layer.Color, into, at ); break;
			case PropertyId.LayerColorEnd: ScriptMachine.WriteColor( layer.ColorEnd ?? layer.Color, into, at ); break;
			case PropertyId.LayerColorMode: into[ at ] = (int)layer.ColorModeIs; break;
			case PropertyId.LayerPalette: return WritePalette( layer.Palette, into, at );
			case PropertyId.LayerSize: into[ at ] = layer.Size; break;
			case PropertyId.LayerSizeEnd: into[ at ] = layer.SizeEnd ?? layer.Size; break;
			case PropertyId.LayerSpeed: into[ at ] = layer.Speed; break;
			case PropertyId.LayerGravity: into[ at ] = layer.Gravity; break;
			case PropertyId.LayerLifetime: into[ at ] = layer.Lifetime; break;
			case PropertyId.LayerAmount: into[ at ] = layer.Amount; break;
			case PropertyId.LayerFollow: into[ at ] = Flag( layer.Follow ); break;
			case PropertyId.LayerSolid: into[ at ] = Flag( layer.Solid ); break;
			case PropertyId.LayerRhythm: into[ at ] = (int)layer.RhythmIs; break;
			case PropertyId.LayerRhythmSpeed: into[ at ] = layer.RhythmSpeed; break;
			case PropertyId.LayerRhythmAmount: into[ at ] = layer.RhythmAmount; break;
			case PropertyId.LayerWhen: into[ at ] = (int)layer.WhenIs; break;
			case PropertyId.LayerBurst: into[ at ] = layer.Burst; break;
			case PropertyId.LayerShowFor: into[ at ] = layer.ShowFor; break;
			case PropertyId.LayerFadeWithSpeed: into[ at ] = Flag( layer.FadeWithSpeed ); break;
			case PropertyId.LayerKeep: into[ at ] = layer.Keep; break;
			case PropertyId.LayerAlign: into[ at ] = (int)layer.AlignIs; break;
			case PropertyId.LayerPictures: return WritePictures( layer.Pictures, into, at );
			case PropertyId.LayerDrag: into[ at ] = layer.Drag; break;
			case PropertyId.LayerAtHit: into[ at ] = Flag( layer.AtHit ); break;
			case PropertyId.LayerSweep: into[ at ] = layer.Sweep; break;
			case PropertyId.LayerSlashArc: into[ at ] = layer.SlashShape.Arc; break;
			case PropertyId.LayerSlashThickness: into[ at ] = layer.SlashShape.Width; break;
			case PropertyId.LayerParticle: into[ at ] = _particles[ index ]; break;
		}

		return true;
	}

	private bool SetLayer( PropertyId property, int index, EffectLayer layer, double[] from, int at )
	{
		var number = (float)ScriptMath.Safe( from[ at ] );
		var flag = from[ at ] != 0;
		switch ( property )
		{
			case PropertyId.LayerOff: layer.Off = flag; break;
			case PropertyId.LayerJoint: return Named( ScriptEnums.Joint, from[ at ], out var joint ) && Assign( () => layer.Joint = joint );
			case PropertyId.LayerComesFrom: return Named( ScriptEnums.ComesFrom, from[ at ], out var shape ) && Assign( () => layer.Shape = shape );
			case PropertyId.LayerSpread: layer.Spread = number; break;
			case PropertyId.LayerOffset: (layer.Forward, layer.Left, layer.Up) = Vector( from, at ); break;
			case PropertyId.LayerFacing: return Named( ScriptEnums.Facing, from[ at ], out var facing ) && Assign( () => layer.Facing = facing );
			case PropertyId.LayerAngle: layer.Angle = number; break;
			case PropertyId.LayerToJoint: return Named( ScriptEnums.Joint, from[ at ], out var to ) && Assign( () => layer.ToJoint = to );
			case PropertyId.LayerFormation: return ShapeName( (int)from[ at ], out var formation ) && Assign( () => layer.Formation = formation );
			case PropertyId.LayerFill: layer.Fill = flag; break;
			case PropertyId.LayerLooksLike: return PictureName( (int)from[ at ], out var picture ) && Assign( () => layer.Picture = picture );
			case PropertyId.LayerColor: layer.Color = Hex( from, at ); break;
			case PropertyId.LayerColorEnd: layer.ColorEnd = Hex( from, at ); break;
			case PropertyId.LayerColorMode: return Named( ScriptEnums.ColorMode, from[ at ], out var mode ) && Assign( () => layer.ColorMode = mode );
			case PropertyId.LayerPalette: return Palette( from, at, out var palette ) && Assign( () => layer.Palette = palette );
			case PropertyId.LayerSize: layer.Size = number; break;
			case PropertyId.LayerSizeEnd: layer.SizeEnd = number; break;
			case PropertyId.LayerSpeed: layer.Speed = number; break;
			case PropertyId.LayerGravity: layer.Gravity = number; break;
			case PropertyId.LayerLifetime: layer.Lifetime = number; break;
			case PropertyId.LayerAmount: layer.Amount = number; break;
			case PropertyId.LayerFollow: layer.Follow = flag; break;
			case PropertyId.LayerSolid: layer.Solid = flag; break;
			case PropertyId.LayerRhythm: return Named( ScriptEnums.Rhythm, from[ at ], out var rhythm ) && Assign( () => layer.Rhythm = rhythm );
			case PropertyId.LayerRhythmSpeed: layer.RhythmSpeed = number; break;
			case PropertyId.LayerRhythmAmount: layer.RhythmAmount = number; break;
			case PropertyId.LayerWhen: return Named( ScriptEnums.When, from[ at ], out var when ) && Assign( () => layer.When = when );
			case PropertyId.LayerBurst: layer.Burst = number; break;
			case PropertyId.LayerShowFor: layer.ShowFor = number; break;
			case PropertyId.LayerFadeWithSpeed: layer.FadeWithSpeed = flag; break;
			case PropertyId.LayerKeep: layer.Keep = number; break;
			case PropertyId.LayerAlign: return Named( ScriptEnums.Align, from[ at ], out var align ) && Assign( () => layer.Align = align );
			case PropertyId.LayerPictures: return ReadPictures( from, at, out var pictures ) && Assign( () => layer.Pictures = pictures );
			case PropertyId.LayerDrag: layer.Drag = number; break;
			case PropertyId.LayerAtHit: layer.AtHit = flag; break;
			case PropertyId.LayerSweep: layer.Sweep = number; break;
			case PropertyId.LayerSlashArc: layer.SlashArcDegrees = number; break;
			case PropertyId.LayerSlashThickness: layer.SlashThickness = number; break;
			case PropertyId.LayerParticle: _particles[ index ] = (int)from[ at ]; break;
			default: return Fail( "That can't be changed" ) >= 0;
		}

		return true;
	}

	// A layer's pictures read: their handles copied into the machine's lists; false once the lists are full
	private bool WritePictures( List<string> pictures, double[] into, int at )
	{
		var count = pictures?.Count ?? 0;
		var start = count == 0 ? 0 : _machine.TryAppendArena( count );
		if ( start < 0 )
			return Fail( ScriptMachine.ArenaFull ) >= 0;

		for ( var i = 0; i < count; i++ )
			_machine.Arena[ start + i ] = PictureHandle( pictures[ i ] );

		(into[ at ], into[ at + 1 ]) = (start, count);
		return true;
	}

	// Pictures given as a list of handles: their names (a drawn shape by its mark until it is made); null for none
	private bool ReadPictures( double[] from, int at, out List<string> pictures )
	{
		pictures = null;
		var (start, count) = ((int)from[ at ], (int)from[ at + 1 ]);
		if ( count > EffectLayer.MaxPictures )
			return Fail( $"A layer has at most {EffectLayer.MaxPictures} pictures" ) >= 0;

		if ( count == 0 )
			return true;

		pictures = new List<string>( count );
		for ( var i = 0; i < count; i++ )
		{
			if ( !PictureName( (int)_machine.Arena[ start + i ], out var name ) )
				return false;

			pictures.Add( name );
		}

		return true;
	}
}
