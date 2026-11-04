using System;
using System.Globalization;

namespace PlayerModels.Core;

/// <summary>
/// Arithmetic typed into a number box: numbers (a point or a comma for the decimals), + - * / %, signs and parentheses,
/// worked out the usual way, so "2*3+1" is 7 and "(4-1)/2" is 1.5. Anything else, or a result that isn't a number (a
/// division by zero), is no answer, and the box keeps the number it had.
/// </summary>
public static class MathText
{
	/// <summary>The longest text worked out: a sum, not a program.</summary>
	public const int MaxLength = 64;

	// How deep parentheses and signs nest
	private const int MaxDepth = 16;

	/// <summary>
	/// Works out <paramref name="text"/> into <paramref name="value"/>. False when it isn't arithmetic, is longer than
	/// <see cref="MaxLength"/>, or comes to something that isn't a finite number.
	/// </summary>
	public static bool TryEvaluate( string text, out float value )
	{
		value = 0f;
		if ( string.IsNullOrWhiteSpace( text ) || text.Length > MaxLength )
			return false;

		var reader = new Reader( text );
		if ( !reader.TrySum( 0, out var result ) || !reader.AtEnd || !double.IsFinite( result ) )
			return false;

		value = (float)result;
		return float.IsFinite( value );
	}

	private sealed class Reader
	{
		private readonly string _text;
		private int _at;

		internal Reader( string text ) => _text = text;

		internal bool AtEnd
		{
			get
			{
				SkipSpaces();
				return _at >= _text.Length;
			}
		}

		// sum: product, then any number of + or - products
		internal bool TrySum( int depth, out double value )
		{
			if ( !TryProduct( depth, out value ) )
				return false;

			while ( Next() is '+' or '-' )
			{
				var plus = _text[ _at++ ] == '+';
				if ( !TryProduct( depth, out var right ) )
					return false;

				value = plus ? value + right : value - right;
			}

			return true;
		}

		// product: signed, then any number of * / or % signed
		private bool TryProduct( int depth, out double value )
		{
			if ( !TrySigned( depth, out value ) )
				return false;

			while ( Next() is '*' or '/' or '%' )
			{
				var operation = _text[ _at++ ];
				if ( !TrySigned( depth, out var right ) )
					return false;

				value = operation switch
				{
					'*' => value * right,
					'/' => value / right,
					_ => value % right,
				};
			}

			return true;
		}

		// signed: any number of signs before a number or a sum in parentheses
		private bool TrySigned( int depth, out double value )
		{
			value = 0;
			if ( depth > MaxDepth )
				return false;

			var next = Next();
			if ( next is '+' or '-' )
			{
				_at++;
				if ( !TrySigned( depth + 1, out value ) )
					return false;

				value = next == '-' ? -value : value;
				return true;
			}

			if ( next == '(' )
			{
				_at++;
				if ( !TrySum( depth + 1, out value ) || Next() != ')' )
					return false;

				_at++;
				return true;
			}

			return TryNumber( out value );
		}

		private bool TryNumber( out double value )
		{
			value = 0;
			var start = _at;
			while ( _at < _text.Length && (char.IsAsciiDigit( _text[ _at ] ) || _text[ _at ] is '.' or ',') )
				_at++;

			if ( _at == start )
				return false;

			var number = _text[ start.._at ].Replace( ',', '.' );
			return double.TryParse( number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value );
		}

		// The next character that isn't a space, without taking it; none at the end
		private char Next()
		{
			SkipSpaces();
			return _at < _text.Length ? _text[ _at ] : '\0';
		}

		private void SkipSpaces()
		{
			while ( _at < _text.Length && char.IsWhiteSpace( _text[ _at ] ) )
				_at++;
		}
	}
}
