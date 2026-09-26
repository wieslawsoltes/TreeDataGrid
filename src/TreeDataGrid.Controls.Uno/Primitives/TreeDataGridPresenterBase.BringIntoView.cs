using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;

namespace Uno.Controls.Primitives;

public abstract partial class TreeDataGridPresenterBase<TItem>
{
    private long _bringIntoViewRequest;

    public Control? BringIntoView(int index, Rect? rect = null)
    {
        // A later explicit request supersedes any deferred correction, including
        // requests made reentrantly by native bring-into-view event handlers.
        var request = ++_bringIntoViewRequest;
        var generation = _generation;
        try
        {
            var element = BringIntoViewCore(index, rect);
            if (element is not null && generation == _generation && request == _bringIntoViewRequest &&
                ReferenceEquals(element.Parent, this))
            {
                var bounds = element.TransformToVisual(this).TransformBounds(
                    rect ?? new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                QueueBringIntoViewCorrection(new(this), request, generation, index, rect, bounds, 0);
            }
            return element;
        }
        catch (RetiredLayoutException) { return null; }
        finally { if (!_isInLayout && !_resetting) FlushRetiredLayout(); }
    }

    private Control? BringIntoViewCore(int index, Rect? rect)
    {
        var items = Items;
        if (_isInLayout || _resetting || items is null || index < 0 || index >= items.Count ||
            _realizedElements is null || !IsEffectivelyVisible()) return null;
        var generation = _generation;
        if (GetRealizedElement(index) is Control element)
        {
            RequestNativeBringIntoView(element, rect);
            EnsureGeneration(generation);
            return element;
        }
        if (!IsLoaded) return null;

        var scrollToElement = GetOrCreateElement(items, index);
        MeasureElement(index, scrollToElement, new Size(double.PositiveInfinity, double.PositiveInfinity));
        EnsureGeneration(generation);
        var anchorU = GetElementPosition(index);
        if (anchorU < 0) anchorU = _realizedElements.GetOrEstimateElementU(index, ref _lastEstimatedElementSizeU);
        var elementRect = Orientation == Orientation.Horizontal
            ? new Rect(anchorU, 0, scrollToElement.DesiredSize.Width, scrollToElement.DesiredSize.Height)
            : new Rect(0, anchorU, scrollToElement.DesiredSize.Width, scrollToElement.DesiredSize.Height);
        scrollToElement.Arrange(elementRect);
        EnsureGeneration(generation);
        _scrollToElement = scrollToElement;
        _scrollToIndex = index;
        try
        {
            if (!Contains(new Rect(0, 0, ActualWidth, ActualHeight), elementRect) && !Contains(Viewport, elementRect))
            {
                _isWaitingForViewportUpdate = true;
                try { UpdateLayout(); }
                finally { _isWaitingForViewportUpdate = false; }
                EnsureGeneration(generation);
            }
            RequestNativeBringIntoView(scrollToElement, rect);
            EnsureGeneration(generation);
            _isWaitingForViewportUpdate = !Contains(Viewport, elementRect);
            UpdateLayout();
            EnsureGeneration(generation);
            if (_isWaitingForViewportUpdate)
            {
                _isWaitingForViewportUpdate = false;
                InvalidateMeasure();
                UpdateLayout();
                EnsureGeneration(generation);
            }
            RequestNativeBringIntoView(scrollToElement, rect);
            EnsureGeneration(generation);
            return scrollToElement;
        }
        finally
        {
            if (generation == _generation && ReferenceEquals(_scrollToElement, scrollToElement))
            {
                _scrollToElement = null;
                _scrollToIndex = -1;
            }
            _isWaitingForViewportUpdate = false;
        }
    }

    private void QueueBringIntoViewCorrection(WeakReference<TreeDataGridPresenterBase<TItem>> owner,
        long request, int generation, int index, Rect? rect, Rect requestedBounds, int pass)
    {
        // Native effective-viewport notification can arrive after UpdateLayout
        // returns. A newly measured preceding row then shifts the requested row.
        // Repeating the old container rectangle synchronously cannot fix this.
        // No source or container is captured by the queued work; it is bounded,
        // generation-checked, and incurs no polling on ordinary scroll frames.
        if (pass >= 8 || !IsLoaded || _isDetached) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!owner.TryGetTarget(out var presenter) || !presenter.IsLoaded || presenter._isDetached ||
                generation != presenter._generation || request != presenter._bringIntoViewRequest ||
                presenter.Items is not { } items || (uint)index >= (uint)items.Count) return;
            if (presenter._isInLayout || presenter._resetting)
            {
                presenter.QueueBringIntoViewCorrection(owner, request, generation, index, rect, requestedBounds, pass + 1);
                return;
            }
            var position = presenter.GetElementPosition(index);
            if (generation != presenter._generation || request != presenter._bringIntoViewRequest) return;
            // Generic custom presenters may not expose precise position geometry.
            // Only use a current realization as their fallback, never a recycled
            // instance saved by the original request.
            var current = presenter.GetRealizedElement(index);
            if (position < 0 && current is not null)
            {
                var slot = LayoutInformation.GetLayoutSlot(current);
                position = presenter.Orientation == Orientation.Horizontal ? slot.Left : slot.Top;
            }
            if (!double.IsFinite(position) || position < 0) return;
            var next = requestedBounds;
            if (presenter.Orientation == Orientation.Horizontal)
            {
                next.X = position + (rect?.X ?? 0);
                if (rect is null && current is not null) next.Width = current.ActualWidth;
            }
            else
            {
                next.Y = position + (rect?.Y ?? 0);
                if (rect is null && current is not null) next.Height = current.ActualHeight;
            }
            if (next == requestedBounds) return;
            // Keep native routing, clipping, cancellation, nested viewers and
            // alignment. Direct ChangeView would bypass those contracts.
            presenter.StartBringIntoView(new BringIntoViewOptions { TargetRect = next, AnimationDesired = false });
            if (generation != presenter._generation || request != presenter._bringIntoViewRequest) return;
            presenter.InvalidateMeasure();
            presenter.QueueBringIntoViewCorrection(owner, request, generation, index, rect, next, pass + 1);
        });
    }

    private void RequestNativeBringIntoView(Control element, Rect? rect)
    {
        if (element.IsLoaded)
        {
            element.StartBringIntoView(new BringIntoViewOptions { TargetRect = rect, AnimationDesired = false });
            return;
        }
        // Newly created virtualized elements can be measured/arranged before
        // Loaded. Route their measured rectangle through the loaded presenter.
        if (!ReferenceEquals(element.Parent, this) || !IsLoaded) return;
        var local = rect ?? new Rect(0, 0, element.ActualWidth, element.ActualHeight);
        var target = element.TransformToVisual(this).TransformBounds(local);
        StartBringIntoView(new BringIntoViewOptions { TargetRect = target, AnimationDesired = false });
    }
}
