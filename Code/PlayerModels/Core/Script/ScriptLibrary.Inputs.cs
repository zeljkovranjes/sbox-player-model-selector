using System.Collections.Generic;

namespace PlayerModels.Core.Script;

public static partial class ScriptLibrary
{
	private static void AddInputs( List<LibraryEntry> entries )
	{
		const string inputs = "Inputs", sticker = "Sticker", particle = "Particle", shot = "Shot";
		var run = ScriptPlace.Run;
		var element = ScriptPlace.StickerRun;
		var b = ScriptType.Bool;
		var v = ScriptType.Vector3;
		var c = ScriptType.Color;
		entries.Add( Input( "time", N, ScriptInput.Time, inputs, "Milliseconds since the effect started (whole steps of the game's clock).", "local step = Math.Floor( time / 100 ) --> 15", run ) );
		entries.Add( Input( "t", N, ScriptInput.Seconds, inputs, "Seconds since the effect started.", "local seconds = t --> 1.5", run ) );
		entries.Add( Input( "speed", N, ScriptInput.Speed, inputs, "How fast the body moves, in units a second.", "local running = speed > 100 --> true", run ) );
		entries.Add( Input( "velocity", v, ScriptInput.VelocityX, inputs, "Which way and how fast the body moves (X forwards, Y left, Z up).", "local sideways = velocity.Y --> 120", run ) );
		entries.Add( Input( "grounded", b, ScriptInput.Grounded, inputs, "Whether the body stands on the ground.", "local onGround = grounded --> true", run ) );
		entries.Add( Input( "crouching", b, ScriptInput.Crouching, inputs, "Whether the body crouches.", "local low = crouching --> false", run ) );
		entries.Add( Input( "moving", b, ScriptInput.Moving, inputs, "Whether the body moves.", "local going = moving --> true", run ) );
		entries.Add( Input( "inAir", b, ScriptInput.InAir, inputs, "Whether the body is in the air.", "local flying = inAir --> false", run ) );
		entries.Add( Input( "holdingWeapon", b, ScriptInput.HoldingWeapon, inputs, "Whether the body holds a weapon.", "local armed = holdingWeapon --> false", run ) );
		entries.Add( Input( "anchorSpeed", N, ScriptInput.AnchorSpeed, inputs, "How fast the effect's own place moves (a swung weapon's tip, a running foot).", "local swing = anchorSpeed / 100 --> 0.8", run ) );

		entries.Add( Input( "i", N, ScriptInput.Index, sticker, "Which element this run colours, from 0: a particle, a shape point, an emblem layer.", "local second = i == 1 --> false", element ) );
		entries.Add( Input( "count", N, ScriptInput.Count, sticker, "How many elements there are.", "local last = i == count - 1 --> false", element ) );
		entries.Add( Input( "life", N, ScriptInput.Life, sticker, "How far through its life the element is, 0 to 1 (a particle's; 0 for a shape's points).", "local young = life < 0.5 --> true", element ) );
		entries.Add( Reserved( "palette", ScriptType.ListOf( c ), PaletteSlot, sticker, "The sticker's palette (the game's five colours unless it has its own).", "local first = palette[ 0 ] --> #ff3333\nlocal n = palette.Count --> 5" ) );
		entries.Add( Reserved( "highlight", c, HighlightSlot, sticker, "The Cyln scanner's highlight: the palette's first colour when it has two or more, else red.", "local eye = highlight --> #ff3333" ) );
		entries.Add( Reserved( "rest", c, RestSlot, sticker, "The Cyln scanner's other colour: the palette's second when it has two or more, else white.", "local other = rest --> #ffffff" ) );
		entries.Add( Output( "color", c, ScriptOutput.ColorR, sticker, "The element's colour (write it to change it).", "color = Color.Red\nlocal now = color --> #ff0000", element, 0, 1 ) );
		entries.Add( Output( "alpha", N, ScriptOutput.Alpha, sticker, "How solid the element is, 0 to 1.", "alpha = 0.5\nlocal now = alpha --> 0.5", element, 0, 1 ) );
		entries.Add( Output( "size", N, ScriptOutput.Size, sticker, "How big the element is, times its own size.", "size = 1.5\nlocal now = size --> 1.5", element, 0, EffectLayer.MaxSize ) );
		entries.Add( Output( "rotation", N, ScriptOutput.Rotation, sticker, "How far the element is turned, in degrees.", "rotation = 90\nlocal now = rotation --> 90", element, -MaxRotation, MaxRotation ) );
		entries.Add( Output( "offset", v, ScriptOutput.OffsetX, sticker, "How far the element is moved from its place.", "offset = Vector3( 0, 0, 1 )\nlocal now = offset --> Vector3( 0, 0, 1 )", element, -EffectLayer.MaxOffset, EffectLayer.MaxOffset ) );

		var particleRun = ScriptPlace.EffectRun;
		const string setup = "local glow = Sprite()\nfunction glow.Particle( p )\n\t";
		entries.Add( Member( "Particle.Index", N, PropertyAccess.Input, (int)ScriptInput.Index, particle, "Which sprite this is, from 0, in the order they appeared.", setup + "local even = p.Index % 2 == 0\nend", particleRun ) );
		entries.Add( Member( "Particle.Count", N, PropertyAccess.Input, (int)ScriptInput.Count, particle, "How many sprites the layer has out.", setup + "local half = p.Count / 2\nend", particleRun ) );
		entries.Add( Member( "Particle.Age", N, PropertyAccess.Input, (int)ScriptInput.Age, particle, "How long the sprite has been out, in seconds.", setup + "p.Size = 1 + p.Age\nend", particleRun ) );
		entries.Add( Member( "Particle.Life", N, PropertyAccess.Input, (int)ScriptInput.Life, particle, "How far through its life it is, 0 to 1.", setup + "p.Alpha = 1 - p.Life\nend", particleRun ) );
		entries.Add( Member( "Particle.Color", c, PropertyAccess.Output, (int)ScriptOutput.ColorR, particle, "Its colour now.", setup + "p.Color = Color.FromHsv( p.Index * 30, 1, 1 )\nend", particleRun, true, 0, 1 ) );
		entries.Add( Member( "Particle.Alpha", N, PropertyAccess.Output, (int)ScriptOutput.Alpha, particle, "How solid it is now, 0 to 1.", setup + "p.Alpha = 0.5\nend", particleRun, true, 0, 1 ) );
		entries.Add( Member( "Particle.Size", N, PropertyAccess.Output, (int)ScriptOutput.Size, particle, "How big it is now, times its own size.", setup + "p.Size = 2\nend", particleRun, true, 0, EffectLayer.MaxSize ) );
		entries.Add( Member( "Particle.Rotation", N, PropertyAccess.Output, (int)ScriptOutput.Rotation, particle, "How far it is turned now, in degrees.", setup + "p.Rotation = t * 90\nend", particleRun, true, -MaxRotation, MaxRotation ) );
		entries.Add( Member( "Particle.Offset", v, PropertyAccess.Output, (int)ScriptOutput.OffsetX, particle, "How far it is moved from where it would be.", setup + "p.Offset = Vector3( 0, 0, Math.Sin( t ) )\nend", particleRun, true, -EffectLayer.MaxOffset, EffectLayer.MaxOffset ) );

		const string shotSetup = "local sparks = Sprite()\nfunction OnShoot( shot )\n\t";
		entries.Add( Member( "Shot.Origin", v, PropertyAccess.Input, (int)ScriptInput.ShotOriginX, shot, "Where the shot left the weapon (its muzzle), in the world.", shotSetup + "sparks.EmitAt( shot.Origin, 4 )\nend", particleRun ) );
		entries.Add( Member( "Shot.Direction", v, PropertyAccess.Input, (int)ScriptInput.ShotDirectionX, shot, "Which way it went: the aim (spread isn't known on other machines).", shotSetup + "sparks.EmitAt( shot.Origin + shot.Direction * 8, 4 )\nend", particleRun ) );
		entries.Add( Member( "Shot.Hit", ScriptType.Bool, PropertyAccess.Input, (int)ScriptInput.ShotHit, shot, "Whether it hit something.", shotSetup + "if shot.Hit then\n\t\tsparks.Emit( 4 )\n\tend\nend", particleRun ) );
		entries.Add( Member( "Shot.HitPosition", v, PropertyAccess.Input, (int)ScriptInput.ShotHitPositionX, shot, "Where it hit (where it ran out, when it hit nothing).", shotSetup + "sparks.EmitAt( shot.HitPosition, 12 )\nend", particleRun ) );
		entries.Add( Member( "Shot.HitNormal", v, PropertyAccess.Input, (int)ScriptInput.ShotHitNormalX, shot, "The way the surface it hit faces.", shotSetup + "sparks.EmitAt( shot.HitPosition + shot.HitNormal * 2, 8 )\nend", particleRun ) );
		entries.Add( Member( "Shot.Distance", N, PropertyAccess.Input, (int)ScriptInput.ShotDistance, shot, "How far it went.", shotSetup + "sparks.Emit( Math.Min( shot.Distance / 100, 20 ) )\nend", particleRun ) );
	}

	/// <summary>The furthest an element output can be turned, in degrees.</summary>
	internal const double MaxRotation = 3600;

	private static LibraryEntry Input( string name, ScriptType type, ScriptInput slot, string category, string doc, string example, ScriptPlace where ) => new()
	{
		Name = name, Kind = EntryKind.Input, Type = type, Category = category, Doc = doc, Example = example, Where = where,
		Access = PropertyAccess.Input, Slot = (int)slot,
	};

	private static LibraryEntry Reserved( string name, ScriptType type, int slot, string category, string doc, string example ) => new()
	{
		Name = name, Kind = EntryKind.Input, Type = type, Category = category, Doc = doc, Example = example, Where = ScriptPlace.StickerRun,
		Access = PropertyAccess.Reserved, Slot = slot,
	};

	private static LibraryEntry Output( string name, ScriptType type, ScriptOutput slot, string category, string doc, string example, ScriptPlace where, double min, double max ) => new()
	{
		Name = name, Kind = EntryKind.Output, Type = type, Category = category, Doc = doc, Example = example, Where = where, WriteWhere = where,
		Access = PropertyAccess.Output, Slot = (int)slot, Min = min, Max = max,
	};

	private static LibraryEntry Member( string name, ScriptType type, PropertyAccess access, int slot, string category, string doc, string example,
		ScriptPlace where, bool writable = false, double min = double.NaN, double max = double.NaN ) => new()
	{
		Name = name, Kind = EntryKind.Property, Type = type, Category = category, Doc = doc, Example = example, Where = where,
		WriteWhere = writable ? where : ScriptPlace.None, Access = access, Slot = slot, Min = min, Max = max, ExampleKind = ScriptKind.Effect,
	};

	private static void AddEvents( List<LibraryEntry> entries )
	{
		const int shots = 20, others = 10;
		Event( entries, ScriptEvent.Spawn, "When the effect starts (put on, or shown again).", "local burst = Sprite()\nfunction OnSpawn()\n\tburst.Emit( 30 )\nend", 0 );
		Event( entries, ScriptEvent.Update, "Every frame, with how long the frame took: change layers as things change.", "local glow = Sprite()\nfunction OnUpdate( dt )\n\tglow.Size = 2 + Math.Sin( t * 4 )\nend", 0,
			P( "dt", N, "Seconds since the last frame" ) with { Input = ScriptInput.Dt } );
		Event( entries, ScriptEvent.Jump, "When the player jumps.", "local puff = Sprite()\nfunction OnJump()\n\tpuff.Emit( 10 )\nend", others );
		Event( entries, ScriptEvent.Land, "When the player lands, with how fast they were falling.", "local dust = Sprite()\nfunction OnLand( fallSpeed )\n\tdust.Emit( Math.Min( fallSpeed / 20, 40 ) )\nend", others,
			P( "fallSpeed", N, "How fast they fell, in units a second" ) with { Input = ScriptInput.Amount } );
		Event( entries, ScriptEvent.Hurt, "When the player is hurt, with how much.", "local blood = Sprite()\nfunction OnHurt( damage )\n\tblood.Emit( damage )\nend", others,
			P( "damage", N, "How much health it took" ) with { Input = ScriptInput.Amount } );
		Event( entries, ScriptEvent.Death, "When the player dies.", "local soul = Sprite()\nfunction OnDeath()\n\tsoul.Emit( 40 )\nend", others );
		Event( entries, ScriptEvent.Respawn, "When the player comes back after dying.", "local halo = Sprite()\nfunction OnRespawn()\n\thalo.Emit( 20 )\nend", others );
		Event( entries, ScriptEvent.Footstep, "When a foot touches down, with which one.", "local step = Sprite()\nfunction OnFootstep( foot )\n\tif foot == Joint.LeftFoot then\n\t\tstep.Emit( 3 )\n\tend\nend", others,
			P( "foot", ScriptEnums.Joint.Type, "Joint.LeftFoot or Joint.RightFoot" ) with { Input = ScriptInput.Foot } );
		Event( entries, ScriptEvent.StartMoving, "When the player starts moving.", "local kick = Sprite()\nfunction OnStartMoving()\n\tkick.Emit( 6 )\nend", others );
		Event( entries, ScriptEvent.StopMoving, "When the player stops moving.", "local settle = Sprite()\nfunction OnStopMoving()\n\tsettle.Emit( 6 )\nend", others );
		Event( entries, ScriptEvent.Crouch, "When the player crouches.", "local glow = Sprite()\nfunction OnCrouch()\n\tglow.Off = true\nend", others );
		Event( entries, ScriptEvent.Uncrouch, "When the player stands up from crouching.", "local glow = Sprite()\nfunction OnUncrouch()\n\tglow.Off = false\nend", others );
		Event( entries, ScriptEvent.Impact, "When the body hits something hard, with how fast.", "local crack = Sprite()\nfunction OnImpact( speed )\n\tcrack.Emit( speed / 50 )\nend", others,
			P( "speed", N, "How fast it hit, in units a second" ) with { Input = ScriptInput.Amount } );
		Event( entries, ScriptEvent.Shoot, "When the player shoots or swings, with the shot every client works out for itself.", "local tracer = Sprite()\nfunction OnShoot( shot )\n\ttracer.EmitAt( shot.HitPosition, 6 )\nend", shots,
			P( "shot", ScriptType.Shot, "Origin, Direction, Hit, HitPosition, HitNormal, Distance" ) );
		Event( entries, ScriptEvent.Reload, "When the player reloads.", "local shells = Sprite()\nfunction OnReload()\n\tshells.Emit( 5 )\nend", others );
		Event( entries, ScriptEvent.Equip, "When the player takes out a weapon.", "local shine = Sprite()\nfunction OnEquip()\n\tshine.Emit( 8 )\nend", others );
		Event( entries, ScriptEvent.Holster, "When the player puts a weapon away.", "local shine = Sprite()\nfunction OnHolster()\n\tshine.Emit( 8 )\nend", others );
		Event( entries, ScriptEvent.Hit, "When the player's weapon hits something.", "local spark = Sprite()\nfunction OnHit()\n\tspark.Emit( 10 )\nend", others );
		Event( entries, ScriptEvent.Key, "When the effect's key is pressed (one key start per player every 2 minutes, for everyone).", "local flash = Sprite()\nfunction OnKey( key )\n\tif key == \"g\" then\n\t\tflash.Emit( 50 )\n\tend\nend", others,
			P( "key", ScriptType.Key, "The key, by name" ) with { Input = ScriptInput.Key } );
	}

	private static void Event( List<LibraryEntry> entries, ScriptEvent which, string doc, string example, int rate, params LibraryParameter[] parameters )
	{
		entries.Add( new LibraryEntry
		{
			Name = "On" + which, Kind = EntryKind.Event, Type = ScriptType.Void, Category = "Events", Doc = doc, Example = example,
			ExampleKind = ScriptKind.Effect, Where = ScriptPlace.Effect, Parameters = parameters, Required = parameters.Length, Event = which, RatePerSecond = rate,
		} );
	}
}
