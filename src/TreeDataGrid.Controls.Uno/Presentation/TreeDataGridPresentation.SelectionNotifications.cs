using System;
using System.Collections.Generic;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Selection;

namespace Uno.Controls.Presentation;

public abstract partial class TreeDataGridPresentation
{
    private int _nativeSelectionRevision;
    internal event EventHandler<TreeDataGridSelectionChangedEventArgs>? NativeSelectionChanged;

    /// <summary>Publishes a custom presentation's Core row-selection delta through the native grid.</summary>
    /// <remarks>
    /// This is an explicit notification hook, not a second Core selection observer.
    /// Built-in selection already publishes its own deltas. Indexes/items are captured
    /// before native observers can mutate the source; borrowed models are not copied.
    /// </remarks>
    protected void RaiseNativeSelectionChanged(TreeSelectionModelSelectionChangedEventArgs e)
    {
        if (!_viewRowsActive || _viewRowsDisposed || NativeSelectionChanged is null) return;
        ArgumentNullException.ThrowIfNull(e);
        var revision = unchecked(++_nativeSelectionRevision);
        var deselected = e.DeselectedIndexes.ToArray();
        if (!Current()) return;
        var selected = e.SelectedIndexes.ToArray();
        if (!Current()) return;
        var deselectedItems = e.DeselectedItems.ToArray();
        if (!Current()) return;
        var selectedItems = e.SelectedItems.ToArray();
        if (!Current()) return;
        NativeSelectionChanged?.Invoke(this, new(deselected, selected, deselectedItems, selectedItems));
        bool Current() => !_viewRowsDisposed && _viewRowsActive && revision == _nativeSelectionRevision;
    }

    /// <summary>Publishes model-space cell-selection deltas without fabricating row selections.</summary>
    protected void RaiseNativeCellSelectionChanged(IEnumerable<CellIndex> deselected, IEnumerable<CellIndex> selected)
    {
        if (!_viewRowsActive || _viewRowsDisposed || NativeSelectionChanged is null) return;
        ArgumentNullException.ThrowIfNull(deselected);
        ArgumentNullException.ThrowIfNull(selected);
        var revision = unchecked(++_nativeSelectionRevision);
        var oldCells = deselected.ToArray();
        if (_viewRowsDisposed || !_viewRowsActive || revision != _nativeSelectionRevision) return;
        var newCells = selected.ToArray();
        if (_viewRowsDisposed || !_viewRowsActive || revision != _nativeSelectionRevision) return;
        NativeSelectionChanged?.Invoke(this, new(deselectedCellIndexes: oldCells, selectedCellIndexes: newCells));
    }
}
