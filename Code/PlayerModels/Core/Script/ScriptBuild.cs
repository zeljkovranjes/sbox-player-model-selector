using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>Where a built value came from in the script.</summary>
public enum FieldOrigin
{
	/// <summary>Nothing in the script sets it: it is at its default.</summary>
	Default,

	/// <summary>One statement at the script's top sets it to a literal: the visual editor rewrites that literal in place.</summary>
	Literal,

	/// <summary>Code works it out (an expression, a loop, a function, or one statement setting several): "set by script".</summary>
	Script,

	/// <summary>A [Parameter] gives it (<see cref="ScriptSite.Parameter"/>): the editor changes the parameter, not the field.</summary>
	Parameter,
}

/// <summary>
/// How the visual editor may treat a field: its <see cref="Origin"/>, the statement setting it (<see cref="Site"/>, with
/// its <see cref="Line"/> for "jump to line"), and whether an event or particle function also changes it while the
/// effect runs (and where).
/// </summary>
public readonly record struct FieldSource( FieldOrigin Origin, ScriptSite Site, int Line, bool ChangesWhileRunning, int RunningLine );

/// <summary>
/// What building a script made: an effect (with the drawn shapes its layers use), or a shape (and its emblem layers),
/// and for the two-way editor which statement set each field. Or the error that stopped it.
/// </summary>
public sealed class ScriptBuild
{
	public Effect Effect { get; init; }
	public DrawnShape Shape { get; init; }

	/// <summary>A weapon design script's design, its weapon left for the caller to name.</summary>
	public WeaponDesign Design { get; init; }

	/// <summary>A shape script's emblem layers (empty for a plain shape).</summary>
	public IReadOnlyList<EmblemPart> Emblem { get; init; } = System.Array.Empty<EmblemPart>();

	/// <summary>The emblem layers of the drawn shapes an effect script made, by the shape's id.</summary>
	public IReadOnlyDictionary<string, IReadOnlyList<EmblemPart>> Emblems { get; init; } = new Dictionary<string, IReadOnlyList<EmblemPart>>();

	public ScriptError Error { get; init; }
	public bool Succeeded => Error is null;

	internal ScriptProgram Program { get; init; }
	internal Dictionary<(int Layer, PropertyId Property), int> Sites { get; init; } = new();
	internal HashSet<int> SharedSites { get; init; } = new();
	internal int[] LayerSites { get; init; } = System.Array.Empty<int>();
	internal List<(int Layer, PropertyId Property, int Site)> Running { get; init; } = new();

	/// <summary>The effect-level owner in <see cref="Sites"/> (a layer is its index).</summary>
	internal const int EffectOwner = -1;

	/// <summary>
	/// A weapon design's owners in <see cref="Sites"/>: the weapon and its skin, then its stickers, props and effects by
	/// their place in it.
	/// </summary>
	public const int WeaponOwner = -2;
	public const int StickerOwner = 10_000;
	public const int PropOwner = 20_000;
	public const int EffectOwnerBase = 30_000;

	internal int[] StickerSites { get; init; } = System.Array.Empty<int>();
	internal int[] PropSites { get; init; } = System.Array.Empty<int>();
	internal int[] PartSites { get; init; } = System.Array.Empty<int>();
	internal int[] EffectSites { get; init; } = System.Array.Empty<int>();

	/// <summary>Where design field <paramref name="property"/> of <paramref name="owner"/> (see <see cref="WeaponOwner"/>) came from.</summary>
	public FieldSource SourceOf( PropertyId property, int owner ) => Source( owner, property );

	/// <summary>The statement that put sticker, prop, hidden part or effect <paramref name="index"/> on the weapon; null when unknown.</summary>
	public ScriptSite StickerSite( int index ) => SiteOf( StickerSites, index );
	public ScriptSite PropSite( int index ) => SiteOf( PropSites, index );
	public ScriptSite PartSite( int index ) => SiteOf( PartSites, index );
	public ScriptSite EffectSite( int index ) => SiteOf( EffectSites, index );

	private ScriptSite SiteOf( int[] sites, int index ) => index >= 0 && index < sites.Length && sites[ index ] >= 0 ? Program.Sites[ sites[ index ] ] : null;

	internal static ScriptBuild Failed( ScriptError error ) => new() { Error = error };

	/// <summary>Where layer <paramref name="layer"/>'s <paramref name="property"/> came from.</summary>
	public FieldSource SourceOf( int layer, PropertyId property ) => Source( layer, property );

	/// <summary>Where the effect's own <paramref name="property"/> (its name, key, anchor, sliders) came from.</summary>
	public FieldSource SourceOf( PropertyId property ) => Source( EffectOwner, property );

	/// <summary>The statement that made layer <paramref name="layer"/> (<c>local glow = Sprite()</c>); null when unknown.</summary>
	public ScriptSite LayerSite( int layer ) =>
		layer >= 0 && layer < LayerSites.Length && LayerSites[ layer ] >= 0 ? Program.Sites[ LayerSites[ layer ] ] : null;

	private FieldSource Source( int owner, PropertyId property )
	{
		var running = false;
		var runningLine = 0;
		foreach ( var (layer, set, site) in Running )
		{
			if ( layer == owner && set == property )
				(running, runningLine) = (true, Line( Program.Sites[ site ] ));
		}

		if ( Program is null || !Sites.TryGetValue( (owner, property), out var index ) )
			return new FieldSource( FieldOrigin.Default, null, 0, running, runningLine );

		var statement = Program.Sites[ index ];
		var origin = statement.Parameter is not null ? FieldOrigin.Parameter
			: statement.IsLiteral && statement.IsTopLevel && !SharedSites.Contains( index ) ? FieldOrigin.Literal : FieldOrigin.Script;
		return new FieldSource( origin, statement, Line( statement ), running, runningLine );
	}

	private int Line( ScriptSite site ) => Program.Lines.Position( site.StatementStart ).Line;
}
