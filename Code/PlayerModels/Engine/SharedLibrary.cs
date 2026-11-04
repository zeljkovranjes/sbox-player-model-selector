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

	// The most listed for one model: the votes on all of them are counted in one ask, two groups each (100 at most)
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

	// What is listed for a model is asked for again next time: this player changed it
	private void Forget( string model ) => _listed.Remove( model );

	private async Task<List<Entry>> ReadListAsync( string model )
	{
		var records = await Shared.ReadAsync( Database.GetTable( _table ), GroupOf( model ) );
		var shared = records
			.Select( x => (Record: x, Post: SharePost.Parse( x.Value )) )
			.Select( x => (x.Record, x.Post, Item: x.Post is null ? default : Parse( x.Post.Item )) )
			.Where( x => x.Item is not null && x.Post.Kind == _kind && x.Post.Model == model && _modelOf( x.Item ) == model )
			.GroupBy( x => x.Record.OwnerSteamId )
			.SelectMany( x => x.OrderByDescending( y => y.Record.CreatedAt ).Take( _perPlayer ) )
			.OrderByDescending( x => x.Record.CreatedAt )
			.Take( MaxListed )
			.ToList();

		_counts[ model ] = (shared.Count, 0f);
		if ( shared.Count == 0 )
			return new List<Entry>();

		var table = Database.GetTable( _votes );
		var written = await table.GetPublicGroupInfo(
			shared.SelectMany( x => new[] { UpGroup( x.Record.Id ), DownGroup( x.Record.Id ) } ).ToArray() );
		long WrittenIn( string group ) => written.TryGetValue( group, out var info ) ? info.Count : 0;

		var entries = new List<Entry>();
		foreach ( var (record, post, item) in shared )
		{
			var up = await CountVotesAsync( table, UpGroup( record.Id ), WrittenIn( UpGroup( record.Id ) ), record );
			var down = await CountVotesAsync( table, DownGroup( record.Id ), WrittenIn( DownGroup( record.Id ) ), record );
			entries.Add( new Entry( record.Id, record.OwnerSteamId, item, post.Name, post.Description, record.CreatedAt, up, down ) );
		}

		var listed = new List<Entry>();
		foreach ( var entry in entries.OrderByDescending( x => x.Score ).ThenBy( x => x.Changed ) )
		{
			if ( entry.IsMine || !listed.Any( x => Same( x.Item, entry.Item ) ) )
				listed.Add( entry );
		}

		return listed.OrderByDescending( x => x.Score ).ThenByDescending( x => x.Changed ).ToList();
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
			var votes = Votes.Count( records.Select( x => (x.OwnerSteamId, x.Key) ), shared.Id, shared.OwnerSteamId );
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
		if ( (await ListAsync( model )).FirstOrDefault( x => Same( x.Item, item ) ) is { } same )
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
		var stale = models
			.Where( x => !string.IsNullOrEmpty( x ) && (!_counts.TryGetValue( x, out var known ) || known.Age > CountLife) )
			.Distinct()
			.Take( MaxCounted )
			.ToList();

		if ( stale.Count == 0 )
			return false;

		foreach ( var model in stale )
			_counts[ model ] = (CountOf( model ), 0f);

		var info = await Database.GetTable( _table ).GetPublicGroupInfo( stale.Select( GroupOf ).ToArray() );
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
}
