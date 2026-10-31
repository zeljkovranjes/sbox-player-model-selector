using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Stand-ins for materials whose shader didn't load. A mounted game's materials are made on shaders its mount brings,
/// and one compiled for an older s&amp;box, or not found, draws the error material although its textures loaded. The
/// stand-in is a copy of the engine's default material carrying those textures, so the model shows its colours. A
/// failed shader is told apart by what the engine says about it, so this works for any mount on any platform.
/// </summary>
internal static class StandInMaterials
{
	// The engine's default material, on its standard shader; a copy keeps every texture slot it doesn't get filled
	private const string BaseMaterial = "materials/default.vmat";

	// A shader that can't exist: the engine describes it the way it describes every shader that failed to load
	private const string MissingShader = "shaders/player_model_selector_missing.shader";

	// The textures a stand-in takes, each from the first of the names mounts give it that the material has
	private static readonly (string Slot, string[] Names)[] Textures =
	{
		("g_tColor", new[] { "g_tColor", "g_tColor1", "g_tAlbedo", "g_tDiffuse", "g_tBaseTexture" }),
		("g_tNormal", new[] { "g_tNormal", "g_tNormalMap", "g_tBumpMap" }),
	};

	// Features deciding which pixels draw at all: without them hair cards and leaves would show as solid sheets
	private static readonly string[] Features = { "F_ALPHA_TEST", "F_TRANSLUCENT" };

	private static readonly Dictionary<string, bool> FailedShaders = new();
	private static readonly Dictionary<Material, Material> Made = new();
	private static readonly HashSet<Material> StandIns = new();
	private static string _failedSchema;

	/// <summary>
	/// Puts stand-ins on <paramref name="renderer"/> for the materials of its model, in its skin, whose shader didn't
	/// load, and takes off the ones a new model or skin no longer needs. Overrides put on by others are left alone.
	/// </summary>
	internal static void Apply( ModelRenderer renderer )
	{
		if ( renderer?.Model is not { IsValid: true } model )
			return;

		var worn = string.IsNullOrEmpty( renderer.MaterialGroup ) ? model.Materials.ToArray() : model.GetMaterials( renderer.MaterialGroup ).ToArray();
		for ( var i = 0; i < worn.Length; i++ )
		{
			var current = renderer.Materials.GetOverride( i );
			if ( current is not null && !StandIns.Contains( current ) )
				continue;

			var standIn = StandInFor( worn[ i ] );
			if ( current != standIn )
				renderer.Materials.SetOverride( i, standIn );
		}
	}

	/// <summary>Whether any of <paramref name="model"/>'s own materials needs a stand-in.</summary>
	internal static bool Needs( Model model )
	{
		return model is { IsValid: true } && model.Materials.Any( x => x is not null && HasFailedShader( x ) );
	}

	private static Material StandInFor( Material material )
	{
		if ( material is null || !HasFailedShader( material ) )
			return null;

		if ( Made.TryGetValue( material, out var made ) )
			return made;

		made = Make( material );
		Made[ material ] = made;
		if ( made is not null )
			StandIns.Add( made );

		return made;
	}

	private static Material Make( Material material )
	{
		var made = Material.Load( BaseMaterial )?.CreateCopy();
		if ( made is null )
		{
			Log.Warning( $"Player Model Selector: couldn't copy {BaseMaterial} to stand in for {material.Name}." );
			return null;
		}

		foreach ( var (slot, names) in Textures )
		{
			var texture = names.Select( material.GetTexture ).FirstOrDefault( x => x is { IsValid: true, IsError: false } );
			if ( texture is not null )
				made.Set( slot, texture );
		}

		foreach ( var feature in Features )
		{
			var value = material.GetFeature( feature );
			if ( value > 0 )
				made.SetFeature( feature, value );
		}

		return made;
	}

	private static bool HasFailedShader( Material material )
	{
		var name = material.ShaderName;
		if ( string.IsNullOrEmpty( name ) )
			return false;

		if ( FailedShaders.TryGetValue( name, out var failed ) )
			return failed;

		_failedSchema ??= SchemaOf( Shader.Load( MissingShader ) );
		failed = SchemaOf( material.Shader ) == _failedSchema && (ShaderNamed( name ) is not { } named || SchemaOf( named ) == _failedSchema);
		FailedShaders[ name ] = failed;

		if ( failed )
			Log.Info( $"Player Model Selector: {name} didn't load, so its materials show their textures on the standard shader." );

		return failed;
	}

	// A material made with a short shader name ("goldsrc", as the GoldSrc and San Andreas mounts make theirs) draws with
	// shaders/goldsrc.shader, but hands back a shader described like one that failed: that file is the one to ask
	private static Shader ShaderNamed( string name )
	{
		return name.Contains( '/' ) || name.EndsWith( ".shader", StringComparison.OrdinalIgnoreCase ) ? null : Shader.Load( $"shaders/{name}.shader" );
	}

	// What a shader has to set: its variables and combos by name
	private static string SchemaOf( Shader shader )
	{
		var schema = shader?.Schema;
		var variables = schema?.Variables?.Select( x => x.Name ) ?? Enumerable.Empty<string>();
		var combos = schema?.Combos?.Select( x => x.Name ) ?? Enumerable.Empty<string>();

		return $"{string.Join( ",", variables )}|{string.Join( ",", combos )}";
	}
}
