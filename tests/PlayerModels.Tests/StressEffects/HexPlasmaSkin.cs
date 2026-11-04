using PlayerModels.Core.Script;

namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>A weapon skin of hexagon tiles filled with moving plasma, a bright scan line sweeping up it.</summary>
	public static readonly StressEffect HexPlasmaSkin = new(
		"Hex plasma shield skin",
		"https://realtimevfx.com/t/reinhardt-shield-shader-tutorial-for-unity3d/7494",
		"A weapon skin of hexagon tiles filled with moving plasma, a bright scan line sweeping up it",
		"""
		-- Hex plasma skin: a honeycomb of thirty hexagon tiles for a weapon skin (an emblem, one tile a layer); the sticker
		-- script colours each tile by where it is.

		-- Rows of tiles, every other row shifted half a tile, filling the square
		local across = 6
		local rows = 5
		for row = 0, rows - 1 do
			for column = 0, across - 1 do
				local tile = shape.AddLayer( Shapes.Hexagon )
				local shift = if row % 2 == 1 then 0.5 else 0
				tile.Position = Vector3( (column + shift + 0.5) / across * 2 - 1, row / (rows - 1) * 1.6 - 0.8, 0 )
				tile.Scale = Vector3( 0.17, 0.17, 1 )
				tile.Rotation = 30
				tile.OutlineColor = Color.Parse( "#0a1a2a" )
				tile.OutlineWidth = 0.08
			end
		end
		""", ScriptKind.Shape,
		"""
		-- Hex plasma: each tile glows with plasma (sines of its place and the time added together, as the demoscene did),
		-- shifting round a hue, and a bright scan line sweeps up the skin lighting the tiles it passes.

		[Parameter( "Hue", Min = 0, Max = 360, Step = 5, Tip = "The middle of the plasma's colours, round the colour wheel" )]
		local hue = 195

		[Parameter( "Flow speed", Min = 0, Max = 4, Step = 0.1, Tip = "How fast the plasma moves" )]
		local flow = 1

		local x = origin.X * 3
		local y = origin.Y * 3
		local f = t * flow
		local plasma = (Math.Sin( x + f * 1.3 ) + Math.Sin( y * 1.7 - f ) + Math.Sin( (x + y) * 0.8 + f * 0.7 ) + Math.Sin( Math.Sqrt( x * x + y * y ) * 2 - f * 2 )) / 4
		local bright = plasma * 0.5 + 0.5
		color = Color.FromHsv( hue + plasma * 40, 0.85, 0.35 + 0.65 * bright )

		-- The scan line, sweeping up and starting again
		local scan = Math.Frac( t * 0.35 ) * 2.6 - 1.3
		color = Color.Lerp( color, Color.White, Math.Exp( -Math.Abs( origin.Y - scan ) * 10 ) * 0.8 )
		alpha = 0.75 + 0.25 * bright
		""" );
}
