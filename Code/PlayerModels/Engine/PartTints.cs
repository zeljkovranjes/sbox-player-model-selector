using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Colours single parts of a model: each coloured material is swapped for a copy of it with its colour tint set, the
/// way Sandbox recolours its TV screen. One per renderer; the copies live as long as it does.
/// </summary>
internal sealed class PartTints
{
	// The shader attribute standard materials multiply their colour by
	private const string TintAttribute = "g_vColorTint";

	// The copy standing in for each coloured material, and the materials that can't be copied, by material index
	private readonly Dictionary<int, Material> _copies = new();
	private readonly HashSet<int> _uncopyable = new();

	// The skin the copies were made from
	private string _skin;

	/// <summary>The name a material goes by as a part: its file name, "jacket" for <c>models/x/jacket.vmat</c>.</summary>
	internal static string PartName( Material material )
	{
		var name = material.Name ?? "";
		var start = name.LastIndexOf( '/' ) + 1;
		var end = name.IndexOf( '.', start );

		return end < 0 ? name[ start.. ] : name[ start..end ];
	}

	/// <summary>The parts of <paramref name="model"/> that can be coloured, each name once, in the model's order.</summary>
	internal static List<string> PartsOf( Model model )
	{
		var parts = new List<string>();
		foreach ( var material in model.Materials )
		{
			if ( material is null )
				continue;

			var name = PartName( material );
			if ( name != "" && !parts.Contains( name ) )
				parts.Add( name );
		}

		return parts;
	}

	/// <summary>
	/// Colours the parts <paramref name="colours"/> lists (part name to <c>#rrggbb</c>) and puts every other one back.
	/// Parts keep the names of the model's default materials whatever <paramref name="skin"/> (a material group, null
	/// for the default) is worn; the colour goes on that skin's material.
	/// </summary>
	internal void Apply( ModelRenderer renderer, IReadOnlyDictionary<string, string> colours, string skin )
	{
		if ( skin != _skin )
		{
			// Copies of the old skin's materials would put that skin back on those parts
			foreach ( var index in _copies.Keys )
				renderer.Materials.SetOverride( index, null );

			_copies.Clear();
			_uncopyable.Clear();
			_skin = skin;
		}

		var materials = renderer.Model.Materials;
		var worn = skin is null ? materials.ToArray() : renderer.Model.GetMaterials( skin ).ToArray();

		for ( var i = 0; i < materials.Length; i++ )
		{
			if ( materials[ i ] is not { } material )
				continue;

			if ( colours.TryGetValue( PartName( material ), out var hex ) && Color.Parse( hex ) is { } colour )
			{
				if ( !_copies.TryGetValue( i, out var copy ) )
				{
					var source = i < worn.Length && worn[ i ] is not null ? worn[ i ] : material;

					// A mounted game's materials are made as they load and can't be copied: that part keeps its colour
					if ( _uncopyable.Contains( i ) || source.CreateCopy() is not { } made )
					{
						_uncopyable.Add( i );
						continue;
					}

					copy = made;
					_copies[ i ] = copy;
					renderer.Materials.SetOverride( i, copy );
				}

				copy.Set( TintAttribute, colour );
			}
			else if ( _copies.Remove( i ) )
			{
				renderer.Materials.SetOverride( i, null );
			}
		}
	}
}
