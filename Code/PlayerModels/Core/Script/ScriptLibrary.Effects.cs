using System;
using System.Collections.Generic;

namespace PlayerModels.Core.Script;

public static partial class ScriptLibrary
{
	private static void AddEffects( List<LibraryEntry> entries )
	{
		const string effect = "Effect", layer = "Layer";
		var build = ScriptPlace.EffectBuild;
		var name = ScriptType.String;
		entries.Add( new LibraryEntry
		{
			Name = "Effect", Kind = EntryKind.Constant, Type = ScriptType.Effect, Category = effect, Where = build, Value = new[] { (double)Handles.CurrentEffect },
			Doc = "The effect this script makes: its name, key, anchor and sliders.", Example = "Effect.Name = \"Sparks\"\nlocal named = Effect.Name --> \"Sparks\"", ExampleKind = ScriptKind.Effect,
		} );
		entries.Add( Function( "Sprite", Intrinsic.Sprite, ScriptType.Layer, layer, "Adds a layer of sprites (flames, sparks, hearts) to the effect: at most 8 layers.", "local glow = Sprite()\nlocal kind = glow.Kind --> Kind.Sprite", build ) );
		entries.Add( Function( "Light", Intrinsic.Light, ScriptType.Layer, layer, "Adds a light to the effect: its Size is how far it reaches, its Amount how bright.", "local lamp = Light()\nlocal kind = lamp.Kind --> Kind.Light", build ) );
		entries.Add( Function( "Trail", Intrinsic.Trail, ScriptType.Layer, layer, "Adds a trail left behind as its place moves.", "local streak = Trail()\nlocal kind = streak.Kind --> Kind.Trail", build ) );
		entries.Add( Space( "Effects", effect, "Whole effects to include in this one: the player's own (\"My effects\") and the presets." ) );
		entries.Add( Function( "Effects.Mine", Intrinsic.EffectsMine, ScriptType.Effect, effect, "One of the player's saved effects, by name, to include.", "Effect.Include( Effects.Mine( \"Fire hands\" ) )", build,
			P( "name", name, "Its name in My effects" ) ) );
		entries.Add( Function( "Effects.Preset", Intrinsic.EffectsPreset, ScriptType.Effect, effect, "One of the stock presets, by name, to include.", "Effect.Include( Effects.Preset( \"Hearts\" ) )", build,
			P( "name", name, "Its name in the Effects tab" ) ) );
		entries.Add( Method( "Effect.Include", Intrinsic.EffectInclude, ScriptType.Void, effect, "Adds every layer of another effect to this one (as long as there is room for them).", "Effect.Include( Effects.Preset( \"Hearts\" ) )", build,
			P( "other", ScriptType.Effect, "From Effects.Mine or Effects.Preset" ) ) );
		entries.Add( Method( "Layer.Emit", Intrinsic.LayerEmit, ScriptType.Void, layer, "Sends out count sprites of the layer at once, where it is (within the budget).", "local sparks = Sprite()\nfunction OnJump()\n\tsparks.Emit( 20 )\nend", ScriptPlace.EffectRun,
			P( "count", N, "How many", "0 to 100" ) ) );
		entries.Add( Method( "Layer.EmitAt", Intrinsic.LayerEmitAt, ScriptType.Void, layer, "Sends out count sprites of the layer at a place in the world (a shot's HitPosition, say).", "local sparks = Sprite()\nfunction OnShoot( shot )\n\tif shot.Hit then\n\t\tsparks.EmitAt( shot.HitPosition, 12 )\n\tend\nend", ScriptPlace.EffectRun,
			P( "position", ScriptType.Vector3, "Where, in the world" ), P( "count", N, "How many", "0 to 100" ) ) );

		foreach ( var property in ScriptProperties.All )
			entries.Add( PropertyEntry( property ) );
	}

	private static LibraryEntry PropertyEntry( ScriptProperty property )
	{
		var (owner, category, where, write) = property.Owner switch
		{
			TypeKind.Layer => ("Layer", "Layer", ScriptPlace.Effect, property.Live ? ScriptPlace.Effect : ScriptPlace.EffectBuild),
			TypeKind.Effect => ("Effect", "Effect", ScriptPlace.EffectBuild, ScriptPlace.EffectBuild),
			TypeKind.Shape => ("Shape", "Shape", ScriptPlace.Build, ScriptPlace.Build),
			_ => ("EmblemLayer", "Emblem", ScriptPlace.Build, ScriptPlace.Build),
		};

		return new LibraryEntry
		{
			Name = owner + "." + property.Name, Kind = EntryKind.Property, Type = property.Type, Category = category, Doc = property.Doc,
			Where = where, WriteWhere = property.ReadOnly ? ScriptPlace.None : write, Access = PropertyAccess.Builder, Slot = (int)property.Id,
			Min = property.Min, Max = property.Max, Example = PropertyExample( property ), ExampleKind = ScriptKind.Effect,
		};
	}

	private static string PropertyExample( ScriptProperty property )
	{
		var (setup, value) = property.Owner switch
		{
			TypeKind.Layer => ("local glow = Sprite()\n", "glow"),
			TypeKind.Effect => ("", "Effect"),
			TypeKind.Shape => ("local badge = Shapes.New( \"Badge\" )\n", "badge"),
			_ => ("local badge = Shapes.New( \"Badge\" )\nlocal star = badge.AddLayer( Shapes.Star5 )\n", "star"),
		};

		if ( property.Id == PropertyId.LayerParticle )
			return setup + "function glow.Particle( p )\n\tp.Alpha = 1 - p.Life\nend";

		var set = property.ExampleValue is null ? "" : $"{value}.{property.Name} = {property.ExampleValue}\n";
		return $"{setup}{set}local value = {value}.{property.Name} --> {property.ExampleResult}";
	}

	private static void AddShapes( List<LibraryEntry> entries )
	{
		const string shape = "Shape", emblem = "Emblem";
		var build = ScriptPlace.Build;
		var s = ScriptType.Shape;
		entries.Add( Space( "Shapes", shape, "Shapes: the 40 stock ones, the player's own (shapes, stickers, emblems), and new ones the script draws." ) );
		entries.Add( new LibraryEntry
		{
			Name = "shape", Kind = EntryKind.Constant, Type = s, Category = shape, Where = ScriptPlace.ShapeBuild, Value = new[] { (double)Handles.DraftBase },
			Doc = "In a shape script, the shape it makes.", Example = "shape.Point( 0, 0 )\nshape.Point( 1, 1 )\nshape.EndLine()\nlocal points = shape.PointCount --> 2", ExampleKind = ScriptKind.Shape,
		} );
		entries.Add( Function( "Shapes.New", Intrinsic.ShapeNew, s, shape, "A new empty shape to draw into, by name.", "local badge = Shapes.New( \"Badge\" )\nlocal points = badge.PointCount --> 0", build,
			P( "name", ScriptType.String, "What it is called" ) ) );
		entries.Add( Function( "Shapes.Text", Intrinsic.ShapeText, s, shape, "Text as a shape, in single lines the same on every machine (letters, digits, common punctuation; up to 24).", "local word = Shapes.Text( \"COOL\" )", build,
			P( "text", ScriptType.String, "What it says" ) ) );
		entries.Add( Function( "Shapes.Mine", Intrinsic.ShapesMine, s, shape, "One of the player's own shapes (My shapes), by name; checked when the script compiles.", "local logo = Shapes.Mine( \"COOL\" )", build,
			P( "name", ScriptType.String, "Its name in My shapes" ) ) );
		entries.Add( Space( "Stickers", shape, "The player's own stickers (shapes with colours), from My shapes." ) );
		entries.Add( Function( "Stickers.Mine", Intrinsic.StickersMine, s, shape, "One of the player's own stickers, by name; checked when the script compiles.", "local skull = Stickers.Mine( \"Gold skull\" )", build,
			P( "name", ScriptType.String, "Its name in My shapes" ) ) );
		entries.Add( Space( "Emblems", shape, "The player's own emblems (stacks of shape layers), from My shapes." ) );
		entries.Add( Function( "Emblems.Mine", Intrinsic.EmblemsMine, s, shape, "One of the player's own emblems, by name; checked when the script compiles.", "local clan = Emblems.Mine( \"Clan\" )", build,
			P( "name", ScriptType.String, "Its name in My shapes" ) ) );
		entries.Add( Method( "Shape.Point", Intrinsic.ShapePoint, ScriptType.Void, shape, "Adds a point to the line being drawn (the square is -1 to 1 each way, y up).", "local mark = Shapes.New( \"Mark\" )\nmark.Point( -1, 0 )\nmark.Point( 1, 0 )\nmark.EndLine()\nlocal points = mark.PointCount --> 2", build,
			P( "x", N, "Across", "-1 to 1" ), P( "y", N, "Up", "-1 to 1" ) ) );
		entries.Add( Method( "Shape.Points", Intrinsic.ShapePoints, ScriptType.Void, shape, "Adds points to the line being drawn, from a list of x, y pairs.", "local box = Shapes.New( \"Box\" )\nbox.Points( { -1, -1, 1, -1, 1, 1, -1, 1 } )\nbox.EndOutline()\nlocal points = box.PointCount --> 4", build,
			P( "xy", ScriptType.ListOf( N ), "x, y, x, y, ..." ) ) );
		entries.Add( Method( "Shape.EndLine", Intrinsic.ShapeEndLine, ScriptType.Void, shape, "Finishes the line being drawn as an open line.", "local mark = Shapes.New( \"Mark\" )\nmark.Points( { -1, 0, 1, 0 } )\nmark.EndLine()", build ) );
		entries.Add( Method( "Shape.EndOutline", Intrinsic.ShapeEndOutline, ScriptType.Void, shape, "Finishes the line being drawn as a closed outline, filled in.", "local tri = Shapes.New( \"Tri\" )\ntri.Points( { 0, 1, 1, -1, -1, -1 } )\ntri.EndOutline()", build ) );
		entries.Add( Method( "Shape.Letter", Intrinsic.ShapeLetter, ScriptType.Void, shape, "Draws text at a place, size and angle (to put letters along an arc, say).", "local ring = Shapes.New( \"Ring text\" )\nring.Letter( \"A\", 0, 0.5, 0.4, 0 )", build,
			P( "text", ScriptType.String, "What it says" ), P( "x", N, "Its middle, across", "-1 to 1" ), P( "y", N, "Its middle, up", "-1 to 1" ),
			P( "size", N, "How tall: 2 fills the square" ), P( "angle", N, "Turned, in degrees", null, new[] { 0.0 } ) ) );
		entries.Add( Method( "Shape.Add", Intrinsic.ShapeAdd, ScriptType.Void, shape, "Draws another shape into this one at a place, size and angle (combining shapes).", "local pair = Shapes.New( \"Pair\" )\npair.Add( Shapes.Heart, -0.5, 0, 0.5 )\npair.Add( Shapes.Heart, 0.5, 0, 0.5 )", build,
			P( "other", s, "The shape to draw" ), P( "x", N, "Its middle, across", "-1 to 1", new[] { 0.0 } ), P( "y", N, "Its middle, up", "-1 to 1", new[] { 0.0 } ),
			P( "size", N, "1 is its own size", null, new[] { 1.0 } ), P( "angle", N, "Turned, in degrees", null, new[] { 0.0 } ) ) );
		entries.Add( Method( "Shape.Fit", Intrinsic.ShapeFit, ScriptType.Void, shape, "Moves the shape to the middle and sizes it to fill the square, keeping its proportions.", "local small = Shapes.New( \"Small\" )\nsmall.Points( { 0, 0, 0.1, 0.1 } )\nsmall.EndLine()\nsmall.Fit()", build ) );
		entries.Add( Method( "Shape.AddLayer", Intrinsic.ShapeAddLayer, ScriptType.EmblemLayer, emblem, "Adds a layer to the shape, making it an emblem: a stack of shapes, each moved, turned, scaled and coloured (at most 32).", "local badge = Shapes.New( \"Badge\" )\nlocal star = badge.AddLayer( Shapes.Star5 )\nlocal drawn = star.Shape --> Shapes.Star5", build,
			P( "shape", s, "What the layer draws" ) ) );

		var standard = Enum.GetNames<StandardShape>();
		for ( var i = 0; i < standard.Length; i++ )
		{
			var what = StandardShapes.IsSolid( (StandardShape)i ) ? "A standard solid" : StandardShapes.IsOpen( (StandardShape)i ) ? "A standard line" : "A standard shape";
			entries.Add( Constant( "Shapes." + standard[ i ], s, shape, $"{what}: {standard[ i ]}.", $"local picked = Shapes.{standard[ i ]} --> Shapes.{standard[ i ]}", i ) );
		}

		entries.Add( Space( "Picture", "Layer", "The curated pictures a sprite can look like (a shape can be one too)." ) );
		var pictures = Enum.GetNames<LayerPicture>();
		for ( var i = 0; i < pictures.Length; i++ )
			entries.Add( Constant( "Picture." + pictures[ i ], ScriptType.Picture, "Layer", $"A sprite picture: {pictures[ i ]}.", $"local picked = Picture.{pictures[ i ]} --> Picture.{pictures[ i ]}", Handles.PictureBase + i ) );
	}
}
