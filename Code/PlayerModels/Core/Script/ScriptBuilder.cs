using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

/// <summary>
/// What the top of an effect or shape script makes while it is built: the effect's layers and its own fields, shapes
/// drawn point by point (or from text, other shapes and the player's own), emblem layers, and other effects included.
/// Builds the same <see cref="Effect"/>, <see cref="EffectLayer"/> and <see cref="DrawnShape"/> data the visual editor
/// makes (so the rest of the addon needs nothing new), held to the same caps, and records which statement set each
/// field. Runs once per look change, so it may allocate.
/// </summary>
internal sealed partial class ScriptBuilder
{
	private sealed class Draft
	{
		public string Name;
		public readonly List<ShapeStroke> Strokes = new();
		public readonly List<float> Pending = new();
		public readonly List<int> Parts = new();
		public Sticker Sticker;
		public DrawnShape Made;
		public bool IsMade;
	}

	// While building, a layer's shape or picture naming a drawn shape not made yet: this, then the draft's index
	private const char DraftMark = '\u0001';

	private readonly ScriptMachine _machine;
	private readonly ScriptProgram _program;
	private readonly IPlayerLibrary _library;
	private readonly Effect _effect = new() { Anchor = new EffectAnchor() };
	private List<EffectLayer> _layers = new();
	private readonly List<int> _particles = new();
	private readonly List<int> _layerSites = new();
	private readonly List<Draft> _drafts = new();
	private readonly List<EmblemPart> _parts = new();
	private readonly List<Effect> _included = new() { null };
	private readonly Dictionary<(int, PropertyId), int> _sites = new();
	private readonly Dictionary<int, (int, PropertyId)> _siteTarget = new();
	private readonly HashSet<int> _sharedSites = new();

	/// <summary>Why the last operation failed, in plain words.</summary>
	public string Error { get; private set; }

	public ScriptBuilder( ScriptMachine machine, IPlayerLibrary library )
	{
		_machine = machine;
		_program = machine.Program;
		_library = library;
		if ( _program.Kind == ScriptKind.Shape )
			_drafts.Add( new Draft { Name = "" } );
	}

	private int Fail( string message )
	{
		Error = message;
		return -1;
	}

	private string Text( double index )
	{
		var i = (int)index;
		return i >= 0 && i < _machine.Strings.Length ? _machine.Strings[ i ] : "";
	}

	/// <summary>Runs a making built-in on the stack (its arguments at the top), giving the new top, or -1 with <see cref="Error"/>.</summary>
	public int Run( Intrinsic op, double[] s, int sp )
	{
		switch ( op )
		{
			case Intrinsic.Sprite or Intrinsic.Light or Intrinsic.Trail or Intrinsic.Beam:
			{
				var kind = op switch { Intrinsic.Light => LayerKind.Light, Intrinsic.Trail => LayerKind.Trail, Intrinsic.Beam => LayerKind.Beam, _ => LayerKind.Sprite };
				var layer = AddLayer( new EffectLayer { Kind = kind.ToString() }.Sanitized(), (int)s[ sp - 1 ] );
				if ( layer < 0 )
					return -1;

				s[ sp - 1 ] = layer;
				return sp;
			}

			case Intrinsic.ShapeNew or Intrinsic.ShapeText or Intrinsic.ShapesMine or Intrinsic.StickersMine or Intrinsic.EmblemsMine:
			{
				var draft = op switch
				{
					Intrinsic.ShapeText => TextShape( Text( s[ sp - 2 ] ) ),
					Intrinsic.ShapesMine => MyShape( Text( s[ sp - 2 ] ), LibraryItemKind.Shape ),
					Intrinsic.StickersMine => MyShape( Text( s[ sp - 2 ] ), LibraryItemKind.Sticker ),
					Intrinsic.EmblemsMine => MyShape( Text( s[ sp - 2 ] ), LibraryItemKind.Emblem ),
					_ => NewDraft( Text( s[ sp - 2 ] ) ),
				};

				if ( draft < 0 )
					return -1;

				s[ sp - 2 ] = Handles.DraftBase + draft;
				return sp - 1;
			}

			case Intrinsic.EffectsMine or Intrinsic.EffectsPreset:
			{
				var name = Text( s[ sp - 1 ] );
				var found = op == Intrinsic.EffectsMine ? _library?.Effect( name ) : Preset( name );
				if ( found?.Sanitized() is not { } effect )
					return Fail( op == Intrinsic.EffectsMine ? $"There is no effect called \"{name}\" in My effects" : $"There is no preset called \"{name}\"" );

				_included.Add( effect );
				s[ sp - 1 ] = _included.Count - 1;
				return sp;
			}

			case Intrinsic.EffectInclude: return Include( (int)s[ sp - 2 ], (int)s[ sp - 1 ] ) ? sp - 2 : -1;
			case Intrinsic.ShapePoint: return Point( (int)s[ sp - 3 ], s[ sp - 2 ], s[ sp - 1 ] ) ? sp - 3 : -1;
			case Intrinsic.ShapePoints: return Points( (int)s[ sp - 3 ], (int)s[ sp - 2 ], (int)s[ sp - 1 ] ) ? sp - 3 : -1;
			case Intrinsic.ShapeEndLine or Intrinsic.ShapeEndOutline: return EndStroke( (int)s[ sp - 1 ], op == Intrinsic.ShapeEndOutline ) ? sp - 1 : -1;
			case Intrinsic.ShapeFit: return Fit( (int)s[ sp - 1 ] ) ? sp - 1 : -1;
			case Intrinsic.ShapeLetter:
				return Letter( (int)s[ sp - 6 ], Text( s[ sp - 5 ] ), s[ sp - 4 ], s[ sp - 3 ], s[ sp - 2 ], s[ sp - 1 ] ) ? sp - 6 : -1;
			case Intrinsic.ShapeAdd:
				return Stamp( (int)s[ sp - 6 ], (int)s[ sp - 5 ], s[ sp - 4 ], s[ sp - 3 ], s[ sp - 2 ], s[ sp - 1 ] ) ? sp - 6 : -1;
			case Intrinsic.ShapeAddLayer:
			{
				var part = AddPart( (int)s[ sp - 3 ], (int)s[ sp - 2 ] );
				if ( part < 0 )
					return -1;

				s[ sp - 3 ] = part;
				return sp - 2;
			}

			default:
				return Fail( "This can't be done while building" );
		}
	}

	private static Effect Preset( string name )
	{
		foreach ( var preset in EffectPresets.All )
		{
			if ( preset.Name == name )
				return preset;
		}

		return null;
	}

	private int AddLayer( EffectLayer layer, int site )
	{
		if ( _program.Kind != ScriptKind.Effect )
			return Fail( "Layers belong to effect scripts" );

		if ( _layers.Count >= Effect.MaxLayers )
			return Fail( $"An effect has at most {Effect.MaxLayers} layers" );

		_layers.Add( layer );
		_particles.Add( -1 );
		_layerSites.Add( site );
		return _layers.Count - 1;
	}

	private bool Include( int into, int other )
	{
		if ( into != Handles.CurrentEffect || other <= 0 || other >= _included.Count )
		{
			Fail( "Only the effect this script makes can include another" );
			return false;
		}

		var effect = _included[ other ];
		foreach ( var shape in effect.Shapes ?? new List<DrawnShape>() )
			_drafts.Add( new Draft { Name = shape.Name, Made = shape, IsMade = true } );

		foreach ( var layer in effect.Layers )
		{
			var copy = layer.Sanitized( x => x );
			copy.Joint ??= effect.Anchor.Joint;
			if ( AddLayer( copy, -1 ) < 0 )
				return false;
		}

		return true;
	}

	private int NewDraft( string name )
	{
		if ( _drafts.Count >= DrawnShape.MaxPerLook + (_program.Kind == ScriptKind.Shape ? 1 : 0) )
			return Fail( $"A script makes at most {DrawnShape.MaxPerLook} shapes" );

		_drafts.Add( new Draft { Name = name } );
		return _drafts.Count - 1;
	}

	private int TextShape( string text )
	{
		var draft = NewDraft( text );
		if ( draft >= 0 && !AddStrokes( _drafts[ draft ], HersheyFont.Strokes( text ), 0, 0, 1, 1, 0 ) )
			return -1;

		return draft;
	}

	private int MyShape( string name, LibraryItemKind kind )
	{
		var shape = _library?.Shape( name, kind )?.Sanitized();
		if ( shape is null )
			return Fail( $"There is no {kind.ToString().ToLowerInvariant()} called \"{name}\" in My shapes" );

		var draft = NewDraft( shape.Name ?? name );
		if ( draft < 0 )
			return -1;

		_drafts[ draft ].Strokes.AddRange( shape.Decode() );
		_drafts[ draft ].Sticker = shape.Sticker;
		return draft;
	}

	private int AddPart( int handle, int shape )
	{
		if ( DraftOf( handle ) is not { } draft )
			return -1;

		if ( draft.Parts.Count >= EmblemPart.MaxPerEmblem )
			return Fail( $"An emblem has at most {EmblemPart.MaxPerEmblem} layers" );

		if ( StrokesOf( shape ) is null )
			return -1;

		_parts.Add( new EmblemPart { ShapeHandle = shape } );
		draft.Parts.Add( _parts.Count - 1 );
		return _parts.Count - 1;
	}
}
