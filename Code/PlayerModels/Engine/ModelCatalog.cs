using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Everything the window can offer beyond the listed models: player models and first-person arms from the sbox.game
/// searches and from the mounted games. Each is shown once it passes its check; checks done on this machine before are
/// answered at once. The rest run one at a time, since each downloads or loads a model, and only for models the player
/// can see in the grid: thousands listed load as their groups are opened, never all at once.
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

		// First-person arms, for the View Model grid, rather than a body
		public bool Arms { get; init; }

		// Loads the model and says whether it can be used, remembering the answer for next time
		public Func<Task<bool>> Check { get; init; }

		// Says, without loading the model, whether it may be usable at all (sbox.game's data); null when there is no such
		// quick look. Run before any full check, so what can't be a body leaves the grid at once
		public Func<Task<bool>> PreCheck { get; init; }
		public bool PreChecked { get; set; }
	}

	// Arms packages are few among the props a search for hands or arms brings; only these get a check (a download)
	private static readonly string[] ArmsWords = { "arms", "hands", "hand_", "_hand" };

	private readonly List<Entry> _bodies = new();
	private readonly List<Entry> _arms = new();
	private bool _started;

	// A body search, and how far it has been fetched: a page at a time, as the player reaches the end of what is listed
	private sealed class Search
	{
		public string Query { get; init; }
		public string Tag { get; init; }
		public int Fetched { get; set; }
		public int Total { get; set; } = -1;
		public bool IsDone => Total >= 0 && Fetched >= Total;
	}

	private List<Search> _searches = new();
	private readonly HashSet<string> _bodyModels = new();
	private ISet<string> _listed = new HashSet<string>();
	private int _pageSize;
	private bool _searchesStarted;
	private bool _fetchingMore;

	// The folders a player added to where mounted characters are looked for, and whether the mounts are listed yet
	private IReadOnlyCollection<string> _mountFolders = Array.Empty<string>();
	private bool _mountsListed;

	// How long the checker rests when nothing the player can see needs a check
	private const int IdleMilliseconds = 250;

	// Whether the player can see an entry right now (its group open in the grid shown, or matching the search)
	private Func<Entry, bool> _shown = _ => false;

	/// <summary>Player models from sbox.game, then the mounted games' characters.</summary>
	internal IReadOnlyList<Entry> Bodies => _bodies;

	/// <summary>First-person arms from sbox.game, then the mounted games'.</summary>
	internal IReadOnlyList<Entry> Arms => _arms;

	/// <summary>Goes up whenever entries arrive or a check finishes, for redrawing.</summary>
	internal int Revision { get; private set; }

	/// <summary>Whether an sbox.game search failed (no connection, usually).</summary>
	internal bool SearchFailed { get; private set; }

	/// <summary>Whether a body search has results on sbox.game not listed yet (see <see cref="LoadMoreAsync"/>).</summary>
	internal bool HasMore => _searches.Any( x => !x.IsDone );

	/// <summary>
	/// Fills the catalog, once, then checks what the player sees, one model at a time. Body searches are a query and
	/// the tag their results must carry (null for none); models already in <paramref name="listed"/> are left out.
	/// <paramref name="shown"/> says whether the player can see an entry; <paramref name="stillWanted"/> stops the checks
	/// when the window goes away.
	/// </summary>
	internal async Task LoadAsync( IEnumerable<(string Query, string Tag)> bodySearches, IEnumerable<string> armsSearches,
		int count, ISet<string> listed, Func<Entry, bool> shown, Func<bool> stillWanted )
	{
		if ( _started )
			return;

		_started = true;
		_shown = shown;
		_listed = listed;
		_pageSize = Math.Max( 1, count );
		_searches = bodySearches.Where( x => !string.IsNullOrWhiteSpace( x.Query ) ).Select( x => new Search { Query = x.Query, Tag = x.Tag } ).ToList();

		await FetchBodiesAsync();
		_searchesStarted = true;

		foreach ( var found in await SearchArmsAsync( armsSearches, count ) )
		{
			if ( LooksLikeArms( found ) )
				_arms.Add( new Entry { Title = found.Title, Model = found.Ident, Arms = true, Usable = ViewModelArms.Known( found.Ident ), Check = () => ViewModelArms.CheckAsync( found.Ident ) } );
		}

		_arms.AddRange( MountModels.ArmsCandidates()
			.Select( x => new Entry { Title = x.Title, Model = x.Path, Game = x.Game, Arms = true, Usable = ViewModelArms.Known( x.Path ), Check = () => ViewModelArms.CheckAsync( x.Path ) } ) );

		AddMountBodies( MountModels.Candidates( _mountFolders ) );
		Revision++;

		await CheckShownAsync( stillWanted );
	}

	/// <summary>
	/// Lists the next page of every body search that has more on sbox.game, for when the player reaches the end of what
	/// is listed. One fetch at a time; each page is kept on this machine for a while (see <see cref="CloudModels"/>).
	/// </summary>
	internal async Task LoadMoreAsync()
	{
		if ( !_searchesStarted || _fetchingMore || !HasMore )
			return;

		_fetchingMore = true;
		try
		{
			await FetchBodiesAsync();
			Revision++;
		}
		finally
		{
			_fetchingMore = false;
		}
	}

	// The next page of each body search not done yet
	private async Task FetchBodiesAsync()
	{
		foreach ( var search in _searches.Where( x => !x.IsDone ) )
		{
			try
			{
				var page = await CloudModels.FindAsync( search.Query, _pageSize, search.Fetched, true, search.Tag );
				search.Fetched += _pageSize;
				search.Total = page.Total;

				foreach ( var found in page.Found.Where( x => !_listed.Contains( x.Ident ) && _bodyModels.Add( x.Ident ) ) )
					_bodies.Add( new Entry { Title = found.Title, Model = found.Ident, Usable = CloudModels.Known( found ), Check = () => CloudModels.CheckAsync( found ), PreCheck = () => CloudModels.PreCheckAsync( found ) } );
			}
			catch ( Exception e )
			{
				// Not asked again this session: a search that fails would fail every time the end of the list is reached
				Log.Warning( $"Player Model Selector: couldn't search sbox.game for '{search.Query}': {e.Message}" );
				SearchFailed = true;
				search.Total = search.Fetched;
			}
		}
	}

	/// <summary>
	/// Looks for mounted characters in <paramref name="folders"/> (mount paths a player added) as well as the usual
	/// folders: what they hold is added, to be checked as the player sees it, and what only folders taken away held goes.
	/// </summary>
	internal void UseMountFolders( IReadOnlyCollection<string> folders )
	{
		_mountFolders = folders;

		// Until LoadAsync lists the mounts it uses these folders itself
		if ( !_mountsListed )
			return;

		var candidates = MountModels.Candidates( folders );
		var wanted = candidates.Select( x => x.Path ).ToHashSet();
		_bodies.RemoveAll( x => x.Game is not null && !wanted.Contains( x.Model ) );

		AddMountBodies( candidates );
		Revision++;
	}

	// Adds the mounted characters not listed yet
	private void AddMountBodies( List<MountModels.Found> candidates )
	{
		_mountsListed = true;

		var listed = _bodies.Where( x => x.Game is not null ).Select( x => x.Model ).ToHashSet();
		var added = candidates
			.Where( x => !listed.Contains( x.Path ) )
			.Select( x => new Entry { Title = x.Title, Model = x.Path, Game = x.Game, Usable = MountModels.Known( x.Path ), Check = () => MountModels.CheckAsync( x.Path ) } )
			.ToList();

		_bodies.AddRange( added );
	}

	// One model at a time, and only one the player can see: the quick looks first, for every unchecked model shown, then
	// the full checks, bodies then arms, in grid order
	private async Task CheckShownAsync( Func<bool> stillWanted )
	{
		while ( stillWanted() )
		{
			var quick = _bodies.FirstOrDefault( x => x.Usable is null && x.PreCheck is not null && !x.PreChecked && _shown( x ) );
			var next = quick ?? _bodies.FirstOrDefault( x => x.Usable is null && _shown( x ) ) ?? _arms.FirstOrDefault( x => x.Usable is null && _shown( x ) );
			if ( next is null )
			{
				await GameTask.Delay( IdleMilliseconds );
				continue;
			}

			try
			{
				if ( next == quick )
				{
					next.PreChecked = true;
					if ( !await next.PreCheck() )
						next.Usable = false;
				}
				else
				{
					next.Usable = await next.Check();
				}
			}
			catch ( Exception e )
			{
				Log.Warning( $"Player Model Selector: couldn't check {next.Model}: {e.Message}" );
				next.Usable = false;
			}

			Revision++;
		}
	}

	private static bool LooksLikeArms( CloudModels.Found found )
	{
		return ArmsWords.Any( x => found.Ident.Contains( x, StringComparison.OrdinalIgnoreCase )
			|| found.Title.Contains( x, StringComparison.OrdinalIgnoreCase ) );
	}

	private async Task<List<CloudModels.Found>> SearchArmsAsync( IEnumerable<string> searches, int count )
	{
		var found = new List<CloudModels.Found>();
		foreach ( var query in searches.Where( x => !string.IsNullOrWhiteSpace( x ) ) )
		{
			try
			{
				found.AddRange( (await CloudModels.FindAsync( query, count, 0, false )).Found );
			}
			catch ( Exception e )
			{
				Log.Warning( $"Player Model Selector: couldn't search sbox.game for '{query}': {e.Message}" );
				SearchFailed = true;
			}
		}

		return found.DistinctBy( x => x.Ident ).ToList();
	}
}
