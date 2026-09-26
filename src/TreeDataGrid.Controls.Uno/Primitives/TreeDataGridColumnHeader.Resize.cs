using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Uno.Controls.Presentation;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridColumnHeader
{
    private int _resizeVersion;
    private int _dragRealization = -1;

    private readonly struct ResizeGuard(TreeDataGridColumnHeader header, int realization, int operation) : IColumnResizeGuard
    {
        public bool IsCurrent => !header._unrealizing && !header._pendingUnrealize &&
            realization == header._realizationVersion && operation == header._resizeVersion &&
            header._model is not null && header.CanUserResize;
    }

    private void OnResizeStarted(object sender, DragStartedEventArgs e)
    {
        if (!ReferenceEquals(sender, _resizer) || _unrealizing || _pendingUnrealize || _model is null || !CanUserResize) return;
        // An earlier native event observer may have cancelled/retired the drag.
        if (_resizer is Thumb { IsDragging: true } or TreeDataGridColumnResizer { IsDragging: true })
        {
            _resizing = true;
            _dragRealization = _realizationVersion;
        }
    }

    private void OnResizeCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!ReferenceEquals(sender, _resizer)) return;
        _resizing = false;
        _dragRealization = -1;
    }

    private void OnResizeDelta(object sender, DragDeltaEventArgs e)
    {
        if (!ReferenceEquals(sender, _resizer) || !_resizing || _dragRealization != _realizationVersion ||
            !CanUserResize || _columns is not { } columns || _model is not { } column) return;
        var guard = new ResizeGuard(this, _realizationVersion, unchecked(++_resizeVersion));
        ColumnResizeTransaction.Resize(columns, column, ColumnIndex, ActualWidth, e.HorizontalChange, guard);
    }

    private void OnResizeDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, _resizer) || !CanUserResize || _columns is not { } columns || _model is not { } column) return;
        var guard = new ResizeGuard(this, _realizationVersion, unchecked(++_resizeVersion));
        if (ColumnResizeTransaction.SetWidth(columns, column, ColumnIndex, GridLength.Auto, guard)) e.Handled = true;
    }

    private void CancelHeaderResize()
    {
        _dragRealization = -1;
        unchecked { ++_resizeVersion; }
        if (_resizer is Thumb thumb) TreeDataGridColumnResizer.CancelThumbDrag(thumb);
        else if (_resizer is TreeDataGridColumnResizer grip) grip.CancelDrag();
    }
}
