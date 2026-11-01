using System.Collections.Generic;
using System.Linq;
using PlayerModels.Core;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Colours single parts of a model and gives them finishes: each coloured material is swapped for a copy of it with its
/// colour tint set, the way Sandbox recolours its TV screen, and each finished one for a material of that finish. One
/// per renderer; the copies live as long as it does.
/// </summary>
internal sealed class PartTints
{
	// The shader attribute standard materials multiply their colour by
	private const string TintAttribute = "g_vColorTint";

	// The copy standing in for each coloured material, the finish it was made for ("" for its own material's copy), and
	// the materials that can't be copied, by material index
	private readonly Dictionary<int, Material> _copies = new();
	private readonly Dictionary<int, string> _finishes = new();
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
	/// The parts drawn by a body group's choice with mask <paramref name="choice"/>, each once. Parts the model also
	/// draws whatever that choice is aren't told apart from it: a colour goes on all of a part.
	/// </summary>
	internal static List<string> PartsOfChoice( Model model, ulong choice )
	{
		var parts = new List<string>();
		foreach ( var mesh in model.MeshInfo.Meshes )
		{
			if ( (mesh.MeshGroupMask & choice) == 0 )
				continue;

			foreach ( var drawCall in mesh.DrawCalls )
			{
				var name = drawCall.Material is null ? "" : PartName( drawCall.Material );
				if ( name != "" && !parts.Contains( name ) )
					parts.Add( name );
			}
		}

		return parts;
	}

	/// <summary>
	/// Colours the parts <paramref name="colours"/> lists (part name to <c>#rrggbb</c>), gives every part
	/// <paramref name="finish"/> or its own from <paramref name="partFinishes"/>, and puts every other one back. Parts keep
	/// the names of the model's default materials whatever <paramref name="skin"/> (a material group, null for the default)
	/// is worn; the colour goes on that skin's material.
	/// </summary>
	internal void Apply( ModelRenderer renderer, IReadOnlyDictionary<string, string> colours, string finish,
		IReadOnlyDictionary<string, string> partFinishes, string skin )
	{
		if ( skin != _skin )
		{
			// Copies of the old skin's materials would put that skin back on those parts
			foreach ( var index in _copies.Keys )
				renderer.Materials.SetOverride( index, null );

			_copies.Clear();
			_finishes.Clear();
			_uncopyable.Clear();
			_skin = skin;
		}

		var materials = renderer.Model.Materials;
		var worn = skin is null ? materials.ToArray() : renderer.Model.GetMaterials( skin ).ToArray();
		var hidden = Hiding.HiddenParts( PartsOf( renderer.Model ), partFinishes );

		for ( var i = 0; i < materials.Length; i++ )
		{
			if ( materials[ i ] is not { } material )
				continue;

			var part = PartName( material );
			var partFinish = partFinishes.TryGetValue( part, out var own ) ? own : finish ?? "";

			// Refused here on every client, so no player can make themselves invisible to the others
			if ( partFinish == MaterialFinish.Hidden && !hidden.Contains( part ) )
				partFinish = finish ?? "";

			Color? colour = colours.TryGetValue( part, out var hex ) ? Color.Parse( hex ) : null;

			// A part given another finish starts again from that finish, not from the copy it has
			if ( (partFinish == "" && colour is null) || (_finishes.TryGetValue( i, out var had ) && had != partFinish) )
				TakeOff( renderer, i );

			if ( partFinish == "" && colour is null )
				continue;

			if ( !_copies.TryGetValue( i, out var copy ) )
			{
				copy = Dress( i < worn.Length && worn[ i ] is not null ? worn[ i ] : material, i, partFinish );
				if ( copy is null )
					continue;

				_copies[ i ] = copy;
				_finishes[ i ] = partFinish;
				renderer.Materials.SetOverride( i, copy );
			}

			copy.Set( TintAttribute, colour ?? FinishMaterials.ColourOf( partFinish ) );
		}
	}

	// The material a part wears for a finish, or the copy of its own to colour; null when there can be none
	private Material Dress( Material source, int index, string finish )
	{
		if ( finish != "" )
			return FinishMaterials.Make( finish, source );

		// A mounted game's materials are made as they load and can't be copied: that part keeps its colour
		if ( _uncopyable.Contains( index ) || source.CreateCopy() is not { } made )
		{
			_uncopyable.Add( index );
			return null;
		}

		return made;
	}

	private void TakeOff( ModelRenderer renderer, int index )
	{
		if ( _copies.Remove( index ) )
			renderer.Materials.SetOverride( index, null );

		_finishes.Remove( index );
	}
}
