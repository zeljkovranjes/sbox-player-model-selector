using System;
using System.Collections.Generic;
using Sandbox;

namespace PlayerModels.Engine;

/// <summary>
/// Where a sticker lands on a weapon: the first point of the weapon's own mesh along the way it is pressed, coming from
/// outside, so the skin can press it in a thin slab around that point (as a real sticker sits on the surface it is
/// put on) rather than through everything behind it. Each model's mesh is read once (its points and triangles, in its
/// own space, as the skin shader sees them) and kept for <see cref="MostKept"/> models; each answer is kept too, so
/// dressing the same design again costs nothing.
/// </summary>
internal static class StickerSurface
{
	// The most models whose meshes are kept, and the most answers: past either they are let go of and worked out again
	private const int MostKept = 16;
	private const int MostAnswers = 1024;

	// Rays that graze a triangle's plane closer than this miss it
	private const float Parallel = 1e-7f;

	private sealed record Mesh( Vector3[] Points, uint[] Triangles );

	// Made afresh on a hot reload: what it keeps is made again as it is needed
	[SkipHotload]
	private static readonly RecentCache<string, Mesh> Meshes = new( MostKept );
	[SkipHotload]
	private static readonly Dictionary<(string, Vector3, Vector3), float?> Answers = new();

	/// <summary>
	/// How far along <paramref name="direction"/> (a unit vector) from <paramref name="from"/> the first triangle of
	/// <paramref name="model"/> is, in its own space, within <paramref name="reach"/>; null when the ray meets none (or
	/// the model's mesh can't be read). Reads the model's mesh the first time it is asked about it.
	/// </summary>
	internal static float? Along( Model model, Vector3 from, Vector3 direction, float reach )
	{
		var name = model?.ResourcePath ?? model?.Name;
		if ( name is null )
			return null;

		var key = (name, from, direction);
		if ( Answers.TryGetValue( key, out var known ) )
			return known;

		var found = MeshOf( name, model ) is { } mesh ? First( mesh, from, direction, reach ) : null;
		if ( Answers.Count >= MostAnswers )
			Answers.Clear();

		Answers[ key ] = found;
		return found;
	}

	/// <summary>Counts the mesh of the weapon <paramref name="model"/> (its path) as used by a body <paramref name="distance"/> away (see <see cref="RecentCache{TKey,TValue}.Count"/>).</summary>
	internal static void Count( string model, float distance ) => Meshes.Count( model, distance );

	/// <summary>Reads <paramref name="model"/>'s mesh now, so the first sticker on it doesn't: as a look's designs arrive.</summary>
	internal static void Prepare( Model model )
	{
		if ( (model?.ResourcePath ?? model?.Name) is { } name )
			MeshOf( name, model );
	}

	private static Mesh MeshOf( string name, Model model )
	{
		if ( Meshes.TryGet( name, out var mesh ) )
			return mesh;

		Vertex[] vertices;
		uint[] indices;
		try
		{
			vertices = model.GetVertices();
			indices = model.GetIndices();
		}
		catch ( Exception e )
		{
			Log.Warning( $"Player Model Selector: couldn't read {name}'s mesh for its stickers ({e.Message}); they sit where they are placed." );
			vertices = null;
			indices = null;
		}

		// Only the points are kept: a weapon's full vertices are several times their size
		mesh = null;
		if ( vertices is { Length: > 0 } && indices is { Length: >= 3 } )
		{
			var points = new Vector3[ vertices.Length ];
			for ( var i = 0; i < vertices.Length; i++ )
				points[ i ] = vertices[ i ].Position;

			mesh = new Mesh( points, indices );
		}

		Meshes.Set( name, mesh, name );
		return mesh;
	}

	// The nearest triangle the ray meets (Möller-Trumbore), either side facing
	private static float? First( Mesh mesh, Vector3 from, Vector3 direction, float reach )
	{
		var points = mesh.Points;
		var triangles = mesh.Triangles;
		var nearest = reach;
		var hit = false;
		for ( var t = 0; t + 2 < triangles.Length; t += 3 )
		{
			var (ia, ib, ic) = (triangles[ t ], triangles[ t + 1 ], triangles[ t + 2 ]);
			if ( ia >= points.Length || ib >= points.Length || ic >= points.Length )
				continue;

			var a = points[ ia ];
			var edge1 = points[ ib ] - a;
			var edge2 = points[ ic ] - a;
			var p = Vector3.Cross( direction, edge2 );
			var determinant = Vector3.Dot( edge1, p );
			if ( MathF.Abs( determinant ) < Parallel )
				continue;

			var inverse = 1f / determinant;
			var offset = from - a;
			var u = Vector3.Dot( offset, p ) * inverse;
			if ( u < 0f || u > 1f )
				continue;

			var q = Vector3.Cross( offset, edge1 );
			var v = Vector3.Dot( direction, q ) * inverse;
			if ( v < 0f || u + v > 1f )
				continue;

			var distance = Vector3.Dot( edge2, q ) * inverse;
			if ( distance > 0f && distance < nearest )
			{
				nearest = distance;
				hit = true;
			}
		}

		return hit ? nearest : null;
	}
}
