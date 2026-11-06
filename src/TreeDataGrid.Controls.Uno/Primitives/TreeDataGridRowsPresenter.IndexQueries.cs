using Microsoft.UI.Xaml;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridRowsPresenter
{
    /// <summary>Returns a row container's current visible-row index, or -1 for another or retired element.</summary>
    /// <remarks>
    /// The index belongs to the container, not a copied model-index table. As in the reference,
    /// this query does not validate parent membership or realize an offscreen row.
    /// </remarks>
    public int GetChildIndex(DependencyObject child) => child is TreeDataGridRow row ? row.RowIndex : -1;

    /// <summary>Gets the current flattened visible-row count, not the realized viewport count.</summary>
    /// <returns>False and zero when Items is null; true for an attached empty collection.</returns>
    public bool TryGetTotalCount(out int count)
    {
        var items = Items;
        count = items?.Count ?? 0;
        return items is not null;
    }
}
