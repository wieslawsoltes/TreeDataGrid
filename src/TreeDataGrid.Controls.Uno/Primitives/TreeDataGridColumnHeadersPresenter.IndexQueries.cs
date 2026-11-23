using Microsoft.UI.Xaml;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridColumnHeadersPresenter
{
    /// <summary>Returns a header's current visible-column index, or -1 for another or retired element.</summary>
    /// <remarks>
    /// Reads the supplied native container's index, as the reference logical-child query does.
    /// Parent membership is not inferred and no offscreen header is realized.
    /// </remarks>
    public int GetChildIndex(DependencyObject child) => child is TreeDataGridColumnHeader header ? header.ColumnIndex : -1;

    /// <summary>Gets the complete current column count, independently of realized headers.</summary>
    /// <returns>False and zero when Items is null; true for an attached empty collection.</returns>
    public bool TryGetTotalCount(out int count)
    {
        var items = Items;
        count = items?.Count ?? 0;
        return items is not null;
    }
}
