using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PlayerModels.Core;
using Sandbox;
using Sandbox.Services;

namespace PlayerModels.Engine;

/// <summary>
/// Things of one kind players share for models (armatures, looks), or for everyone (effects, shapes: see
/// <see cref="Shared.Everyone"/>), kept in the addon's cloud tables (<see cref="Database"/>): a public record for each thing
/// published (a <see cref="SharePost"/>: the item with the name and description its author gave it), grouped by model,
/// and one public vote per player per item, grouped as an up or a down vote on it so the votes are counted without being
/// read. What is published stays as it was published: changing it means publishing it again, as a new one, and taking
/// the old one down. Each share is also kept by its author (<see cref="AuthorIndex"/>). What is listed is kept for a
/// little while, so pages opening one after another don't each ask again, and writes are spaced out (see
/// <see cref="Shared.PaceAsync"/>). Calls fail with the backend's own exception; <see cref="Shared.Why"/> words it.
/// </summary>
public sealed class SharedLibrary<T>
{
	/// <summary>The most one player has published for one model (for armatures and looks) at a time.</summary>
	internal const int MaxPerPlayer = 5;

	// How long a model's list is kept before it is asked for again, unless this player changed it meanwhile
	private const float ListLife = 20f;

	// The most listed for one model, and how many more each read on lists: the votes on that many are counted in one
	// ask, two groups each (100 at most)
	private const int MaxListed = 50;

	// The most groups counted in one ask
	private const int MaxCounted = 100;

	// How long a model's count of shared items is trusted before it is asked for again
	private const float CountLife = 300f;

	private readonly string _kind;
	private readonly string _table;
	private readonly string _votes;
	private readonly Func<string, T> _parse;
	private readonly Func<T, string> _serialize;
	private readonly Func<T, string> _modelOf;
	private readonly Func<T, T, bool> _same;
	private readonly int _perPlayer;
	private readonly Dictionary<string, (long Count, RealTimeSince Age)> _counts = new();
	private readonly Dictionary<string, (long Written, long Votes)> _votesCounted = new();
	private readonly Dictionary<string, (Task<List<Entry>> Listing, RealTimeSince Age)> _listed = new();

	/// <summary>
	/// The items of <paramref name="kind"/> ("armature", "look") kept in table <paramref name="table"/> (their votes in
	/// table <paramref name="votes"/>), read by <paramref name="parse"/> (null for anything that isn't one), written by
	/// <paramref name="serialize"/>, made for the model <paramref name="modelOf"/> says, and alike when
	/// <paramref name="same"/> says; each player may have <paramref name="perPlayer"/> published for a model at once.
	/// </summary>
	internal SharedLibrary( string kind, string table, string votes, Func<string, T> parse, Func<T, string> serialize, Func<T, string> modelOf,
		Func<T, T, bool> same, int perPlayer = MaxPerPlayer )
	{
		_perPlayer = perPlayer;
		_kind = kind;
		_table = table;
		_votes = votes;
		_parse = parse;
		_serialize = serialize;
		_modelOf = modelOf;
		_same = same;
	}

	/// <summary>What one item is called ("armature", "look").</summary>
	internal string Kind => _kind;

	/// <summary>The most one player has published for one model at a time.</summary>
	internal int PerPlayer => _perPlayer;

	/// <summary>
	/// An item shared for a model: its record, who shared it, the name and description they gave it, and how it was rated.
	/// </summary>
	public sealed record Entry( long Id, long Author, T Item, string Name, string Description, DateTimeOffset Changed, long Up, long Down )
	{
		/// <summary>Up votes less down votes.</summary>
		internal long Score => Up - Down;

		/// <summary>Whether this player shared it.</summary>
		internal bool IsMine => Author == Game.SteamId.Value;
	}

	/// <summary>An item read from text, or null when the text isn't one.</summary>
	internal T Parse( string text ) => text is null ? default : _parse( text );

	/// <summary>The item as text, as it is stored and put in codes.</summary>
	internal string Serialize( T item ) => _serialize( item );

	/// <summary>The model the item was made for.</summary>
	internal string ModelOf( T item ) => _modelOf( item );

	/// <summary>Whether two items are alike: the same one is never shared twice.</summary>
	internal bool Same( T a, T b ) => a is not null && b is not null && _same( a, b );

	/// <summary>
	/// The items shared for <paramref name="model"/>, the best rated first, then the newest. One shared twice (by two
	/// players at once) is listed once, the better rated or else the first shared, except that this player's own always
	/// is, and no player is listed more often than they may publish: only the client checks that when publishing, so
	/// what is listed is what any client could have written, read within bounds. Ratings count one vote per player.
	/// </summary>
	internal Task<List<Entry>> ListAsync( string model )
	{
		if ( _listed.TryGetValue( model, out var listed ) && listed.Age < ListLife && !listed.Listing.IsFaulted && !listed.Listing.IsCanceled )
			return listed.Listing;

		var listing = ReadListAsync( model );
		_listed[ model ] = (listing, 0f);
		return listing;
	}

	// What is listed for a model is asked for again next time, and read again from the start: this player changed it
	private void Forget( string model )
	{
		_listed.Remove( model );
		_read?.Remove( model );
	}

	// Reads the model's shares the first time, on from where it stopped when paged past (see MoreAsync), and past the end
	// of what was read once it got there (what was shared since); else what was read is listed again, its votes counted anew
	private async Task<List<Entry>> ReadListAsync( string model, bool more = false )
	{
		_read ??= new();
		if ( !_read.TryGetValue( model, out var reading ) )
			_read[ model ] = reading = new Reading();

		if ( !reading.Started || reading.Ended || more )
		{
			var (records, next) = await Shared.ReadFromAsync( Database.GetTable( _table ), GroupOf( model ), reading.Cursor );
			reading.Started = true;
			reading.Ended = next is null;
			reading.Cursor = next ?? (records.Count > 0 ? records[ ^1 ].Id : reading.Cursor);

			// Loops and named methods rather than lambdas, here and below: a lambda of a generic type can't be carried over
			// a hot reload, and LINQ keeps one in a static field once it has run
			foreach ( var record in records )
			{
				var post = SharePost.Parse( record.Value );
				var item = post is null ? default : Parse( post.Item );
				if ( item is not null && post.Kind == _kind && post.Model == model && _modelOf( item ) == model )
					reading.Found.Add( new Listed( record, post, item ) );
			}
		}

		if ( more )
			reading.Listed += MaxListed;

		// The newest first, each player's newest few only, as many as paging on has asked for
		var shared = new List<Listed>();
		var perOwner = new Dictionary<long, int>();
		reading.MoreToList = false;
		foreach ( var listed in reading.Found.OrderByDescending( CreatedAt ) )
		{
			var owner = listed.Record.OwnerSteamId;
			var count = perOwner.GetValueOrDefault( owner );
			if ( count >= _perPlayer && owner != Shared.Curator )
				continue;

			if ( shared.Count >= reading.Listed )
			{
				reading.MoreToList = true;
				break;
			}

			perOwner[ owner ] = count + 1;
			shared.Add( listed );
		}

		_counts[ model ] = (shared.Count, 0f);
		if ( shared.Count == 0 )
			return new List<Entry>();

		var groups = new List<string>( shared.Count * 2 );
		foreach ( var listed in shared )
		{
			groups.Add( UpGroup( listed.Record.Id ) );
			groups.Add( DownGroup( listed.Record.Id ) );
		}

		// How many votes each was given, MaxCounted groups an ask
		var table = Database.GetTable( _votes );
		var written = new Dictionary<string, long>();
		for ( var at = 0; at < groups.Count; at += MaxCounted )
		{
			foreach ( var (group, info) in await table.GetPublicGroupInfo( groups.GetRange( at, Math.Min( MaxCounted, groups.Count - at ) ).ToArray() ) )
				written[ group ] = info.Count;
		}

		long WrittenIn( string group ) => written.GetValueOrDefault( group );

		var entries = new List<Entry>();
		foreach ( var (record, post, item) in shared )
		{
			var up = await CountVotesAsync( table, UpGroup( record.Id ), WrittenIn( UpGroup( record.Id ) ), record );
			var down = await CountVotesAsync( table, DownGroup( record.Id ), WrittenIn( DownGroup( record.Id ) ), record );
			entries.Add( new Entry( record.Id, record.OwnerSteamId, item, post.Name, post.Description, record.CreatedAt, up, down ) );
		}

		var kept = new List<Entry>();
		foreach ( var entry in entries.OrderByDescending( ScoreOf ).ThenBy( ChangedOf ) )
		{
			if ( entry.IsMine || !Lists( kept, entry.Item ) )
				kept.Add( entry );
		}

		return kept.OrderByDescending( ScoreOf ).ThenByDescending( ChangedOf ).ToList();
	}

	// A share read from its table, before its votes are counted
	private readonly record struct Listed( Database.Record Record, SharePost Post, T Item );

	private static DateTimeOffset CreatedAt( Listed listed ) => listed.Record.CreatedAt;

	private static long ScoreOf( Entry entry ) => entry.Score;

	private static DateTimeOffset ChangedOf( Entry entry ) => entry.Changed;

	private static (long Owner, string Key) VoteOf( Database.Record record ) => (record.OwnerSteamId, record.Key);

	// Whether entries has one the same as item
	private bool Lists( List<Entry> entries, T item )
	{
		foreach ( var entry in entries )
		{
			if ( Same( entry.Item, item ) )
				return true;
		}

		return false;
	}

	private Entry SameAs( List<Entry> entries, T item )
	{
		foreach ( var entry in entries )
		{
			if ( Same( entry.Item, item ) )
				return entry;
		}

		return null;
	}

	// The votes that count in a group of <paramref name="written"/> records (see Votes). Each group is its own request, so
	// one is only read when it has votes, and only again once their number changes. Told to slow down, what is known stands
	private async Task<long> CountVotesAsync( Database.Table table, string group, long written, Database.Record shared )
	{
		if ( written == 0 )
			return 0;

		var known = _votesCounted.TryGetValue( group, out var counted );
		if ( known && counted.Written == written )
			return counted.Votes;

		try
		{
			var records = await Shared.ReadAsync( table, group );
			var votes = Votes.Count( records.Select( VoteOf ), shared.Id, shared.OwnerSteamId );
			_votesCounted[ group ] = (written, votes);
			return votes;
		}
		catch ( DatabaseException e ) when ( e.StatusCode == 429 )
		{
			Log.Warning( $"Player Model Selector: told to slow down counting the votes on {shared.Id}: {e.Message}" );
			return known ? counted.Votes : written;
		}
	}

	/// <summary>
	/// Publishes <paramref name="item"/> for its model under <paramref name="name"/> with <paramref name="description"/>
	/// (both tidied, see <see cref="SharePost.Tidy"/>). When the same is shared already, by anyone, that is returned and
	/// nothing is published. The model's items are listed afresh for it, so one shared since the page listed them counts
	/// too. Fails on a duplicate key only if a random id comes up twice.
	/// </summary>
	internal async Task<Entry> ShareAsync( T item, string name, string description )
	{
		var model = _modelOf( item );
		Forget( model );
		if ( SameAs( await ListAsync( model ), item ) is { } same )
			return same;

		var post = PostOf( item, name, description );
		var text = post.Serialize();
		if ( SharePost.TooBig( text ) is { } why )
			throw new ArgumentException( why );

		await Shared.PaceAsync();
		var record = await Database.GetTable( _table ).Create( NewKey(), text, GroupOf( model ), isPublic: true );
		_counts.Remove( model );
		Forget( model );

		// The share stands without it; only the author's list of shares misses it
		try
		{
			await AuthorIndex.RecordAsync( post, record.Id );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Player Model Selector: couldn't add the {_kind} shared for '{model}' to your shares: {e.Message}" );
		}

		return null;
	}

	/// <summary>
	/// <paramref name="item"/> as it would be published under <paramref name="name"/> with <paramref name="description"/>:
	/// for its size, before publishing (see <see cref="SharePost.TooBig"/>).
	/// </summary>
	internal SharePost PostOf( T item, string name, string description ) => new()
	{
		Kind = _kind,
		Model = _modelOf( item ),
		Name = SharePost.Tidy( name, SharePost.MaxNameLength ),
		Description = SharePost.Tidy( description, SharePost.MaxDescriptionLength ),
		Item = _serialize( item ),
	};

	/// <summary>Takes down what this player published as record <paramref name="id"/>. False when it isn't theirs.</summary>
	internal async Task<bool> UnshareAsync( long id, string model )
	{
		var record = await Database.GetTable( _table ).Get( id );
		if ( record is null || record.OwnerSteamId != Game.SteamId.Value )
			return false;

		await Shared.PaceAsync();
		await record.Delete();
		_counts.Remove( model );
		Forget( model );

		try
		{
			await AuthorIndex.ForgetAsync( _kind, id );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Player Model Selector: couldn't take the {_kind} for '{model}' off your shares: {e.Message}" );
		}

		return true;
	}

	/// <summary>
	/// Rates the item with record id <paramref name="id"/>: 1 up, -1 down, 0 takes the rating back. A player's vote is
	/// one record of theirs, in the up or the down group, so changing it moves the record.
	/// </summary>
	internal async Task VoteAsync( long id, int vote )
	{
		var table = Database.GetTable( _votes );
		foreach ( var group in new[] { UpGroup( id ), DownGroup( id ) } )
		{
			foreach ( var record in (await table.ListOwned( group, 0, 1 )).Records )
			{
				await Shared.PaceAsync();
				await record.Delete();
			}
		}

		if ( vote != 0 )
		{
			await Shared.PaceAsync();
			await table.Create( Votes.KeyOf( id, Game.SteamId.Value ), "1", vote > 0 ? UpGroup( id ) : DownGroup( id ), isPublic: true );
		}

		LocalPrefs.SetVote( VoteKey( id ), vote );
		_listed.Clear();
	}

	/// <summary>How this player rated the item with record id <paramref name="id"/>: 1 up, -1 down, 0 not at all.</summary>
	internal int VoteOn( long id ) => LocalPrefs.VoteOn( VoteKey( id ) );

	/// <summary>
	/// Asks how many items are shared for the <paramref name="models"/> not known yet, or known for over five minutes, in
	/// one go (see <see cref="CountOf"/>). Each is marked asked at once, so a page drawn again meanwhile doesn't ask
	/// again. False when nothing needed asking.
	/// </summary>
	internal async Task<bool> CountAsync( IEnumerable<string> models )
	{
		var stale = new List<string>();
		foreach ( var model in models )
		{
			if ( stale.Count < MaxCounted && !string.IsNullOrEmpty( model ) && !stale.Contains( model )
				&& (!_counts.TryGetValue( model, out var known ) || known.Age > CountLife) )
				stale.Add( model );
		}

		if ( stale.Count == 0 )
			return false;

		foreach ( var model in stale )
			_counts[ model ] = (CountOf( model ), 0f);

		var groups = new string[ stale.Count ];
		for ( var i = 0; i < stale.Count; i++ )
			groups[ i ] = GroupOf( stale[ i ] );

		var info = await Database.GetTable( _table ).GetPublicGroupInfo( groups );
		foreach ( var model in stale )
			_counts[ model ] = (info.TryGetValue( GroupOf( model ), out var group ) ? group.Count : 0, 0f);

		return true;
	}

	/// <summary>How many items are shared for <paramref name="model"/>, as last asked; 0 before that.</summary>
	internal long CountOf( string model )
	{
		return model is not null && _counts.TryGetValue( model, out var known ) ? known.Count : 0;
	}

	// A model's group: the same for everyone, within the 128 characters a group takes
	private static string GroupOf( string model ) => model.Md5();

	// Keys are unique across a table, whoever owns the record, and a player publishes many
	private static string NewKey() => $"{Game.SteamId.Value}:{Guid.NewGuid():N}";

	private static string UpGroup( long id ) => $"up:{id}";
	private static string DownGroup( long id ) => $"down:{id}";

	// Votes are remembered by table, as two tables can give records the same id
	private string VoteKey( long id ) => $"{_table}:{id}";

	// Added since the first version of this class, so last: hot reload finds the closures and async methods above by their
	// place in the class

	/// <summary>
	/// Whether there is more of <paramref name="model"/>'s shares than is listed (see <see cref="MoreAsync"/>): the group
	/// goes on past what was read, or more was read than is listed.
	/// </summary>
	internal bool HasMore( string model ) => _read is not null && _read.TryGetValue( model, out var reading ) && (!reading.Ended || reading.MoreToList);

	/// <summary>
	/// Lists more of <paramref name="model"/>'s shares: reads on from where the last read stopped (the next 300 records,
	/// those made after the ones read, as the service gives them in the order they were made) and lists 50 more of all
	/// that was read, ranked with the rest. Fails with the backend's own exception.
	/// </summary>
	internal Task<List<Entry>> MoreAsync( string model )
	{
		var listing = ReadListAsync( model, true );
		_listed[ model ] = (listing, 0f);
		return listing;
	}

	// What has been read of a model's shares: every share found, oldest first; where reading goes on from (after the last
	// record read); whether it got to the end; how many of them are listed, and whether more could be
	private sealed class Reading
	{
		public readonly List<Listed> Found = new();
		public long Cursor;
		public bool Started;
		public bool Ended;
		public int Listed = MaxListed;
		public bool MoreToList;
	}

	// Made where used: a hot reload keeps this library but not fields added since
	private Dictionary<string, Reading> _read;
}
