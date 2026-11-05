using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

public static partial class ScriptLibrary
{
	[Sandbox.SkipHotload]
	private static readonly ScriptType N = ScriptType.Number;

	private static void AddMath( List<LibraryEntry> entries )
	{
		const string math = "Math";
		var all = ScriptPlace.All;
		entries.Add( Space( math, math, "Numbers: rounding, limits, trigonometry (radians), noise and repeatable randomness, named like C#'s Math." ) );
		entries.Add( Constant( "Math.PI", N, math, "Half a turn in radians: 3.14159...", "local pi = Math.PI --> 3.1416", Math.PI ) );
		entries.Add( Function( "Math.Abs", Intrinsic.Abs, N, math, "x without its sign.", "local a = Math.Abs( -3.5 ) --> 3.5", all, P( "x", N, "Any number" ) ) );
		entries.Add( Function( "Math.Sign", Intrinsic.Sign, N, math, "-1, 0 or 1: the sign of x.", "local s = Math.Sign( -2 ) --> -1", all, P( "x", N, "Any number" ) ) );
		entries.Add( Function( "Math.Floor", Intrinsic.Floor, N, math, "The largest whole number at or below x.", "local f = Math.Floor( 2.7 ) --> 2\nlocal g = Math.Floor( -2.5 ) --> -3", all, P( "x", N, "Any number" ) ) );
		entries.Add( Function( "Math.Ceiling", Intrinsic.Ceiling, N, math, "The smallest whole number at or above x.", "local c = Math.Ceiling( 2.1 ) --> 3", all, P( "x", N, "Any number" ) ) );
		entries.Add( Function( "Math.Round", Intrinsic.Round, N, math, "x to the nearest whole number; halves go to the even one, like C#.", "local r = Math.Round( 2.6 ) --> 3\nlocal half = Math.Round( 2.5 ) --> 2", all, P( "x", N, "Any number" ) ) );
		entries.Add( Function( "Math.Frac", Intrinsic.Frac, N, math, "The part of x after its whole number (x - Math.Floor( x )), 0 up to 1.", "local f = Math.Frac( 2.75 ) --> 0.75", all, P( "x", N, "Any number" ) ) );
		entries.Add( Function( "Math.Min", Intrinsic.Min, N, math, "The smaller of a and b.", "local m = Math.Min( 3, 7 ) --> 3", all, P( "a", N, "A number" ), P( "b", N, "Another" ) ) );
		entries.Add( Function( "Math.Max", Intrinsic.Max, N, math, "The larger of a and b.", "local m = Math.Max( 3, 7 ) --> 7", all, P( "a", N, "A number" ), P( "b", N, "Another" ) ) );
		entries.Add( Function( "Math.Clamp", Intrinsic.Clamp, N, math, "x held between min and max (min when min is above max).", "local c = Math.Clamp( 12, 0, 10 ) --> 10", all,
			P( "x", N, "The number" ), P( "min", N, "The least it may be" ), P( "max", N, "The most it may be" ) ) );
		entries.Add( Function( "Math.Lerp", Intrinsic.Lerp, N, math, "Part way from a to b: t = 0 is a, 1 is b (t held to 0..1, like s&box).", "local l = Math.Lerp( 0, 10, 0.25 ) --> 2.5", all,
			P( "a", N, "Where it starts" ), P( "b", N, "Where it ends" ), P( "t", N, "How far along", "0 to 1" ) ) );
		entries.Add( Function( "Math.Sin", Intrinsic.Sin, N, math, "The sine of an angle in radians.", "local s = Math.Sin( Math.PI / 2 ) --> 1", all, P( "radians", N, "The angle" ) ) );
		entries.Add( Function( "Math.Cos", Intrinsic.Cos, N, math, "The cosine of an angle in radians.", "local c = Math.Cos( 0 ) --> 1", all, P( "radians", N, "The angle" ) ) );
		entries.Add( Function( "Math.Tan", Intrinsic.Tan, N, math, "The tangent of an angle in radians.", "local t = Math.Tan( Math.PI / 4 ) --> 1", all, P( "radians", N, "The angle" ) ) );
		entries.Add( Function( "Math.Atan2", Intrinsic.Atan2, N, math, "The angle in radians from the X axis to the point (x, y).", "local a = Math.Atan2( 1, 1 ) --> 0.7854", all,
			P( "y", N, "Up" ), P( "x", N, "Across" ) ) );
		entries.Add( Function( "Math.Sqrt", Intrinsic.Sqrt, N, math, "The square root of x; 0 for a negative x.", "local s = Math.Sqrt( 16 ) --> 4", all, P( "x", N, "A number", "0 or more" ) ) );
		entries.Add( Function( "Math.Pow", Intrinsic.Pow, N, math, "x to the power y; 0 where that isn't a number.", "local p = Math.Pow( 2, 10 ) --> 1024", all, P( "x", N, "The base" ), P( "y", N, "The power" ) ) );
		entries.Add( Function( "Math.Exp", Intrinsic.Exp, N, math, "e (2.71828...) to the power x: Math.Exp( -k * t ) dies away smoothly from 1.", "local e = Math.Exp( 1 ) --> 2.7183\nlocal fading = Math.Exp( -3 ) --> 0.0498", all,
			P( "x", N, "The power" ) ) );
		entries.Add( Function( "Math.SmoothStep", Intrinsic.SmoothStep, N, math, "0 at or below from, 1 at or above to, easing smoothly between (like HLSL's smoothstep).", "local half = Math.SmoothStep( 0, 1, 0.5 ) --> 0.5\nlocal eased = Math.SmoothStep( 0, 1, 0.25 ) --> 0.1562", all,
			P( "from", N, "Where it starts rising" ), P( "to", N, "Where it reaches 1" ), P( "x", N, "The number" ) ) );
		entries.Add( Function( "Math.Noise", Intrinsic.Noise, N, math, "Smooth noise, 0 to 1, the same on every client: nearby inputs give nearby values.", "local n = Math.Noise( 0.5, 2 ) --> 0.2582", all,
			P( "x", N, "Where along it" ), P( "y", N, "A second direction, for 2D noise", null, new[] { 0.0 } ) ) );
		entries.Add( Function( "Math.Random", Intrinsic.Random, N, math, "A number from 0 up to 1 picked by seed: the same seed gives the same number on every client.", "local r = Math.Random( 7 ) --> 0.9843", all,
			P( "seed", N, "Any number; the same one always gives the same result" ) ) );
	}

	private static void AddColor( List<LibraryEntry> entries )
	{
		const string color = "Color";
		var c = ScriptType.Color;
		var all = ScriptPlace.All;
		entries.Add( Space( color, color, "Colours as red, green, blue and alpha from 0 to 1, like s&box's Color." ) );
		entries.Add( Function( "Color", Intrinsic.ColorNew, c, color, "A colour from red, green, blue and (optional) alpha, each 0 to 1.", "local orange = Color( 1, 0.5, 0 ) --> #ff8000", all,
			P( "r", N, "Red", "0 to 1" ), P( "g", N, "Green", "0 to 1" ), P( "b", N, "Blue", "0 to 1" ), P( "a", N, "Alpha (opacity)", "0 to 1", new[] { 1.0 } ) ) );
		entries.Add( Function( "Color.Parse", Intrinsic.ColorParse, c, color, "A colour from text like \"#ff3333\" (or \"#f33\", or \"#ff333380\" with alpha).", "local red = Color.Parse( \"#ff3333\" ) --> #ff3333", all,
			P( "hex", ScriptType.String, "The colour as # and hex digits" ) ) );
		entries.Add( Function( "Color.FromHsv", Intrinsic.ColorFromHsv, c, color, "A colour from hue (degrees round the wheel), saturation and value.", "local green = Color.FromHsv( 120, 1, 1 ) --> #00ff00", all,
			P( "hue", N, "Round the wheel: 0 red, 120 green, 240 blue", "0 to 360" ), P( "saturation", N, "0 grey to 1 full", "0 to 1" ),
			P( "value", N, "0 black to 1 bright", "0 to 1" ), P( "a", N, "Alpha", "0 to 1", new[] { 1.0 } ) ) );
		entries.Add( Function( "Color.Lerp", Intrinsic.ColorLerp, c, color, "Part way from colour a to b (t held to 0..1).", "local grey = Color.Lerp( Color.Black, Color.White, 0.5 ) --> #808080", all,
			P( "a", c, "Where it starts" ), P( "b", c, "Where it ends" ), P( "t", N, "How far along", "0 to 1" ) ) );

		foreach ( var (name, r, g, b, a) in NamedColors )
			entries.Add( Constant( "Color." + name, c, color, $"{name}.", $"local named = Color.{name} --> {ColorText( r, g, b, a )}", r, g, b, a ) );

		var components = new[] { ("R", "Red"), ("G", "Green"), ("B", "Blue"), ("A", "Alpha (opacity)") };
		for ( var i = 0; i < components.Length; i++ )
		{
			entries.Add( new LibraryEntry
			{
				Name = "Color." + components[ i ].Item1, Kind = EntryKind.Property, Type = N, Category = color, Access = PropertyAccess.Component, Slot = i,
				Doc = $"Its {components[ i ].Item2.ToLowerInvariant()}, 0 to 1.",
				Example = $"local orange = Color( 1, 0.5, 0 )\nlocal part = orange.{components[ i ].Item1} --> {(i switch { 0 => "1", 1 => "0.5", 2 => "0", _ => "1" })}",
			} );
		}
	}

	/// <summary>The colours named on <c>Color</c>, as r, g, b, a.</summary>
	[Sandbox.SkipHotload]
	internal static readonly (string Name, double R, double G, double B, double A)[] NamedColors =
	{
		("White", 1, 1, 1, 1), ("Black", 0, 0, 0, 1), ("Red", 1, 0, 0, 1), ("Green", 0, 1, 0, 1), ("Blue", 0, 0, 1, 1),
		("Yellow", 1, 1, 0, 1), ("Cyan", 0, 1, 1, 1), ("Magenta", 1, 0, 1, 1), ("Orange", 1, 0.5, 0, 1), ("Gray", 0.5, 0.5, 0.5, 1),
		("Transparent", 0, 0, 0, 0),
	};

	private static string ColorText( double r, double g, double b, double a ) => ScriptValue.ColorText( r, g, b, a );

	private static void AddVector( List<LibraryEntry> entries )
	{
		const string vector = "Vector3";
		var v = ScriptType.Vector3;
		var all = ScriptPlace.All;
		entries.Add( Space( vector, vector, "Positions and directions in three numbers, like s&box's Vector3: X forwards, Y left, Z up." ) );
		entries.Add( Function( "Vector3", Intrinsic.VectorNew, v, vector, "A vector from x, y and z.", "local v = Vector3( 1, 2, 3 ) --> Vector3( 1, 2, 3 )", all,
			P( "x", N, "Forwards" ), P( "y", N, "Left" ), P( "z", N, "Up" ) ) );
		entries.Add( Function( "Vector3.Lerp", Intrinsic.VectorLerp, v, vector, "Part way from a to b (t held to 0..1).", "local mid = Vector3.Lerp( Vector3.Zero, Vector3( 2, 4, 6 ), 0.5 ) --> Vector3( 1, 2, 3 )", all,
			P( "a", v, "Where it starts" ), P( "b", v, "Where it ends" ), P( "t", N, "How far along", "0 to 1" ) ) );
		entries.Add( Function( "Vector3.Dot", Intrinsic.VectorDot, N, vector, "The dot product: how much a and b point the same way, times their lengths.", "local d = Vector3.Dot( Vector3( 1, 2, 3 ), Vector3( 4, 5, 6 ) ) --> 32", all,
			P( "a", v, "A vector" ), P( "b", v, "Another" ) ) );
		entries.Add( Function( "Vector3.Cross", Intrinsic.VectorCross, v, vector, "The cross product: a vector square to both a and b.", "local up = Vector3.Cross( Vector3.Forward, Vector3.Left ) --> Vector3( 0, 0, 1 )", all,
			P( "a", v, "A vector" ), P( "b", v, "Another" ) ) );
		entries.Add( Function( "Vector3.Distance", Intrinsic.VectorDistance, N, vector, "How far apart a and b are.", "local d = Vector3.Distance( Vector3.Zero, Vector3( 3, 4, 0 ) ) --> 5", all,
			P( "a", v, "A point" ), P( "b", v, "Another" ) ) );

		foreach ( var (name, x, y, z) in NamedVectors )
			entries.Add( Constant( "Vector3." + name, v, vector, $"{name}: ({x}, {y}, {z}).", $"local named = Vector3.{name} --> Vector3( {x}, {y}, {z} )", x, y, z ) );

		var axes = new[] { "X", "Y", "Z" };
		for ( var i = 0; i < axes.Length; i++ )
		{
			entries.Add( new LibraryEntry
			{
				Name = "Vector3." + axes[ i ], Kind = EntryKind.Property, Type = N, Category = vector, Access = PropertyAccess.Component, Slot = i,
				Doc = $"Its {axes[ i ]}.", Example = $"local v = Vector3( 1, 2, 3 )\nlocal part = v.{axes[ i ]} --> {i + 1}",
			} );
		}

		entries.Add( new LibraryEntry
		{
			Name = "Vector3.Length", Kind = EntryKind.Property, Type = N, Category = vector, Access = PropertyAccess.Intrinsic, Slot = (int)Intrinsic.VectorLength,
			Doc = "How long it is.", Example = "local v = Vector3( 3, 4, 0 )\nlocal length = v.Length --> 5",
		} );
		entries.Add( new LibraryEntry
		{
			Name = "Vector3.Normal", Kind = EntryKind.Property, Type = v, Category = vector, Access = PropertyAccess.Intrinsic, Slot = (int)Intrinsic.VectorNormal,
			Doc = "The same direction, 1 long (zero stays zero).", Example = "local v = Vector3( 0, 0, 5 )\nlocal n = v.Normal --> Vector3( 0, 0, 1 )",
		} );
		entries.Add( Space( "List", "Lists", "Lists made to a size, to fill in and change item by item (list[ i ] = value)." ) );
		entries.Add( Function( "List.Create", Intrinsic.ListCreate, ScriptType.ListOf( ScriptType.Any ), "Lists",
			"A list of count copies of value (at most 256), made while the script is built: state to change item by item as it runs, like a path to follow.",
			"local path = List.Create( 3, Vector3.Zero )\npath[ 1 ] = Vector3.Up\nlocal second = path[ 1 ] --> Vector3( 0, 0, 1 )\nlocal n = path.Count --> 3", ScriptPlace.Build,
			P( "count", N, "How many items", "0 to 256" ), P( "value", ScriptType.Any, "What each item starts as; the list holds its type" ) ) );
		entries.Add( new LibraryEntry
		{
			Name = "List.Count", Kind = EntryKind.Property, Type = N, Category = "Lists", Access = PropertyAccess.ListCount,
			Doc = "How many items the list has; they are numbered from 0 to Count - 1.", Example = "local odd = { 1, 3, 5 }\nlocal n = odd.Count --> 3\nlocal first = odd[ 0 ] --> 1",
		} );
	}

	/// <summary>The directions named on <c>Vector3</c>.</summary>
	[Sandbox.SkipHotload]
	internal static readonly (string Name, double X, double Y, double Z)[] NamedVectors =
	{
		("Zero", 0, 0, 0), ("One", 1, 1, 1), ("Forward", 1, 0, 0), ("Backward", -1, 0, 0), ("Left", 0, 1, 0), ("Right", 0, -1, 0),
		("Up", 0, 0, 1), ("Down", 0, 0, -1),
	};
}
