using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridCellsPresenter
{
    // Native Visibility changes damage the subtree even when a parented cell is
    // reused later in the very same horizontal measure. Retain only the pending
    // visual decision; model identities and lifecycle events still retire normally.
    private readonly HashSet<TreeDataGridCell> _layoutVisibility = new();
    private bool _measuringCellVisibility;

    protected override bool PreserveRecycledElementVisibility(Control element)
    {
        if (element is not TreeDataGridCell cell || !_deferred.Contains(cell)) return false;
        // Existing row retirement is covered by the parent's synchronous hide.
        if (_deferRowRebind) return true;
        // This extension never defers an ordinary collection removal or a public
        // Unrealize across dispatcher turns. Only our own guarded measure may
        // retain visibility, with unconditional completion before that call returns.
        if (!_measuringCellVisibility || !IsInLayout || _resettingCells) return false;
        _layoutVisibility.Add(cell);
        return true;
    }

    private Size MeasureWithRecyclingVisibility(Size availableSize)
    {
        if (_measuringCellVisibility) return DesiredSize;
        _measuringCellVisibility = true;
        Exception? failure = null;
        try
        {
            if (_resettingCells || RowIndex < 0 || _row?.IsResettingCells == true || Rows is null ||
                RowIndex >= Rows.Count || Items is not Models.TreeDataGrid.IColumns) return default;
            // Preserve natural-width discovery and native measurement exactly.
            // No cached DesiredSize is substituted for a dirty native subtree.
            var result = base.MeasureOverride(Presenter is not null ? new Size(double.PositiveInfinity, availableSize.Height) : availableSize);
            return Presenter is { } presenter
                ? new(presenter.Geometry.TotalWidth, Math.Max(presenter.RowEstimate, result.Height)) : result;
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            _measuringCellVisibility = false;
            try { FinishCellRecyclingVisibility(); }
            catch (Exception cleanup) when (failure is not null) { throw new AggregateException(failure, cleanup); }
        }
    }

    private void FinishCellRecyclingVisibility()
    {
        List<Exception>? errors = null;
        while (_layoutVisibility.Count != 0)
        {
            // Native setters can retire the source or start a newer measure.
            // Release this decision before invoking one; never enumerate across
            // application callbacks or collapse a newer realized cell identity.
            var iterator = _layoutVisibility.GetEnumerator();
            iterator.MoveNext();
            var cell = iterator.Current;
            iterator.Dispose();
            _layoutVisibility.Remove(cell);
            if (cell.RowIndex >= 0) continue;
            try { cell.Visibility = Visibility.Collapsed; }
            catch (Exception error) { (errors ??= new()).Add(error); }
        }
        ThrowErrors(errors);
    }
}
