using System;
using System.Collections.Generic;
using System.Text.Json;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.UI;

/// <summary>
/// A weapon's props and stickers as the Advanced editor's gizmo moves them in weapon mode (see <see cref="IGizmoTargets"/>):
/// the props first, then the stickers, each in the weapon's own directions, from its part's middle for a prop on a part,
/// held to the caps every client holds them to (see <see cref="WeaponProp.FittedTo"/>).
/// </summary>
internal sealed class WeaponTargets : IGizmoTargets
{
	private readonly Func<WeaponDesign> _design;
	private readonly Func<ModelRenderer> _weapon;
	private readonly Action _changed;
	private int _selected = -1;

	/// <param name="design">The design of the weapon being edited (made when there is none yet).</param>
	/// <param name="weapon">The weapon in the preview.</param>
	/// <param name="changed">Told after the design changes.</param>
	public WeaponTargets( Func<WeaponDesign> design, Func<ModelRenderer> weapon, Action changed )
	{
		_design = design;
		_weapon = weapon;
		_changed = changed;
	}

	/// <summary>Told when the selection changes, with the prop or sticker selected.</summary>
	public Action<int> Selecting { get; set; }

	public int Selected => _selected;

	public string NothingSelected => "Select a prop or a sticker first: click it, or pick it in its list.";

	/// <summary>How many props there are: the stickers' places come after them.</summary>
	public int PropCount => _design()?.Props?.Count ?? 0;

	public void Select( int index )
	{
		_selected = index;
		Selecting?.Invoke( index );
	}

	private WeaponProp Prop => _design()?.Props is { } props && _selected >= 0 && _selected < props.Count ? props[ _selected ] : null;

	private WeaponSticker Sticker => _design()?.Stickers is { } stickers && _selected >= PropCount && _selected - PropCount < stickers.Count
		? stickers[ _selected - PropCount ]
		: null;

	public bool TryFrame( out TransformGizmo.Frame frame )
	{
		frame = default;
		if ( _weapon() is not { } weapon || !weapon.IsValid() || !TryPose( out var pose ) )
			return false;

		// Where it is now, as an item's frame is: from its part's middle (or the weapon's origin), moved by its offset
		var from = Prop is { } prop ? PartMiddle( weapon.Model, prop.Part ) : Vector3.Zero;
		var world = weapon.WorldTransform;
		frame = new TransformGizmo.Frame( world.PointToWorld( from + pose.Offset ), world.Rotation, world.Scale.x );
		return true;
	}

	public bool TryPose( out TransformGizmo.Pose pose )
	{
		pose = default;
		if ( Prop is { } prop )
		{
			pose = new TransformGizmo.Pose( new Vector3( prop.Forward, prop.Left, prop.Up ), Rotation.From( prop.Pitch, prop.Yaw, prop.Roll ), prop.Scale );
			return true;
		}

		if ( Sticker is { } sticker )
		{
			pose = new TransformGizmo.Pose( new Vector3( sticker.Forward, sticker.Left, sticker.Up ), Rotation.From( sticker.Pitch, sticker.Yaw, sticker.Roll ),
				sticker.Size );
			return true;
		}

		return false;
	}

	public void Write( in TransformGizmo.Pose pose )
	{
		var angles = pose.Turn.Angles();
		if ( Prop is { } prop )
		{
			(prop.Forward, prop.Left, prop.Up) = (pose.Offset.x, pose.Offset.y, pose.Offset.z);
			(prop.Pitch, prop.Yaw, prop.Roll) = (angles.pitch, angles.yaw, angles.roll);
			prop.Scale = pose.Size;
			Fit( prop );
		}
		else if ( Sticker is { } sticker )
		{
			(sticker.Forward, sticker.Left, sticker.Up) = (pose.Offset.x, pose.Offset.y, pose.Offset.z);
			(sticker.Pitch, sticker.Yaw, sticker.Roll) = (angles.pitch, angles.yaw, angles.roll);
			sticker.Size = pose.Size;
			Fit( sticker );
		}

		_changed?.Invoke();
	}

	public void Clear( string key )
	{
		if ( Prop is { } prop )
		{
			if ( key == "g" )
				prop.Forward = prop.Left = prop.Up = 0f;
			else if ( key == "r" )
				prop.Pitch = prop.Yaw = prop.Roll = 0f;
			else
				prop.Scale = 1f;

			Fit( prop );
		}
		else if ( Sticker is { } sticker )
		{
			if ( key == "g" )
				sticker.Forward = sticker.Left = sticker.Up = 0f;
			else if ( key == "r" )
				sticker.Pitch = sticker.Yaw = sticker.Roll = 0f;
			else
				sticker.Size = new WeaponSticker().Size;

			Fit( sticker );
		}

		_changed?.Invoke();
	}

	public void Duplicate()
	{
		var design = _design();
		if ( Prop is { } prop && design.Props.Count < WeaponDesign.MaxProps )
		{
			design.Props.Add( Copy( prop ) );
			_changed?.Invoke();
			Select( design.Props.Count - 1 );
		}
		else if ( Sticker is { } sticker && design.Stickers.Count < WeaponDesign.MaxStickers )
		{
			design.Stickers.Add( Copy( sticker ) );
			_changed?.Invoke();
			Select( PropCount + design.Stickers.Count - 1 );
		}
	}

	public void Remove()
	{
		var design = _design();
		if ( Prop is { } prop )
			design.Props.Remove( prop );
		else if ( Sticker is { } sticker )
			design.Stickers.Remove( sticker );
		else
			return;

		Select( -1 );
		_changed?.Invoke();
	}

	public int At( Ray ray )
	{
		var design = _design();
		if ( _weapon() is not { } weapon || !weapon.IsValid() || design is null )
			return -1;

		var world = weapon.WorldTransform;
		var nearest = float.MaxValue;
		var found = -1;
		for ( var i = 0; i < (design.Props?.Count ?? 0); i++ )
		{
			var prop = design.Props[ i ];
			var size = AccessoryCatalog.SizeOf( Model.Load( prop.Model ) ) * prop.Scale * world.Scale.x;
			var middle = world.PointToWorld( PartMiddle( weapon.Model, prop.Part ) + new Vector3( prop.Forward, prop.Left, prop.Up ) );
			if ( Hits( ray, middle, size * 0.5f, ref nearest ) )
				found = i;
		}

		for ( var i = 0; i < (design.Stickers?.Count ?? 0); i++ )
		{
			var sticker = design.Stickers[ i ];
			var middle = world.PointToWorld( new Vector3( sticker.Forward, sticker.Left, sticker.Up ) );
			if ( Hits( ray, middle, sticker.Size * 0.5f * world.Scale.x, ref nearest ) )
				found = PropCount + i;
		}

		return found;
	}

	public bool TryBounds( out BBox bounds )
	{
		bounds = default;
		if ( !TryFrame( out var frame ) || !TryPose( out var pose ) )
			return false;

		bounds = BBox.FromPositionAndSize( frame.Pivot, MathF.Max( 4f, Prop is not null ? 8f : pose.Size ) );
		return true;
	}

	public string Snapshot() => JsonSerializer.Serialize( _design() );

	public void Restore( string snapshot )
	{
		var design = _design();
		var was = JsonSerializer.Deserialize<WeaponDesign>( snapshot );
		if ( design is null || was is null )
			return;

		design.Props = was.Props ?? new List<WeaponProp>();
		design.Stickers = was.Stickers ?? new List<WeaponSticker>();
		if ( _selected >= PropCount + design.Stickers.Count )
			Select( -1 );

		_changed?.Invoke();
	}

	/// <summary>The middle of <paramref name="part"/> of <paramref name="model"/>, where a prop on it is placed from; the origin for none.</summary>
	internal static Vector3 PartMiddle( Model model, string part ) => part is not null && WeaponParts.BoundsOf( model, part ) is { } bounds ? bounds.Center : Vector3.Zero;

	// Within the caps every client holds it to, for the weapon in the preview
	private void Fit( WeaponProp prop )
	{
		if ( _weapon() is not { } weapon || !weapon.IsValid() )
			return;

		var model = Model.Load( prop.Model );
		var part = prop.Part is null ? null : WeaponParts.BoundsOf( weapon.Model, prop.Part );
		var from = part?.Center ?? Vector3.Zero;
		var partSize = part is { } box ? MathF.Max( box.Size.x, MathF.Max( box.Size.y, box.Size.z ) ) : 0f;
		if ( prop.FittedTo( AccessoryCatalog.SizeOf( model ), WeaponBounds.Point( from.x, from.y, from.z ), BoundsOf( weapon.Model ), partSize ) is not { } fitted )
			return;

		(prop.Forward, prop.Left, prop.Up, prop.Scale) = (fitted.Forward, fitted.Left, fitted.Up, fitted.Scale);
		(prop.Pitch, prop.Yaw, prop.Roll) = (fitted.Pitch, fitted.Yaw, fitted.Roll);
	}

	private void Fit( WeaponSticker sticker )
	{
		if ( _weapon() is not { } weapon || !weapon.IsValid() )
			return;

		var fitted = sticker.Sanitized( x => x )?.FittedTo( BoundsOf( weapon.Model ) );
		if ( fitted is null )
			return;

		(sticker.Forward, sticker.Left, sticker.Up, sticker.Size) = (fitted.Forward, fitted.Left, fitted.Up, fitted.Size);
		(sticker.Pitch, sticker.Yaw, sticker.Roll) = (fitted.Pitch, fitted.Yaw, fitted.Roll);
	}

	/// <summary>A weapon model's bounds, as the caps take them.</summary>
	internal static WeaponBounds BoundsOf( Model model )
	{
		var bounds = model.Bounds;
		return new WeaponBounds( bounds.Mins.x, bounds.Mins.y, bounds.Mins.z, bounds.Maxs.x, bounds.Maxs.y, bounds.Maxs.z );
	}

	private static bool Hits( Ray ray, Vector3 middle, float radius, ref float nearest )
	{
		var along = Vector3.Dot( middle - ray.Position, ray.Forward );
		if ( along <= 0f || along >= nearest || (ray.Position + ray.Forward * along - middle).Length > MathF.Max( radius, 1f ) )
			return false;

		nearest = along;
		return true;
	}

	private static T Copy<T>( T item ) => JsonSerializer.Deserialize<T>( JsonSerializer.Serialize( item ) );
}
