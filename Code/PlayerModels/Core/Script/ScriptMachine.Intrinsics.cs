using System;

namespace PlayerModels.Core.Script;

public sealed partial class ScriptMachine
{
	// The most sprites one Emit asks for: a layer's burst cap (the host's budget applies on top)
	private const int MaxEmit = (int)EffectLayer.MaxBurst;

	// Runs a built-in on the values at the top of the stack, leaving its result there; the new top, or -1 on a fault
	private int Intrinsic( Intrinsic op, double[] s, int sp, int at )
	{
		switch ( op )
		{
			case Script.Intrinsic.Abs: s[ sp - 1 ] = Math.Abs( s[ sp - 1 ] ); return sp;
			case Script.Intrinsic.Sign: s[ sp - 1 ] = Math.Sign( s[ sp - 1 ] ); return sp;
			case Script.Intrinsic.Floor: s[ sp - 1 ] = Math.Floor( s[ sp - 1 ] ); return sp;
			case Script.Intrinsic.Ceiling: s[ sp - 1 ] = Math.Ceiling( s[ sp - 1 ] ); return sp;
			case Script.Intrinsic.Round: s[ sp - 1 ] = Math.Round( s[ sp - 1 ] ); return sp;
			case Script.Intrinsic.Frac: s[ sp - 1 ] -= Math.Floor( s[ sp - 1 ] ); return sp;
			case Script.Intrinsic.Sin: s[ sp - 1 ] = ScriptMath.Safe( Math.Sin( s[ sp - 1 ] ) ); return sp;
			case Script.Intrinsic.Cos: s[ sp - 1 ] = ScriptMath.Safe( Math.Cos( s[ sp - 1 ] ) ); return sp;
			case Script.Intrinsic.Tan: s[ sp - 1 ] = ScriptMath.Safe( Math.Tan( s[ sp - 1 ] ) ); return sp;
			case Script.Intrinsic.Sqrt: s[ sp - 1 ] = s[ sp - 1 ] > 0 ? Math.Sqrt( s[ sp - 1 ] ) : 0; return sp;
			case Script.Intrinsic.Random: s[ sp - 1 ] = ScriptMath.Random( s[ sp - 1 ] ); return sp;
			case Script.Intrinsic.Exp: s[ sp - 1 ] = ScriptMath.Safe( Math.Exp( s[ sp - 1 ] ) ); return sp;
			case Script.Intrinsic.Min: s[ sp - 2 ] = Math.Min( s[ sp - 2 ], s[ sp - 1 ] ); return sp - 1;
			case Script.Intrinsic.Max: s[ sp - 2 ] = Math.Max( s[ sp - 2 ], s[ sp - 1 ] ); return sp - 1;
			case Script.Intrinsic.Atan2: s[ sp - 2 ] = ScriptMath.Safe( Math.Atan2( s[ sp - 2 ], s[ sp - 1 ] ) ); return sp - 1;
			case Script.Intrinsic.Pow: s[ sp - 2 ] = ScriptMath.Safe( Math.Pow( s[ sp - 2 ], s[ sp - 1 ] ) ); return sp - 1;
			case Script.Intrinsic.Noise: s[ sp - 2 ] = ScriptMath.Noise( s[ sp - 2 ], s[ sp - 1 ] ); return sp - 1;
			case Script.Intrinsic.Clamp:
			{
				var (value, least, most) = (s[ sp - 3 ], s[ sp - 2 ], s[ sp - 1 ]);
				s[ sp - 3 ] = least > most ? least : Math.Clamp( value, least, most );
				return sp - 2;
			}

			case Script.Intrinsic.SmoothStep:
				s[ sp - 3 ] = ScriptMath.SmoothStep( s[ sp - 3 ], s[ sp - 2 ], s[ sp - 1 ] );
				return sp - 2;
			case Script.Intrinsic.Lerp:
				s[ sp - 3 ] = Lerp( s[ sp - 3 ], s[ sp - 2 ], s[ sp - 1 ] );
				return sp - 2;
			case Script.Intrinsic.ColorNew or Script.Intrinsic.VectorNew:
				return sp;
			case Script.Intrinsic.ColorParse:
			{
				var index = (int)s[ sp - 1 ];
				var text = index >= 0 && index < Strings.Length ? Strings[ index ] : null;
				WriteColor( text, s, sp - 1 );
				return sp + 3;
			}

			case Script.Intrinsic.ColorFromHsv:
			{
				var (r, g, b) = ScriptMath.FromHsv( s[ sp - 4 ], s[ sp - 3 ], s[ sp - 2 ] );
				(s[ sp - 4 ], s[ sp - 3 ], s[ sp - 2 ]) = (r, g, b);
				return sp;
			}

			case Script.Intrinsic.ColorLerp:
			{
				var t = s[ sp - 1 ];
				var a = sp - 9;
				for ( var i = 0; i < 4; i++ )
					s[ a + i ] = Lerp( s[ a + i ], s[ a + 4 + i ], t );

				return sp - 5;
			}

			case Script.Intrinsic.VectorLerp:
			{
				var t = s[ sp - 1 ];
				var a = sp - 7;
				for ( var i = 0; i < 3; i++ )
					s[ a + i ] = Lerp( s[ a + i ], s[ a + 3 + i ], t );

				return sp - 4;
			}

			case Script.Intrinsic.VectorDot:
			{
				var a = sp - 6;
				s[ a ] = ScriptMath.Safe( s[ a ] * s[ a + 3 ] + s[ a + 1 ] * s[ a + 4 ] + s[ a + 2 ] * s[ a + 5 ] );
				return a + 1;
			}

			case Script.Intrinsic.VectorCross:
			{
				var a = sp - 6;
				var (ax, ay, az, bx, by, bz) = (s[ a ], s[ a + 1 ], s[ a + 2 ], s[ a + 3 ], s[ a + 4 ], s[ a + 5 ]);
				s[ a ] = ScriptMath.Safe( ay * bz - az * by );
				s[ a + 1 ] = ScriptMath.Safe( az * bx - ax * bz );
				s[ a + 2 ] = ScriptMath.Safe( ax * by - ay * bx );
				return a + 3;
			}

			case Script.Intrinsic.VectorDistance:
			{
				var a = sp - 6;
				s[ a ] = Length( s[ a ] - s[ a + 3 ], s[ a + 1 ] - s[ a + 4 ], s[ a + 2 ] - s[ a + 5 ] );
				return a + 1;
			}

			case Script.Intrinsic.VectorLength:
			{
				var a = sp - 3;
				s[ a ] = Length( s[ a ], s[ a + 1 ], s[ a + 2 ] );
				return a + 1;
			}

			case Script.Intrinsic.VectorNormal:
			{
				var a = sp - 3;
				var length = Length( s[ a ], s[ a + 1 ], s[ a + 2 ] );
				for ( var i = 0; i < 3; i++ )
					s[ a + i ] = ScriptMath.Divide( s[ a + i ], length );

				return sp;
			}

			case Script.Intrinsic.ListCreate: return ListCreate( s, sp, at );
			case Script.Intrinsic.LayerEmit:
				_host?.Emit( (int)s[ sp - 2 ], EmitCount( s[ sp - 1 ] ) );
				return sp - 2;
			case Script.Intrinsic.LayerEmitAt:
				_host?.EmitAt( (int)s[ sp - 5 ], s[ sp - 4 ], s[ sp - 3 ], s[ sp - 2 ], EmitCount( s[ sp - 1 ] ) );
				return sp - 5;
			default:
				return Built( op, s, sp, at );
		}
	}

	// The built-ins that make things: only while building, by the builder
	private int Built( Intrinsic op, double[] s, int sp, int at )
	{
		if ( _builder is null )
			return Stop( FaultKind.BuildOnly, at ) ? sp : -1;

		var result = _builder.Run( op, s, sp );
		if ( result < 0 )
			Stop( FaultKind.Build, at, message: _builder.Error );

		return result;
	}

	// List.Create( count, value ): count copies of the value (its width on top), made in the lists while building
	private int ListCreate( double[] s, int sp, int at )
	{
		if ( _builder is null )
			return Stop( FaultKind.BuildOnly, at ) ? sp : -1;

		var width = (int)s[ sp - 1 ];
		var item = sp - 1 - width;
		var count = Math.Floor( s[ item - 1 ] );
		if ( !(count >= 0 && count <= ScriptLimits.MaxListLength) )
			return Stop( FaultKind.Build, at, message: $"List.Create makes at most {ScriptLimits.MaxListLength} items, not {ScriptValue.NumberText( count )}" ) ? sp : -1;

		var start = TryAppendArena( (int)count * width );
		if ( start < 0 )
			return Stop( FaultKind.Build, at, message: ArenaFull ) ? sp : -1;

		for ( var k = 0; k < count; k++ )
		{
			for ( var i = 0; i < width; i++ )
				Arena[ start + k * width + i ] = s[ item + i ];
		}

		s[ item - 1 ] = start;
		s[ item ] = count;
		return item + 1;
	}

	private static double Lerp( double a, double b, double t ) => ScriptMath.Safe( a + (b - a) * Math.Clamp( t, 0, 1 ) );

	private static double Length( double x, double y, double z ) => ScriptMath.Safe( Math.Sqrt( x * x + y * y + z * z ) );

	private static int EmitCount( double count ) => (int)Math.Clamp( Math.Floor( count ), 0, MaxEmit );

	private int GetProperty( PropertyId property, double[] s, int sp, int at )
	{
		var handle = (int)s[ sp - 1 ];
		var width = ScriptProperties.Of( property ).Type.Width;
		if ( _builder is not null )
		{
			if ( !_builder.Get( property, handle, s, sp - 1 ) )
				return Stop( FaultKind.Build, at, message: _builder.Error ) ? 0 : -1;

			return sp - 1 + width;
		}

		if ( property > PropertyId.LayerParticle )
			return Stop( FaultKind.BuildOnly, at ) ? 0 : -1;

		if ( handle < 0 || handle >= Layers )
			return Stop( FaultKind.Internal, at ) ? 0 : -1;

		var from = handle * ScriptProperties.LiveBlockSize + ScriptProperties.LiveOffset[ (int)property ];
		for ( var i = 0; i < width; i++ )
			s[ sp - 1 + i ] = Live[ from + i ];

		return sp - 1 + width;
	}

	private int SetProperty( PropertyId property, int site, double[] s, int sp, int at )
	{
		var definition = ScriptProperties.Of( property );
		var width = definition.Type.Width;
		var value = sp - width;
		var handle = (int)s[ value - 1 ];
		if ( _builder is not null )
		{
			if ( !_builder.Set( property, handle, s, value, site ) )
				return Stop( FaultKind.Build, at, message: _builder.Error ) ? 0 : -1;

			return value - 1;
		}

		if ( property > PropertyId.LayerParticle )
			return Stop( FaultKind.BuildOnly, at ) ? 0 : -1;

		if ( !definition.Live )
			return Stop( FaultKind.NotLive, at ) ? 0 : -1;

		if ( handle < 0 || handle >= Layers )
			return Stop( FaultKind.Internal, at ) ? 0 : -1;

		var block = handle * ScriptProperties.LiveBlockSize;
		var isLight = (int)Live[ block + ScriptProperties.LiveOffset[ (int)PropertyId.LayerKind ] ] == (int)LayerKind.Light;
		var (least, most) = isLight && !double.IsNaN( definition.LightMin ) ? (definition.LightMin, definition.LightMax) : (definition.Min, definition.Max);
		var to = block + ScriptProperties.LiveOffset[ (int)property ];
		for ( var i = 0; i < width; i++ )
		{
			var slot = ScriptMath.Safe( s[ value + i ] );
			Live[ to + i ] = double.IsNaN( least ) ? slot : Math.Clamp( slot, least, most );
		}

		LiveSet[ handle ] |= 1UL << (int)property;
		return value - 1;
	}
}
