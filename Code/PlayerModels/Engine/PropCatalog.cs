using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// The models a weapon can carry as props (see <see cref="Core.WeaponProp"/>): the stock props every install has, never
/// clothing or anything from the Workshop, so a prop downloads nothing and no look can bring in a model of its own.
/// Checked on every client before a prop goes on, whatever a look says.
/// </summary>
internal static class PropCatalog
{
	// Where the stock props are, and the names of pieces that aren't props on their own (what breaks off a prop)
	[SkipHotload]
	private static readonly string[] Folders = { "models/citizen_props/", "models/sbox_props/", "models/props/" };
	[SkipHotload]
	private static readonly string[] Pieces = { "gib", "_lod", "piece" };
	private const string CompiledExtension = "_c";

	private static List<string> _all;

	// Whether each path asked about is a stock prop: asking the file system takes ~1.7 ms, and every prop on every weapon
	// of every player joining asks. Forgotten past this many (a look can name any path)
	private const int MostKnown = 4096;
	[SkipHotload]
	private static readonly Dictionary<string, bool> Known = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>Every stock prop, by path, in name order. Looked for once.</summary>
	internal static IReadOnlyList<string> All => _all ??= Find();

	/// <summary>Whether <paramref name="path"/> is a stock prop, as a look names one.</summary>
	internal static bool IsStock( string path )
	{
		if ( string.IsNullOrEmpty( path ) || !Folders.Any( x => path.StartsWith( x, StringComparison.OrdinalIgnoreCase ) ) )
			return false;

		if ( Known.TryGetValue( path, out var known ) )
			return known;

		var stock = !IsPiece( path ) && FileSystem.Mounted.FileExists( path + CompiledExtension );
		if ( Known.Count >= MostKnown )
			Known.Clear();

		Known[ path ] = stock;
		return stock;
	}

	/// <summary>What a prop is called: its file's name, "beer_bottle" as "Beer bottle".</summary>
	internal static string NameOf( string path ) => Core.WeaponDesign.NameOf( path );

	private static bool IsPiece( string path ) => Pieces.Any( x => path.Contains( x, StringComparison.OrdinalIgnoreCase ) );

	private static List<string> Find()
	{
		var found = new List<string>();
		foreach ( var folder in Folders )
		{
			foreach ( var file in FileSystem.Mounted.FindFile( folder.TrimEnd( '/' ), "*.vmdl" + CompiledExtension, true ) )
			{
				var path = folder + file[ ..^CompiledExtension.Length ];
				if ( !IsPiece( path ) )
					found.Add( path );
			}
		}

		return found.Distinct().OrderBy( NameOf ).ToList();
	}
}
