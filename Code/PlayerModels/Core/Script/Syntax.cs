using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>A piece of a parsed script, with the characters it covers (<see cref="Start"/> up to <see cref="End"/>).</summary>
public abstract class Node
{
	public int Start { get; set; }
	public int End { get; set; }
}

/// <summary>Something that has a value.</summary>
public abstract class Expr : Node { }

public sealed class NumberExpr : Expr { public double Value { get; init; } }

public sealed class StringExpr : Expr { public string Value { get; init; } }

public sealed class BoolExpr : Expr { public bool Value { get; init; } }

public sealed class NameExpr : Expr { public string Name { get; init; } }

/// <summary><c>target.Name</c>: a library member, a field of a value, or a method when called.</summary>
public sealed class MemberExpr : Expr
{
	public Expr Target { get; init; }
	public string Name { get; init; }
	public int NameStart { get; init; }
}

public sealed class IndexExpr : Expr
{
	public Expr Target { get; init; }
	public Expr Index { get; init; }
}

public sealed class CallExpr : Expr
{
	public Expr Callee { get; init; }
	public List<Expr> Arguments { get; init; }
}

public sealed class UnaryExpr : Expr
{
	public TokenKind Operator { get; init; }
	public Expr Operand { get; init; }
}

public sealed class BinaryExpr : Expr
{
	public TokenKind Operator { get; init; }
	public Expr Left { get; init; }
	public Expr Right { get; init; }
	public int OperatorStart { get; init; }
}

/// <summary><c>{ a, b, c }</c>: a list, every item of one type.</summary>
public sealed class ListExpr : Expr { public List<Expr> Items { get; init; } }

/// <summary><c>(expr)</c>: kept so an edit can tell a literal from a grouping.</summary>
public sealed class ParenExpr : Expr { public Expr Inner { get; init; } }

/// <summary><c>if c then a elseif d then b else e</c>: a value picked by conditions (Luau's if-expression).</summary>
public sealed class IfExpr : Expr
{
	public List<Expr> Conditions { get; init; }
	public List<Expr> Values { get; init; }
	public Expr Else { get; init; }
}

/// <summary><c>function( params ) ... end</c> as a value.</summary>
public sealed class FunctionExpr : Expr { public FunctionBody Body { get; init; } }

/// <summary>A function's parameters, its return type when written, and its body.</summary>
public sealed class FunctionBody : Node
{
	public List<Parameter> Parameters { get; init; }
	public TypeSyntax ReturnType { get; init; }
	public Block Body { get; init; }
}

public sealed class Parameter : Node
{
	public string Name { get; init; }
	public TypeSyntax Type { get; init; }
}

/// <summary>A written type: a name (<c>number</c>, <c>Color</c>), a list <c>{T}</c>, or a function <c>(A, B) -> R</c>.</summary>
public sealed class TypeSyntax : Node
{
	public string Name { get; init; }
	public TypeSyntax Element { get; init; }
	public List<TypeSyntax> Parameters { get; init; }
	public TypeSyntax Return { get; init; }
}

/// <summary>Something that is done.</summary>
public abstract class Stmt : Node
{
	/// <summary>The index of its first token, for the formatter.</summary>
	public int FirstToken { get; set; }
}

public sealed class Block : Node { public List<Stmt> Statements { get; init; } = new(); }

/// <summary>
/// <c>[Parameter( "Title", Min = 0, Max = 10, ... )]</c> before a top-level local: the option it exposes. Its title (the
/// one unnamed argument) and its named arguments, each with where its name is.
/// </summary>
public sealed class AttributeSyntax : Node
{
	public string Name { get; init; }
	public List<Expr> Arguments { get; init; } = new();
	public List<(string Name, int NameStart, Expr Value)> Named { get; init; } = new();
}

public sealed class LocalStmt : Stmt
{
	/// <summary>Its [Parameter] attribute; null for a plain local.</summary>
	public AttributeSyntax Attribute { get; set; }

	public string Name { get; init; }
	public int NameStart { get; init; }
	public TypeSyntax Type { get; init; }
	public Expr Value { get; init; }
}

/// <summary><c>function name( ... ) ... end</c>, <c>function layer.Particle( p ) ... end</c> or <c>local function</c>.</summary>
public sealed class FunctionStmt : Stmt
{
	public Expr Target { get; init; }
	public FunctionBody Body { get; init; }
	public bool IsLocal { get; init; }
}

public sealed class AssignStmt : Stmt
{
	public Expr Target { get; init; }
	public Expr Value { get; init; }
}

public sealed class CallStmt : Stmt { public CallExpr Call { get; init; } }

public sealed class IfStmt : Stmt
{
	public List<Expr> Conditions { get; init; }
	public List<Block> Blocks { get; init; }
	public Block Else { get; init; }
}

public sealed class WhileStmt : Stmt
{
	public Expr Condition { get; init; }
	public Block Body { get; init; }
}

/// <summary><c>for i = from, to[, step] do ... end</c>: both ends included, like Lua.</summary>
public sealed class ForStmt : Stmt
{
	public string Name { get; init; }
	public int NameStart { get; init; }
	public Expr From { get; init; }
	public Expr To { get; init; }
	public Expr Step { get; init; }
	public Block Body { get; init; }
}

public sealed class ReturnStmt : Stmt { public Expr Value { get; init; } }

public sealed class BreakStmt : Stmt { }

/// <summary>
/// A parsed script: its tokens (comments included), its top block, and for the formatter which tokens start a line and
/// at what depth (<see cref="Breaks"/>, -1 for none; with <see cref="Continues"/> for a line that goes on the statement
/// before it, like the local after its attribute, which never has a blank line before it).
/// </summary>
public sealed class SyntaxTree
{
	/// <summary>The flag in <see cref="Breaks"/> for a line continuing a statement, and the mask of the depth.</summary>
	public const int Continues = 1 << 16;
	public const int DepthMask = Continues - 1;

	public string Source { get; init; }
	public LineMap Lines { get; init; }
	public List<Token> Tokens { get; init; }
	public Block Root { get; init; }
	public int[] Breaks { get; init; }
	public ScriptError Error { get; init; }
}
