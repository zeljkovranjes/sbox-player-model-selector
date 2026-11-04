using System;

namespace PlayerModels.Core.Script;

public sealed partial class ScriptMachine
{
	// Runs function from its start until it returns (or the top halts), within budget instructions. One method with one
	// switch, long as it is: the run's state (pc, sp, fp) stays in locals the JIT keeps in registers, the hot path
	private bool Execute( int function, ScriptStack stack, int budget )
	{
		Fault = default;
		InstructionsLeft = 0;
		stack ??= _ownStack ??= new ScriptStack( Math.Max( Program.StackSlots, 64 ) );
		var s = stack.Slots;
		var frames = stack.Frames;
		var code = Program.Code;
		var constants = Program.Constants;
		var functions = Program.Functions;
		var globals = Globals;
		var info = functions[ function ];
		if ( info.FrameSize + info.MaxEval > s.Length )
			return Stop( FaultKind.CallDepth, info.Start );

		var pc = info.Start;
		var fp = 0;
		var sp = info.FrameSize;
		var depth = 0;
		for ( var i = 0; i < sp; i++ )
			s[ i ] = 0;

		try
		{
			while ( true )
			{
				if ( --budget < 0 )
					return Stop( FaultKind.Budget, pc );

				var at = pc;
				switch ( (OpCode)code[ pc++ ] )
				{
					case OpCode.Halt:
						InstructionsLeft = budget;
						return true;
					case OpCode.Const:
					{
						var from = code[ pc++ ];
						var width = code[ pc++ ];
						for ( var i = 0; i < width; i++ )
							s[ sp++ ] = constants[ from + i ];

						break;
					}

					case OpCode.LoadLocal:
					{
						var from = fp + code[ pc++ ];
						var width = code[ pc++ ];
						for ( var i = 0; i < width; i++ )
							s[ sp++ ] = s[ from + i ];

						break;
					}

					case OpCode.StoreLocal:
					{
						var to = fp + code[ pc++ ];
						var width = code[ pc++ ];
						sp -= width;
						for ( var i = 0; i < width; i++ )
							s[ to + i ] = s[ sp + i ];

						break;
					}

					case OpCode.LoadGlobal:
					{
						var from = code[ pc++ ];
						var width = code[ pc++ ];
						for ( var i = 0; i < width; i++ )
							s[ sp++ ] = globals[ from + i ];

						break;
					}

					case OpCode.StoreGlobal:
					{
						var to = code[ pc++ ];
						var width = code[ pc++ ];
						sp -= width;
						for ( var i = 0; i < width; i++ )
							globals[ to + i ] = s[ sp + i ];

						break;
					}

					case OpCode.Pop:
						sp -= code[ pc++ ];
						break;
					case OpCode.Add:
						sp--;
						s[ sp - 1 ] = ScriptMath.Safe( s[ sp - 1 ] + s[ sp ] );
						break;
					case OpCode.Sub:
						sp--;
						s[ sp - 1 ] = ScriptMath.Safe( s[ sp - 1 ] - s[ sp ] );
						break;
					case OpCode.Mul:
						sp--;
						s[ sp - 1 ] = ScriptMath.Safe( s[ sp - 1 ] * s[ sp ] );
						break;
					case OpCode.Div:
						sp--;
						s[ sp - 1 ] = ScriptMath.Divide( s[ sp - 1 ], s[ sp ] );
						break;
					case OpCode.Mod:
						sp--;
						s[ sp - 1 ] = ScriptMath.Remainder( s[ sp - 1 ], s[ sp ] );
						break;
					case OpCode.Neg:
						s[ sp - 1 ] = -s[ sp - 1 ];
						break;
					case OpCode.AddW or OpCode.SubW or OpCode.MulWW:
					{
						var op = (OpCode)code[ pc - 1 ];
						var width = code[ pc++ ];
						sp -= width;
						for ( var i = 0; i < width; i++ )
						{
							ref var left = ref s[ sp - width + i ];
							left = ScriptMath.Safe( op == OpCode.AddW ? left + s[ sp + i ] : op == OpCode.SubW ? left - s[ sp + i ] : left * s[ sp + i ] );
						}

						break;
					}

					case OpCode.NegW:
					{
						var width = code[ pc++ ];
						for ( var i = 1; i <= width; i++ )
							s[ sp - i ] = -s[ sp - i ];

						break;
					}

					case OpCode.MulWN or OpCode.DivWN:
					{
						var divide = (OpCode)code[ pc - 1 ] == OpCode.DivWN;
						var width = code[ pc++ ];
						var by = s[ --sp ];
						for ( var i = 1; i <= width; i++ )
							s[ sp - i ] = divide ? ScriptMath.Divide( s[ sp - i ], by ) : ScriptMath.Safe( s[ sp - i ] * by );

						break;
					}

					case OpCode.MulNW:
					{
						var width = code[ pc++ ];
						var start = sp - width - 1;
						var by = s[ start ];
						for ( var i = 0; i < width; i++ )
							s[ start + i ] = ScriptMath.Safe( by * s[ start + 1 + i ] );

						sp--;
						break;
					}

					case OpCode.Less:
						sp--;
						s[ sp - 1 ] = s[ sp - 1 ] < s[ sp ] ? 1 : 0;
						break;
					case OpCode.LessEqual:
						sp--;
						s[ sp - 1 ] = s[ sp - 1 ] <= s[ sp ] ? 1 : 0;
						break;
					case OpCode.Greater:
						sp--;
						s[ sp - 1 ] = s[ sp - 1 ] > s[ sp ] ? 1 : 0;
						break;
					case OpCode.GreaterEqual:
						sp--;
						s[ sp - 1 ] = s[ sp - 1 ] >= s[ sp ] ? 1 : 0;
						break;
					case OpCode.Equal or OpCode.NotEqual:
					{
						var equal = (OpCode)code[ pc - 1 ] == OpCode.Equal;
						var width = code[ pc++ ];
						sp -= width * 2;
						var same = true;
						for ( var i = 0; i < width; i++ )
							same &= s[ sp + i ] == s[ sp + width + i ];

						s[ sp++ ] = same == equal ? 1 : 0;
						break;
					}

					case OpCode.Not:
						s[ sp - 1 ] = s[ sp - 1 ] != 0 ? 0 : 1;
						break;
					case OpCode.Jump:
					{
						var offset = code[ pc++ ];
						pc += offset;
						break;
					}

					case OpCode.JumpIfFalse:
					{
						var offset = code[ pc++ ];
						if ( s[ --sp ] == 0 )
							pc += offset;

						break;
					}

					case OpCode.JumpIfTrue:
					{
						var offset = code[ pc++ ];
						if ( s[ --sp ] != 0 )
							pc += offset;

						break;
					}

					case OpCode.ForCheck:
					{
						var counter = fp + code[ pc++ ];
						var offset = code[ pc++ ];
						var (value, limit, step) = (s[ counter ], s[ counter + 1 ], s[ counter + 2 ]);
						if ( step >= 0 ? value > limit : value < limit )
							pc += offset;

						break;
					}

					case OpCode.ForStep:
					{
						var counter = fp + code[ pc++ ];
						s[ counter ] = ScriptMath.Safe( s[ counter ] + s[ counter + 2 ] );
						break;
					}

					case OpCode.Call or OpCode.CallValue:
					{
						int target;
						int width;
						if ( (OpCode)code[ pc - 1 ] == OpCode.Call )
						{
							target = code[ pc++ ];
							width = code[ pc++ ];
						}
						else
						{
							width = code[ pc++ ];
							target = (int)s[ sp - width - 1 ];
							for ( var i = sp - width - 1; i < sp - 1; i++ )
								s[ i ] = s[ i + 1 ];

							sp--;
						}

						if ( target <= 0 || target >= functions.Length )
							return Stop( FaultKind.Internal, at );

						var callee = functions[ target ];
						var frame = sp - width;
						if ( depth >= ScriptLimits.MaxCallDepth || frame + callee.FrameSize + callee.MaxEval > s.Length )
							return Stop( FaultKind.CallDepth, at );

						frames[ depth * 3 ] = pc;
						frames[ depth * 3 + 1 ] = fp;
						depth++;
						fp = frame;
						sp = fp + callee.FrameSize;
						for ( var i = fp + width; i < sp; i++ )
							s[ i ] = 0;

						pc = callee.Start;
						break;
					}

					case OpCode.Return:
					{
						var width = code[ pc++ ];
						if ( depth == 0 )
						{
							InstructionsLeft = budget;
							return true;
						}

						for ( var i = 0; i < width; i++ )
							s[ fp + i ] = s[ sp - width + i ];

						sp = fp + width;
						depth--;
						pc = frames[ depth * 3 ];
						fp = frames[ depth * 3 + 1 ];
						break;
					}

					case OpCode.Intrinsic:
						sp = Intrinsic( (Intrinsic)code[ pc++ ], s, sp, at );
						if ( sp < 0 )
							return false;

						break;
					case OpCode.MakeList:
					{
						var start = code[ pc++ ];
						var count = code[ pc++ ];
						var slots = count * code[ pc++ ];
						sp -= slots;
						for ( var i = 0; i < slots; i++ )
							Arena[ start + i ] = s[ sp + i ];

						s[ sp++ ] = start;
						s[ sp++ ] = count;
						break;
					}

					case OpCode.Index:
					{
						var width = code[ pc++ ];
						var index = Math.Floor( s[ --sp ] );
						sp -= 2;
						var (start, count) = ((int)s[ sp ], (int)s[ sp + 1 ]);
						if ( !(index >= 0 && index < count) )
							return Stop( FaultKind.IndexOutOfRange, at, (int)Math.Clamp( index, int.MinValue, int.MaxValue ), count );

						var from = start + (int)index * width;
						for ( var i = 0; i < width; i++ )
							s[ sp++ ] = Arena[ from + i ];

						break;
					}

					case OpCode.StoreIndex:
					{
						var width = code[ pc++ ];
						sp -= width;
						var item = sp;
						var index = Math.Floor( s[ --sp ] );
						sp -= 2;
						var (start, count) = ((int)s[ sp ], (int)s[ sp + 1 ]);
						if ( !(index >= 0 && index < count) )
							return Stop( FaultKind.IndexOutOfRange, at, (int)Math.Clamp( index, int.MinValue, int.MaxValue ), count );

						var to = start + (int)index * width;
						for ( var i = 0; i < width; i++ )
							Arena[ to + i ] = ScriptMath.Safe( s[ item + i ] );

						break;
					}

					case OpCode.ListCount:
						sp--;
						s[ sp - 1 ] = s[ sp ];
						break;
					case OpCode.Pick:
					{
						var width = code[ pc++ ];
						var component = code[ pc++ ];
						sp -= width;
						s[ sp ] = s[ sp + component ];
						sp++;
						break;
					}

					case OpCode.LoadInput or OpCode.LoadOutput:
					{
						var from = (OpCode)code[ pc - 1 ] == OpCode.LoadInput ? _io.Inputs : _io.Outputs;
						var slot = code[ pc++ ];
						var width = code[ pc++ ];
						for ( var i = 0; i < width; i++ )
							s[ sp++ ] = ScriptMath.Safe( from[ slot + i ] );

						break;
					}

					case OpCode.StoreOutput:
					{
						var slot = code[ pc++ ];
						var width = code[ pc++ ];
						var range = code[ pc++ ];
						sp -= width;
						for ( var i = 0; i < width; i++ )
						{
							var value = s[ sp + i ];
							_io.Outputs[ slot + i ] = range >= 0 ? Math.Clamp( value, constants[ range ], constants[ range + 1 ] )
								: slot == (int)ScriptOutput.PositionX ? NearAnchor( value, i ) : value;
						}

						_io.OutputsSet |= ScriptIO.BitOf( slot );
						break;
					}

					case OpCode.GetProp:
						sp = GetProperty( (PropertyId)code[ pc++ ], s, sp, at );
						if ( sp < 0 )
							return false;

						break;
					case OpCode.SetProp:
					{
						var property = (PropertyId)code[ pc++ ];
						sp = SetProperty( property, code[ pc++ ], s, sp, at );
						if ( sp < 0 )
							return false;

						break;
					}

					case OpCode.Fault:
						return Stop( (FaultKind)code[ pc ], at );
					case OpCode.Parameter:
					{
						var index = code[ pc++ ];
						var width = code[ pc++ ];
						if ( _parameterValues?[ index ] is { } chosen )
						{
							for ( var i = 0; i < width; i++ )
								s[ sp - width + i ] = chosen[ i ];
						}

						break;
					}
					default:
						return Stop( FaultKind.Internal, at );
				}
			}
		}
		catch ( IndexOutOfRangeException )
		{
			// A compiler bug, never a script's doing: reported as a fault, never thrown into the host
			return Stop( FaultKind.Internal, pc );
		}
	}
}
