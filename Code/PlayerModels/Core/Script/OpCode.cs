namespace PlayerModels.Core.Script;

/// <summary>
/// The machine's instructions. Each is an int followed by its operands (listed after it); jumps are relative to the
/// instruction after them, so a function's code can sit anywhere. Values are slots of doubles on one stack; widths say
/// how many slots a value takes (see <see cref="ScriptType.Width"/>).
/// </summary>
public enum OpCode
{
	/// <summary>Ends the run.</summary>
	Halt,

	/// <summary>index, width: pushes constants.</summary>
	Const,

	/// <summary>offset, width: a local (from the frame's start) or a script variable.</summary>
	LoadLocal, StoreLocal, LoadGlobal, StoreGlobal,

	/// <summary>width.</summary>
	Pop,

	Add, Sub, Mul, Div, Mod, Neg,

	/// <summary>width: per slot, for colours and vectors (MulWN/DivWN: the number on top; MulNW: the number below).</summary>
	AddW, SubW, NegW, MulWN, DivWN, MulNW, MulWW,

	Less, LessEqual, Greater, GreaterEqual,

	/// <summary>width.</summary>
	Equal, NotEqual,

	Not,

	/// <summary>relative target (JumpIfFalse/JumpIfTrue pop the bool).</summary>
	Jump, JumpIfFalse, JumpIfTrue,

	/// <summary>offset (of the counter, limit and step locals), relative target: leaves the loop once the counter is past the limit.</summary>
	ForCheck,

	/// <summary>offset: adds the step to the counter.</summary>
	ForStep,

	/// <summary>function, argument width.</summary>
	Call,

	/// <summary>argument width: calls the function value below the arguments.</summary>
	CallValue,

	/// <summary>width of the value returned.</summary>
	Return,

	/// <summary>id (see <see cref="Script.Intrinsic"/>).</summary>
	Intrinsic,

	/// <summary>arena offset, item count, item width: moves the items into the arena and pushes the list.</summary>
	MakeList,

	/// <summary>item width: pops the index and the list, pushes the item.</summary>
	Index,

	ListCount,

	/// <summary>width, component.</summary>
	Pick,

	/// <summary>slot, width.</summary>
	LoadInput, LoadOutput,

	/// <summary>slot, width, constant index of the range (min, max) or -1.</summary>
	StoreOutput,

	/// <summary>property.</summary>
	GetProp,

	/// <summary>property, site.</summary>
	SetProp,

	/// <summary>fault code: stops the run (a function that ended without giving its value).</summary>
	Fault,

	/// <summary>parameter, width: replaces the value on top with the parameter's chosen value, when one is set.</summary>
	Parameter,

	/// <summary>item width: pops the item, the index and the list, and writes the item into the list.</summary>
	StoreIndex,
}
