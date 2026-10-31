using System;
using System.Linq;
using PlayerModels.Engine;
using Sandbox;

namespace PlayerModels.Components;

public sealed partial class PlayerModelSelector
{
	/// <summary>
	/// The models offered beside <see cref="Models"/>: sbox.game's and the mounted games', checked on this machine. It
	/// fills from the moment the selector starts here, menu open or not, so the menu opens on models already checked.
	/// </summary>
	internal ModelCatalog Catalog { get; } = new();

	// On every machine with a menu to show; a dedicated server has none
	private void StartCatalog()
	{
		if ( Application.IsDedicatedServer )
			return;

		var listed = Models.Select( x => x.Model ).ToHashSet();
		var bodySearches = new[] { CloudSearch, FacepunchSearch, HumanSearch }
			.Concat( MoreSearches ?? new() )
			.Select( x => (x, (string)null) );

		// Players' added folders join once the host's rules are known (see PlayerModelPage.UseFolders); the host's own
		// apply from the start
		Catalog.UseMountFolders( Networking.IsHost ? LocalPrefs.ModelFolders : Array.Empty<string>() );
		_ = Catalog.LoadAsync( bodySearches, ViewModelSearches, CloudCount, listed, () => IsValid );
	}
}
