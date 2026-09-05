using System;
using System.Collections.Specialized;
using Uno.Controls.Presentation;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridRowsPresenter
{
    private readonly RowGeometry _rows = new();
    private double _rowHeight = double.NaN;
    private double _minimumRowHeight = 28;
    private (int Row, double Offset)? _anchor;
    public double RowHeight
    {
        get => _rowHeight;
        set => ConfigureRows(value, _minimumRowHeight);
    }
    internal bool AutoRowHeight => double.IsNaN(_rowHeight);
    internal double RowEstimate => AutoRowHeight ? _minimumRowHeight : _rowHeight;
    internal void ConfigureRows(double height, double minimum)
    {
        if ((!double.IsNaN(height) && (!double.IsFinite(height) || height <= 0)) || !double.IsFinite(minimum) || minimum <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Row height must be positive or Auto (NaN); minimum height must be positive.");
        if (_rowHeight.Equals(height) && _minimumRowHeight == minimum) return;
        var anchor = CaptureAnchor();
        _rowHeight = height;
        _minimumRowHeight = minimum;
        _rows.Reset(_presentation?.Rows.Count ?? 0, RowEstimate);
        RestoreAnchor(anchor);
        InvalidateMeasure();
    }
    public double GetRowStart(int row) => _rows.Start(row);
    public double GetRowHeight(int row) => _rows.Height(row);
    public int GetRowAt(double offset) => _rows.RowAt(offset);
    internal void CancelPendingAnchor() => _anchor = null;
    internal void InvalidateRowHeight(int row)
    {
        if (!AutoRowHeight || (uint)row >= (uint)_rows.Count) return;
        var anchor = CaptureAnchor();
        _rows.Invalidate(row, 1);
        RestoreAnchor(anchor);
        InvalidateMeasure();
    }
    internal void InvalidateRowMeasurements()
    {
        if (!AutoRowHeight) return;
        var anchor = CaptureAnchor();
        _rows.Reset(_presentation?.Rows.Count ?? 0, RowEstimate);
        RestoreAnchor(anchor);
        InvalidateMeasure();
    }
    private (int Row, double Offset) CaptureAnchor()
    {
        if (_anchor is { } anchor) return anchor;
        if (_rows.Count == 0) return (-1, 0);
        var row = Math.Min(_rows.Count - 1, _rows.RowAt(_verticalOffset));
        return (row, Math.Max(0, _verticalOffset - _rows.Start(row)));
    }
    private void RestoreAnchor((int Row, double Offset) anchor)
    {
        if (anchor.Row < 0 || _rows.Count == 0) { _anchor = null; return; }
        _anchor = (Math.Min(anchor.Row, _rows.Count - 1), anchor.Offset);
        _verticalOffset = _rows.Start(_anchor.Value.Row) + anchor.Offset;
        Owner?.QueueVerticalAnchor(_verticalOffset);
    }
    private void UpdateRowGeometry(NotifyCollectionChangedEventArgs e)
    {
        var anchor = CaptureAnchor();
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewStartingIndex >= 0:
                _rows.Insert(e.NewStartingIndex, e.NewItems!.Count);
                if (anchor.Row >= e.NewStartingIndex) anchor.Row += e.NewItems.Count;
                break;
            case NotifyCollectionChangedAction.Remove when e.OldStartingIndex >= 0:
                _rows.Remove(e.OldStartingIndex, e.OldItems!.Count);
                if (anchor.Row >= e.OldStartingIndex && anchor.Row < e.OldStartingIndex + e.OldItems.Count)
                    anchor = (e.OldStartingIndex, 0);
                else if (anchor.Row >= e.OldStartingIndex) anchor.Row -= e.OldItems.Count;
                break;
            case NotifyCollectionChangedAction.Move when e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0:
                _rows.Move(e.OldStartingIndex, e.NewStartingIndex, e.OldItems!.Count);
                if (anchor.Row >= 0) anchor.Row = RowGeometry.MapMove(anchor.Row, e.OldStartingIndex, e.NewStartingIndex, e.OldItems.Count);
                break;
            case NotifyCollectionChangedAction.Replace when e.NewStartingIndex >= 0:
                _rows.Invalidate(e.NewStartingIndex, e.NewItems!.Count);
                break;
            default:
                // Reset/sort preserves the viewport's display position, not the
                // former model identity: sorting must show the newly sorted rows.
                _rows.Reset(_presentation!.Rows.Count, RowEstimate);
                break;
        }
        RestoreAnchor(anchor);
    }
}
