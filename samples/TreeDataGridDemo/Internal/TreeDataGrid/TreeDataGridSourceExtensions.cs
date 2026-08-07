using Avalonia.Controls;

namespace TreeDataGridDemo.Internal.TreeDataGrid;

internal static class TreeDataGridSourceExtensions
{
    public static int FindDisplayedRowIndex(this ITreeDataGridSource source, object? item)
    {
        if (item is null)
        {
            return -1;
        }

        for (var index = 0; index < source.Rows.Count; index++)
        {
            if (ReferenceEquals(source.Rows[index].Model, item))
            {
                return index;
            }
        }

        return -1;
    }
}
