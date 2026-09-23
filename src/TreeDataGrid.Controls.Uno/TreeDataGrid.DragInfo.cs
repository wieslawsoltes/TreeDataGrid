using System;
using System.Diagnostics.CodeAnalysis;
using Windows.ApplicationModel.DataTransfer;
using Uno.Controls.Models.TreeDataGrid;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    internal static bool TryGetRowDragInfo(DataPackageView data, [NotNullWhen(true)] out DragInfo? info)
    {
        info = null;
        var session = GetRowDrag(data);
        if (session?.Source is not { } source || !session.IsCurrent() || !ReferenceEquals(source, session.Source))
            return false;
        // Allocate only for an explicit public snapshot request, not for every
        // automatic DragOver. No global metadata cache can prolong model lifetime.
        info = new DragInfo(source, session.Indexes);
        return true;
    }
}
