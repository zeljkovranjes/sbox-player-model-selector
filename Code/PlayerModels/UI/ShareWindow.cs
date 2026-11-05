using System;
using System.Collections.Generic;
using PlayerModels.Core;
using Sandbox;
using Sandbox.UI;

namespace PlayerModels.UI;

/// <summary>
/// The window asking what to call a share and what to say about it, before it is published. Made here rather than in
/// <see cref="SharingPanel{T}"/>, which is generic: the window's handlers are lambdas, and a generic type's can't be
/// carried over a hot reload.
/// </summary>
internal static class ShareWindow
{
	/// <summary>
	/// Opens the window over <paramref name="owner"/> for a <paramref name="noun"/>; Publish closes it and calls
	/// <paramref name="publish"/> with the name and description given. Null when it couldn't open.
	/// </summary>
	internal static UIModal Show( Panel owner, string noun, Action<string, string> publish )
	{
		var name = "";
		var description = "";
		UIModal modal = null;

		bool HasName() => !string.IsNullOrWhiteSpace( name );

		void Confirm()
		{
			if ( !HasName() )
				return;

			modal?.Close();
			publish( name, description );
		}

		var properties = new List<SerializedProperty>
		{
			Game.TypeLibrary.CreateProperty( "Name", () => name, v => name = v,
				new Attribute[] { new PropertyAttribute(), new PlaceholderAttribute( $"What to call it, up to {SharePost.MaxNameLength} characters" ) } ),
			Game.TypeLibrary.CreateProperty( "Description", () => description, v => description = v,
				new Attribute[] { new PropertyAttribute(), new TextAreaAttribute(), new PlaceholderAttribute( $"Optional, up to {SharePost.MaxDescriptionLength} characters" ) } ),
			new UIActionProperty( "Publish", "share", Confirm, 1, separator: true ) { Enabled = HasName },
			new UIActionProperty( "Cancel", null, () => modal?.Close(), 2, sameLine: true ),
		};

		modal = UIModal.Show( owner, $"Share your {noun}", properties );
		if ( modal is not null )
			modal.OnSubmit = Confirm;

		return modal;
	}
}
