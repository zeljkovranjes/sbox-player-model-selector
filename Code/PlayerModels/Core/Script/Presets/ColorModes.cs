namespace PlayerModels.Core.Script;

public static partial class PresetScripts
{
	/// <summary>The palette's colours one to each element in turn, kept for its life (confetti).</summary>
	public const string Mix = """
		-- Mix: each element takes the palette's colours in turn, and keeps its colour for its life.
		color = palette[ i % palette.Count ]
		""";

	/// <summary>Each element fading from a start colour to an end colour over its life.</summary>
	public const string Fade = """
		-- Fade: each element goes from the start colour to the end colour over its life.

		[Parameter( "Start color" )]
		local startColor = Color.White

		[Parameter( "End color" )]
		local endColor = Color.White

		color = Color.Lerp( startColor, endColor, life )
		""";

	/// <summary>Brighter and dimmer, smoothly, like breathing.</summary>
	public const string Pulse = """
		-- Pulse: brighter and dimmer, smoothly, like breathing. A beat is one bright-dim-bright.

		[Parameter( "Speed", Min = 0, Max = 10, Step = 0.05, Tip = "Beats a second" )]
		local speed = 1

		[Parameter( "Amount", Min = 0, Max = 1, Step = 0.05, Tip = "How much dimmer it gets" )]
		local amount = 0.5

		[Parameter( "Phase", Tip = "How far into its beat it starts" )]
		local phase = 0

		local beat = t * speed + phase
		local level = 1 - amount * (0.5 - 0.5 * Math.Cos( beat * Math.PI * 2 ))
		color = Color( level, level, level )
		""";

	/// <summary>Uneven brightness, like a flame: three waves out of step.</summary>
	public const string Flicker = """
		-- Flicker: uneven brightness, like a flame, from three waves out of step with each other.

		[Parameter( "Speed", Min = 0, Max = 10, Step = 0.05, Tip = "Flickers a second" )]
		local speed = 1

		[Parameter( "Amount", Min = 0, Max = 1, Step = 0.05, Tip = "How much dimmer it gets" )]
		local amount = 0.5

		[Parameter( "Phase", Tip = "How far into its beat it starts" )]
		local phase = 0

		local turn = (t * speed + phase) * Math.PI * 2
		local wave = 0.5 * Math.Sin( turn ) + 0.3 * Math.Sin( turn * 2.3 + 1.3 ) + 0.2 * Math.Sin( turn * 5.1 + 0.7 )
		local level = 1 - amount * (0.5 + 0.5 * wave)
		color = Color( level, level, level )
		""";

	/// <summary>Through every colour of the wheel, once a beat.</summary>
	public const string RainbowCycle = """
		-- Rainbow: once round the colour wheel each beat.

		[Parameter( "Speed", Min = 0, Max = 10, Step = 0.05, Tip = "Turns of the wheel a second" )]
		local speed = 1

		[Parameter( "Phase", Tip = "How far round it starts" )]
		local phase = 0

		local beat = t * speed + phase
		color = Color.FromHsv( beat % 1 * 360, 1, 1 )
		""";

	/// <summary>Turning round its place once a beat.</summary>
	public const string Spin = """
		-- Spin: once round its place each beat (a layer set off from its joint circles it).

		[Parameter( "Speed", Min = 0, Max = 10, Step = 0.05, Tip = "Turns a second" )]
		local speed = 1

		[Parameter( "Phase", Tip = "How far round it starts" )]
		local phase = 0

		local beat = t * speed + phase
		rotation = beat % 1 * 360
		""";

	/// <summary>A smooth wander about its place, each way out of step with the others.</summary>
	public const string Jiggle = """
		-- Jiggle: a smooth wander about its place, each way out of step with the others, within a ball.

		[Parameter( "Speed", Min = 0, Max = 10, Step = 0.05, Tip = "Wobbles a second" )]
		local speed = 1

		[Parameter( "Amount", Min = 0, Max = 1, Step = 0.05, Tip = "How far it wanders, out of 4 units" )]
		local amount = 0.5

		[Parameter( "Phase", Tip = "How far into its wobble it starts" )]
		local phase = 0

		local turn = (t * speed + phase) * Math.PI * 2
		local wander = Vector3( Math.Sin( turn ) * 0.6, Math.Sin( turn * 1.3 + 1.7 ) * 0.6, Math.Sin( turn * 0.7 + 0.4 ) * 0.5 )
		offset = wander * (amount * 4)
		""";

	/// <summary>
	/// The script a layer's colour mode runs per element: <see cref="Fade"/> for fixed colours, <see cref="Mix"/>, or a
	/// World at War cycle.
	/// </summary>
	public static string ColorModeScript( LayerColorMode mode ) => mode switch
	{
		LayerColorMode.Rain => Rain,
		LayerColorMode.Cycl => Cycl,
		LayerColorMode.Cyln => Cyln,
		LayerColorMode.Mix => Mix,
		_ => Fade,
	};

	/// <summary>The script a layer's rhythm runs (its Speed, Amount and Phase given as parameters); null for steady.</summary>
	public static string RhythmScript( LayerRhythm rhythm ) => rhythm switch
	{
		LayerRhythm.Pulse => Pulse,
		LayerRhythm.Flicker => Flicker,
		LayerRhythm.Rainbow => RainbowCycle,
		LayerRhythm.Spin => Spin,
		LayerRhythm.Jiggle => Jiggle,
		_ => null,
	};
}
