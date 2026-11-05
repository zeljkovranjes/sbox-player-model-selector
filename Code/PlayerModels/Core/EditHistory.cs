using System.Collections.Generic;

namespace PlayerModels.Core;

/// <summary>
/// Undo and redo over states written as text (a look's effects, say): each state recorded that differs from the last
/// is a step back to undo, except that changes coming one right after another (a slider dragged, a number typed) are
/// one step. Keeps the last <see cref="MaxSteps"/> steps.
/// </summary>
public sealed class EditHistory
{
	/// <summary>How many steps back are kept.</summary>
	public const int MaxSteps = 50;

	/// <summary>Changes this close together, in seconds, are one step.</summary>
	public const float JoinSeconds = 0.3f;

	private readonly List<string> _undo = new();
	private readonly List<string> _redo = new();
	private string _current;
	private float _changedAt = float.MinValue;

	/// <summary>Whether there is a step to undo, or to redo.</summary>
	public bool CanUndo => _undo.Count > 0;
	public bool CanRedo => _redo.Count > 0;

	/// <summary>
	/// Records <paramref name="state"/> as it is at <paramref name="time"/> (seconds): the first is where history starts,
	/// one like the last changes nothing, and any other is a new step unless it came within <see cref="JoinSeconds"/> of
	/// the last change. A new step can't be redone past.
	/// </summary>
	public void Record( string state, float time )
	{
		if ( state is null || state == _current )
			return;

		if ( _current is not null && time - _changedAt > JoinSeconds )
		{
			_undo.Add( _current );
			if ( _undo.Count > MaxSteps )
				_undo.RemoveAt( 0 );

			_redo.Clear();
		}

		_current = state;
		_changedAt = time;
	}

	/// <summary>The state a step back, now the current one; null with nothing to undo.</summary>
	public string Undo() => Step( _undo, _redo );

	/// <summary>The state a step forward again, now the current one; null with nothing to redo.</summary>
	public string Redo() => Step( _redo, _undo );

	private string Step( List<string> from, List<string> to )
	{
		if ( from.Count == 0 )
			return null;

		to.Add( _current );
		_current = from[ ^1 ];
		from.RemoveAt( from.Count - 1 );

		// What comes next is a step of its own, however soon
		_changedAt = float.MinValue;
		return _current;
	}
}
