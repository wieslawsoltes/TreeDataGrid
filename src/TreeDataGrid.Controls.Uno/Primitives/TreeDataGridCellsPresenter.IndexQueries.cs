using Microsoft.UI.Xaml;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridCellsPresenter
{
    /// <summary>Returns a cell's current visible-column index, or -1 for a non-cell or retired cell.</summary>
    /// <remarks>
    /// This is the native DependencyObject counterpart of the reference logical-child query.
    /// Like the reference, it reads the container index rather than validating parent membership.
    /// It neither enumerates columns nor realizes offscreen cells.
    /// </remarks>
    public int GetChildIndex(DependencyObject child) => child is TreeDataGridCell cell ? cell.ColumnIndex : -1;

    /// <summary>Gets the complete current column count, independently of the realized viewport.</summary>
    /// <returns>False and zero when Items is null; true for an attached empty collection.</returns>
    public bool TryGetTotalCount(out int count)
    {
        var items = Items;
        count = items?.Count ?? 0;
        return items is not null;
    }
}
