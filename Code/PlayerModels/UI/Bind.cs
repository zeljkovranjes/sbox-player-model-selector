using System;

namespace PlayerModels.UI;

/// <summary>
/// Handlers for a generic panel's markup (see <see cref="SharingPanel{T}"/>): a lambda declared in a generic type can't
/// be carried over a hot reload (its handlers would throw from then on), so the closures are made here, in a plain
/// class, and call the panel's named methods, which can.
/// </summary>
internal static class Bind
{
	/// <summary>A click handler calling <paramref name="action"/> with <paramref name="argument"/>.</summary>
	internal static Action Click( Action<object> action, object argument ) => () => action( argument );

	/// <summary>A dropdown's handler calling <paramref name="action"/> with <paramref name="key"/> and the value picked, as text.</summary>
	internal static Action<object> Picked( Action<string, string> action, string key ) => value => action( key, value as string );
}
