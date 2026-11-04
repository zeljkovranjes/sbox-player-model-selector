using System.Text.Json;
using PlayerModels.Core;
using PlayerModels.Core.Script;
using PlayerModels.Engine;

namespace PlayerModels.UI;

// The code editor on a weapon's skin script (see WeaponSkin.Script): the player's own moving colour, in place of a stock
// one. Opened as the skin's script, or the stock motion it has as a start; each good compile is put on the skin, so the
// weapon in the preview follows
public partial class CodeEditor
{
	/// <summary>Whether it edits the skin script of <see cref="Weapon"/>'s design rather than the whole design.</summary>
	public bool Skin { get; set; }

	// A skin script started from nothing: a rainbow along the weapon, to change
	private const string SkinStarter = """
		-- This weapon's moving colour: runs once a band along the weapon (64 places when it reads along), every frame
		color = Color.FromHsv( Math.Frac( along - t * 0.25 ) * 360, 1, 1 )
		alpha = 0.85
		glow = color * 0.12
		""";

	// The skin's script formatted, or its stock motion's to start from
	private string OpenSkin( PlayerLook look )
	{
		var design = DesignOf( look );
		_openedAs = design is null ? null : JsonSerializer.Serialize( design );
		_applied = _openedAs;
		_name = $"{WeaponDesign.NameOf( Weapon )} skin";
		var text = design?.Skin?.Script ?? PresetScripts.SkinMotionScript( design?.Skin?.AnimationIs ?? WeaponSkin.Motion.None ) ?? SkinStarter;
		return Formatter.Format( text ) ?? text;
	}

	// One run of a skin script compiled, as a weapon runs it, so a fault shows here first; true when it ran
	private bool RunsAsSkin( ScriptProgram program )
	{
		var io = new ScriptIO();
		io.Set( ScriptInput.Time, 1500 );
		io.Set( ScriptInput.Seconds, 1.5 );
		io.Set( ScriptInput.Index, 2 );
		io.Set( ScriptInput.Count, 8 );
		io.Set( ScriptInput.Along, 0.25 );
		var machine = new ScriptMachine( program );
		if ( machine.RunElement( io, ScriptPrograms.Stack ) )
			return true;

		_errors = new System.Collections.Generic.List<ScriptError> { machine.Fault.Describe( program ) };
		return false;
	}

	// The script on the weapon's skin (a design made for it when it has none yet): the preview's weapon follows
	private void PutSkin()
	{
		var look = LookNow?.Invoke();
		if ( look is null )
			return;

		var at = look.Weapons.FindIndex( x => x.Weapon == Weapon );
		if ( at < 0 && look.Weapons.Count >= WeaponDesign.MaxPerLook )
		{
			_note = $"A look has designs for up to {WeaponDesign.MaxPerLook} weapons: reset one to design this";
			return;
		}

		var design = at >= 0 ? look.Weapons[ at ] : new WeaponDesign { Weapon = Weapon };
		design.Skin ??= new WeaponSkin();
		design.Skin.Script = Minifier.Minify( _source );
		if ( at < 0 )
			look.Weapons.Add( design );

		_applied = JsonSerializer.Serialize( design );
		Changed?.Invoke();
	}

	// The skin's script changed elsewhere (undo, the panel's Stock button): that is the text now
	private void SyncSkin()
	{
		var design = DesignOf( LookNow?.Invoke() );
		if ( design is null || _entry is null )
			return;

		var now = JsonSerializer.Serialize( design );
		if ( now == _applied )
			return;

		_applied = now;
		if ( design.Skin?.Script is { } script && script != Minifier.Minify( _source ) )
		{
			_entry.Rewrite( Formatter.Format( script ) ?? script );
			_source = _entry.Source;
			Compile( false );
		}
	}
}
