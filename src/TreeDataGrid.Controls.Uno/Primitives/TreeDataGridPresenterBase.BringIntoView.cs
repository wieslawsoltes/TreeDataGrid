using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Uno.Controls.Primitives;

public abstract partial class TreeDataGridPresenterBase<TItem>
{
    public Control? BringIntoView(int index, Rect? rect = null)
    {
        try { return BringIntoViewCore(index, rect); }
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
        // Specialized presenters must record the target's actual height/width.
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
            // Commit an enlarged extent before requesting an appended target.
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
            // Layout can change the cross-axis extent. Use the final rectangle.
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

    private void RequestNativeBringIntoView(Control element, Rect? rect)
    {
        if (element.IsLoaded)
        {
            element.StartBringIntoView(new BringIntoViewOptions { TargetRect = rect, AnimationDesired = false });
            return;
        }
        // Uno ignores StartBringIntoView on an unloaded FrameworkElement. A new
        // virtualized row has already been measured/arranged but may not receive
        // Loaded until the next dispatcher turn. Request its measured rectangle
        // through this loaded presenter instead of losing the request or waiting
        // for a callback that can refer to a recycled/source-retired container.
        // Native scrolling still owns nesting, clipping and routed request handling.
        if (!ReferenceEquals(element.Parent, this) || !IsLoaded) return;
        var local = rect ?? new Rect(0, 0, element.ActualWidth, element.ActualHeight);
        var target = element.TransformToVisual(this).TransformBounds(local);
        StartBringIntoView(new BringIntoViewOptions { TargetRect = target, AnimationDesired = false });
    }
}
