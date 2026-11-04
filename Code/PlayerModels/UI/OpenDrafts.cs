using System.Collections.Generic;
using PlayerModels.Core;

namespace PlayerModels.UI;

/// <summary>
/// The drafts open in the editors now (see <see cref="Core.Draft"/>): effects of the look being edited (by the effect's
/// id) and weapon designs (by the weapon), each with the draft it is, and how its editor was left, for the editor to
/// take up. While open they show in the preview but aren't worn: Apply wears the look without them (see
/// <see cref="Core.PlayerLook.WithoutDrafts"/>), until each is finished or reverted away.
/// </summary>
public sealed class OpenDrafts
{
	/// <summary>The effects that are drafts, by id, and the draft each is.</summary>
	public Dictionary<string, string> Effects { get; } = new();

	/// <summary>The weapon designs that are drafts, by weapon, and the draft each is.</summary>
	public Dictionary<string, string> Weapons { get; } = new();

	/// <summary>How an effect's editor was left when its draft was kept, by the effect's id, until the editor takes it.</summary>
	public Dictionary<string, string> States { get; } = new();

	/// <summary>Counts up whenever a draft opens or closes, for what shows them.</summary>
	public int Revision { get; private set; }

	/// <summary>Forgets every open draft: the look they were in went back to what is worn.</summary>
	public void Clear()
	{
		Effects.Clear();
		Weapons.Clear();
		States.Clear();
		Changed();
	}

	/// <summary>Says something changed, for what shows them.</summary>
	public void Changed() => Revision++;

	/// <summary>
	/// Keeps <paramref name="effect"/> of <paramref name="look"/> as a draft (the same draft when it is one already) with
	/// how its editor was left (<paramref name="state"/>): it shows, unworn, until it is finished. False when it can't be
	/// kept: <paramref name="full"/> says whether that is because the drafts are full (asked about before
	/// <paramref name="dropOldest"/> lets the oldest go, named in <paramref name="dropped"/>).
	/// </summary>
	public bool KeepEffect( PlayerLook look, Effect effect, string state, bool dropOldest, out Draft dropped, out bool full )
	{
		dropped = null;
		full = false;
		var item = look.Standalone( effect )?.SharedForm();
		if ( item is null )
			return false;

		var draft = Engine.Drafts.Make( Draft.Of.Effect, Effects.GetValueOrDefault( effect.Id ), effect.Name, item, state );
		if ( !Engine.Drafts.Save( draft, dropOldest, out dropped ) )
		{
			full = Engine.Drafts.Find( Draft.Of.Effect, draft.Id ) is null && Engine.Drafts.Of( Draft.Of.Effect ).Count >= Draft.MaxPerKind;
			return false;
		}

		Effects[ effect.Id ] = draft.Id;
		Changed();
		return true;
	}
}
