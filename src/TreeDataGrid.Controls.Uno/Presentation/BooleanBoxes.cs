using System.Runtime.CompilerServices;

namespace Uno.Controls.Presentation;

/// <summary>Immutable boxes for our own object-valued dependency-property setters.</summary>
internal static class BooleanBoxes
{
    private static readonly object True = true;
    private static readonly object False = false;

    // Still call SetValue on every setter invocation: skipping an equal value
    // would change local-value/binding precedence. Only the redundant box goes.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object Box(bool value) => value ? True : False;
}
