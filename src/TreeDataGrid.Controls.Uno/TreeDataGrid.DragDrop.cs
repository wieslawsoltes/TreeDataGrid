using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TreeDataGridCore;
using TreeDataGridCore.Selection;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Microsoft.UI.Input;
using RowDropPosition = Uno.Controls.TreeDataGridRowDropPosition;
using IndexPath = TreeDataGridCore.IndexPath;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    public static readonly DependencyProperty AutoDragDropRowsProperty = DependencyProperty.Register(
        nameof(AutoDragDropRows), typeof(bool), typeof(TreeDataGrid), new PropertyMetadata(false, AutoDragDropRowsChanged));
    private const string RowDragFormat = "TreeDataGrid.Controls.Uno.RowDrag";
    // Tokens cross the native data-package boundary; the registry must never
    // keep a source/model alive after the owning operation has gone away.
    private static readonly Dictionary<string, WeakReference<RowDragSession>> RowDrags = new();
    private RowDragCandidate? _rowDragCandidate;
    private RowDragSession? _rowDrag;
    private bool _nativeRowDragActive;
    private Canvas? _dragOverlay;
    private Border? _dropIndicator;
    private RectangleGeometry? _dropClip;
    private DispatcherTimer? _dragScrollTimer;
    private Point? _dragScrollPoint;
    private string? _dragOverToken;
    public bool AutoDragDropRows { get => (bool)GetValue(AutoDragDropRowsProperty); set => SetValue(AutoDragDropRowsProperty, value); }
    public event EventHandler<TreeDataGridRowDragStartedEventArgs>? RowDragStarted;
    public event EventHandler<TreeDataGridRowDragEventArgs>? RowDragOver;
    public event EventHandler<TreeDataGridRowDragEventArgs>? RowDrop;
    public event EventHandler<TreeDataGridRowDragFailedEventArgs>? RowDragFailed;

    private void InitializeRowDragDrop()
    {
        DragStarting += OnNativeRowDragStarting;
        DragEnter += OnNativeRowDragOver;
        DragOver += OnNativeRowDragOver;
        DragLeave += OnNativeRowDragLeave;
        Drop += OnNativeRowDrop;
    }
    private static void AutoDragDropRowsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var grid = (TreeDataGrid)sender;
        grid.AllowDrop = (bool)e.NewValue;
        if (!(bool)e.NewValue) grid.CancelRowDrag();
    }
    private void ApplyDragTemplate()
    {
        StopDropFeedback();
        _dragOverlay = GetTemplateChild("PART_DragOverlay") as Canvas;
        _dropIndicator = GetTemplateChild("PART_DropIndicator") as Border;
        _dropClip = _dragOverlay is null ? null : new RectangleGeometry();
        if (_dragOverlay is not null) _dragOverlay.Clip = _dropClip;
    }
    private void ArmRowDrag(Primitives.TreeDataGridCell cell, PointerRoutedEventArgs e)
    {
        _rowDragCandidate = null;
        if ((!AutoDragDropRows && RowDragStarted is null) || _nativeRowDragActive || ActiveSource is not { } source) return;
        var point = e.GetCurrentPoint(this);
        var dragButton = e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse
            ? point.Properties.IsLeftButtonPressed : e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Pen && point.Properties.IsRightButtonPressed;
        if (!dragButton || IsDragInteractive(e.OriginalSource as DependencyObject, cell)) return;
        if ((uint)cell.RowIndex >= (uint)source.Rows.Count || cell.RowModel is not { } model) return;
        _rowDragCandidate = new(source, source.Rows.RowIndexToModelIndex(cell.RowIndex), model, point.Position, e.Pointer.PointerId);
    }
    private static bool IsDragInteractive(DependencyObject? current, Primitives.TreeDataGridCell cell)
    {
        while (current is not null && !ReferenceEquals(current, cell))
        {
            if (current is ButtonBase or TextBox or PasswordBox or ComboBox or Slider or ToggleSwitch) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }
    protected override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        base.OnPointerMoved(e);
        if (IsOwnInput(e.OriginalSource as DependencyObject)) _selectionInteraction?.OnPointerMoved(this, e);
        if (e.Handled || _rowDragCandidate is not { } candidate || e.Pointer.PointerId != candidate.PointerId) return;
        var point = e.GetCurrentPoint(this);
        var dragButton = e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse
            ? point.Properties.IsLeftButtonPressed : e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Pen && point.Properties.IsRightButtonPressed;
        if (!dragButton) { _rowDragCandidate = null; return; }
        if (Math.Abs(point.Position.X - candidate.Point.X) <= 3 && Math.Abs(point.Position.Y - candidate.Point.Y) <= 3) return;
        _rowDragCandidate = null;
        _pressedPoint = null;
        _tapAllowed = false;
        e.Handled = true;
        _ = StartRowDragAsync(candidate, point);
    }
    private async Task StartRowDragAsync(RowDragCandidate candidate, PointerPoint point)
    {
        if (_nativeRowDragActive) return;
        _nativeRowDragActive = true;
        RowDragSession? session = null;
        try
        {
            if (!ReferenceEquals(ActiveSource, candidate.Source) || !ReferenceEquals(GetModelAt(candidate.Source, candidate.Index), candidate.Model) ||
                !CommitEdit() || !ReferenceEquals(ActiveSource, candidate.Source)) return;
            if (candidate.Source.Selection is not ITreeDataGridRowSelectionModel selection) return;
            if (!selection.IsSelected(candidate.Index))
            {
                var row = candidate.Source.Rows.ModelIndexToRowIndex(candidate.Index);
                if (row < 0 || !SelectCell(row, 0)) return;
            }
            if (!ReferenceEquals(ActiveSource, candidate.Source) || !ReferenceEquals(candidate.Source.Selection, selection)) return;
            var selected = selection.SelectedIndexes.OrderBy(x => x).ToArray();
            if (selected.Length == 0) return;
            // Preserve Avalonia's selected-index contract, including explicitly
            // selected descendants. Core owns movement semantics for that set.
            var models = selected.Select(index => GetModelAt(candidate.Source, index)
                ?? throw new InvalidOperationException("A dragged row is no longer in the source.")).ToArray();
            session = new(candidate.Source, selected, models);
            _rowDrag = session;
            lock (RowDrags) RowDrags.Add(session.Token, new(session));
            // Await the real operation, including cancellation by a later
            // DragStarting handler. Uno does not raise DropCompleted in that case.
            await StartDragAsync(point);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { ReportRowDragError(error); }
        finally
        {
            _nativeRowDragActive = false;
            if (session is not null)
            {
                lock (RowDrags) RowDrags.Remove(session.Token);
                session.Release();
                if (ReferenceEquals(_rowDrag, session)) _rowDrag = null;
            }
            StopDropFeedback();
        }
    }
    private void OnNativeRowDragStarting(UIElement sender, DragStartingEventArgs e)
    {
        if (_rowDrag is not { Source: { } source } session || !session.IsCurrent()) { e.Cancel = true; return; }
        e.AllowedOperations = AutoDragDropRows && !source.IsSorted ? DataPackageOperation.Move : DataPackageOperation.None;
        RowDragStarted?.Invoke(this, new(source, session.Indexes, session.Models, e));
        if (e.Cancel || e.AllowedOperations == DataPackageOperation.None || !session.IsCurrent() || !ReferenceEquals(ActiveSource, source))
        { e.Cancel = true; return; }
        e.Data.Properties[RowDragFormat] = session.Token;
    }
    private static RowDragSession? GetRowDrag(DataPackageView data)
    {
        if (!data.Properties.TryGetValue(RowDragFormat, out var value) || value is not string token) return null;
        lock (RowDrags)
        {
            if (RowDrags.TryGetValue(token, out var weak) && weak.TryGetTarget(out var session) && session.Source is not null) return session;
            RowDrags.Remove(token);
        }
        return null;
    }
    private bool CanAutoDrop(RowDragSession? session, IndexPath target, RowDropPosition position, DataPackageOperation allowed)
    {
        if (!AutoDragDropRows || session?.Source is not { } source || !ReferenceEquals(ActiveSource, source) || source.IsSorted ||
            position is not (RowDropPosition.Before or RowDropPosition.After or RowDropPosition.Inside) ||
            (allowed & DataPackageOperation.Move) == 0 || !session.IsCurrent()) return false;
        if (position == RowDropPosition.Inside && !source.IsHierarchical) return false;
        return !session.Indexes.Any(index => index.IsAncestorOf(target) ||
            (index == target && position == RowDropPosition.Inside));
    }
    private TreeDataGridRowDragEventArgs CreateDropArgs(DragEventArgs e)
    {
        var row = -1;
        var index = default(IndexPath);
        object? model = null;
        var position = RowDropPosition.None;
        if (_scroll is not null && _presenter is not null && ActiveSource is { } source)
        {
            var viewportPoint = e.GetPosition(_scroll);
            var p = e.GetPosition(_presenter);
            if (viewportPoint.X >= 0 && viewportPoint.X <= _scroll.ViewportWidth && viewportPoint.Y >= 0 && viewportPoint.Y <= _scroll.ViewportHeight)
            {
                var candidate = _presenter.GetRowAt(p.Y);
                if ((uint)candidate < (uint)source.Rows.Count)
                {
                    var start = _presenter.GetRowStart(candidate);
                    var height = _presenter.GetRowHeight(candidate);
                    if (p.Y >= start && p.Y < start + height)
                    {
                        row = candidate;
                        index = source.Rows.RowIndexToModelIndex(row);
                        model = source.Rows[row].Model;
                        var fraction = (p.Y - start) / height;
                        position = source.IsHierarchical
                            ? fraction < 0.33 ? RowDropPosition.Before : fraction > 0.66 ? RowDropPosition.After : RowDropPosition.Inside
                            : fraction < 0.5 ? RowDropPosition.Before : RowDropPosition.After;
                    }
                }
            }
        }
        return new(e, TryGetRow(row), row, index, model, position);
    }
    private void OnNativeRowDragOver(object sender, DragEventArgs e)
    {
        if (e.Handled) return;
        try
        {
            var targetSource = ActiveSource;
            var args = CreateDropArgs(e);
            var session = GetRowDrag(e.DataView);
            e.AcceptedOperation = CanAutoDrop(session, args.TargetIndex, args.Position, e.AllowedOperations)
                ? DataPackageOperation.Move : DataPackageOperation.None;
            RowDragOver?.Invoke(this, args);
            e.AcceptedOperation &= e.AllowedOperations;
            if (!ReferenceEquals(ActiveSource, targetSource)) e.AcceptedOperation = DataPackageOperation.None;
            if (e.AcceptedOperation != DataPackageOperation.None && args.Position != RowDropPosition.None)
                ShowDropFeedback(args.TargetRowIndex, args.Position);
            else HideDropIndicator();
            _dragOverToken = session?.Token;
            _dragScrollPoint = _scroll is null ? null : e.GetPosition(_scroll);
            UpdateDragAutoScroll();
            e.Handled = true;
        }
        catch (Exception error) { e.AcceptedOperation = DataPackageOperation.None; StopDropFeedback(); ReportRowDragError(error); }
    }
    private void OnNativeRowDragLeave(object sender, DragEventArgs e) => StopDropFeedback();
    private void OnNativeRowDrop(object sender, DragEventArgs e)
    {
        StopDropFeedback();
        if (e.Handled) return;
        try
        {
            var targetSource = ActiveSource;
            var args = CreateDropArgs(e);
            var session = GetRowDrag(e.DataView);
            e.AcceptedOperation = CanAutoDrop(session, args.TargetIndex, args.Position, e.AllowedOperations)
                ? DataPackageOperation.Move : DataPackageOperation.None;
            // Autoscrolling can put a different model under a stationary pointer.
            // Re-evaluate the application's allow/drop policy for the final row.
            RowDragOver?.Invoke(this, args);
            RowDrop?.Invoke(this, args);
            e.AcceptedOperation &= e.AllowedOperations;
            if (!args.Handled)
            {
                if (e.AcceptedOperation == DataPackageOperation.Move && ReferenceEquals(ActiveSource, targetSource) &&
                    ReferenceEquals(GetModelAt(targetSource, args.TargetIndex), args.TargetModel) &&
                    CanAutoDrop(session, args.TargetIndex, args.Position, e.AllowedOperations))
                    targetSource!.MoveRows(session!.Source!, session.Indexes, args.TargetIndex, (TreeDataGridCore.RowDropPosition)args.Position, RowMoveEffects.Move);
                else e.AcceptedOperation = DataPackageOperation.None;
            }
            e.Handled = true;
        }
        catch (Exception error) { e.AcceptedOperation = DataPackageOperation.None; ReportRowDragError(error); }
    }
    private void ShowDropFeedback(int row, RowDropPosition position)
    {
        if (_dropIndicator is null || _dragOverlay is null || _presenter is null || _scroll is null ||
            ActiveSource is null || (uint)row >= (uint)ActiveSource.Rows.Count) { HideDropIndicator(); return; }
        var top = _presenter.TransformToVisual(_dragOverlay).TransformPoint(new(0, _presenter.GetRowStart(row))).Y;
        var height = _presenter.GetRowHeight(row);
        var left = _scroll.TransformToVisual(_dragOverlay).TransformPoint(default).X;
        Canvas.SetLeft(_dropIndicator, left);
        Canvas.SetTop(_dropIndicator, position == RowDropPosition.After ? top + height - 2 : top);
        _dropIndicator.Width = _scroll.ViewportWidth;
        _dropIndicator.Height = position == RowDropPosition.Inside ? height : 2;
        if (_dropClip is not null) _dropClip.Rect = new(0, 0, _dragOverlay.ActualWidth, _dragOverlay.ActualHeight);
        _dropIndicator.Visibility = Visibility.Visible;
    }
    private void HideDropIndicator() { if (_dropIndicator is not null) _dropIndicator.Visibility = Visibility.Collapsed; }
    private void UpdateDragAutoScroll()
    {
        if (_dragScrollPoint is not { } p || _scroll is null || p.X < 0 || p.X > _scroll.ViewportWidth ||
            (p.Y >= 60 && p.Y <= _scroll.ViewportHeight - 60)) { _dragScrollTimer?.Stop(); return; }
        if (_dragScrollTimer is null)
        {
            _dragScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _dragScrollTimer.Tick += OnDragAutoScroll;
        }
        _dragScrollTimer.Start();
    }
    private void OnDragAutoScroll(object? sender, object e)
    {
        if (!_loaded || _scroll is null || _dragScrollPoint is not { } p) { StopDropFeedback(); return; }
        if (_dragOverToken is { } token)
        {
            lock (RowDrags)
                if (!RowDrags.TryGetValue(token, out var weak) || !weak.TryGetTarget(out var session) || session.Source is null)
                { StopDropFeedback(); return; }
        }
        var step = p.Y < Math.Min(60, _scroll.ViewportHeight / 2) ? -Math.Max(24, MinRowHeight) : Math.Max(24, MinRowHeight);
        var next = Math.Clamp(_scroll.VerticalOffset + step, 0, Math.Max(0, _scroll.ExtentHeight - _scroll.ViewportHeight));
        if (Math.Abs(next - _scroll.VerticalOffset) < 0.01) return;
        HideDropIndicator(); // A new native DragOver (or Drop) recomputes policy and position.
        _scroll.ChangeView(null, next, null, true);
        UpdateViewport();
    }
    private void StopDropFeedback()
    {
        _dragScrollTimer?.Stop();
        _dragScrollPoint = null;
        _dragOverToken = null;
        HideDropIndicator();
    }
    private void CancelRowDrag()
    {
        _rowDragCandidate = null;
        if (_rowDrag is { } session)
        {
            lock (RowDrags) RowDrags.Remove(session.Token);
            session.Release();
            _rowDrag = null;
        }
        StopDropFeedback();
    }
    private void ReportRowDragError(Exception error)
    {
        System.Diagnostics.Trace.TraceError($"TreeDataGrid row drag failed: {error}");
        RowDragFailed?.Invoke(this, new(error));
    }
    private static object? GetModelAt(ITreeDataGridSource? source, IndexPath index)
    {
        if (source is null || index.Count == 0) return null;
        IEnumerable? items = source.Items;
        object? result = null;
        for (var depth = 0; depth < index.Count; ++depth)
        {
            result = null;
            if (items is IList list)
            {
                if ((uint)index[depth] >= (uint)list.Count) return null;
                result = list[index[depth]];
            }
            else if (items is not null)
            {
                var offset = 0;
                foreach (var item in items) if (offset++ == index[depth]) { result = item; break; }
            }
            if (result is null) return null;
            if (depth + 1 < index.Count) items = source.GetModelChildren(result);
        }
        return result;
    }
    private readonly record struct RowDragCandidate(ITreeDataGridSource Source, IndexPath Index, object Model, Point Point, uint PointerId);
    private sealed class RowDragSession(ITreeDataGridSource source, IndexPath[] indexes, object[] models)
    {
        public string Token { get; } = Guid.NewGuid().ToString("N");
        public ITreeDataGridSource? Source { get; private set; } = source;
        public IReadOnlyList<IndexPath> Indexes { get; private set; } = Array.AsReadOnly(indexes);
        public IReadOnlyList<object> Models { get; private set; } = Array.AsReadOnly(models);
        public bool IsCurrent()
        {
            if (Source is null || Indexes.Count == 0) return false;
            for (var i = 0; i < Indexes.Count; ++i)
                if (!ReferenceEquals(GetModelAt(Source, Indexes[i]), Models[i])) return false;
            return true;
        }
        public void Release() { Source = null; Indexes = Array.Empty<IndexPath>(); Models = Array.Empty<object>(); }
    }
}
