using PlayerModels.Core.Script;

namespace PlayerModels.UI;

// The reference's first pages: what a script is, expressions, parameters; each with its clip and something to try
public partial class ScriptReferencePanel
{
	// A page to start from: what a script is, how expressions work, parameters; each with a clip and something to try
	sealed record Page( string Title, string Clip, string[] Text, string Example, ScriptKind Kind = ScriptKind.Effect );

	[Sandbox.SkipHotload]
	static readonly Page[] Pages =
	{
		new( "Script", "script", new[]
		{
			"Every effect is a script: a few lines that say what its layers are, where they come from and how they look, the same things the sliders and the Advanced editor set. Edit as code opens it, and changing either changes the other.",
			"Code adds what sliders can't: values worked out from what the player does (faster when running, brighter when hurt), something on every jump or key press, each sprite moved on its own (orbits, waves, formations), and options of your own for others to set.",
			"A script runs on every player's machine, within limits no script can get past: it can only reach its own effect, it stops at its budget, and the host can switch scripts off.",
		}, """
			Effect.Name = "Ember"
			Effect.Joint = Joint.RightHand

			local embers = Sprite()
			embers.ComesFrom = ComesFrom.Sphere
			embers.Spread = 2
			embers.LooksLike = Picture.Spark
			embers.Color = Color.Parse( "#ffb04a" )
			embers.Gravity = -30
			embers.Amount = 25

			function OnUpdate( dt )
				embers.Amount = 10 + speed / 4
			end
			""" ),
		new( "Expressions basics", "expressions", new[]
		{
			"An expression works a value out: 2 + speed / 100, Math.Sin( t * 4 ), if crouching then 0.5 else 1. Write one wherever a value goes.",
			"Inside OnUpdate it runs every frame, so the value follows the player: t is the seconds since the effect started, speed how fast they move, grounded and crouching what they do.",
			"Each sprite can work out its own: function layer.Particle( p ) runs for every sprite, with p.Index, p.Life and p.Seed to tell them apart.",
			"Type and the preview follows. A mistake is underlined where it is, with what is wrong; the effect keeps its last good version meanwhile.",
		}, """
			local glow = Sprite()
			glow.LooksLike = Picture.Glow
			glow.Color = Color.Parse( "#7af0ff" )
			glow.Amount = 30

			function OnUpdate( dt )
				glow.Size = 2 + Math.Sin( t * 4 ) + speed / 100
			end

			function glow.Particle( p )
				p.Color = Color.FromHsv( (p.Index * 30 + t * 90) % 360, 1, 1 )
			end
			""" ),
		new( "Parameters", "parameters", new[]
		{
			"[Parameter] on a variable at the top of a script makes it an option: it shows under the effect's sliders, where anyone can change it without opening the code.",
			"A number gets a slider, a colour a picker, a list of colours a palette, a key a key picker, Choices a dropdown. Min, Max, Step, Group and Tip say how.",
			"A value chosen is kept with the effect; Reset puts the script's own back and Save as default writes it into the script.",
		}, """
			[Parameter( "Glow size", Min = 0.5, Max = 10, Step = 0.1, Tip = "How big each glow is" )]
			local glowSize = 2.6

			[Parameter]
			local glowColor = Color.Parse( "#7af0ff" )

			local glow = Sprite()
			glow.Size = glowSize
			glow.Color = glowColor
			""" ),
		new( "Weapon skins", "skinscript", new[]
		{
			"A weapon's moving colour is a script too: in weapon mode, under the skin's Moving color, Write your own opens the colour picked as code, and the weapon follows as you type.",
			"It runs once for each band along the weapon (i of count), or at 64 places along it when it reads along (0 at its back, 1 at its front), and sets color and alpha (painted over the weapon, keeping its light and dark), glow, sheen and shade.",
			"Your skin keeps it, every player's machine runs it within the same budget as the stock ones, and Stock lets it go again.",
		}, """
			-- A heat haze: hot white at the muzzle cooling to deep red at the back, flickering as if it just fired
			local heat = Math.Pow( along, 2 ) * (0.8 + 0.2 * Math.Sin( t * 30 + along * 12 ))
			color = Color.Lerp( Color.Parse( "#3a0000" ), Color.Parse( "#fff2c0" ), heat )
			alpha = 0.3 + 0.6 * along
			glow = color * (heat * 0.8)
			""", ScriptKind.Skin ),
	};
}
