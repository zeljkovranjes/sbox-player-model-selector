using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HumanoidRetargeter.Core.Mapping;

namespace PlayerModels.Core;

/// <summary>
/// What a player looks like: the model they picked and how they customised it (skin, body groups, morphs, colour). The host
/// shares it with everyone as one string; an empty model is the default citizen.
/// </summary>
public sealed class PlayerLook
{
	/// <summary>
	/// The longest look text read: well over the longest a look within the caps below can be, so a client can't make the
	/// host parse an unbounded one.
	/// </summary>
	public const int MaxTextLength = 64 * 1024;

	// Enough for any model; a client can't make the host store an unbounded look
	private const int MaxBodyGroups = 64;
	private const int MaxMorphs = 256;
	private const int MaxParts = 64;
	private const int MaxNameLength = 64;
	private const int MaxModelLength = 256;

	private static readonly Regex HexColour = new( "^#[0-9a-fA-F]{6}$" );

	/// <summary>The picked model (see <see cref="PlayerModelEntry.Model"/>); empty for the default citizen.</summary>
	public string Model { get; set; } = "";

	/// <summary>The chosen choice of each body group, by group name. A group not listed keeps the model's default.</summary>
	public Dictionary<string, int> BodyGroups { get; set; } = new();

	/// <summary>Morph weights by morph name, 0 to 1. A morph not listed is 0.</summary>
	public Dictionary<string, float> Morphs { get; set; } = new();

	/// <summary>The model's skin (a material group) by name; empty for its default.</summary>
	public string Skin { get; set; } = "";

	/// <summary>The colour the whole model is tinted, as <c>#rrggbb</c>; empty for none.</summary>
	public string Tint { get; set; } = "";

	/// <summary>Colours of single parts (the model's materials) by part name, as <c>#rrggbb</c>. A part not listed keeps its own.</summary>
	public Dictionary<string, string> Parts { get; set; } = new();

	/// <summary>The finish of the whole model (see <see cref="MaterialFinish"/>); empty for its own materials.</summary>
	public string Finish { get; set; } = "";

	/// <summary>Finishes of single parts (the model's materials) by part name, over <see cref="Finish"/>.</summary>
	public Dictionary<string, string> PartFinishes { get; set; } = new();

	/// <summary>The stock items worn on the model; see <see cref="Accessory"/>.</summary>
	public List<Accessory> Accessories { get; set; } = new();

	/// <summary>The body shape sliders (<see cref="BodyShape.Part"/> names), -1 to 1. A slider not listed is 0.</summary>
	public Dictionary<string, float> Shape { get; set; } = new();

	/// <summary>
	/// Bones picked by hand for body parts the automatic mapping got wrong: a humanoid role (a <c>BoneRole</c> name) to
	/// the model's bone, or empty for none. A role not listed keeps the automatic choice.
	/// </summary>
	public Dictionary<string, string> Bones { get; set; } = new();

	/// <summary>The effects worn (glowing eyes, a halo); see <see cref="Effect"/>.</summary>
	[JsonIgnore]
	public List<Effect> Effects { get; set; } = new();

	// Effects as they are saved: left out with none, so a look without any reads exactly as it did before there were
	// effects (a shared look already published is still recognised as the same)
	[JsonPropertyName( nameof( Effects ) ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<Effect> SavedEffects
	{
		get => Effects is { Count: > 0 } ? Effects : null;
		set => Effects = value ?? new();
	}

	/// <summary>
	/// The drawn shapes its effects' layers name (see <see cref="DrawnShape"/>), each once however many layers name it, at
	/// most <see cref="DrawnShape.MaxPerLook"/>.
	/// </summary>
	[JsonIgnore]
	public List<DrawnShape> DrawnShapes { get; set; } = new();

	// Drawn shapes as they are saved: left out with none, so a look without any reads exactly as it did before them
	[JsonPropertyName( nameof( DrawnShapes ) ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<DrawnShape> SavedDrawnShapes
	{
		get => DrawnShapes is { Count: > 0 } ? DrawnShapes : null;
		set => DrawnShapes = value ?? new();
	}

	/// <summary>
	/// The player's designs for weapons (see <see cref="WeaponDesign"/>), one a weapon, at most
	/// <see cref="WeaponDesign.MaxPerLook"/>. They belong to the player rather than the model: shared looks leave them out.
	/// </summary>
	[JsonIgnore]
	public List<WeaponDesign> Weapons { get; set; } = new();

	// Designs as they are saved: left out with none, so a look without any reads exactly as it did before them
	[JsonPropertyName( nameof( Weapons ) ), JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public List<WeaponDesign> SavedWeapons
	{
		get => Weapons is { Count: > 0 } ? Weapons : null;
		set => Weapons = value ?? new();
	}

	/// <summary>Whether this is the default citizen.</summary>
	[JsonIgnore]
	public bool IsDefault => string.IsNullOrEmpty( Model );

	/// <summary>Whether it customises its model at all: anything but the model and the bones picked by hand.</summary>
	[JsonIgnore]
	public bool IsCustomised => (BodyGroups?.Count ?? 0) > 0 || (Morphs?.Count ?? 0) > 0 || (Parts?.Count ?? 0) > 0
		|| (Shape?.Count ?? 0) > 0 || !string.IsNullOrEmpty( Tint ) || !string.IsNullOrEmpty( Skin )
		|| !string.IsNullOrEmpty( Finish ) || (PartFinishes?.Count ?? 0) > 0 || (Accessories?.Count ?? 0) > 0
		|| (Effects?.Count ?? 0) > 0;

	/// <summary>The look as one string, for the network and for saving.</summary>
	public string Serialize()
	{
		return JsonSerializer.Serialize( this );
	}

	/// <summary>
	/// A look read back from <see cref="Serialize"/>. A bare model name (what older picks were) is that model, not
	/// customised; anything unreadable is the default citizen.
	/// </summary>
	public static PlayerLook Parse( string text )
	{
		if ( string.IsNullOrWhiteSpace( text ) || text.Length > MaxTextLength )
			return new PlayerLook();

		if ( !text.TrimStart().StartsWith( '{' ) )
			return new PlayerLook { Model = text.Trim() }.Sanitized();

		try
		{
			return JsonSerializer.Deserialize<PlayerLook>( text )?.Sanitized() ?? new PlayerLook();
		}
		catch ( JsonException )
		{
			return new PlayerLook();
		}
	}

	/// <summary>
	/// A copy within bounds: weights clamped to 0..1, choices non-negative, names and counts capped, colours only if
	/// they are valid hex colours, effects within <see cref="Effect"/>'s caps. What the host keeps for a player is always
	/// this, and so is every look read, anyone's.
	/// </summary>
	public PlayerLook Sanitized()
	{
		var model = Model?.Trim() ?? "";

		// The look's shapes, and any its effects carried on their own: in the look they are the look's, each once
		var shapes = ShapePool.Of( DrawnShapes );
		foreach ( var effect in Effects ?? new() )
			shapes.Add( effect?.Shapes );

		foreach ( var design in Weapons ?? new() )
			shapes.Add( design?.Shapes );

		// The body's effects name shapes first: past the look's cap, a weapon's sticker gives way
		var effects = Effect.Sanitized( Effects, shapes );
		var weapons = WeaponDesign.Sanitized( Weapons, shapes );

		// A look's scripts, its body's then its weapons', are held to their size together
		Effect.HoldScriptsTo( effects.Concat( weapons.SelectMany( x => x.Effects ?? new() ) ).ToList(), Script.ScriptLimits.MaxLookBytes );
		return new PlayerLook
		{
			Model = model.Length <= MaxModelLength ? model : "",
			BodyGroups = (BodyGroups ?? new())
				.Where( x => IsName( x.Key ) )
				.Take( MaxBodyGroups )
				.ToDictionary( x => x.Key, x => Math.Max( 0, x.Value ) ),
			Morphs = (Morphs ?? new())
				.Where( x => IsName( x.Key ) && float.IsFinite( x.Value ) && x.Value > 0f )
				.Take( MaxMorphs )
				.ToDictionary( x => x.Key, x => Math.Clamp( x.Value, 0f, 1f ) ),
			Skin = IsName( Skin ) ? Skin.Trim() : "",
			Tint = IsColour( Tint ) ? Tint.ToLowerInvariant() : "",
			Parts = (Parts ?? new())
				.Where( x => IsName( x.Key ) && IsColour( x.Value ) )
				.Take( MaxParts )
				.ToDictionary( x => x.Key, x => x.Value.ToLowerInvariant() ),
			Finish = MaterialFinish.IsKnown( Finish ) ? Finish : "",
			PartFinishes = (PartFinishes ?? new())
				.Where( x => IsName( x.Key ) && MaterialFinish.IsKnownForPart( x.Value ) )
				.Take( MaxParts )
				.ToDictionary( x => x.Key, x => x.Value ),
			Accessories = (Accessories ?? new())
				.Select( x => x?.Sanitized() )
				.Where( x => x is not null )
				.Take( Accessory.MaxWorn )
				.ToList(),
			Shape = (Shape ?? new())
				.Where( x => x.Key is not null && Enum.IsDefined( typeof( BodyShape.Part ), x.Key ) && float.IsFinite( x.Value ) && x.Value != 0f )
				.ToDictionary( x => x.Key, x => Math.Clamp( x.Value, -1f, 1f ) ),
			Bones = (Bones ?? new())
				.Where( x => x.Key is not null && Enum.IsDefined( typeof( BoneRole ), x.Key ) && x.Value is not null && x.Value.Length <= MaxNameLength )
				.ToDictionary( x => x.Key, x => x.Value.Trim() ),
			Effects = effects,
			Weapons = weapons,
			DrawnShapes = new List<DrawnShape>( shapes.Used ),
		};
	}

	/// <summary>The bone picks as one string, to tell when they change (a change means posing the model anew).</summary>
	public string BonesKey()
	{
		return string.Join( ";", (Bones ?? new()).OrderBy( x => x.Key, StringComparer.Ordinal ).Select( x => $"{x.Key}={x.Value}" ) );
	}

	/// <summary>The look with the model's own body groups: what shows while the host keeps players' choices off.</summary>
	public PlayerLook WithoutBodyGroups()
	{
		var copy = Sanitized();
		copy.BodyGroups.Clear();
		return copy;
	}

	/// <summary>
	/// The customisation alone, within bounds, for sharing: the bones picked by hand are left out, as they belong to the
	/// model's armature (shared on its own), not to the look, and so are the weapon designs, which are the player's (shared
	/// in the Weapon Workshop), with the drawn shapes only they name.
	/// </summary>
	public PlayerLook Customisation()
	{
		var copy = Sanitized();
		copy.Bones.Clear();
		if ( copy.Weapons.Count == 0 )
			return copy;

		copy.Weapons.Clear();
		return copy.Sanitized();
	}

	/// <summary>A look shared as JSON (see <see cref="Customisation"/>), or null when it isn't one that customises a model.</summary>
	public static PlayerLook ParseShared( string json )
	{
		var look = string.IsNullOrWhiteSpace( json ) || !json.TrimStart().StartsWith( '{' ) ? null : Parse( json ).Customisation();
		return look is { IsDefault: false, IsCustomised: true } ? look : null;
	}

	/// <summary>Whether <paramref name="other"/> customises the same model the same way, the bones picked by hand aside.</summary>
	public bool SameCustomisationAs( PlayerLook other )
	{
		if ( other is null )
			return false;

		var mine = Sanitized();
		var theirs = other.Sanitized();
		return mine.Model == theirs.Model && mine.Skin == theirs.Skin && mine.Tint == theirs.Tint && mine.Finish == theirs.Finish
			&& SameEntries( mine.BodyGroups, theirs.BodyGroups ) && SameEntries( mine.Morphs, theirs.Morphs )
			&& SameEntries( mine.Parts, theirs.Parts ) && SameEntries( mine.Shape, theirs.Shape )
			&& SameEntries( mine.PartFinishes, theirs.PartFinishes )
			&& mine.Accessories.Count == theirs.Accessories.Count
			&& mine.Accessories.Zip( theirs.Accessories ).All( x => x.First.SameAs( x.Second ) )
			&& JsonSerializer.Serialize( mine.Effects ) == JsonSerializer.Serialize( theirs.Effects )
			&& JsonSerializer.Serialize( mine.DrawnShapes ) == JsonSerializer.Serialize( theirs.DrawnShapes );
	}

	/// <summary>
	/// Takes <paramref name="other"/>'s customisation (skin, colours, finishes, body groups, morphs, body shape, items,
	/// effects) in place of this look's own. The model, the bones picked by hand and the weapon designs stay: they belong
	/// to the model and to the player, not the look (the drawn shapes the designs name stay with them).
	/// </summary>
	public void TakeCustomisationFrom( PlayerLook other )
	{
		var look = other.Sanitized();
		var weapons = Weapons ?? new();
		var named = new HashSet<string>();
		foreach ( var design in weapons )
			named.UnionWith( design.DrawnShapeIds() );

		foreach ( var shape in DrawnShapes ?? new() )
		{
			if ( named.Contains( shape.Id ) && !look.DrawnShapes.Exists( x => x.Id == shape.Id ) )
				look.DrawnShapes.Add( shape );
		}

		Skin = look.Skin;
		Tint = look.Tint;
		BodyGroups = look.BodyGroups;
		Morphs = look.Morphs;
		Parts = look.Parts;
		Shape = look.Shape;
		Finish = look.Finish;
		PartFinishes = look.PartFinishes;
		Accessories = look.Accessories;
		Effects = look.Effects;
		Weapons = weapons;
		DrawnShapes = look.DrawnShapes;
	}

	/// <summary>The look without its items: what shows while the host keeps players' items off.</summary>
	public PlayerLook WithoutAccessories()
	{
		var copy = Sanitized();
		copy.Accessories.Clear();
		return copy;
	}

	/// <summary>The look without its Workshop items: what shows while the host keeps sbox.game downloads off.</summary>
	public PlayerLook WithoutWorkshopItems()
	{
		var copy = Sanitized();
		copy.Accessories.RemoveAll( x => x.IsWorkshop );
		return copy;
	}

	/// <summary>
	/// The look without its effects: what shows while the host keeps players' effects off. Its weapon designs stay, with
	/// the drawn shapes they name.
	/// </summary>
	public PlayerLook WithoutEffects()
	{
		var copy = Sanitized();
		copy.Effects.Clear();
		return copy.Sanitized();
	}

	/// <summary>The look without its weapon designs: what shows while the host keeps players' weapon designs off.</summary>
	public PlayerLook WithoutWeapons()
	{
		var copy = Sanitized();
		copy.Weapons.Clear();
		return copy.Sanitized();
	}

	/// <summary>
	/// The look with only what of its weapon designs a server allows: without their props while <paramref name="props"/>
	/// is off, and without their effects while <paramref name="effects"/> is off. This very look when nothing is held
	/// back, else a copy.
	/// </summary>
	public PlayerLook WithWeaponsAllowed( bool props, bool effects )
	{
		if ( (props || !Weapons.Exists( x => x.Props is not null )) && (effects || !Weapons.Exists( x => x.Effects is not null )) )
			return this;

		var copy = Sanitized();
		foreach ( var design in copy.Weapons )
		{
			design.Props = props ? design.Props : null;
			design.Effects = effects ? design.Effects : null;
		}

		return copy.Sanitized();
	}

	/// <summary>
	/// The look with <paramref name="designs"/> (each on its own, carrying the drawn shapes it names, see
	/// <see cref="StandaloneWeapons"/>) in place of its own: the player's designs follow them from model to model.
	/// </summary>
	public PlayerLook WithWeapons( IEnumerable<WeaponDesign> designs )
	{
		var copy = Sanitized();
		copy.Weapons = designs?.Where( x => x is not null ).ToList() ?? new();
		return copy.Sanitized();
	}

	/// <summary>Its weapon designs each on its own, carrying the drawn shapes it names: to keep, share or copy as a code.</summary>
	public List<WeaponDesign> StandaloneWeapons()
	{
		var designs = new List<WeaponDesign>();
		foreach ( var design in Weapons )
		{
			if ( StandaloneWeapon( design ) is { } copy )
				designs.Add( copy );
		}

		return designs;
	}

	/// <summary><paramref name="design"/> of this look on its own, within bounds, carrying the drawn shapes it names; null for none.</summary>
	public WeaponDesign StandaloneWeapon( WeaponDesign design )
	{
		if ( design is null )
			return null;

		var copy = JsonSerializer.Deserialize<WeaponDesign>( JsonSerializer.Serialize( design ) );
		var named = design.DrawnShapeIds().ToHashSet();
		copy.Shapes = DrawnShapes.Where( x => named.Contains( x.Id ) ).ToList();
		return copy.Sanitized();
	}

	/// <summary>
	/// The look as it is worn while some of it is still a draft (see <see cref="Draft"/>): the effects with ids in
	/// <paramref name="effects"/> and the designs for the weapons in <paramref name="weapons"/> as <paramref name="worn"/>
	/// has them (left out where it has none). This very look when nothing is a draft.
	/// </summary>
	public PlayerLook WithoutDrafts( ICollection<string> effects, ICollection<string> weapons, PlayerLook worn )
	{
		if ( effects.Count == 0 && weapons.Count == 0 )
			return this;

		var copy = Sanitized();
		var shown = new List<Effect>();
		foreach ( var effect in copy.Effects )
		{
			var kept = effects.Contains( effect.Id ) ? worn?.Standalone( worn.Effects.Find( x => x.Id == effect.Id ) ) : copy.Standalone( effect );
			if ( kept is not null )
				shown.Add( kept );
		}

		copy.Effects = shown;
		var designs = copy.StandaloneWeapons().Where( x => !weapons.Contains( x.Weapon ) ).ToList();
		foreach ( var weapon in weapons )
		{
			if ( worn?.StandaloneWeapon( worn.DesignFor( weapon ) ) is { } kept )
				designs.Add( kept );
		}

		return copy.WithWeapons( designs );
	}

	/// <summary>The design for the weapon whose world model is <paramref name="weapon"/>; null for none.</summary>
	public WeaponDesign DesignFor( string weapon )
	{
		if ( weapon is null )
			return null;

		foreach ( var design in Weapons )
		{
			if ( design.Weapon == weapon )
				return design;
		}

		return null;
	}

	/// <summary>
	/// The default citizen wearing this look's effects and weapon designs and nothing else: what a look without a model
	/// keeps, as the citizen has nothing else to customise.
	/// </summary>
	public PlayerLook EffectsAlone()
	{
		var copy = Sanitized();
		return new PlayerLook { Effects = copy.Effects, Weapons = copy.Weapons, DrawnShapes = copy.DrawnShapes };
	}

	/// <summary>
	/// The look with only the effects a server allows, for anyone but its host: none from a Workshop share while
	/// <paramref name="workshop"/> is off, none from a share <paramref name="blocked"/> says the host blocked (by its
	/// <see cref="ShareOrigin.Key"/>), and drawn shapes (all of them while <paramref name="drawnShapes"/> is off, else
	/// those from a Workshop that isn't allowed or blocked) shown as plain ones: a circle to come from, a glow to look like,
	/// a star on a weapon. Weapon designs from such a share are left out the same way. This very look when nothing is
	/// held back, as mostly nothing is; else a copy. Nothing is taken from the look itself.
	/// </summary>
	public PlayerLook WithEffectsAllowed( bool workshop, bool drawnShapes, Func<string, bool> blocked )
	{
		bool Refused( ShareOrigin origin ) => origin is not null && (!workshop || blocked( origin.Key ));
		bool Plain( DrawnShape shape ) => !drawnShapes || Refused( shape.ForkedFrom );

		if ( !Effects.Exists( x => Refused( x.ForkedFrom ) ) && !Weapons.Exists( x => Refused( x.ForkedFrom ) ) && !DrawnShapes.Exists( Plain ) )
			return this;

		var copy = Sanitized();
		copy.Effects.RemoveAll( x => Refused( x.ForkedFrom ) );
		copy.Weapons.RemoveAll( x => Refused( x.ForkedFrom ) );
		var plain = copy.DrawnShapes.Where( Plain ).Select( x => x.Id ).ToHashSet();
		foreach ( var design in copy.Weapons )
		{
			foreach ( var sticker in design.Stickers ?? new() )
			{
				if ( sticker.DrawnPicture is { } id && plain.Contains( id ) )
					sticker.Picture = nameof( StandardShape.Star5 );
			}

			if ( design.Skin is { } skin && DrawnShape.IsReference( skin.PatternShape ) && plain.Contains( skin.PatternShape[ 1.. ] ) )
				skin.PatternShape = nameof( StandardShape.Star5 );
		}

		foreach ( var layer in copy.Effects.Concat( copy.Weapons.SelectMany( x => x.Effects ?? new() ) ).SelectMany( x => x.Layers ) )
		{
			if ( layer.DrawnFormation is { } formation && plain.Contains( formation ) )
				layer.Formation = null;

			if ( layer.DrawnPicture is { } picture && plain.Contains( picture ) )
				layer.Picture = null;

			if ( layer.Pictures?.Exists( x => DrawnShape.IsReference( x ) && plain.Contains( x[ 1.. ] ) ) == true )
				layer.Pictures = null;
		}

		copy.DrawnShapes.RemoveAll( x => plain.Contains( x.Id ) );
		return copy;
	}

	/// <summary>The drawn shape the look carries with id <paramref name="id"/>; null for none.</summary>
	public DrawnShape DrawnShapeOf( string id ) => id is null ? null : DrawnShapes.Find( x => x.Id == id );

	/// <summary>
	/// <paramref name="effect"/> of this look on its own, within bounds: carrying the drawn shapes its layers name, to keep,
	/// share or copy as a code. Null when it has nothing to show.
	/// </summary>
	public Effect Standalone( Effect effect )
	{
		if ( effect is null )
			return null;

		var copy = JsonSerializer.Deserialize<Effect>( JsonSerializer.Serialize( effect ) );
		copy.Shapes = DrawnShapes.Where( x => effect.Layers.Exists( l => l.DrawnShapeIds().Contains( x.Id ) ) ).ToList();
		return copy.Sanitized();
	}

	/// <summary>
	/// Carries <paramref name="shape"/> for a layer to name, letting go of shapes no layer names any more. False, with
	/// nothing changed, when the look already carries <see cref="DrawnShape.MaxPerLook"/> shapes its layers name, or with
	/// it would carry more points or emblem layers than a look may (see <see cref="ShapePool"/>).
	/// </summary>
	public bool Carry( DrawnShape shape )
	{
		var named = Named( Effects );
		if ( DrawnShapes.Exists( x => x.Id == shape.Id ) )
			return true;

		if ( named.Count( x => x != shape.Id ) >= DrawnShape.MaxPerLook || !ShapesFit( DrawnShapes.Where( x => named.Contains( x.Id ) ).Append( shape ) ) )
			return false;

		DrawnShapes.RemoveAll( x => !named.Contains( x.Id ) );
		DrawnShapes.Add( shape );
		return true;
	}

	/// <summary>
	/// Wears <paramref name="effect"/> (one on its own, see <see cref="Standalone"/>): its drawn shapes become the look's,
	/// each once. False, with nothing changed, when the look would carry more than <see cref="DrawnShape.MaxPerLook"/>
	/// shapes its layers name.
	/// </summary>
	public bool Adopt( Effect effect )
	{
		var shapes = effect.Shapes ?? new List<DrawnShape>();
		var named = Named( Effects.Append( effect ) );

		if ( named.Count > DrawnShape.MaxPerLook || !ShapesFit( DrawnShapes.Concat( shapes ).Where( x => x is not null && named.Contains( x.Id ) ).DistinctBy( x => x.Id ) ) )
			return false;

		// Shapes no layer names any more are let go of here too
		DrawnShapes.RemoveAll( x => !named.Contains( x.Id ) );
		foreach ( var shape in shapes )
		{
			if ( shape is not null && named.Contains( shape.Id ) && !DrawnShapes.Exists( x => x.Id == shape.Id ) )
				DrawnShapes.Add( shape );
		}

		effect.Shapes = null;
		Effects.Add( effect );
		return true;
	}

	// Whether shapes together are within what a look carries: their points, and their emblems' layers
	private static bool ShapesFit( IEnumerable<DrawnShape> shapes )
	{
		var all = shapes.ToList();
		return all.Sum( x => x.PointCount ) <= DrawnShape.MaxLookPoints && all.Sum( x => x.Emblem?.Count ?? 0 ) <= DrawnShape.MaxLookEmblemLayers;
	}

	// The ids of the drawn shapes effects' layers and the look's weapon designs name
	private HashSet<string> Named( IEnumerable<Effect> effects )
	{
		var named = effects.SelectMany( x => x.Layers ).SelectMany( x => x.DrawnShapeIds() ).ToHashSet();
		foreach ( var design in Weapons )
			named.UnionWith( design.DrawnShapeIds() );

		return named;
	}

	/// <summary>
	/// The look with its effects within <paramref name="budget"/> (see <see cref="EffectBudget.Trim"/>), and each weapon
	/// design's effects within what is left of it, as one weapon shows at a time beside the body's: this very look when
	/// they fit, as they mostly do, else a copy.
	/// </summary>
	public PlayerLook WithinBudget( EffectBudget budget )
	{
		if ( budget.Fits( Effects ) && !Weapons.Exists( x => x.Effects is not null && !budget.Less( Effects ).Fits( x.Effects ) ) )
			return this;

		var copy = Sanitized();
		budget.Trim( copy.Effects );
		var left = budget.Less( copy.Effects );
		foreach ( var design in copy.Weapons )
		{
			if ( design.Effects is null )
				continue;

			left.Trim( design.Effects );
			design.Effects.RemoveAll( x => x.Layers.Count == 0 );
			design.Effects = design.Effects.Count > 0 ? design.Effects : null;
		}

		return copy;
	}

	/// <summary>The same customisation on another model: what each model was last customised to is kept apart.</summary>
	public PlayerLook WithModel( string model )
	{
		var copy = Sanitized();
		copy.Model = model ?? "";
		return copy;
	}

	private static bool SameEntries<TValue>( Dictionary<string, TValue> mine, Dictionary<string, TValue> theirs )
	{
		return mine.Count == theirs.Count
			&& mine.All( x => theirs.TryGetValue( x.Key, out var value ) && EqualityComparer<TValue>.Default.Equals( x.Value, value ) );
	}

	private static bool IsName( string name )
	{
		return !string.IsNullOrWhiteSpace( name ) && name.Length <= MaxNameLength;
	}

	/// <summary>Whether <paramref name="text"/> is a colour as looks keep them, <c>#rrggbb</c>.</summary>
	internal static bool IsColour( string text )
	{
		return text is not null && HexColour.IsMatch( text );
	}
}
