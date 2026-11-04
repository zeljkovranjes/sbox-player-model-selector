namespace PlayerModels.Core.Script;

/// <summary>
/// The value slots a host fills before a run: the body's state, the element being coloured (a particle, a shape point,
/// an emblem layer), the event's arguments. Vectors take three slots in a row (x, y, z).
/// </summary>
public enum ScriptInput
{
	Time, Seconds, Speed, VelocityX, VelocityY, VelocityZ, Grounded, Crouching, Moving, InAir, HoldingWeapon, AnchorSpeed,
	AnchorPositionX, AnchorPositionY, AnchorPositionZ,
	Index, Count, Age, Life, Seed, OriginX, OriginY, OriginZ,
	Dt, Amount, Foot, Key,
	ShotOriginX, ShotOriginY, ShotOriginZ, ShotDirectionX, ShotDirectionY, ShotDirectionZ, ShotHit,
	ShotHitPositionX, ShotHitPositionY, ShotHitPositionZ, ShotHitNormalX, ShotHitNormalY, ShotHitNormalZ, ShotDistance,
	Total,
}

/// <summary>
/// The value slots an element run writes: its colour (r, g, b, a), alpha, size, rotation, offset (x, y, z) and, for a
/// particle, its place in the world (x, y, z) and which of its layer's pictures it shows.
/// </summary>
public enum ScriptOutput
{
	ColorR, ColorG, ColorB, ColorA, Alpha, Size, Rotation, OffsetX, OffsetY, OffsetZ, PositionX, PositionY, PositionZ, Frame,
	Total,
}

/// <summary>The events a script handles with <c>function On&lt;Event&gt;( ... ) ... end</c>; the visual "When" picker shares them.</summary>
public enum ScriptEvent
{
	None, Spawn, Update, Jump, Land, Hurt, Death, Respawn, Footstep, StartMoving, StopMoving, Crouch, Uncrouch, Impact,
	Shoot, Reload, Equip, Holster, Hit, Kill, Key,
	Total,
}

/// <summary>
/// What the host and a script exchange for one run, filled by the host and read back after: <see cref="Inputs"/> (see
/// <see cref="ScriptInput"/>), and <see cref="Outputs"/> (see <see cref="ScriptOutput"/>) with a bit in
/// <see cref="OutputsSet"/> for each output the script wrote (bit 0 colour, 1 alpha, 2 size, 3 rotation, 4 offset,
/// 5 position, 6 frame).
/// Outputs start as the host's own values, so a script can read and change them. One is reused for every run.
/// </summary>
public sealed class ScriptIO
{
	/// <summary>The bits of <see cref="OutputsSet"/>.</summary>
	public const int ColorSet = 1, AlphaSet = 2, SizeSet = 4, RotationSet = 8, OffsetSet = 16, PositionSet = 32, FrameSet = 64;

	public readonly double[] Inputs = new double[ (int)ScriptInput.Total ];
	public readonly double[] Outputs = new double[ (int)ScriptOutput.Total ];
	public int OutputsSet;

	/// <summary>Sets an input.</summary>
	public void Set( ScriptInput input, double value ) => Inputs[ (int)input ] = value;

	/// <summary>Sets a vector input (its x slot), three slots in a row.</summary>
	public void Set( ScriptInput input, double x, double y, double z )
	{
		Inputs[ (int)input ] = x;
		Inputs[ (int)input + 1 ] = y;
		Inputs[ (int)input + 2 ] = z;
	}

	/// <summary>An output, as the script left it.</summary>
	public double Get( ScriptOutput output ) => Outputs[ (int)output ];

	/// <summary>The output bit for the output at slot <paramref name="slot"/>.</summary>
	internal static int BitOf( int slot ) => slot switch
	{
		<= (int)ScriptOutput.ColorA => ColorSet,
		(int)ScriptOutput.Alpha => AlphaSet,
		(int)ScriptOutput.Size => SizeSet,
		(int)ScriptOutput.Rotation => RotationSet,
		<= (int)ScriptOutput.OffsetZ => OffsetSet,
		<= (int)ScriptOutput.PositionZ => PositionSet,
		_ => FrameSet,
	};
}

/// <summary>
/// What a running effect script can ask its host to do; the core never knows the engine. Called from inside a run, so
/// an implementation must not throw, and should only queue the request (the host's budget applies).
/// </summary>
public interface IScriptHost
{
	/// <summary>Spawns <paramref name="count"/> sprites of layer <paramref name="layer"/> now, where it is.</summary>
	void Emit( int layer, int count );

	/// <summary>Spawns <paramref name="count"/> sprites of layer <paramref name="layer"/> at a world position.</summary>
	void EmitAt( int layer, double x, double y, double z, int count );
}

/// <summary>What a thing in the player's own library is.</summary>
public enum LibraryItemKind { Shape, Sticker, Emblem, Effect }

/// <summary>One thing in the player's library: its name, kind, and the id of its content (for its thumbnail).</summary>
public readonly record struct LibraryItem( string Name, LibraryItemKind Kind, string ContentId );

/// <summary>
/// The player's own saved things, for <c>Shapes.Mine( "..." )</c>, <c>Stickers.Mine</c>, <c>Emblems.Mine</c> and
/// <c>Effects.Mine</c>: on the author's machine their library (My shapes, My effects); on everyone else's, what the look
/// carries. Names are checked against <see cref="Items"/> when a script compiles; their content is read when it builds,
/// so a built effect keeps its own copy whatever happens to the library after.
/// </summary>
public interface IPlayerLibrary
{
	/// <summary>Everything in it, by name and kind.</summary>
	System.Collections.Generic.IReadOnlyList<LibraryItem> Items { get; }

	/// <summary>The effect called <paramref name="name"/>; null for none.</summary>
	Effect Effect( string name );

	/// <summary>The shape, sticker or emblem called <paramref name="name"/>, as a drawn shape; null for none.</summary>
	DrawnShape Shape( string name, LibraryItemKind kind );
}
