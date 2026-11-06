namespace Uno.Controls.Presentation;

/// <summary>Arithmetic for retained container indices, without source ownership.</summary>
internal static class RowIndexMath
{
    /// <summary>Checks the final remapped index, not an overflowing intermediate sum.</summary>
    /// <remarks>The caller validates the source range and nonnegative counts.</remarks>
    internal static int ShiftSuffix(int index, int removedCount, int addedCount) =>
        checked((int)((long)index - removedCount + addedCount));
}
