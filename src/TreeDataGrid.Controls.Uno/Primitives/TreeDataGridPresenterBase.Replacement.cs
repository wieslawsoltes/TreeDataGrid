using System;
using System.Collections.Specialized;
using Microsoft.UI.Xaml.Controls;

namespace Uno.Controls.Primitives;

public abstract partial class TreeDataGridPresenterBase<TItem>
{
    private int _replacementGeneration;
    private Action<Control>? _replacementRecycle;
    private Action<Control, int, int>? _replacementUpdate;
    private Action<Control, int>? _replacementDisplace;

    private void ReplaceItems(NotifyCollectionChangedEventArgs change)
    {
        if (change.OldStartingIndex < 0 || change.OldStartingIndex != change.NewStartingIndex ||
            change.OldItems is null || change.NewItems is null)
        {
            _pendingReset = true;
            return;
        }
        var generation = _replacementGeneration = _generation;
        var index = change.OldStartingIndex;
        var oldCount = change.OldItems.Count;
        var newCount = change.NewItems.Count;
        var focused = _focusedElement;
        var focusedIndex = _focusedIndex;
        var scrollTo = _scrollToElement;
        var scrollToIndex = _scrollToIndex;
        // Cached delegates avoid a closure allocation on ordinary row replacement.
        var recycle = _replacementRecycle ??= RecycleReplacementElement;
        if (oldCount == newCount)
            _realizedElements!.ItemsReplaced(index, oldCount, recycle);
        else
            _realizedElements!.ItemsReplaced(index, oldCount, newCount,
                _replacementUpdate ??= UpdateReplacementElementIndex, recycle,
                _replacementDisplace ??= RecycleDisplacedReplacementElement);
        EnsureGeneration(generation);
        RemapReplacementSpecial(ref _focusedElement, ref _focusedIndex, focused, focusedIndex, index, oldCount, newCount);
        EnsureGeneration(generation);
        RemapReplacementSpecial(ref _scrollToElement, ref _scrollToIndex, scrollTo, scrollToIndex, index, oldCount, newCount);
        EnsureGeneration(generation);
    }

    private void RecycleReplacementElement(Control element)
    {
        EnsureGeneration(_replacementGeneration);
        RecycleElementOnItemRemoved(element);
        EnsureGeneration(_replacementGeneration);
    }

    private void UpdateReplacementElementIndex(Control element, int oldIndex, int newIndex)
    {
        EnsureGeneration(_replacementGeneration);
        UpdateElementIndex(element, oldIndex, newIndex);
        EnsureGeneration(_replacementGeneration);
    }

    private void RecycleDisplacedReplacementElement(Control element, int index)
    {
        EnsureGeneration(_replacementGeneration);
        RecycleElement(element, index);
        EnsureGeneration(_replacementGeneration);
    }

    private void RemapReplacementSpecial(ref Control? element, ref int elementIndex,
        Control? captured, int capturedIndex, int index, int oldCount, int newCount)
    {
        // A callback may have installed a newer focus/bring-into-view lease.
        if (captured is null || !ReferenceEquals(element, captured) || elementIndex != capturedIndex) return;
        var oldEnd = checked(index + oldCount);
        if (capturedIndex >= index && capturedIndex < oldEnd)
        {
            // Detach every alias and the focus handler before model cleanup.
            if (ReferenceEquals(_focusedElement, captured))
            {
                captured.LostFocus -= OnUnrealizedFocusedElementLostFocus;
                _focusedElement = null;
                _focusedIndex = -1;
            }
            if (ReferenceEquals(_scrollToElement, captured))
            {
                _scrollToElement = null;
                _scrollToIndex = -1;
            }
            element = null;
            elementIndex = -1;
            RecycleReplacementElement(captured);
        }
        else if (capturedIndex >= oldEnd && oldCount != newCount)
        {
            var target = checked(capturedIndex + newCount - oldCount);
            if ((ReferenceEquals(_focusedElement, captured) && _focusedIndex == target) ||
                (ReferenceEquals(_scrollToElement, captured) && _scrollToIndex == target))
                elementIndex = target;
            else
                UpdateSpecialElementIndex(captured, ref elementIndex, target);
        }
    }
}
