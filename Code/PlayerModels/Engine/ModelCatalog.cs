using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Everything the window can offer beyond the listed models: player models and first-person arms from the sbox.game
/// searches and from the mounted games. Each is shown once it passes its check; checks done on this machine before are
/// answered at once, the rest run one at a time, since each loads a model.
/// </summary>
internal sealed class ModelCatalog
{
	/// <summary>A model on offer: usable, not, or (null) still being checked.</summary>
	internal sealed class Entry
	{
		public string Title { get; init; }
		public string Model { get; init; }
		public bool? Usable { get; set; }

		// The mounted game a model comes from; null for sbox.game models
		public string Game { get; init; }

		// Loads the model and says whether it can be used, remembering the answer for next time
		public Func<Task<bool>> Check { get; init; }
	}

	// Arms packages are few among the props a search for hands or arms brings; only these get a check (a download)
	private static readonly string[] ArmsWords = { "arms", "hands", "hand_", "_hand" };

	private readonly List<Entry> _bodies = new();
	private readonly List<Entry> _arms = new();
	private bool _started;

	/// <summary>Player models from sbox.game, then the mounted games' characters.</summary>
	internal IReadOnlyList<Entry> Bodies => _bodies;

	/// <summary>First-person arms from sbox.game, then the mounted games'.</summary>
	internal IReadOnlyList<Entry> Arms => _arms;

	/// <summary>Goes up whenever entries arrive or a check finishes, for redrawing.</summary>
	internal int Revision { get; private set; }

	/// <summary>Whether an sbox.game search failed (no connection, usually).</summary>
	internal bool SearchFailed { get; private set; }

	/// <summary>
	/// Fills the catalog, once. Models already in <paramref name="listed"/> are left out; <paramref name="stillWanted"/>
	/// stops the checks when the window goes away.
	/// </summary>
	internal async Task LoadAsync( IEnumerable<string> bodySearches, IEnumerable<string> armsSearches, int count,
		ISet<string> listed, Func<bool> stillWanted )
	{
		if ( _started )
			return;

		_started = true;

		foreach ( var found in await SearchAsync( bodySearches, count ) )
		{
			if ( !listed.Contains( found.Ident ) )
				_bodies.Add( new Entry { Title = found.Title, Model = found.Ident, Usable = CloudModels.Known( found ), Check = () => CloudModels.CheckAsync( found ) } );
		}

		var mounts = MountModels.Candidates()
			.Select( x => new Entry { Title = x.Title, Model = x.Path, Game = x.Game, Usable = MountModels.Known( x.Path ), Check = () => MountModels.CheckAsync( x.Path ) } )
			.ToList();

		foreach ( var found in await SearchAsync( armsSearches, count ) )
		{
			if ( LooksLikeArms( found ) )
				_arms.Add( new Entry { Title = found.Title, Model = found.Ident, Usable = ViewModelArms.Known( found.Ident ), Check = () => ViewModelArms.CheckAsync( found.Ident ) } );
		}

		var mountArms = MountModels.ArmsCandidates()
			.Select( x => new Entry { Title = x.Title, Model = x.Path, Game = x.Game, Usable = ViewModelArms.Known( x.Path ), Check = () => ViewModelArms.CheckAsync( x.Path ) } )
			.ToList();

		_bodies.AddRange( mounts );
		_arms.AddRange( mountArms );
		Revision++;

		// sbox.game first, then arms, then the mounts (the slowest: a game's models are converted as they load)
		await CheckAsync( _bodies.Except( mounts ), stillWanted );
		await CheckAsync( _arms.Except( mountArms ), stillWanted );
		await CheckAsync( mounts, stillWanted );
		await CheckAsync( mountArms, stillWanted );
	}

	private static bool LooksLikeArms( CloudModels.Found found )
	{
		return ArmsWords.Any( x => found.Ident.Contains( x, StringComparison.OrdinalIgnoreCase )
			|| found.Title.Contains( x, StringComparison.OrdinalIgnoreCase ) );
	}

	private async Task<List<CloudModels.Found>> SearchAsync( IEnumerable<string> searches, int count )
	{
		var found = new List<CloudModels.Found>();
		foreach ( var search in searches.Where( x => !string.IsNullOrWhiteSpace( x ) ) )
		{
			try
			{
				found.AddRange( await CloudModels.FindAsync( search, count ) );
			}
			catch ( Exception e )
			{
				Log.Warning( $"Player Model Selector: couldn't search sbox.game for '{search}': {e.Message}" );
				SearchFailed = true;
			}
		}

		return found.DistinctBy( x => x.Ident ).ToList();
	}

	private async Task CheckAsync( IEnumerable<Entry> entries, Func<bool> stillWanted )
	{
		foreach ( var entry in entries.Where( x => x.Usable is null ).ToArray() )
		{
			if ( !stillWanted() )
				return;

			entry.Usable = await entry.Check();
			Revision++;
		}
	}
}
