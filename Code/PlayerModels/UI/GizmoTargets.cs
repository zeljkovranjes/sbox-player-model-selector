using System;
using System.Collections.Generic;
using System.Text.Json;
using PlayerModels.Core;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.UI;

/// <summary>
/// What the Advanced editor's gizmo moves, turns and sizes (see <see cref="EditorViewport"/>): the items worn, or in
/// weapon mode a weapon's props and stickers (see <see cref="WeaponTargets"/>). Each knows where the selected one is,
/// writes a pose back within its caps, and keeps snapshots for undo.
/// </summary>
internal interface IGizmoTargets
{
	/// <summary>The one selected, by place; -1 for none.</summary>
	int Selected { get; }

	/// <summary>Selects one, or none with -1.</summary>
	void Select( int index );

	/// <summary>Where the selected one's frame is now (its pivot and axes); false while there is none.</summary>
	bool TryFrame( out TransformGizmo.Frame frame );

	/// <summary>The selected one's pose (offset, turn, size); false while there is none.</summary>
	bool TryPose( out TransformGizmo.Pose pose );

	/// <summary>Sets the selected one's pose, within its caps.</summary>
	void Write( in TransformGizmo.Pose pose );

	/// <summary>Puts the selected one's place ("g"), turn ("r") or size ("s") back as it starts.</summary>
	void Clear( string key );

	/// <summary>Adds a copy of the selected one, and selects it.</summary>
	void Duplicate();

	/// <summary>Takes the selected one off.</summary>
	void Remove();

	/// <summary>The one <paramref name="ray"/> hits first; -1 for none.</summary>
	int At( Ray ray );

	/// <summary>Where the selected one is, to frame the view on; false for none.</summary>
	bool TryBounds( out BBox bounds );

	/// <summary>All of them as text, and back: undo's steps.</summary>
	string Snapshot();
	void Restore( string snapshot );

	/// <summary>What to say when there is nothing selected to move.</summary>
	string NothingSelected { get; }
}

/// <summary>The items worn, as the gizmo moves them: each from its joint, sized within what every client lets it be.</summary>
internal sealed class ItemTargets : IGizmoTargets
{
	private readonly ModelPreview _stage;
	private readonly Func<PlayerLook> _look;
	private readonly Action _changed;
	private readonly Func<int> _selected;
	private readonly Action<int> _select;

	/// <param name="stage">The preview the items are worn in.</param>
	/// <param name="look">The look being edited now.</param>
	/// <param name="changed">Told after the look's items change.</param>
	/// <param name="selected">The item selected, by its place in the look; -1 for none.</param>
	/// <param name="select">Selects an item, or none with -1.</param>
	public ItemTargets( ModelPreview stage, Func<PlayerLook> look, Action changed, Func<int> selected, Action<int> select )
	{
		_stage = stage;
		_look = look;
		_changed = changed;
		_selected = selected;
		_select = select;
	}

	public int Selected => _selected();

	public string NothingSelected => "Select an item first: click it, or pick it on the Accessories tab.";

	public void Select( int index ) => _select( index );

	private AccessoryRack Rack => _stage.Body?.Accessories;

	private Accessory Chosen
	{
		get
		{
			var look = _look();
			var index = _selected();
			return look is not null && index >= 0 && index < look.Accessories.Count ? look.Accessories[ index ] : null;
		}
	}

	public bool TryFrame( out TransformGizmo.Frame frame )
	{
		frame = default;
		if ( Rack is not { } rack || Chosen is null || !rack.TryGetPlacement( _selected(), out var placement ) )
			return false;

		frame = new TransformGizmo.Frame( placement.Pivot, placement.Heading, placement.Scale );
		return true;
	}

	public bool TryPose( out TransformGizmo.Pose pose )
	{
		pose = default;
		if ( Chosen is not { } accessory )
			return false;

		pose = new TransformGizmo.Pose( new Vector3( accessory.Forward, accessory.Left, accessory.Up ), Rotation.From( accessory.Pitch, accessory.Yaw, accessory.Roll ),
			accessory.Scale );
		return true;
	}

	public void Write( in TransformGizmo.Pose pose )
	{
		if ( Chosen is not { } accessory )
			return;

		accessory.Forward = pose.Offset.x;
		accessory.Left = pose.Offset.y;
		accessory.Up = pose.Offset.z;
		var angles = pose.Turn.Angles();
		accessory.Pitch = angles.pitch;
		accessory.Yaw = angles.yaw;
		accessory.Roll = angles.roll;
		accessory.Scale = pose.Size;
		Clamp( accessory );
		_changed?.Invoke();
	}

	public void Clear( string key )
	{
		if ( Chosen is not { } accessory )
			return;

		if ( key == "g" )
			accessory.Forward = accessory.Left = accessory.Up = 0f;
		else if ( key == "r" )
			accessory.Pitch = accessory.Yaw = accessory.Roll = 0f;
		else
			accessory.Scale = 1f;

		Clamp( accessory );
		_changed?.Invoke();
	}

	public void Duplicate()
	{
		var look = _look();
		if ( Chosen is not { } accessory || look.Accessories.Count >= Accessory.MaxWorn )
			return;

		look.Accessories.Add( JsonSerializer.Deserialize<Accessory>( JsonSerializer.Serialize( accessory ) ) );
		_changed?.Invoke();
		_select( look.Accessories.Count - 1 );
	}

	public void Remove()
	{
		var look = _look();
		var index = _selected();
		if ( index < 0 || index >= look.Accessories.Count )
			return;

		look.Accessories.RemoveAt( index );
		_select( -1 );
		_changed?.Invoke();
	}

	public int At( Ray ray )
	{
		if ( Rack is not { } rack )
			return -1;

		var nearest = float.MaxValue;
		var found = -1;
		for ( var i = 0; i < rack.Count; i++ )
		{
			if ( !rack.TryGetPlacement( i, out var placement ) || !placement.Renderer.IsValid() )
				continue;

			if ( placement.Renderer.Bounds.Trace( ray, 10000f, out var distance ) && distance < nearest )
			{
				nearest = distance;
				found = i;
			}
		}

		return found;
	}

	public bool TryBounds( out BBox bounds )
	{
		bounds = default;
		if ( Rack is not { } rack || !rack.TryGetPlacement( _selected(), out var placement ) || !placement.Renderer.IsValid() )
			return false;

		bounds = placement.Renderer.Bounds;
		return true;
	}

	public string Snapshot() => JsonSerializer.Serialize( _look()?.Accessories ?? new List<Accessory>() );

	public void Restore( string snapshot )
	{
		var look = _look();
		if ( look is null )
			return;

		look.Accessories = JsonSerializer.Deserialize<List<Accessory>>( snapshot ) ?? new List<Accessory>();
		if ( _selected() >= look.Accessories.Count )
			_select( -1 );

		_changed?.Invoke();
	}

	// Within what every client lets the item do on this model, as the sliders are
	private void Clamp( Accessory accessory )
	{
		var body = _stage.Body?.Renderer?.Model;
		if ( Rack is not { } rack || !rack.TryGetPlacement( _selected(), out var placement ) )
			return;

		var fitted = accessory.FittedTo( AccessoryCatalog.HeightOf( body ), placement.Size, placement.Follows );
		accessory.Forward = fitted.Forward;
		accessory.Left = fitted.Left;
		accessory.Up = fitted.Up;
		accessory.Pitch = fitted.Pitch;
		accessory.Yaw = fitted.Yaw;
		accessory.Roll = fitted.Roll;
		accessory.Scale = fitted.Scale;
	}
}
