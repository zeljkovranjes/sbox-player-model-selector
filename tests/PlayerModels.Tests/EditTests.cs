using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using Xunit;

namespace PlayerModels.Tests;

/// <summary>
/// The visual editor edits scripts in place: random edits to every preset's script (written by codegen and by hand)
/// rebuild into exactly the edited effect, keeping every comment; fields worked out by code or parameters are refused
/// with their line; colours keep the form they were written in.
/// </summary>
public class EditTests
{
	public static IEnumerable<object[]> Sources()
	{
		foreach ( var (name, script) in PresetScripts.All )
		{
			yield return new object[] { name + " (hand-written)", script };
			yield return new object[] { name + " (codegen)", ScriptCodegen.Effect( ScriptKit.Preset( name ) ) };
		}
	}

	private static readonly PropertyId[] Editable =
	{
		PropertyId.LayerOff, PropertyId.LayerSpread, PropertyId.LayerOffset, PropertyId.LayerAngle, PropertyId.LayerColor, PropertyId.LayerColorEnd,
		PropertyId.LayerSize, PropertyId.LayerSizeEnd, PropertyId.LayerSpeed, PropertyId.LayerGravity, PropertyId.LayerLifetime, PropertyId.LayerAmount,
		PropertyId.LayerFollow, PropertyId.LayerSolid, PropertyId.LayerRhythm, PropertyId.LayerRhythmSpeed, PropertyId.LayerRhythmAmount,
		PropertyId.LayerFacing, PropertyId.LayerComesFrom, PropertyId.LayerLooksLike,
	};

	private static void Change( EffectLayer layer, PropertyId property, Random random )
	{
		float Number( float most ) => MathF.Round( (float)random.NextDouble() * most, 2 );
		string Hex() => $"#{random.Next( 0x1000000 ):x6}";
		switch ( property )
		{
			case PropertyId.LayerOff: layer.Off = !layer.Off; break;
			case PropertyId.LayerSpread: layer.Spread = Number( EffectLayer.MaxSpread ); break;
			case PropertyId.LayerOffset: (layer.Forward, layer.Left, layer.Up) = (Number( 10 ), -Number( 10 ), Number( 10 )); break;
			case PropertyId.LayerAngle: layer.Angle = Number( EffectLayer.MaxAngle ); break;
			case PropertyId.LayerColor: layer.Color = Hex(); break;
			case PropertyId.LayerColorEnd: layer.ColorEnd = Hex(); break;
			case PropertyId.LayerSize: layer.Size = layer.KindIs == LayerKind.Light ? 8 + Number( 300 ) : 0.1f + Number( 40 ); break;
			case PropertyId.LayerSizeEnd: layer.SizeEnd = layer.KindIs == LayerKind.Light ? null : 0.1f + Number( 40 ); break;
			case PropertyId.LayerSpeed: layer.Speed = Number( EffectLayer.MaxSpeed ); break;
			case PropertyId.LayerGravity: layer.Gravity = Number( 400 ) - 200; break;
			case PropertyId.LayerLifetime: layer.Lifetime = 0.1f + Number( 4 ); break;
			case PropertyId.LayerAmount: layer.Amount = Number( layer.KindIs == LayerKind.Light ? 4 : 100 ); break;
			case PropertyId.LayerFollow: layer.Follow = !layer.Follow; break;
			case PropertyId.LayerSolid: layer.Solid = !layer.Solid; break;
			case PropertyId.LayerRhythm: layer.Rhythm = Enum.GetNames<LayerRhythm>()[ random.Next( 1, 6 ) ]; break;
			case PropertyId.LayerRhythmSpeed: layer.RhythmSpeed = Number( 10 ); break;
			case PropertyId.LayerRhythmAmount: layer.RhythmAmount = Number( 1 ); break;
			case PropertyId.LayerFacing: layer.Facing = Enum.GetNames<LayerFacing>()[ random.Next( 1, 6 ) ]; break;
			case PropertyId.LayerComesFrom: layer.Shape = Enum.GetNames<LayerShape>()[ random.Next( 1, 6 ) ]; break;
			case PropertyId.LayerLooksLike: layer.Picture = Enum.GetNames<LayerPicture>()[ random.Next( 1, 8 ) ]; break;
		}
	}

	private static Effect Copy( Effect effect ) => JsonSerializer.Deserialize<Effect>( JsonSerializer.Serialize( effect ) );

	[Theory]
	[MemberData( nameof( Sources ) )]
	public void VisualEditsRebuildExactly( string name, string source )
	{
		var random = new Random( name.GetHashCode( StringComparison.Ordinal ) & 0xffff );
		for ( var round = 0; round < 6; round++ )
		{
			var (_, build) = ScriptKit.Build( source );
			var before = build.Effect;
			var after = Copy( before );
			after.Hue = MathF.Round( (float)random.NextDouble(), 2 );
			after.Name = "Edited " + round;
			for ( var edit = 0; edit < 5; edit++ )
			{
				var layer = random.Next( after.Layers.Count );
				var property = Editable[ random.Next( Editable.Length ) ];
				if ( build.SourceOf( layer, property ).Origin is FieldOrigin.Literal or FieldOrigin.Default )
					Change( after.Layers[ layer ], property, random );
			}

			var edited = ScriptEdit.Apply( source, build, before, after );
			Assert.True( edited.Succeeded, $"{edited.Problem} (line {edited.Line})" );
			ScriptKit.SameEffect( after, ScriptKit.BuildEffect( edited.Source ) );
			foreach ( var comment in Comments( source ) )
				Assert.Contains( comment, edited.Source );

			source = edited.Source;
		}
	}

	private static IEnumerable<string> Comments( string source ) =>
		Lexer.All( source, new LineMap( source ), out _ ).Where( x => x.Kind == TokenKind.Comment ).Select( x => source.Substring( x.Start, x.Length ) );

	private const string Sourced = """
		[Parameter]
		local tint = Color.Red

		local glow = Sprite()
		glow.Size = 2
		glow.Speed = 1 + 2
		for k = 1, 2 do
			glow.Amount = k
		end

		glow.Color = tint
		local other = Sprite()
		local both = 3
		glow.Spread = both
		other.Spread = 4

		function OnUpdate( dt )
			glow.Gravity = 5
		end
		""";

	[Fact]
	public void FieldsKnowWhereTheyComeFrom()
	{
		var (_, build) = ScriptKit.Build( Sourced );
		Assert.Equal( (FieldOrigin.Literal, 5), Origin( build, PropertyId.LayerSize ) );
		Assert.Equal( (FieldOrigin.Script, 6), Origin( build, PropertyId.LayerSpeed ) );
		Assert.Equal( (FieldOrigin.Script, 8), Origin( build, PropertyId.LayerAmount ) );
		Assert.Equal( (FieldOrigin.Parameter, 11), Origin( build, PropertyId.LayerColor ) );
		Assert.Equal( (FieldOrigin.Default, 0), Origin( build, PropertyId.LayerLifetime ) );
		var running = build.SourceOf( 0, PropertyId.LayerGravity );
		Assert.True( running.ChangesWhileRunning );
		Assert.Equal( 18, running.RunningLine );
		Assert.Equal( "tint", build.SourceOf( 0, PropertyId.LayerColor ).Site.Parameter );
	}

	private static (FieldOrigin, int) Origin( ScriptBuild build, PropertyId property ) => (build.SourceOf( 0, property ).Origin, build.SourceOf( 0, property ).Line);

	[Theory]
	[InlineData( PropertyId.LayerSpeed, "Set by script (line 6)" )]
	[InlineData( PropertyId.LayerColor, "Set by the parameter 'tint' (line 11): change the parameter" )]
	public void CodeAndParametersAreLeftAlone( PropertyId property, string problem )
	{
		var (_, build) = ScriptKit.Build( Sourced );
		var changed = Copy( build.Effect ).Layers[ 0 ];
		changed.Speed = 50;
		changed.Color = "#123456";
		var edit = ScriptEdit.SetLayer( Sourced, build, 0, property, changed );
		Assert.Equal( problem, edit.Problem );
		Assert.Equal( Sourced, edit.Source );
	}

	[Theory]
	[InlineData( "Color( 1, 0, 0 )", "Color( 0, 1, 0.502 )" )]
	[InlineData( "Color.Parse( \"#FF0000\" )", "Color.Parse( \"#00FF80\" )" )]
	[InlineData( "Color.FromHsv( 0, 1, 1 )", "Color.FromHsv( 150.1, 1, 1 )" )]
	[InlineData( "Color.Red", "Color.Parse( \"#00ff80\" )" )]
	public void AColourKeepsItsForm( string literal, string expected )
	{
		var source = $"local glow = Sprite()\nglow.Color = {literal} -- tint";
		var (_, build) = ScriptKit.Build( source );
		var changed = Copy( build.Effect ).Layers[ 0 ];
		changed.Color = "#00ff80";
		Assert.Equal( $"local glow = Sprite()\nglow.Color = {expected} -- tint", ScriptEdit.SetLayer( source, build, 0, PropertyId.LayerColor, changed ).Source );
	}

	[Fact]
	public void AFieldNotSetYetIsAddedAfterItsLayersOthers()
	{
		const string source = "local glow = Sprite()\nglow.Size = 2\n\n-- lights\nlocal lamp = Light()\n";
		var (_, build) = ScriptKit.Build( source );
		var changed = Copy( build.Effect ).Layers[ 0 ];
		changed.Gravity = -30;
		var edit = ScriptEdit.SetLayer( source, build, 0, PropertyId.LayerGravity, changed );
		Assert.Equal( "local glow = Sprite()\nglow.Size = 2\nglow.Gravity = -30\n\n-- lights\nlocal lamp = Light()\n", edit.Source );

		var effect = Copy( build.Effect );
		effect.Name = "Named";
		Assert.Equal( "Effect.Name = \"Named\"\n" + source, ScriptEdit.SetEffect( source, build, PropertyId.EffectName, effect ).Source );
	}

	[Fact]
	public void LayersAreAddedBeforeFunctionsAndRemovedWithTheirStatements()
	{
		const string source = "local glow = Sprite()\nglow.Size = 2\nlocal spark = Sprite()\nspark.Size = 1\n\nfunction OnUpdate( dt )\n\tglow.Size = 3\nend\n";
		var added = ScriptEdit.AddLayer( source, new EffectLayer { Kind = nameof( LayerKind.Light ), Size = 40 }.Sanitized() );
		Assert.Equal( "local glow = Sprite()\nglow.Size = 2\nlocal spark = Sprite()\nspark.Size = 1\n\nlocal light = Light()\nlight.Size = 40\n\nfunction OnUpdate( dt )\n\tglow.Size = 3\nend\n", added.Source );

		var (_, build) = ScriptKit.Build( source );
		Assert.Equal( "local glow = Sprite()\nglow.Size = 2\n\nfunction OnUpdate( dt )\n\tglow.Size = 3\nend\n", ScriptEdit.RemoveLayer( source, build, 1 ).Source );

		var refused = ScriptEdit.RemoveLayer( source, build, 0 );
		Assert.Equal( ("'glow' is used by the script here: remove it in code", 7), (refused.Problem, refused.Line) );
	}

	[Fact]
	public void AddedLayersComeThroughApply()
	{
		var source = PresetScripts.Hearts;
		var (_, build) = ScriptKit.Build( source );
		var after = Copy( build.Effect );
		after.Layers.Add( new EffectLayer { Kind = nameof( LayerKind.Light ), Color = "#ff0000", Size = 50 }.Sanitized() );
		var edited = ScriptEdit.Apply( source, build, build.Effect, after );
		ScriptKit.SameEffect( after, ScriptKit.BuildEffect( edited.Source ) );
	}
}
