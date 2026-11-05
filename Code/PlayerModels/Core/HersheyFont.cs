using System.Collections.Generic;

namespace PlayerModels.Core;

/// <summary>
/// Text as a shape: each letter drawn in single lines from the Hershey "Futura Light" font (A. V. Hershey, U.S. National
/// Bureau of Standards; public domain, see THIRD_PARTY_NOTICES.md), so a word written on one machine is the same lines on
/// every other, with nothing to download. Only the letters, digits and common punctuation are kept; anything else is a
/// space.
/// </summary>
public static class HersheyFont
{
	/// <summary>The longest text written as a shape.</summary>
	public const int MaxLength = 24;

	// Hershey's coordinates are letters counted from R, with y down; a font unit on the shape's square before it is fitted
	private const char Zero = 'R';
	private const float Unit = 0.05f;
	private const char FirstCharacter = ' ';

	/// <summary>
	/// <paramref name="text"/> (up to <see cref="MaxLength"/> characters) as open strokes, one line of text, fitted to the
	/// square; empty for text with nothing to draw.
	/// </summary>
	public static List<ShapeStroke> Strokes( string text )
	{
		var strokes = new List<ShapeStroke>();
		if ( string.IsNullOrEmpty( text ) )
			return strokes;

		var cursor = 0f;
		var length = System.Math.Min( text.Length, MaxLength );
		for ( var c = 0; c < length; c++ )
		{
			var glyph = GlyphOf( text[ c ] ) ?? Glyphs[ 0 ];
			var left = glyph[ 0 ] - Zero;
			var right = glyph[ 1 ] - Zero;
			var points = new List<float>();
			for ( var i = 2; i + 1 < glyph.Length; i += 2 )
			{
				// " R" lifts the pen: the line so far is a stroke of its own
				if ( glyph[ i ] == ' ' && glyph[ i + 1 ] == Zero )
				{
					Add( strokes, points );
					points = new List<float>();
					continue;
				}

				points.Add( (cursor + glyph[ i ] - Zero - left) * Unit );
				points.Add( -(glyph[ i + 1 ] - Zero) * Unit );
			}

			Add( strokes, points );
			cursor += right - left;
		}

		return ShapeStroke.Fitted( strokes );
	}

	/// <summary>Whether <paramref name="character"/> is one the font draws (a space draws nothing, but is one).</summary>
	public static bool Draws( char character ) => GlyphOf( character ) is not null;

	private static string GlyphOf( char character )
	{
		var index = character - FirstCharacter;
		return index >= 0 && index < Glyphs.Length ? Glyphs[ index ] : null;
	}

	private static void Add( List<ShapeStroke> strokes, List<float> points )
	{
		if ( points.Count >= 2 )
			strokes.Add( new ShapeStroke { Points = points.ToArray() } );
	}

	// Each printable character from the space on, as Hershey's data has it: its left and right edges, then its points,
	// " R" between lines; null for a character left out
	[Sandbox.SkipHotload]
	private static readonly string[] Glyphs =
	{
		@"JZ", @"MWRFRT RRYQZR[SZRY", @"JZNFNM RVFVM", @"H]SBLb RYBRb RLOZO RKUYU", null, null,
		@"E_\O\N[MZMYNXPVUTXRZP[L[JZIYHWHUISJRQNRMSKSIRGPFNGMIMKNNPQUXWZY[[[\Z\Y", @"MWRHQGRFSGSIRKQL", @"KYVBTDRGPKOPOTPYR]T`Vb", @"KYNBPDRGTKUPUTTYR]P`Nb", @"JZRLRX RMOWU RWOMU", @"E_RIR[ RIR[R",
		@"NVSWRXQWRVSWSYQ[", @"E_IR[R", @"NVRVQWRXSWRV", @"G][BIb", @"H\QFNGLJKOKRLWNZQ[S[VZXWYRYOXJVGSFQF", @"H\NJPISFS[",
		@"H\LKLJMHNGPFTFVGWHXJXLWNUQK[Y[", @"H\MFXFRNUNWOXPYSYUXXVZS[P[MZLYKW", @"H\UFKTZT RUFU[", @"H\WFMFLOMNPMSMVNXPYSYUXXVZS[P[MZLYKW", @"H\XIWGTFRFOGMJLOLTMXOZR[S[VZXXYUYTXQVOSNRNOOMQLT", @"H\YFO[ RKFYF",
		@"H\PFMGLILKMMONSOVPXRYTYWXYWZT[P[MZLYKWKTLRNPQOUNWMXKXIWGTFPF", @"H\XMWPURRSQSNRLPKMKLLINGQFRFUGWIXMXRWWUZR[P[MZLX", @"NVROQPRQSPRO RRVQWRXSWRV", @"NVROQPRQSPRO RSWRXQWRVSWSYQ[", null, @"E_IO[O RIU[U",
		null, @"I[LKLJMHNGPFTFVGWHXJXLWNVORQRT RRYQZR[SZRY", @"E`WNVLTKQKOLNMMPMSNUPVSVUUVS RQKOMNPNSOUPV RWKVSVUXVZV\T]Q]O\L[JYHWGTFQFNGLHJJILHOHRIUJWLYNZQ[T[WZYYZX RXKWSWUXV", @"I[RFJ[ RRFZ[ RMTWT", @"G\KFK[ RKFTFWGXHYJYLXNWOTP RKPTPWQXRYTYWXYWZT[K[", @"H]ZKYIWGUFQFOGMILKKNKSLVMXOZQ[U[WZYXZV",
		@"G\KFK[ RKFRFUGWIXKYNYSXVWXUZR[K[", @"H[LFL[ RLFYF RLPTP RL[Y[", @"HZLFL[ RLFYF RLPTP", @"H]ZKYIWGUFQFOGMILKKNKSLVMXOZQ[U[WZYXZVZS RUSZS", @"G]KFK[ RYFY[ RKPYP", @"NVRFR[",
		@"JZVFVVUYTZR[P[NZMYLVLT", @"G\KFK[ RYFKT RPOY[", @"HYLFL[ RL[X[", @"F^JFJ[ RJFR[ RZFR[ RZFZ[", @"G]KFK[ RKFY[ RYFY[", @"G]PFNGLIKKJNJSKVLXNZP[T[VZXXYVZSZNYKXIVGTFPF",
		@"G\KFK[ RKFTFWGXHYJYMXOWPTQKQ", @"G]PFNGLIKKJNJSKVLXNZP[T[VZXXYVZSZNYKXIVGTFPF RSWY]", @"G\KFK[ RKFTFWGXHYJYLXNWOTPKP RRPY[", @"H\YIWGTFPFMGKIKKLMMNOOUQWRXSYUYXWZT[P[MZKX", @"JZRFR[ RKFYF", @"G]KFKULXNZQ[S[VZXXYUYF",
		@"I[JFR[ RZFR[", @"F^HFM[ RRFM[ RRFW[ R\FW[", @"H\KFY[ RYFK[", @"I[JFRPR[ RZFRP", @"H\YFK[ RKFYF RK[Y[", null,
		null, null, null, null, null, @"I\XMX[ RXPVNTMQMONMPLSLUMXOZQ[T[VZXX",
		@"H[LFL[ RLPNNPMSMUNWPXSXUWXUZS[P[NZLX", @"I[XPVNTMQMONMPLSLUMXOZQ[T[VZXX", @"I\XFX[ RXPVNTMQMONMPLSLUMXOZQ[T[VZXX", @"I[LSXSXQWOVNTMQMONMPLSLUMXOZQ[T[VZXX", @"MYWFUFSGRJR[ ROMVM", @"I\XMX]W`VaTbQbOa RXPVNTMQMONMPLSLUMXOZQ[T[VZXX",
		@"I\MFM[ RMQPNRMUMWNXQX[", @"NVQFRGSFREQF RRMR[", @"MWRFSGTFSERF RSMS^RaPbNb", @"IZMFM[ RWMMW RQSX[", @"NVRFR[", @"CaGMG[ RGQJNLMOMQNRQR[ RRQUNWMZM\N]Q][",
		@"I\MMM[ RMQPNRMUMWNXQX[", @"I\QMONMPLSLUMXOZQ[T[VZXXYUYSXPVNTMQM", @"H[LMLb RLPNNPMSMUNWPXSXUWXUZS[P[NZLX", @"I\XMXb RXPVNTMQMONMPLSLUMXOZQ[T[VZXX", @"KXOMO[ ROSPPRNTMWM", @"J[XPWNTMQMNNMPNRPSUTWUXWXXWZT[Q[NZMX",
		@"MYRFRWSZU[W[ ROMVM", @"I\MMMWNZP[S[UZXW RXMX[", @"JZLMR[ RXMR[", @"G]JMN[ RRMN[ RRMV[ RZMV[", @"J[MMX[ RXMM[", @"JZLMR[ RXMR[P_NaLbKb",
		@"J[XMM[ RMMXM RM[X[", null, null, null, null,
	};
}
