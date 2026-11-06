using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Uno.Controls.Primitives;

/// <summary>
/// A themeable header grip that delegates dragging and pointer capture to a
/// native Thumb. Thumb is sealed on Uno and Windows App SDK.
/// </summary>
[TemplatePart(Name = "PART_Thumb", Type = typeof(Thumb))]
public partial class TreeDataGridColumnResizer : Control
{
    private InputSystemCursor? _resizeCursor;
    private Thumb? _thumb;
    private int _templateVersion;

    public TreeDataGridColumnResizer()
    {
        DefaultStyleKey = typeof(TreeDataGridColumnResizer);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public event DragStartedEventHandler? DragStarted;
    public event DragDeltaEventHandler? DragDelta;
    public event DragCompletedEventHandler? DragCompleted;

    public bool IsDragging => _thumb?.IsDragging == true;
    public void CancelDrag()
    {
        // Capture the original native resource: completion can replace the template.
        if (_thumb is { } thumb) CancelThumbDrag(thumb);
    }

    internal static void CancelThumbDrag(Thumb thumb)
    {
        Exception? failure = null;
        try { thumb.CancelDrag(); }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            // Uno's Thumb.CancelDrag changes IsDragging and emits completion,
            // but does not release capture. The retired grip must not retain it.
            try { thumb.ReleasePointerCaptures(); }
            catch (Exception cleanup) when (failure is not null) { throw new AggregateException(failure, cleanup); }
        }
    }

    protected override void OnApplyTemplate()
    {
        var version = ++_templateVersion;
        var previous = _thumb;
        if (previous is not null)
        {
            // Complete the old drag while its completion event is still wired,
            // so a header cannot remain in the resizing state after retemplating.
            try { CancelThumbDrag(previous); }
            finally
            {
                previous.DragStarted -= OnDragStarted;
                previous.DragDelta -= OnDragDelta;
                previous.DragCompleted -= OnDragCompleted;
                if (ReferenceEquals(_thumb, previous)) _thumb = null;
            }
        }
        if (version != _templateVersion) return;
        base.OnApplyTemplate();
        if (version != _templateVersion) return;
        _thumb = GetTemplateChild("PART_Thumb") as Thumb;
        if (_thumb is not null)
        {
            _thumb.DragStarted += OnDragStarted;
            _thumb.DragDelta += OnDragDelta;
            _thumb.DragCompleted += OnDragCompleted;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        ProtectedCursor = _resizeCursor ??= InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        try { CancelDrag(); }
        finally
        {
            ProtectedCursor = null;
            _resizeCursor?.Dispose();
            _resizeCursor = null;
        }
    }

    private void OnDragStarted(object sender, DragStartedEventArgs e)
    {
        if (ReferenceEquals(sender, _thumb)) DragStarted?.Invoke(this, e);
    }
    private void OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (ReferenceEquals(sender, _thumb)) DragDelta?.Invoke(this, e);
    }
    private void OnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (ReferenceEquals(sender, _thumb)) DragCompleted?.Invoke(this, e);
    }
}
