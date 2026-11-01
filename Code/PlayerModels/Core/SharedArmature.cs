using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PlayerModels.Core;

/// <summary>
/// An armature passed between players: the model it was made for and the bones picked by hand for the body parts the
/// automatic setup couldn't find (see <see cref="PlayerLook.Bones"/>). Whoever uses one takes only what fits their copy
/// of the model.
/// </summary>
public sealed class SharedArmature
{
	// Room for every body part with a long bone name, and nothing far beyond: a shared armature is read from anyone
	private const int MaxModelLength = 512;
	private const int MaxBones = 64;
	private const int MaxNameLength = 128;

	/// <summary>The model it was made for: an sbox.game package, a mount path or a model path.</summary>
	public string Model { get; set; } = "";

	/// <summary>A humanoid role (a <c>BoneRole</c> name) to the name of the bone picked for it.</summary>
	public Dictionary<string, string> Bones { get; set; } = new();

	/// <summary>The armature as JSON.</summary>
	public string Serialize()
	{
		return JsonSerializer.Serialize( this );
	}

	/// <summary>
	/// An armature read back from <see cref="Serialize"/>, or null when <paramref name="json"/> isn't one: unreadable,
	/// without a model or bones, or past the limits a real one keeps to.
	/// </summary>
	public static SharedArmature Parse( string json )
	{
		if ( string.IsNullOrWhiteSpace( json ) )
			return null;

		SharedArmature armature;
		try
		{
			armature = JsonSerializer.Deserialize<SharedArmature>( json );
		}
		catch ( JsonException )
		{
			return null;
		}

		if ( armature?.Bones is null || string.IsNullOrWhiteSpace( armature.Model ) || armature.Model.Length > MaxModelLength )
			return null;

		if ( armature.Bones.Count == 0 || armature.Bones.Count > MaxBones )
			return null;

		var named = armature.Bones.All( x => IsName( x.Key ) && IsName( x.Value ) );
		return named ? armature : null;
	}

	/// <summary>
	/// The picks this armature can give a model: for a part that model's setup leaves open (<paramref name="open"/>, by
	/// role name), a bone it has (<paramref name="bones"/>) that its automatic setup doesn't use (<paramref name="taken"/>),
	/// each bone once. What doesn't fit is left out, so one made for another version of the model gives what still does.
	/// </summary>
	public Dictionary<string, string> FittedTo( IReadOnlySet<string> open, IReadOnlySet<string> bones, IReadOnlySet<string> taken )
	{
		var fitted = new Dictionary<string, string>();
		var used = new HashSet<string>();
		foreach ( var (role, bone) in Bones )
		{
			if ( open.Contains( role ) && bones.Contains( bone ) && !taken.Contains( bone ) && used.Add( bone ) )
				fitted[ role ] = bone;
		}

		return fitted;
	}

	/// <summary>Whether <paramref name="other"/> gives the same bones to the same parts of the same model.</summary>
	public bool SameAs( SharedArmature other )
	{
		return other is not null && other.Model == Model && other.Bones.Count == Bones.Count
			&& Bones.All( x => other.Bones.TryGetValue( x.Key, out var bone ) && bone == x.Value );
	}

	private static bool IsName( string name )
	{
		return !string.IsNullOrWhiteSpace( name ) && name.Length <= MaxNameLength;
	}
}
