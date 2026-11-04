using System.Collections.Generic;
using System.Text.Json;

namespace PlayerModels.Core;

/// <summary>
/// The effects the Effects page offers to start from. Made on the citizen, in its sizes: a body of another size gets
/// them in proportion, and joints a model lacks are stood in for, so each shows on any model.
/// </summary>
public static class EffectPresets
{
	// Confetti's colours, one to each piece in turn. Before All, which is made from it: statics start in order
	private static readonly string[] ConfettiColours = { "#ff4a6a", "#ffd23a", "#3ad8ff", "#7aff6a", "#c07aff" };

	/// <summary>Every preset, in the order they are offered. Never worn as they are: see <see cref="Copy"/>.</summary>
	public static IReadOnlyList<Effect> All { get; } = new[]
	{
		GlowingEyes(), FireHands(), SparkleHalo(), HolyAura(), DarkAura(), Hearts(), SnowCloud(), RainCloud(),
		ElectricBody(), MagicOrbs(), FlameHead(), LanternHand(), SpeedTrail(), SwordSlash(), ConfettiBurst(), Rainbow(),
	};

	/// <summary>A copy of <paramref name="preset"/> to wear beside <paramref name="worn"/>, with an id none of them has.</summary>
	public static Effect Copy( Effect preset, IReadOnlyList<Effect> worn )
	{
		var copy = JsonSerializer.Deserialize<Effect>( JsonSerializer.Serialize( preset ) ).Sanitized();
		copy.Id = Effect.NewId( worn );
		return copy;
	}

	/// <summary>What a new effect made from scratch is called, then a number none worn has.</summary>
	public const string NewName = "My effect";

	/// <summary>
	/// An effect to build from scratch, to wear beside <paramref name="worn"/>: "My effect" and a number none of them has,
	/// from the chest, with one layer of sparkles so something shows from the start (see <see cref="StartingLayer"/>).
	/// </summary>
	public static Effect New( IReadOnlyList<Effect> worn )
	{
		var number = 1;
		while ( Named( worn, $"{NewName} {number}" ) )
			number++;

		var effect = Made( $"{NewName} {number}", Effect.Joint.Chest, StartingLayer( LayerKind.Sprite ) ).Sanitized();
		effect.Id = Effect.NewId( worn );
		return effect;
	}

	/// <summary>A layer of <paramref name="kind"/> as one is added: something that shows at once, to change from there.</summary>
	public static EffectLayer StartingLayer( LayerKind kind ) => kind switch
	{
		LayerKind.Light => new EffectLayer { Kind = nameof( LayerKind.Light ), Color = "#ffd27a", Size = 60f, Amount = 1f },
		LayerKind.Trail => new EffectLayer { Kind = nameof( LayerKind.Trail ), Color = "#8af4ff", ColorEnd = "#2a5aff", Size = 3f, SizeEnd = 0.5f, Lifetime = 0.4f },
		_ => new EffectLayer
		{
			Kind = nameof( LayerKind.Sprite ), Picture = nameof( LayerPicture.Sparkle ), Shape = nameof( LayerShape.Sphere ), Spread = 3f, Color = "#ffd27a",
			Size = 2f, SizeEnd = 0.5f, Speed = 10f, Lifetime = 0.8f, Amount = 20f,
		},
	};

	private static bool Named( IReadOnlyList<Effect> worn, string name )
	{
		for ( var i = 0; i < worn.Count; i++ )
		{
			if ( worn[ i ]?.Name == name )
				return true;
		}

		return false;
	}

	private static Effect Made( string name, Effect.Joint joint, params EffectLayer[] layers )
	{
		return new Effect { Name = name, Anchor = new EffectAnchor { Joint = joint.ToString() }, Layers = new List<EffectLayer>( layers ) };
	}

	private static EffectLayer Sprite( LayerPicture picture, string colour, string colourEnd, float size, float sizeEnd, float lifetime, float amount )
	{
		return new EffectLayer
		{
			Kind = nameof( LayerKind.Sprite ), Picture = picture.ToString(), Color = colour, ColorEnd = colourEnd, Size = size, SizeEnd = sizeEnd,
			Lifetime = lifetime, Amount = amount,
		};
	}

	private static EffectLayer Light( string colour, float reach, float brightness )
	{
		return new EffectLayer { Kind = nameof( LayerKind.Light ), Color = colour, Size = reach, Amount = brightness };
	}

	// A trail width wide where it starts and widthEnd at its end, staying behind for lifetime seconds, as much as its
	// place moves
	private static EffectLayer Trail( string colour, string colourEnd, float width, float widthEnd, float lifetime )
	{
		return new EffectLayer
		{
			Kind = nameof( LayerKind.Trail ), Color = colour, ColorEnd = colourEnd, Size = width, SizeEnd = widthEnd, Lifetime = lifetime,
			FadeWithSpeed = true,
		};
	}

	private static Effect From( this Effect effect, AnchorKind kind, Effect.Joint fallback )
	{
		effect.Anchor = new EffectAnchor { Kind = kind.ToString(), Joint = fallback.ToString() };
		return effect;
	}

	// Shows for a moment, burst sprites at once, and for showFor seconds after
	private static EffectLayer At( this EffectLayer layer, LayerWhen when, float burst = 0f, float showFor = 0f )
	{
		layer.When = when.ToString();
		layer.Burst = burst;
		layer.ShowFor = showFor;
		return layer;
	}

	private static EffectLayer While( this EffectLayer layer, LayerWhen when )
	{
		layer.When = when.ToString();
		return layer;
	}

	private static EffectLayer Looking( this EffectLayer layer, StandardShape shape )
	{
		layer.Picture = shape.ToString();
		return layer;
	}

	private static EffectLayer Palette( this EffectLayer layer, LayerColorMode mode, params string[] colours )
	{
		layer.ColorMode = mode.ToString();
		layer.Palette = colours.Length > 0 ? new List<string>( colours ) : null;
		return layer;
	}

	private static EffectLayer Beating( this EffectLayer layer, LayerRhythm rhythm, float speed, float amount )
	{
		layer.Rhythm = rhythm.ToString();
		layer.RhythmSpeed = speed;
		layer.RhythmAmount = amount;
		return layer;
	}

	private static EffectLayer From( this EffectLayer layer, LayerShape shape, float spread, float forward = 0f, float left = 0f, float up = 0f )
	{
		layer.Shape = shape.ToString();
		layer.Spread = spread;
		layer.Forward = forward;
		layer.Left = left;
		layer.Up = up;
		return layer;
	}

	private static EffectLayer On( this EffectLayer layer, Effect.Joint joint )
	{
		layer.Joint = joint.ToString();
		return layer;
	}

	private static EffectLayer Moving( this EffectLayer layer, float speed, float gravity, bool follow = false )
	{
		layer.Speed = speed;
		layer.Gravity = gravity;
		layer.Follow = follow;
		return layer;
	}

	private static EffectLayer Cycling( this EffectLayer layer, LayerColorMode mode )
	{
		layer.ColorMode = mode.ToString();
		return layer;
	}

	private static EffectLayer Covering( this EffectLayer layer )
	{
		layer.Solid = true;
		return layer;
	}

	// A glow in front of each eye (in front of glasses, too), and a soft light on the face
	private static Effect GlowingEyes() => Made( "Glowing eyes", Effect.Joint.Eyes,
		Sprite( LayerPicture.Glow, "#7af0ff", "#2aa8ff", 2.6f, 1.6f, 0.3f, 16f ).On( Effect.Joint.LeftEye ).From( LayerShape.Point, 0f, 3f )
			.Moving( 0f, 0f, true ).Beating( LayerRhythm.Pulse, 1.2f, 0.35f ),
		Sprite( LayerPicture.Glow, "#7af0ff", "#2aa8ff", 2.6f, 1.6f, 0.3f, 16f ).On( Effect.Joint.RightEye ).From( LayerShape.Point, 0f, 3f )
			.Moving( 0f, 0f, true ).Beating( LayerRhythm.Pulse, 1.2f, 0.35f ),
		Light( "#5ad8ff", 28f, 0.5f ).From( LayerShape.Point, 0f, 5f ).Beating( LayerRhythm.Pulse, 1.2f, 0.35f ) );

	// Flames rising from each fist, smoke above them and a flickering light in each hand
	private static Effect FireHands() => Made( "Fire hands", Effect.Joint.LeftHand,
		Sprite( LayerPicture.Flame, "#ffd25a", "#ff3d00", 5.5f, 1.2f, 0.5f, 40f ).On( Effect.Joint.LeftHand ).From( LayerShape.Sphere, 2f, 0f, 3f )
			.Moving( 4f, -110f ),
		Sprite( LayerPicture.Flame, "#ffd25a", "#ff3d00", 5.5f, 1.2f, 0.5f, 40f ).On( Effect.Joint.RightHand ).From( LayerShape.Sphere, 2f, 0f, -3f )
			.Moving( 4f, -110f ),
		Sprite( LayerPicture.Smoke, "#2a2624", "#5e5955", 3f, 8f, 1.1f, 6f ).On( Effect.Joint.LeftHand ).From( LayerShape.Sphere, 1.5f, 0f, 3f, 5f )
			.Moving( 2f, -40f ).Covering(),
		Sprite( LayerPicture.Smoke, "#2a2624", "#5e5955", 3f, 8f, 1.1f, 6f ).On( Effect.Joint.RightHand ).From( LayerShape.Sphere, 1.5f, 0f, -3f, 5f )
			.Moving( 2f, -40f ).Covering(),
		Light( "#ff7a1f", 60f, 1.2f ).On( Effect.Joint.LeftHand ).From( LayerShape.Point, 0f, 0f, 3f ).Beating( LayerRhythm.Flicker, 8f, 0.35f ),
		Light( "#ff7a1f", 60f, 1.2f ).On( Effect.Joint.RightHand ).From( LayerShape.Point, 0f, 0f, -3f ).Beating( LayerRhythm.Flicker, 8f, 0.35f ) );

	// A turning ring of stars over the head, on a faint ring of light
	private static Effect SparkleHalo() => Made( "Sparkle halo", Effect.Joint.Crown,
		Sprite( LayerPicture.Sparkle, "#fff4b0", "#ffc94d", 2.2f, 0.3f, 1f, 22f ).From( LayerShape.Ring, 6f, 0f, 0f, 4f ).Moving( 0f, 0f, true )
			.Beating( LayerRhythm.Spin, 0.3f, 1f ),
		Sprite( LayerPicture.Glow, "#ffe27a", "#b88a2a", 2.4f, 1.6f, 0.6f, 30f ).From( LayerShape.Ring, 6f, 0f, 0f, 4f ).Moving( 0f, 0f, true )
			.Beating( LayerRhythm.Spin, 0.3f, 1f ) );

	// A soft warm glow all around the body, sparkles drifting up through it, and a light that breathes with it
	private static Effect HolyAura() => Made( "Holy aura", Effect.Joint.Hips,
		Sprite( LayerPicture.Glow, "#b89a5a", "#7a5a24", 9f, 15f, 1.6f, 16f ).From( LayerShape.AroundBody, 34f ).Moving( 0f, 0f, true )
			.Beating( LayerRhythm.Pulse, 0.5f, 0.3f ),
		Sprite( LayerPicture.Sparkle, "#fff6d0", "#ffd27a", 1.6f, 0.3f, 1.5f, 10f ).From( LayerShape.AroundBody, 34f ).Moving( 2f, -20f ),
		Light( "#ffe2a0", 90f, 0.8f ).From( LayerShape.Point, 0f, 0f, 0f, 10f ).Beating( LayerRhythm.Pulse, 0.5f, 0.3f ) );

	// Dark smoke rolling off the body, big enough to read as smoke in daylight, with a purple glow inside it that shows it
	// on a dark night
	private static Effect DarkAura() => Made( "Dark aura", Effect.Joint.Hips,
		Sprite( LayerPicture.Smoke, "#2a1238", "#08040c", 14f, 22f, 2.4f, 18f ).From( LayerShape.AroundBody, 32f ).Moving( 3f, -5f ).Covering(),
		Sprite( LayerPicture.Glow, "#6a2a9a", "#1a0628", 8f, 13f, 1.8f, 9f ).From( LayerShape.AroundBody, 30f ).Moving( 0f, 0f, true ) );

	// Hearts floating up from the chest
	private static Effect Hearts() => Made( "Hearts", Effect.Joint.Chest,
		Sprite( LayerPicture.Heart, "#ff3d7f", "#ff6f9f", 2.6f, 3.4f, 2.4f, 5f ).From( LayerShape.Sphere, 3f, 5f ).Moving( 6f, -8f ).Covering() );

	// A little white cloud over the head, snowing
	private static Effect SnowCloud() => Made( "Snow cloud", Effect.Joint.Crown,
		Sprite( LayerPicture.Smoke, "#f4f6fa", "#d8dde6", 8f, 10f, 2f, 8f ).From( LayerShape.Sphere, 6f, 0f, 0f, 10f ).Moving( 0f, 0f, true ).Covering(),
		Sprite( LayerPicture.Snowflake, "#ffffff", null, 2f, 1.6f, 2f, 18f ).From( LayerShape.Sphere, 7f, 0f, 0f, 8f ).Moving( 0f, 25f ).Covering() );

	// A dark cloud over the head, raining
	private static Effect RainCloud() => Made( "Rain cloud", Effect.Joint.Crown,
		Sprite( LayerPicture.Smoke, "#5a6270", "#3a404a", 8f, 10f, 2f, 8f ).From( LayerShape.Sphere, 6f, 0f, 0f, 10f ).Moving( 0f, 0f, true ).Covering(),
		Sprite( LayerPicture.Droplet, "#9fc6ff", null, 3.5f, 3.5f, 0.5f, 40f ).From( LayerShape.Sphere, 6f, 0f, 0f, 7f ).Moving( 0f, 260f ) );

	// Sparks crackling all over the body, flashes of blue between them, and a flickering blue light
	private static Effect ElectricBody() => Made( "Electric body", Effect.Joint.Hips,
		Sprite( LayerPicture.Sparkle, "#ffffff", "#7ab8ff", 6f, 1f, 0.3f, 70f ).From( LayerShape.AroundBody, 32f ).Moving( 40f, 0f )
			.Beating( LayerRhythm.Flicker, 12f, 0.4f ),
		Sprite( LayerPicture.Glow, "#9ccaff", "#3a6aff", 14f, 6f, 0.12f, 40f ).From( LayerShape.AroundBody, 30f ).Moving( 0f, 0f, true )
			.Beating( LayerRhythm.Flicker, 14f, 0.6f ),
		Light( "#5aa0ff", 70f, 1.2f ).On( Effect.Joint.Chest ).Beating( LayerRhythm.Flicker, 14f, 0.8f ) );

	// Two glowing orbs circling the hips, leaving a short streak, and a third bobbing about beside the head
	private static Effect MagicOrbs() => Made( "Magic orbs", Effect.Joint.Hips,
		Sprite( LayerPicture.Glow, "#c08aff", "#5a2aff", 3.4f, 0.8f, 0.4f, 45f ).From( LayerShape.Point, 0f, 16f, 0f, 6f )
			.Beating( LayerRhythm.Spin, 0.45f, 1f ),
		Sprite( LayerPicture.Glow, "#7ad8ff", "#2a6aff", 3.4f, 0.8f, 0.4f, 45f ).From( LayerShape.Point, 0f, 16f, 0f, -2f )
			.Beating( LayerRhythm.Spin, 0.45f, 1f ),
		Sprite( LayerPicture.Glow, "#ff8ae0", "#ff2a8a", 3f, 1f, 0.35f, 35f ).On( Effect.Joint.Head ).From( LayerShape.Point, 0f, 2f, 11f, 4f )
			.Beating( LayerRhythm.Jiggle, 0.8f, 0.7f ),
		Light( "#a08aff", 60f, 0.8f ).From( LayerShape.Point, 0f, 0f, 0f, 4f ).Beating( LayerRhythm.Pulse, 0.6f, 0.3f ) );

	// A streak behind each foot as you run, and sparks kicked up while moving
	private static Effect SpeedTrail() => Made( "Speed trail", Effect.Joint.Hips,
		Trail( "#8af4ff", "#2a5aff", 5f, 1f, 0.7f ).On( Effect.Joint.LeftFoot ).From( LayerShape.Point, 0f, 0f, 0f, 1f ),
		Trail( "#8af4ff", "#2a5aff", 5f, 1f, 0.7f ).On( Effect.Joint.RightFoot ).From( LayerShape.Point, 0f, 0f, 0f, 1f ),
		Sprite( LayerPicture.Spark, "#d8fcff", "#3a8aff", 1.6f, 0.4f, 0.35f, 30f ).On( Effect.Joint.LeftFoot ).From( LayerShape.Sphere, 2f )
			.Moving( 30f, 60f ).While( LayerWhen.Moving ),
		Sprite( LayerPicture.Spark, "#d8fcff", "#3a8aff", 1.6f, 0.4f, 0.35f, 30f ).On( Effect.Joint.RightFoot ).From( LayerShape.Sphere, 2f )
			.Moving( 30f, 60f ).While( LayerWhen.Moving ) );

	// A bright arc behind the tip of the weapon held as it swings, and sparks when it strikes
	private static Effect SwordSlash() => Made( "Sword slash", Effect.Joint.RightHand,
			Trail( "#ffffff", "#5a9aff", 5f, 0.5f, 0.25f ),
			Sprite( LayerPicture.Spark, "#fff4c8", "#ff9a3a", 1.8f, 0.4f, 0.45f, 0f ).From( LayerShape.Sphere, 1f ).Moving( 90f, 220f )
				.At( LayerWhen.OnShoot, 16f ) )
		.From( AnchorKind.HeldWeapon, Effect.Joint.RightHand );

	// Confetti thrown up when you jump, and a ring of it when you land
	private static Effect ConfettiBurst() => Made( "Confetti burst", Effect.Joint.Chest,
		Sprite( LayerPicture.Glow, "#ffffff", null, 1.6f, 1.6f, 1.8f, 0f ).Looking( StandardShape.Rectangle ).From( LayerShape.Sphere, 4f )
			.Moving( 140f, 220f ).Covering().At( LayerWhen.OnJump, 50f ).Palette( LayerColorMode.Mix, ConfettiColours ),
		Sprite( LayerPicture.Glow, "#ffffff", null, 1.4f, 1.4f, 1.4f, 0f ).Looking( StandardShape.Star5 ).On( Effect.Joint.Hips )
			.From( LayerShape.Ring, 10f, 0f, 0f, -20f ).Moving( 90f, 160f ).Covering().At( LayerWhen.OnLand, 40f ).Palette( LayerColorMode.Mix, ConfettiColours ) );

	// The head on fire, lighting up around it
	private static Effect FlameHead() => Made( "Flame head", Effect.Joint.Crown,
		Sprite( LayerPicture.Flame, "#ffd25a", "#ff3300", 7f, 1.5f, 0.6f, 40f ).From( LayerShape.Sphere, 4f, 0f, 0f, -1f ).Moving( 5f, -100f ),
		Light( "#ff8a2a", 70f, 1.2f ).From( LayerShape.Point, 0f, 0f, 0f, 2f ).Beating( LayerRhythm.Flicker, 8f, 0.3f ) );

	// A warm lantern glow held in the left hand, lighting the way
	private static Effect LanternHand() => Made( "Lantern hand", Effect.Joint.LeftHand,
		Sprite( LayerPicture.Glow, "#ffb04a", "#ff8a1a", 5f, 5f, 0.6f, 10f ).From( LayerShape.Point, 0f, 0f, 4f ).Moving( 0f, 0f, true )
			.Beating( LayerRhythm.Flicker, 3f, 0.15f ),
		Sprite( LayerPicture.Glow, "#fff0c8", "#ffd08a", 2f, 2f, 0.4f, 10f ).From( LayerShape.Point, 0f, 0f, 4f ).Moving( 0f, 0f, true ),
		Light( "#ffb35a", 140f, 1.4f ).From( LayerShape.Point, 0f, 0f, 4f ).Beating( LayerRhythm.Flicker, 3f, 0.12f ) );

	// World at War's rain: the game's colours scrolling round a turning ring at the waist, sparkles rising through it
	private static Effect Rainbow() => Made( "Rainbow", Effect.Joint.Hips,
		Sprite( LayerPicture.Glow, "#ffffff", null, 3.5f, 2.5f, 1.2f, 20f ).From( LayerShape.Ring, 14f, 0f, 0f, 4f ).Moving( 0f, 0f, true )
			.Beating( LayerRhythm.Spin, 0.25f, 1f ).Cycling( LayerColorMode.Rain ),
		Sprite( LayerPicture.Sparkle, "#ffffff", null, 2.2f, 0.4f, 1.4f, 16f ).From( LayerShape.AroundBody, 30f ).Moving( 2f, -18f )
			.Cycling( LayerColorMode.Rain ) );
}
