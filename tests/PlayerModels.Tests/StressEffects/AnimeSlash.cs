namespace PlayerModels.Tests.StressEffects;

internal static partial class StressScripts
{
	/// <summary>A big crescent flashed along each swing, its leading tip wide and white-hot, its tail tapering to nothing, sweeping on as it fades, with speed lines flying off it.</summary>
	public static readonly StressEffect AnimeSlash = new(
		"Anime crescent slash",
		"https://jasontomlee.itch.io/slashfx/devlog/629732/tutorial-2-slash-shape-fundamentals",
		"A big crescent flashed along each swing, its leading tip wide and white-hot, its tail tapering to nothing, sweeping on as it fades, with speed lines flying off it",
		"""
		-- Anime slash: a big crescent flashed along each swing. Its leading tip is wide and its tail tapers to nothing behind
		-- it, a coloured glow round a white-hot core; it grows and sweeps on a little as it fades, with speed lines flying off.

		[Parameter( "Slash color", Group = "Look", Tip = "The colour of the glow round the core" )]
		local slashColor = Color.Parse( "#5ad8ff" )

		[Parameter( "Reach", Min = 10, Max = 24, Step = 1, Group = "Look", Tip = "How far the slash reaches from its middle" )]
		local reach = 24

		[Parameter( "Sweep", Min = 0, Max = 120, Step = 5, Group = "Rhythm", Tip = "Degrees it sweeps on as it fades" )]
		local sweep = 40

		Effect.Name = "Anime slash"
		Effect.Anchor = Anchor.HeldWeapon
		Effect.Joint = Joint.RightHand

		-- The crescent: 150 degrees of a circle from its tail round to its front (straight up), thickest near the front and
		-- tapering along to the tail (its thickness follows sin( π u³ ) along it)
		local arc = 150
		local steps = 32
		local crescent = Shapes.New( "Crescent slash" )
		for k = 0, steps do
			local angle = (90 - arc + arc * k / steps) * Math.PI / 180
			crescent.Point( Math.Cos( angle ) * 0.95, Math.Sin( angle ) * 0.95 )
		end

		for k = steps, 0, -1 do
			local along = k / steps
			local angle = (90 - arc + arc * along) * Math.PI / 180
			local inner = 0.95 - 0.35 * Math.Sin( Math.PI * along * along * along )
			crescent.Point( Math.Cos( angle ) * inner, Math.Sin( angle ) * inner )
		end

		crescent.EndOutline()

		-- Clear at its tail, whole by two thirds of the way round
		crescent.Fade( 90 - arc, 40 )

		-- The glow, lying in the swing's plane round the chest
		local glow = Sprite()
		glow.Joint = Joint.Chest
		glow.Offset = Vector3( 8, 0, 0 )
		glow.LooksLike = crescent
		glow.Color = slashColor
		glow.Size = reach * 2
		glow.Lifetime = 0.3
		glow.Amount = 0
		glow.Follow = true
		glow.Align = Align.Swing

		-- The white core, a little smaller inside it
		local core = Sprite()
		core.Joint = Joint.Chest
		core.Offset = Vector3( 8, 0, 0 )
		core.LooksLike = crescent
		core.Color = Color.White
		core.Size = reach * 1.8
		core.Lifetime = 0.3
		core.Amount = 0
		core.Follow = true
		core.Align = Align.Swing

		-- Both grow a little and sweep on as they fade, gone quickly
		function glow.Particle( p )
			p.Rotation = p.Life * sweep
			p.Size = 0.9 + p.Life * 0.2
			p.Alpha = 1 - p.Life * p.Life * p.Life
		end

		core.Particle = glow.Particle

		-- Speed lines flying off its edge
		local lines = Sprite()
		lines.Joint = Joint.Chest
		lines.ComesFrom = ComesFrom.Sphere
		lines.Spread = reach * 0.8
		lines.Offset = Vector3( 8, 0, 0 )
		lines.LooksLike = Picture.Spark
		lines.Color = Color.White
		lines.ColorEnd = slashColor
		lines.Size = 1.5
		lines.SizeEnd = 0.2
		lines.Speed = 120
		lines.Lifetime = 0.2
		lines.Amount = 0

		function OnShoot( shot )
			glow.Emit( 1 )
			core.Emit( 1 )
			lines.Emit( 14 )
		end
		""" );
}
