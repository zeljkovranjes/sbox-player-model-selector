using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Carries a player's avatar outfit over to a picked model. Only the stock characters wear it: avatar clothing is made
/// for the citizens that ship with s&amp;box, and any other model keeps its own look. The outfit is read the way the
/// Skateboard's rider reads it, from the dresser that dresses the player's citizen.
/// </summary>
internal static class ModelOutfit
{
	private const string ClassicCitizen = "models/citizen/citizen.vmdl";
	private const string HumanFemale = "models/citizen_human/citizen_human_female.vmdl";

	// The citizens that ship with s&box, the bodies avatar clothing is made for
	private static readonly HashSet<string> StockCharacters = new( StringComparer.OrdinalIgnoreCase )
	{
		ClassicCitizen,
		HumanFemale,
		"models/citizen_human/citizen_human_male.vmdl",
		"models/citizen_human/human.vmdl",
	};

	// An avatar's skin item is what picks the human body (and the female cut of every garment), so a stock character is
	// dressed with the skin of its own sex: the same skin tone where s&box has a twin of it.
	private static readonly Dictionary<string, string> FemaleTwin = new( StringComparer.OrdinalIgnoreCase )
	{
		[ "models/citizen_clothes/body/human01.clothing" ] = "models/citizen_clothes/body/human02.clothing",
	};

	private static readonly Dictionary<string, string> MaleTwin = new( StringComparer.OrdinalIgnoreCase )
	{
		[ "models/citizen_clothes/body/human02.clothing" ] = "models/citizen_clothes/body/human01.clothing",
		[ "models/citizen_clothes/body/human04.clothing" ] = "models/citizen_clothes/body/human03.clothing",
	};

	private const string DefaultFemaleSkin = "models/citizen_clothes/body/human04.clothing";
	private const string DefaultMaleSkin = "models/citizen_clothes/body/human01.clothing";

	/// <summary>Whether <paramref name="model"/> is a stock character, which wears the avatar outfit.</summary>
	internal static bool IsStock( string model )
	{
		return model is not null && StockCharacters.Contains( model );
	}

	/// <summary>
	/// What <paramref name="model"/> should be dressed in for someone wearing <paramref name="avatar"/>: the avatar fitted
	/// to it for a stock character, nothing for any other model (it keeps its own look).
	/// </summary>
	internal static ClothingContainer For( string model, ClothingContainer avatar )
	{
		return IsStock( model ) && avatar is not null ? ForModel( avatar, model ) : null;
	}

	/// <summary>
	/// <paramref name="outfit"/> made to fit the stock character <paramref name="model"/>. The dresser takes the body from
	/// the outfit's skin item, so a human gets the skin of its own sex, and the classic citizen keeps its own body.
	/// </summary>
	internal static ClothingContainer ForModel( ClothingContainer outfit, string model )
	{
		var fitted = new ClothingContainer();
		fitted.Deserialize( outfit.Serialize() );

		if ( string.Equals( model, ClassicCitizen, StringComparison.OrdinalIgnoreCase ) )
		{
			fitted.PrefersHuman = false;
			return fitted;
		}

		fitted.PrefersHuman = true;

		var female = string.Equals( model, HumanFemale, StringComparison.OrdinalIgnoreCase );
		var skin = fitted.Clothing.FirstOrDefault( x => x.Clothing?.Category == Clothing.ClothingCategory.Skin );
		var isFemale = skin?.Clothing.HumanSkinTags?.Has( "female" ) ?? false;

		if ( skin is not null && isFemale == female )
			return fitted;

		var twins = female ? FemaleTwin : MaleTwin;
		var path = skin is not null && twins.TryGetValue( skin.Clothing.ResourcePath, out var twin )
			? twin
			: female ? DefaultFemaleSkin : DefaultMaleSkin;

		if ( ResourceLibrary.Get<Clothing>( path ) is not { } replacement )
			return fitted;

		if ( skin is not null )
			fitted.Clothing.Remove( skin );

		fitted.Add( replacement );
		return fitted;
	}

	/// <summary>
	/// The outfit <paramref name="citizen"/> is dressed in: from its dresser when it has one, otherwise (a ragdoll, whose
	/// clothes are copies) the avatar of <paramref name="owner"/>. Null when there is neither.
	/// </summary>
	internal static ClothingContainer OutfitOf( SkinnedModelRenderer citizen, Connection owner )
	{
		var dresser = citizen.GameObject.Root.Components.GetAll<Dresser>( FindMode.EverythingInSelfAndDescendants )
			.FirstOrDefault( x => x.BodyTarget == citizen );

		if ( dresser is null )
			return owner is null ? null : ClothingContainer.CreateFromConnection( owner, true );

		return dresser.Source switch
		{
			Dresser.ClothingSource.OwnerConnection => OwnersOutfit( dresser ),

			// The local user is the player only on their own client; everywhere else their citizen wears their connection's avatar
			Dresser.ClothingSource.LocalUser => dresser.IsProxy ? OwnersOutfit( dresser ) : ClothingContainer.CreateFromLocalUser(),

			_ => ManualOutfit( dresser )
		};
	}

	/// <summary>Dresses <paramref name="body"/> in <paramref name="outfit"/>, fetching any clothing not downloaded yet.</summary>
	internal static async Task DressAsync( SkinnedModelRenderer body, ClothingContainer outfit, CancellationToken token )
	{
		var dresser = Dresser.GetOrCreate( body );
		dresser.Source = Dresser.ClothingSource.Manual;

		// The citizen it's posed from already has the avatar's height
		dresser.ApplyHeightScale = false;

		outfit.Normalize();
		dresser.UpdateAppearance( outfit );
		await dresser.ApplyAsync( outfit, token );
	}

	private static ClothingContainer OwnersOutfit( Dresser dresser )
	{
		return dresser.Network.Owner is { } owner
			? ClothingContainer.CreateFromConnection( owner, dresser.RemoveUnownedItems )
			: new ClothingContainer();
	}

	private static ClothingContainer ManualOutfit( Dresser dresser )
	{
		var outfit = new ClothingContainer();
		outfit.AddRange( dresser.Clothing );
		outfit.Height = dresser.ManualHeight;
		outfit.Age = dresser.ManualAge;
		outfit.Tint = dresser.ManualTint;
		outfit.NeckSize = dresser.NeckSize;
		outfit.WaistSize = dresser.WaistSize;
		outfit.ChestSize = dresser.ChestSize;
		outfit.HeadShape = dresser.HeadShape;
		outfit.NoseSize = dresser.NoseSize;
		outfit.ChinSize = dresser.ChinSize;
		outfit.EyeColor = dresser.EyeColor;
		outfit.EyeAlign = dresser.EyeAlign;
		outfit.Normalize();
		return outfit;
	}
}
