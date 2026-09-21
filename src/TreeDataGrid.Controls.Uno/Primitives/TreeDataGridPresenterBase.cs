using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Diagnostics;
using System.Linq;

namespace Uno.Controls.Primitives
{
    public abstract partial class TreeDataGridPresenterBase<TItem> : Panel
    {
        [Conditional("DEBUG")]
        private void Trace(string message)
        {
            if (PresenterDiagnostics.EnableTracing)
                Debug.WriteLine($"[{GetType().Name}] {message}");
        }

        public static readonly DependencyProperty ElementFactoryProperty = DependencyProperty.Register(
            nameof(ElementFactory), typeof(TreeDataGridElementFactory), typeof(TreeDataGridPresenterBase<TItem>),
            new PropertyMetadata(null, OnElementFactoryChanged));
        public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
            nameof(Items), typeof(IReadOnlyList<TItem>), typeof(TreeDataGridPresenterBase<TItem>),
            new PropertyMetadata(null, OnItemsChanged));

        private static void OnElementFactoryChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var presenter = (TreeDataGridPresenterBase<TItem>)sender;
            ++presenter._generation;
            presenter._pendingReset = presenter._pendingFactoryChange = true;
            presenter.InvalidateMeasure();
            if (!presenter._isInLayout && !presenter._resetting) presenter.FlushRetiredLayout();
        }

        private static void OnItemsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var presenter = (TreeDataGridPresenterBase<TItem>)sender;
            var oldItems = presenter._items;
            presenter.UnsubscribeFromItemChanges();
            presenter._items = (IReadOnlyList<TItem>?)e.NewValue;
            presenter.SubscribeToItemChanges();
            try { presenter.OnItemsCollectionChanged(null, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)); }
            finally { presenter.OnItemsChanged(oldItems, presenter._items); }
        }
        // Dependency-property callbacks replace Avalonia's OnPropertyChanged hook.
        protected virtual void OnItemsChanged(IReadOnlyList<TItem>? oldItems, IReadOnlyList<TItem>? newItems) { }
        private static readonly Rect s_invalidViewport = new(double.PositiveInfinity, double.PositiveInfinity, 0, 0);
        private readonly Action<Control, int> _recycleElement;
        private readonly Action<Control> _recycleElementOnItemRemoved;
        private readonly Action<Control, int, int> _updateElementIndex;
        private int _scrollToIndex = -1;
        private Control? _scrollToElement;
        private TreeDataGridElementFactory? _elementFactory;
        private bool _isInLayout;
        private bool _isWaitingForViewportUpdate;
        private IReadOnlyList<TItem>? _items;
        private Rect _lastMeasureViewport = s_invalidViewport;
        private bool _isSubscribedToItemChanges;
        private bool _isDetached;
        private int _generation;
        private int _layoutGeneration;
        private bool _pendingReset;
        private bool _pendingFactoryChange;
        private bool _resetting;
        private RealizedStackElements? _measureElements;
        private RealizedStackElements? _realizedElements;
        private IScrollAnchorProvider? _scrollAnchorProvider;
        private double _lastEstimatedElementSizeU = 25;
        private Control? _focusedElement;
        private int _focusedIndex = -1;
        private bool _preserveRecycledElementLogicalTreeMembership;
        private bool _preserveRecycledElementVisualTreeMembership;
        private int _retainedRecycledElementCount;
        private readonly Dictionary<Control, Size> _previousConstraints = new();
        // Cached state for fast reattachment (fixes for tab switching performance)
        private Rect _cachedViewport;

        public TreeDataGridPresenterBase()
        {
            _recycleElement = RecycleElement;
            _recycleElementOnItemRemoved = RecycleElementOnItemRemoved;
            _updateElementIndex = UpdateElementIndex;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public TreeDataGridElementFactory? ElementFactory
        {
            get => (TreeDataGridElementFactory?)GetValue(ElementFactoryProperty);
            set => SetValue(ElementFactoryProperty, value);
        }

        public IReadOnlyList<TItem>? Items
        {
            get => (IReadOnlyList<TItem>?)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        internal IReadOnlyList<Control?> RealizedElements => _realizedElements?.Elements ?? Array.Empty<Control>();

        protected abstract Orientation Orientation { get; }
        protected bool IsInLayout => _isInLayout;
        protected int PresenterGeneration => _generation;
        internal bool IsLayoutInProgress => _isInLayout;

        internal Rect Viewport { get; private set; } = s_invalidViewport;

        /// <summary>
        /// Gets the position of the first realized element on the primary axis.
        /// </summary>
        protected double StartU => _realizedElements?.StartU ?? 0;

        protected int FirstIndex => _realizedElements?.FirstIndex ?? 0;

        public Control? BringIntoView(int index, Rect? rect = null)
        {
            try { return BringIntoViewCore(index, rect); }
            catch (RetiredLayoutException) { return null; }
            finally { if (!_isInLayout && !_resetting) FlushRetiredLayout(); }
        }

        private Control? BringIntoViewCore(int index, Rect? rect)
        {
            var items = Items;

            if (_isInLayout || _resetting ||
                items is null ||
                index < 0 ||
                index >= items.Count ||
                _realizedElements is null ||
                !IsEffectivelyVisible())
                return null;

            if (GetRealizedElement(index) is Control element)
            {
                if (rect.HasValue)
                    element.StartBringIntoView(new BringIntoViewOptions { TargetRect = rect.Value, AnimationDesired = false });
                else
                    element.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
                return element;
            }
            else if (IsLoaded)
            {
                // Create and measure the element to be brought into view. Store it in a field so that
                // it can be re-used in the layout pass.
                var scrollToElement = GetOrCreateElement(items, index);
                // A specialized presenter must record the target's actual row
                // height/column width before computing its scroll rectangle.
                // Calling Control.Measure directly bypasses that layout contract.
                MeasureElement(index, scrollToElement, new Size(double.PositiveInfinity, double.PositiveInfinity));

                // Get the expected position of the element and put it in place.
                var anchorU = GetElementPosition(index);
                if (anchorU < 0) anchorU = _realizedElements.GetOrEstimateElementU(index, ref _lastEstimatedElementSizeU);
                var elementRect = Orientation == Orientation.Horizontal ?
                    new Rect(anchorU, 0, scrollToElement.DesiredSize.Width, scrollToElement.DesiredSize.Height) :
                    new Rect(0, anchorU, scrollToElement.DesiredSize.Width, scrollToElement.DesiredSize.Height);
                scrollToElement.Arrange(elementRect);

                // Store the element and index so that they can be used in the layout pass.
                _scrollToElement = scrollToElement;
                _scrollToIndex = index;

                // If the item being brought into view was added since the last layout pass then
                // our bounds won't be updated, so any containing scroll viewers will not have an
                // updated extent. Do a layout pass to ensure that the containing scroll viewers
                // will be able to scroll the new item into view.
                if (!Contains(new Rect(0, 0, ActualWidth, ActualHeight), elementRect) && !Contains(Viewport, elementRect))
                {
                    _isWaitingForViewportUpdate = true;
                    UpdateLayout();
                    _isWaitingForViewportUpdate = false;
                }

                // Try to bring the item into view and do a layout pass.
                if (rect.HasValue)
                    scrollToElement.StartBringIntoView(new BringIntoViewOptions { TargetRect = rect.Value, AnimationDesired = false });
                else
                    scrollToElement.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });

                // If the viewport does not contain the item to scroll to, set _isWaitingForViewportUpdate:
                // this should cause the following chain of events:
                // - Measure is first done with the old viewport (which will be a no-op, see MeasureOverride)
                // - The viewport is then updated by the layout system which invalidates our measure
                // - Measure is then done with the new viewport.
                _isWaitingForViewportUpdate = !Contains(Viewport, elementRect);
                UpdateLayout();

                // If for some reason the layout system didn't give us a new viewport during the layout, we
                // need to do another layout pass as the one that took place was a no-op.
                if (_isWaitingForViewportUpdate)
                {
                    _isWaitingForViewportUpdate = false;
                    InvalidateMeasure();
                    UpdateLayout();
                }

                // The first request can use an out-of-date cross-axis extent when elements have
                // different sizes. The intervening layout has now updated that extent, so retry
                // the same request with the final geometry.
                if (rect.HasValue)
                    scrollToElement.StartBringIntoView(new BringIntoViewOptions { TargetRect = rect.Value, AnimationDesired = false });
                else
                    scrollToElement.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });

                _scrollToElement = null;
                _scrollToIndex = -1;
                return scrollToElement;
            }

            return null;
        }

        public virtual IEnumerable<Control> GetRealizedElements()
        {
            if (_realizedElements is not null)
                return _realizedElements.Elements.Where(x => x is not null)!;
            else
                return Array.Empty<Control>();
        }

        public virtual Control? TryGetElement(int index) => GetRealizedElement(index);

        internal void RecycleAllElements(
            bool preserveVisualTreeMembership = false,
            bool preserveLogicalTreeMembership = false)
        {
            var previousVisualValue = _preserveRecycledElementVisualTreeMembership;
            var previousLogicalValue = _preserveRecycledElementLogicalTreeMembership;
            _preserveRecycledElementVisualTreeMembership = preserveVisualTreeMembership;
            _preserveRecycledElementLogicalTreeMembership = preserveLogicalTreeMembership;

            try
            {
                _realizedElements?.RecycleAllElements(_recycleElement);
            }
            finally
            {
                _preserveRecycledElementVisualTreeMembership = previousVisualValue;
                _preserveRecycledElementLogicalTreeMembership = previousLogicalValue;
            }
        }

        internal void RecycleAllElementsOnItemRemoved(
            bool preserveVisualTreeMembership = false,
            bool preserveLogicalTreeMembership = false)
        {
            var previousVisualValue = _preserveRecycledElementVisualTreeMembership;
            var previousLogicalValue = _preserveRecycledElementLogicalTreeMembership;
            _preserveRecycledElementVisualTreeMembership = preserveVisualTreeMembership;
            _preserveRecycledElementLogicalTreeMembership = preserveLogicalTreeMembership;

            try
            {
                if (_realizedElements?.Count > 0)
                {
                    _realizedElements.ItemsRemoved(
                        _realizedElements.FirstIndex,
                        _realizedElements.Count,
                        _updateElementIndex,
                        _recycleElementOnItemRemoved);
                }
                RecycleSpecialElement(ref _focusedElement, ref _focusedIndex);
                RecycleSpecialElement(ref _scrollToElement, ref _scrollToIndex);
            }
            finally
            {
                _preserveRecycledElementVisualTreeMembership = previousVisualValue;
                _preserveRecycledElementLogicalTreeMembership = previousLogicalValue;
            }
        }

        protected virtual Rect ArrangeElement(int index, Control element, Rect rect)
        {
            element.Arrange(rect);
            return rect;
        }

        protected virtual Size MeasureElement(int index, Control element, Size availableSize)
        {
            // Uno's Measure already caches identical valid constraints. Its public
            // API does not expose Avalonia's IsMeasureValid/previous-constraint pair.
            MeasureNative(element, availableSize);
            return element.DesiredSize;
        }

        protected Size? GetPreviousMeasureConstraint(Control element) =>
            _previousConstraints.TryGetValue(element, out var value) ? value : null;

        protected void MeasureNative(Control element, Size availableSize)
        {
            _previousConstraints[element] = availableSize;
            element.Measure(availableSize);
        }

        /// <summary>
        /// Gets the initial constraint for the first pass of the two-pass measure.
        /// </summary>
        /// <param name="element">The element being measured.</param>
        /// <param name="index">The index of the element.</param>
        /// <param name="availableSize">The available size.</param>
        /// <returns>The measure constraint for the element.</returns>
        /// <remarks>
        /// The measure pass is split into two parts:
        ///
        /// - The initial pass is used to determine the "natural" size of the elements. In this
        ///   pass, infinity can be used as the measure constraint if the element has no other
        ///   constraints on its size.
        /// - The final pass is made once the "natural" sizes of the elements are known and any
        ///   layout logic has been run. This pass is needed because controls should not be
        ///   arranged with a size less than that passed as the constraint during the measure
        ///   pass. This pass is only run if <see cref="NeedsFinalMeasurePass(int, IReadOnlyList{Control})"/> returns
        ///   true.
        /// </remarks>
        protected virtual Size GetInitialConstraint(
            Control element,
            int index,
            Size availableSize)
        {
            return availableSize;
        }

        /// <summary>
        /// Called when the initial pass of the two-pass measure has been completed, in order to determine
        /// whether a final measure pass is necessary.
        /// </summary>
        /// <param name="firstIndex">The index of the first element in <paramref name="elements"/>.</param>
        /// <param name="elements">The elements being measured.</param>
        /// <returns>
        /// true if a final pass should be run; otherwise false.
        /// </returns>
        /// <see cref="GetInitialConstraint(Control, int, Size)"/>
        protected virtual bool NeedsFinalMeasurePass(
            int firstIndex,
            IReadOnlyList<Control?> elements) => false;

        /// <summary>
        /// Gets the final constraint for the second pass of the two-pass measure.
        /// </summary>
        /// <param name="element">The element being measured.</param>
        /// <param name="index">The index of the element.</param>
        /// <param name="availableSize">The available size.</param>
        /// <returns>
        /// The measure constraint for the element.
        /// </returns>
        /// <see cref="GetInitialConstraint(Control, int, Size)"/>
        protected virtual Size GetFinalConstraint(
            Control element,
            int index,
            Size availableSize)
        {
            return element.DesiredSize;
        }

        protected virtual Control GetElementFromFactory(TItem item, int index)
        {
            return GetElementFromFactory(item!, index, this);
        }

        protected Control GetElementFromFactory(object data, int index, FrameworkElement parent)
        {
            return _elementFactory!.GetOrCreateElement(data, parent);
        }

        protected virtual (int index, double position) GetElementAt(double position) => (-1, -1);
        protected virtual double GetElementPosition(int index) => -1;
        protected abstract void RealizeElement(Control element, TItem item, int index);
        protected abstract void UpdateElementIndex(Control element, int oldIndex, int newIndex);
        protected abstract void UnrealizeElement(Control element);

        protected virtual void FinalizeRecycledElement(Control element)
        {
        }

        protected virtual double CalculateSizeU(Size availableSize)
        {
            if (Items is null)
                return 0;

            // Return the estimated size of all items based on the elements currently realized.
            return EstimateElementSizeU() * Items.Count;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_isInLayout || _resetting) return DesiredSize;
            FlushRetiredLayout();
            var items = Items;

            if (items is null || items.Count == 0)
            {
                TrimUnrealizedChildren();
                return default;
            }

            // If we're bringing an item into view, ignore any layout passes until we receive a new
            // effective viewport.
            if (_isWaitingForViewportUpdate)
                return EstimateDesiredSize(Orientation, items.Count);

            _isInLayout = true;
            _layoutGeneration = _generation;
            Exception? failure = null;
            var previousVisualValue = _preserveRecycledElementVisualTreeMembership;
            var previousLogicalValue = _preserveRecycledElementLogicalTreeMembership;
            _preserveRecycledElementVisualTreeMembership = true;
            _preserveRecycledElementLogicalTreeMembership = true;

            try
            {
                var orientation = Orientation;

                var traceRealizedElements = PresenterDiagnostics.EnableTracing;
                _realizedElements ??= new(traceRealizedElements);
                _measureElements ??= new(traceRealizedElements);

                // We handle horizontal and vertical layouts here so X and Y are abstracted to:
                // - Horizontal layouts: U = horizontal, V = vertical
                // - Vertical layouts: U = vertical, V = horizontal
                var viewport = CalculateMeasureViewport(items, availableSize);
                RequireCurrentLayout();

                // If the viewport is disjunct then we can recycle everything.
                if (viewport.viewportIsDisjunct)
                    _realizedElements.RecycleAllElements(_recycleElement);
                RequireCurrentLayout();

                // Do the measure, creating/recycling elements as necessary to fill the viewport. Don't
                // write to _realizedElements yet, only _measureElements.
                RealizeElements(items, availableSize, ref viewport);

                // Run the final measure pass if necessary.
                var needsFinalPass = NeedsFinalMeasurePass(_measureElements.FirstIndex, _measureElements.Elements);
                RequireCurrentLayout();
                if (needsFinalPass)
                {
                    var count = _measureElements.Count;

                    for (var i = 0; i < count; ++i)
                    {
                        var e = _measureElements.Elements[i]!;
                        var previous = GetPreviousMeasureConstraint(e)!.Value;

                        var index = _measureElements.FirstIndex + i;
                        var constraint = GetFinalConstraint(e, index, availableSize);
                        RequireCurrentLayout();

                        var needsFinalMeasure = this is IFinalMeasureSelector selector ?
                            selector.NeedsFinalMeasure(e, index) :
                            HasInfinity(previous);
                        RequireCurrentLayout();

                        if (needsFinalMeasure && previous != constraint)
                        {
                            MeasureNative(e, constraint);
                            RequireCurrentLayout();
                            viewport.measuredV = Math.Max(
                                viewport.measuredV,
                                Orientation == Orientation.Horizontal ?
                                    e.DesiredSize.Height : e.DesiredSize.Width);
                        }
                    }
                }

                // Now swap the measureElements and realizedElements collection.
                (_measureElements, _realizedElements) = (_realizedElements, _measureElements);
                _measureElements.ResetForReuse();

                // A focused element is retained when it leaves the realized range. Keep its
                // measurement current so that it can be positioned safely outside the viewport
                // and re-used without losing keyboard focus.
                if (_focusedElement is not null && _focusedIndex >= 0)
                {
                    var constraint = GetInitialConstraint(_focusedElement, _focusedIndex, availableSize);
                    RequireCurrentLayout();
                    MeasureElement(_focusedIndex, _focusedElement, constraint);
                    RequireCurrentLayout();
                }

                TrimUnrealizedChildren();
                RequireCurrentLayout();

                return CalculateDesiredSize(orientation, items.Count, viewport);
            }
            catch (RetiredLayoutException) { return default; }
            catch (Exception error) { failure = error; _pendingReset = true; throw; }
            finally
            {
                _preserveRecycledElementVisualTreeMembership = previousVisualValue;
                _preserveRecycledElementLogicalTreeMembership = previousLogicalValue;
                _isInLayout = false;
                try { FlushRetiredLayout(); }
                catch (Exception cleanup) when (failure is not null) { throw new AggregateException(failure, cleanup); }
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_isInLayout || _resetting) return finalSize;
            FlushRetiredLayout();
            if (_realizedElements is null)
                return finalSize;

            _isInLayout = true;
            _layoutGeneration = _generation;
            Exception? failure = null;

            try
            {
                var orientation = Orientation;
                var u = _realizedElements!.GetOrEstimateElementU(
                    _realizedElements.FirstIndex,
                    ref _lastEstimatedElementSizeU);

                for (var i = 0; i < _realizedElements.Count; ++i)
                {
                    var e = _realizedElements.Elements[i];

                    if (e is not null)
                    {
                        var sizeU = _realizedElements.SizeU[i];
                        var rect = orientation == Orientation.Horizontal ?
                            new Rect(u, 0, sizeU, finalSize.Height) :
                            new Rect(0, u, finalSize.Width, sizeU);
                        rect = ArrangeElement(i + _realizedElements.FirstIndex, e, rect);
                        RequireCurrentLayout();

                        if (e.Visibility == Visibility.Visible && Viewport != s_invalidViewport && Intersects(Viewport, rect))
                        {
                            try
                            {
                                NativeScrollAnchoring.Register(_scrollAnchorProvider, e);
                            }
                            catch (InvalidOperationException ex)
                            {
                                // The element may have been reparented during virtualization.
                                // It is no longer a valid anchor candidate in that case.
                                Trace($"RegisterAnchorCandidate ignored: {ex.Message}");
                            }
                        }

                        u += orientation == Orientation.Horizontal ? rect.Width : rect.Height;
                    }
                }

                // Keep a retained focused element outside the realized range. Position estimates
                // for non-uniform items are approximate, so clamp them to the nearest realized
                // boundary to prevent a retained element from rendering over live rows or cells.
                if (_focusedElement is not null && _focusedIndex >= 0)
                {
                    var realizedEndU = u;
                    var sizeU = orientation == Orientation.Horizontal ?
                        _focusedElement.DesiredSize.Width :
                        _focusedElement.DesiredSize.Height;

                    u = _realizedElements.GetOrEstimateElementU(
                        _focusedIndex,
                        ref _lastEstimatedElementSizeU);

                    if (_realizedElements.Count > 0)
                    {
                        if (_focusedIndex < _realizedElements.FirstIndex)
                            u = Math.Min(u, _realizedElements.StartU - sizeU);
                        else if (_focusedIndex > _realizedElements.LastIndex)
                            u = Math.Max(u, realizedEndU);
                    }

                    var rect = orientation == Orientation.Horizontal ?
                        new Rect(u, 0, sizeU, finalSize.Height) :
                        new Rect(0, u, finalSize.Width, sizeU);
                    ArrangeElement(_focusedIndex, _focusedElement, rect);
                    RequireCurrentLayout();
                }

                return finalSize;
            }
            catch (RetiredLayoutException) { return finalSize; }
            catch (Exception error) { failure = error; _pendingReset = true; throw; }
            finally
            {
                _isInLayout = false;
                try { FlushRetiredLayout(); }
                catch (Exception cleanup) when (failure is not null) { throw new AggregateException(failure, cleanup); }
            }
        }

        protected virtual (int index, double position) GetOrEstimateAnchorElementForViewport(
            double viewportStart,
            double viewportEnd,
            int itemCount)
        {
            Debug.Assert(_realizedElements is not null);

            return _realizedElements.GetOrEstimateAnchorElementForViewport(
                viewportStart,
                viewportEnd,
                itemCount,
                ref _lastEstimatedElementSizeU);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isDetached = false;
            _scrollAnchorProvider = NativeScrollAnchoring.IsSupported ? FindAncestor<IScrollAnchorProvider>() : null;
            EffectiveViewportChanged -= OnEffectiveViewportChanged;
            EffectiveViewportChanged += OnEffectiveViewportChanged;
            SubscribeToItemChanges();
            InvalidateMeasure();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isDetached = true;
            EffectiveViewportChanged -= OnEffectiveViewportChanged;
            UnsubscribeFromItemChanges();
            try
            {
                // Uno has one native parent chain. Keep bounded collapsed children
                // during unload/reload rather than rebuilding their style trees.
                ++_generation;
                _pendingReset = true;
                if (!_isInLayout && !_resetting) FlushRetiredLayout();
            }
            finally { _scrollAnchorProvider = null; }
        }



        protected virtual void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
        {
            Trace($"OnEffectiveViewportChanged: OldViewport={Viewport}, NewEffectiveViewport={e.EffectiveViewport}, BoundsSize={ActualWidth}x{ActualHeight}");
            var vertical = Orientation == Orientation.Vertical;
            var oldViewportWasInvalid = Viewport == s_invalidViewport;
            var oldViewportStart = vertical ? Viewport.Top : Viewport.Left;
            var oldViewportEnd = vertical ? Viewport.Bottom : Viewport.Right;

            // We sometimes get sent a viewport of 0,0 because the EffectiveViewportChanged event
            // is being raised when the parent control hasn't yet been arranged. This is a bug in
            // Avalonia, but we can work around it by forcing MeasureOverride to estimate the
            // viewport.
            Viewport = new Size(e.EffectiveViewport.Width, e.EffectiveViewport.Height) == default ?
                s_invalidViewport :
                Intersect(e.EffectiveViewport, new Rect(0, 0, ActualWidth, ActualHeight));

            // Cache the viewport size for use when estimating viewport on reattachment
            if (Viewport != s_invalidViewport && new Size(Viewport.Width, Viewport.Height) != default)
            {
                _cachedViewport = Viewport;
            }

            _isWaitingForViewportUpdate = false;

            var newViewportStart = vertical ? Viewport.Top : Viewport.Left;
            var newViewportEnd = vertical ? Viewport.Bottom : Viewport.Right;
            if (!PresenterMath.AreClose(oldViewportStart, newViewportStart) ||
                !PresenterMath.AreClose(oldViewportEnd, newViewportEnd))
            {
                if (oldViewportWasInvalid || NeedsMeasureForViewportChange(_lastMeasureViewport, Viewport))
                    InvalidateMeasure();
            }
        }

        protected virtual void UnrealizeElementOnItemRemoved(Control element)
        {
            UnrealizeElement(element);
        }

        private void SubscribeToItemChanges()
        {
            if (!_isDetached && !_isSubscribedToItemChanges && _items is INotifyCollectionChanged newIncc)
            {
                newIncc.CollectionChanged += OnItemsCollectionChanged;
                _isSubscribedToItemChanges = true;
            }
        }

        private void UnsubscribeFromItemChanges()
        {
            if (_isSubscribedToItemChanges && _items is INotifyCollectionChanged oldIncc)
            {
                oldIncc.CollectionChanged -= OnItemsCollectionChanged;
                _isSubscribedToItemChanges = false;
            }
        }

        private void RealizeElements(
            IReadOnlyList<TItem> items,
            Size availableSize,
            ref MeasureViewport viewport)
        {
            Debug.Assert(_measureElements is not null);
            Debug.Assert(_realizedElements is not null);
            Debug.Assert(items.Count > 0);

            var index = viewport.anchorIndex;
            var horizontal = Orientation == Orientation.Horizontal;
            var u = viewport.anchorU;

            // If the anchor element is at the beginning of, or before, the start of the viewport
            // then we can recycle all elements before it.
            if (u <= viewport.anchorU)
                _realizedElements.RecycleElementsBefore(viewport.anchorIndex, _recycleElement);
            RequireCurrentLayout();

            // Start at the anchor element and move forwards, realizing elements.
            do
            {
                var e = GetOrCreateElement(items, index);
                var constraint = GetInitialConstraint(e, index, availableSize);
                RequireCurrentLayout();
                _previousConstraints[e] = constraint;
                var slot = MeasureElement(index, e, constraint);
                RequireCurrentLayout();

                var sizeU = horizontal ? slot.Width : slot.Height;
                var sizeV = horizontal ? slot.Height : slot.Width;

                _measureElements!.Add(index, e, u, sizeU);
                viewport.measuredV = Math.Max(viewport.measuredV, sizeV);

                u += sizeU;
                ++index;
            } while (u < viewport.viewportUEnd && index < items.Count);

            // Store the last index and end U position for the desired size calculation.
            viewport.lastIndex = index - 1;
            viewport.realizedEndU = u;

            // We can now recycle elements after the last element.
            _realizedElements.RecycleElementsAfter(viewport.lastIndex, _recycleElement);
            RequireCurrentLayout();

            // Next move backwards from the anchor element, realizing elements.
            index = viewport.anchorIndex - 1;
            u = viewport.anchorU;

            while (u > viewport.viewportUStart && index >= 0)
            {
                var e = GetOrCreateElement(items, index);
                var constraint = GetInitialConstraint(e, index, availableSize);
                RequireCurrentLayout();
                _previousConstraints[e] = constraint;
                var slot = MeasureElement(index, e, constraint);
                RequireCurrentLayout();

                var sizeU = horizontal ? slot.Width : slot.Height;
                var sizeV = horizontal ? slot.Height : slot.Width;
                u -= sizeU;

                _measureElements.Add(index, e, u, sizeU);
                viewport.measuredV = Math.Max(viewport.measuredV, sizeV);
                --index;
            }

            // We can now recycle elements before the first element.
            _realizedElements.RecycleElementsBefore(index + 1, _recycleElement);
            RequireCurrentLayout();

            Trace($"RealizeElements complete: realized {_measureElements.Count} elements from {_measureElements.FirstIndex} to {viewport.lastIndex}, viewportU=[{viewport.viewportUStart:F1}, {viewport.viewportUEnd:F1}]");
        }

        private Size CalculateDesiredSize(Orientation orientation, int itemCount, in MeasureViewport viewport)
        {
            var sizeU = 0.0;
            var sizeV = viewport.measuredV;

            if (viewport.lastIndex >= 0)
            {
                var remaining = itemCount - viewport.lastIndex - 1;
                sizeU = viewport.realizedEndU + (remaining * EstimateElementSizeU());
            }

            return orientation == Orientation.Horizontal ? new(sizeU, sizeV) : new(sizeV, sizeU);
        }

        private Size EstimateDesiredSize(Orientation orientation, int itemCount)
        {
            if (_scrollToIndex >= 0 && _scrollToElement is not null)
            {
                var remaining = itemCount - _scrollToIndex - 1;
                var realizedEndU = orientation == Orientation.Horizontal ?
                    LayoutInformation.GetLayoutSlot(_scrollToElement).Right :
                    LayoutInformation.GetLayoutSlot(_scrollToElement).Bottom;
                var sizeU = realizedEndU + (remaining * _lastEstimatedElementSizeU);

                return orientation == Orientation.Horizontal ?
                    new Size(sizeU, DesiredSize.Height) :
                    new Size(DesiredSize.Width, sizeU);
            }

            return DesiredSize;
        }

        private MeasureViewport CalculateMeasureViewport(IReadOnlyList<TItem> items, Size availableSize)
        {
            Debug.Assert(_realizedElements is not null);

            // If the control has not yet been laid out then the effective viewport won't have been set.
            // Try to work it out from an ancestor control.
            var viewportIsInvalid = Viewport == s_invalidViewport;
            var viewport = GetViewportForMeasure(availableSize);
            _lastMeasureViewport = viewport;

            Trace($"CalculateMeasureViewport: ViewportIsInvalid={viewportIsInvalid}, Viewport={Viewport}, EstimatedViewport={viewport}, AvailableSize={availableSize}, ItemCount={items.Count}");

            // Get the viewport in the orientation direction.
            var viewportStart = Orientation == Orientation.Horizontal ? viewport.X : viewport.Y;
            var viewportEnd = Orientation == Orientation.Horizontal ? viewport.Right : viewport.Bottom;

            // Get or estimate the anchor element from which to start realization. If we are
            // scrolling to an element, use that as the anchor so that it is consumed by the
            // realization pass instead of being left as a disjoint visible child.
            var itemCount = items.Count;
            int anchorIndex;
            double anchorU;

            if (_scrollToIndex >= 0 && _scrollToElement is not null)
            {
                anchorIndex = _scrollToIndex;
                anchorU = Orientation == Orientation.Horizontal ?
                    LayoutInformation.GetLayoutSlot(_scrollToElement).Left :
                    LayoutInformation.GetLayoutSlot(_scrollToElement).Top;
            }
            else
            {
                (anchorIndex, anchorU) = GetOrEstimateAnchorElementForViewport(
                    viewportStart,
                    viewportEnd,
                    itemCount);
            }

            // Check if the anchor element is not within the currently realized elements.
            var disjunct = anchorIndex < _realizedElements.FirstIndex ||
                anchorIndex > _realizedElements.LastIndex;

            return new MeasureViewport
            {
                anchorIndex = anchorIndex,
                anchorU = anchorU,
                viewportUStart = viewportStart,
                viewportUEnd = viewportEnd,
                viewportIsDisjunct = disjunct,
            };
        }

        protected Control GetOrCreateElement(IReadOnlyList<TItem> items, int index)
        {
            RequireCurrentLayout();
            if (_focusedIndex == index && _focusedElement is { } focused)
                focused.LostFocus -= OnUnrealizedFocusedElementLostFocus;
            var e = GetRealizedElement(index, ref _focusedIndex, ref _focusedElement) ??
                GetRealizedElement(index, ref _scrollToIndex, ref _scrollToElement) ??
                GetRealizedElement(index) ??
                GetRecycledOrCreateElement(items, index);
            return e;
        }

        protected virtual Control? GetRealizedElement(int index)
        {
            return _realizedElements?.GetElement(index);
        }

        private static Control? GetRealizedElement(
            int index,
            ref int specialIndex,
            ref Control? specialElement)
        {
            if (specialIndex == index)
            {
                Debug.Assert(specialElement is not null);

                var result = specialElement;
                specialIndex = -1;
                specialElement = null;
                return result;
            }

            return null;
        }

        private Control GetRecycledOrCreateElement(IReadOnlyList<TItem> items, int index)
        {
            var generation = _generation;
            var item = items[index];
            var element = GetElementFromFactory(item, index);
            if (element.Parent is not null && !ReferenceEquals(element.Parent, this))
                throw new InvalidOperationException("The element factory returned a control belonging to another parent.");
            if (_previousConstraints.ContainsKey(element))
                throw new InvalidOperationException("The element factory returned an already realized control.");
            // This existing constraint map also tracks in-flight realizations so
            // retirement can release controls not yet in the measured range.
            _previousConstraints.Add(element, default);
            var wasRetained = element.Visibility == Visibility.Collapsed && ReferenceEquals(element.Parent, this);
            element.Visibility = Visibility.Visible;
            if (wasRetained && _retainedRecycledElementCount > 0) --_retainedRecycledElementCount;
            try
            {
                if (generation != _generation) throw new RetiredLayoutException();
                // Uno resource/template lookup requires the native parent chain
                // during realization. Reused controls already have this parent.
                if (element.Parent is null) Children.Add(element);
                if (generation != _generation) throw new RetiredLayoutException();
                RealizeElement(element, item, index);
                if (generation != _generation) throw new RetiredLayoutException();
                return element;
            }
            catch (Exception error)
            {
                _previousConstraints.Remove(element);
                try { UnrealizeElement(element); }
                catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                finally { _previousConstraints.Remove(element); RemoveRecycledElement(element); }
                throw;
            }
        }

        protected virtual double EstimateElementSizeU()
        {
            if (_realizedElements is null)
                return _lastEstimatedElementSizeU;

            var result = _realizedElements.EstimateElementSizeU();
            if (result >= 0)
                _lastEstimatedElementSizeU = result;
            return _lastEstimatedElementSizeU;
        }

        protected virtual Rect? GetParentPresenterViewPort()
        {
            return null;
        }

        protected virtual Rect GetMeasureViewport(Rect viewport)
        {
            return viewport;
        }

        protected Rect GetViewportForMeasure(Size availableSize) =>
            GetMeasureViewport(Viewport != s_invalidViewport ? Viewport : EstimateViewport(availableSize));

        protected bool IsViewportCoveredByRealizedElements(Rect viewport)
        {
            var viewportStart = Orientation == Orientation.Vertical ? viewport.Top : viewport.Left;
            var viewportEnd = Orientation == Orientation.Vertical ? viewport.Bottom : viewport.Right;

            return double.IsFinite(viewportStart) &&
                double.IsFinite(viewportEnd) &&
                _realizedElements?.TryGetStableRange(out var realizedStart, out var realizedEnd) == true &&
                !PresenterMath.GreaterThan(realizedStart, viewportStart) &&
                !PresenterMath.GreaterThan(viewportEnd, realizedEnd);
        }

        protected virtual bool NeedsMeasureForViewportChange(Rect measureViewport, Rect viewport)
        {
            return true;
        }

        private Rect EstimateViewport(Size availableSize)
        {
            if (GetParentPresenterViewPort() is { } parentViewport && parentViewport != s_invalidViewport)
                return parentViewport;
            if (_cachedViewport != default) return _cachedViewport;
            for (var parent = VisualTreeHelper.GetParent(this); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is FrameworkElement { ActualWidth: > 0, ActualHeight: > 0 } element)
                {
                    var bounds = element.TransformToVisual(this).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                    return Intersect(bounds, new Rect(0, 0, double.PositiveInfinity, double.PositiveInfinity));
                }
            }
            return new Rect(0, 0, double.IsFinite(availableSize.Width) ? availableSize.Width : XamlRoot?.Size.Width ?? 0,
                double.IsFinite(availableSize.Height) ? availableSize.Height : XamlRoot?.Size.Height ?? 0);
        }

        private void RecycleElement(Control element, int index)
        {
            NativeScrollAnchoring.Unregister(_scrollAnchorProvider, element);

            if (TreeDataGrid.ContainsFocus(element, XamlRoot is { } root ? FocusManager.GetFocusedElement(root) as DependencyObject : null))
            {
                _focusedElement = element;
                _focusedIndex = index;
                _focusedElement.LostFocus += OnUnrealizedFocusedElementLostFocus;
            }
            else
            {
                var factory = _elementFactory;
                _previousConstraints.Remove(element);
                try
                {
                    UnrealizeElement(element);
                    element.Visibility = Visibility.Collapsed;
                    DetachElement(element);
                    RecycleElementToFactory(element, factory);
                }
                catch { RemoveRecycledElement(element); throw; }
            }
        }

        private void RecycleElementOnItemRemoved(Control element)
        {
            var factory = _elementFactory;
            NativeScrollAnchoring.Unregister(_scrollAnchorProvider, element);

            if (element == _focusedElement)
            {
                _focusedElement.LostFocus -= OnUnrealizedFocusedElementLostFocus;
                _focusedElement = null;
                _focusedIndex = -1;
            }

            _previousConstraints.Remove(element);
            try
            {
                UnrealizeElementOnItemRemoved(element);
                element.Visibility = Visibility.Collapsed;
                DetachElement(element);
                RecycleElementToFactory(element, factory);
            }
            catch { RemoveRecycledElement(element); throw; }
        }

        private void DetachElement(Control element)
        {
            if (_preserveRecycledElementVisualTreeMembership || _preserveRecycledElementLogicalTreeMembership)
            {
                if (!OwnsRecyclingPool) ++_retainedRecycledElementCount;
            }
            else if (ReferenceEquals(element.Parent, this))
                RemoveRecycledElement(element);
        }

        protected virtual void RecycleElementToFactory(Control element, TreeDataGridElementFactory? factory) => factory!.RecycleElement(element);
        protected virtual bool OwnsRecyclingPool => false;
        protected virtual void RemoveRecycledElement(Control element) => Children.Remove(element);

        protected virtual void TrimUnrealizedChildren()
        {
            if (_retainedRecycledElementCount == 0) return;
            // Factory pools are bounded at 64; never retain an unbounded second
            // collection of hidden native children outside that pool.
            var allowance = Math.Min(64, Items?.Count ?? 0);
            var retained = 0;
            foreach (var control in Children.OfType<Control>().ToArray())
            {
                if (control.Visibility != Visibility.Collapsed) continue;
                FinalizeRecycledElement(control);
                if (++retained > allowance) RemoveRecycledElement(control);
            }
            _retainedRecycledElementCount = Math.Min(retained, allowance);
        }

        private TAncestor? FindAncestor<TAncestor>() where TAncestor : class
        {
            for (var parent = VisualTreeHelper.GetParent(this); parent is not null; parent = VisualTreeHelper.GetParent(parent))
                if (parent is TAncestor match) return match;
            return null;
        }

        private bool IsEffectivelyVisible()
        {
            if (!IsLoaded) return false;
            for (DependencyObject? current = this; current is not null; current = VisualTreeHelper.GetParent(current))
                if (current is UIElement { Visibility: Visibility.Collapsed }) return false;
            return true;
        }

        protected override IEnumerable<DependencyObject> GetChildrenInTabFocusOrder() => GetRealizedElements();

        private static bool Contains(Rect outer, Rect inner) => inner.Left >= outer.Left && inner.Top >= outer.Top &&
            inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;

        private static bool Intersects(Rect a, Rect b) => a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

        protected virtual void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ++_generation;
            InvalidateMeasure();
            if (_isInLayout || _resetting)
            {
                _pendingReset = true;
                return;
            }

            if (_realizedElements is null)
                return;

            var previousVisualValue = _preserveRecycledElementVisualTreeMembership;
            var previousLogicalValue = _preserveRecycledElementLogicalTreeMembership;
            _preserveRecycledElementVisualTreeMembership = true;
            _preserveRecycledElementLogicalTreeMembership = true;
            _resetting = true;

            try
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        UpdateSpecialElementForInsert(ref _focusedElement, ref _focusedIndex, e.NewStartingIndex, e.NewItems!.Count);
                        UpdateSpecialElementForInsert(ref _scrollToElement, ref _scrollToIndex, e.NewStartingIndex, e.NewItems.Count);
                        _realizedElements.ItemsInserted(e.NewStartingIndex, e.NewItems.Count, _updateElementIndex, _recycleElementOnItemRemoved);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        _realizedElements.ItemsRemoved(e.OldStartingIndex, e.OldItems!.Count, _updateElementIndex, _recycleElementOnItemRemoved);
                        UpdateSpecialElementForRemove(ref _focusedElement, ref _focusedIndex, e.OldStartingIndex, e.OldItems.Count);
                        UpdateSpecialElementForRemove(ref _scrollToElement, ref _scrollToIndex, e.OldStartingIndex, e.OldItems.Count);
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        _realizedElements.ItemsReplaced(e.OldStartingIndex, e.OldItems!.Count, _recycleElementOnItemRemoved);
                        RecycleSpecialElementInRange(ref _focusedElement, ref _focusedIndex, e.OldStartingIndex, e.OldItems.Count);
                        RecycleSpecialElementInRange(ref _scrollToElement, ref _scrollToIndex, e.OldStartingIndex, e.OldItems.Count);
                        break;
                    case NotifyCollectionChangedAction.Move:
                        _realizedElements.ItemsMoved(e.OldStartingIndex, e.NewStartingIndex, e.OldItems!.Count, _updateElementIndex, _recycleElementOnItemRemoved);
                        UpdateSpecialElementForMove(ref _focusedElement, ref _focusedIndex, e.OldStartingIndex, e.NewStartingIndex, e.OldItems.Count);
                        UpdateSpecialElementForMove(ref _scrollToElement, ref _scrollToIndex, e.OldStartingIndex, e.NewStartingIndex, e.OldItems.Count);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        _realizedElements.ItemsReset(_recycleElementOnItemRemoved);
                        RecycleSpecialElement(ref _focusedElement, ref _focusedIndex);
                        RecycleSpecialElement(ref _scrollToElement, ref _scrollToIndex);
                        break;
                }
            }
            catch { _pendingReset = true; throw; }
            finally
            {
                _preserveRecycledElementVisualTreeMembership = previousVisualValue;
                _preserveRecycledElementLogicalTreeMembership = previousLogicalValue;
                _resetting = false;
                FlushRetiredLayout();
            }
        }

        private void UpdateSpecialElementForInsert(
            ref Control? element,
            ref int elementIndex,
            int index,
            int count)
        {
            if (element is not null && elementIndex >= index)
                UpdateSpecialElementIndex(element, ref elementIndex, elementIndex + count);
        }

        private void UpdateSpecialElementForRemove(
            ref Control? element,
            ref int elementIndex,
            int index,
            int count)
        {
            if (element is null)
                return;

            if (elementIndex >= index && elementIndex < index + count)
                RecycleSpecialElement(ref element, ref elementIndex);
            else if (elementIndex >= index + count)
                UpdateSpecialElementIndex(element, ref elementIndex, elementIndex - count);
        }

        private void UpdateSpecialElementForMove(
            ref Control? element,
            ref int elementIndex,
            int oldIndex,
            int newIndex,
            int count)
        {
            if (element is null)
                return;

            var updatedIndex = elementIndex;

            if (elementIndex >= oldIndex && elementIndex < oldIndex + count)
                updatedIndex = newIndex + elementIndex - oldIndex;
            else if (oldIndex < newIndex && elementIndex >= oldIndex + count && elementIndex < newIndex + count)
                updatedIndex -= count;
            else if (oldIndex > newIndex && elementIndex >= newIndex && elementIndex < oldIndex)
                updatedIndex += count;

            UpdateSpecialElementIndex(element, ref elementIndex, updatedIndex);
        }

        private void RecycleSpecialElementInRange(
            ref Control? element,
            ref int elementIndex,
            int index,
            int count)
        {
            if (element is not null && elementIndex >= index && elementIndex < index + count)
                RecycleSpecialElement(ref element, ref elementIndex);
        }

        private void RecycleSpecialElement(ref Control? element, ref int elementIndex)
        {
            if (element is { } value)
                RecycleElementOnItemRemoved(value);

            element = null;
            elementIndex = -1;
        }

        private void UpdateSpecialElementIndex(Control element, ref int elementIndex, int newIndex)
        {
            if (elementIndex != newIndex)
            {
                var oldIndex = elementIndex;
                elementIndex = newIndex;
                UpdateElementIndex(element, oldIndex, newIndex);
            }
        }

        private void OnUnrealizedFocusedElementLostFocus(object? sender, RoutedEventArgs e)
        {
            if (_focusedElement is null || !ReferenceEquals(sender, _focusedElement))
                return;
            var element = _focusedElement;
            if (TreeDataGrid.ContainsFocus(element, XamlRoot is { } root ? FocusManager.GetFocusedElement(root) as DependencyObject : null))
                return;
            var index = _focusedIndex;
            element.LostFocus -= OnUnrealizedFocusedElementLostFocus;
            _focusedElement = null;
            _focusedIndex = -1;
            var visual = _preserveRecycledElementVisualTreeMembership;
            var logical = _preserveRecycledElementLogicalTreeMembership;
            _preserveRecycledElementVisualTreeMembership = _preserveRecycledElementLogicalTreeMembership = true;
            try
            {
                RecycleElement(element, index);
                FinalizeRecycledElement(element);
            }
            finally
            {
                _preserveRecycledElementVisualTreeMembership = visual;
                _preserveRecycledElementLogicalTreeMembership = logical;
            }
            InvalidateMeasure();
        }

        private sealed class RetiredLayoutException : Exception { }

        private void RequireCurrentLayout()
        {
            if (_isInLayout && _layoutGeneration != _generation) throw new RetiredLayoutException();
        }

        protected void EnsureCurrentLayout() => RequireCurrentLayout();

        protected void EnsureGeneration(int generation)
        {
            if (generation != _generation) throw new RetiredLayoutException();
        }

        protected Control? TryRealizeElementAt(int index)
        {
            if (_resetting || Items is not { } items || (uint)index >= (uint)items.Count) return null;
            try { return GetOrCreateElement(items, index); }
            catch (RetiredLayoutException) { return null; }
            finally { if (!_isInLayout && !_resetting) FlushRetiredLayout(); }
        }

        protected void RetireLayout()
        {
            ++_generation;
            _pendingReset = true;
            InvalidateMeasure();
            if (!_isInLayout && !_resetting) FlushRetiredLayout();
        }

        private void FlushRetiredLayout()
        {
            if (!_pendingReset || _isInLayout || _resetting) return;
            _resetting = true;
            var visual = _preserveRecycledElementVisualTreeMembership;
            var logical = _preserveRecycledElementLogicalTreeMembership;
            _preserveRecycledElementVisualTreeMembership = _preserveRecycledElementLogicalTreeMembership = true;
            List<Exception>? errors = null;
            try
            {
                _pendingReset = false;
                var active = _previousConstraints.Keys.ToArray();
                _realizedElements?.ResetForReuse();
                _measureElements?.ResetForReuse();
                if (_focusedElement is { } focused) focused.LostFocus -= OnUnrealizedFocusedElementLostFocus;
                _focusedElement = _scrollToElement = null;
                _focusedIndex = _scrollToIndex = -1;
                _isWaitingForViewportUpdate = false;
                foreach (var element in active)
                {
                    try { RecycleElementOnItemRemoved(element); }
                    catch (Exception e)
                    {
                        (errors ??= new()).Add(e);
                        RemoveRecycledElement(element);
                    }
                }
                try { TrimUnrealizedChildren(); }
                catch (Exception e) { (errors ??= new()).Add(e); }
                if (_pendingFactoryChange)
                {
                    // No retained child from the old factory can be reused by
                    // the replacement factory, even if its type key matches.
                    foreach (var element in Children.OfType<Control>().ToArray())
                    {
                        if (element.Visibility != Visibility.Collapsed) continue;
                        try { FinalizeRecycledElement(element); }
                        catch (Exception e) { (errors ??= new()).Add(e); }
                        finally { RemoveRecycledElement(element); }
                    }
                    _retainedRecycledElementCount = 0;
                }
            }
            finally
            {
                _elementFactory = (TreeDataGridElementFactory?)GetValue(ElementFactoryProperty);
                _previousConstraints.Clear();
                _pendingReset = _pendingFactoryChange = false;
                _resetting = false;
                _preserveRecycledElementVisualTreeMembership = visual;
                _preserveRecycledElementLogicalTreeMembership = logical;
                InvalidateMeasure();
            }
            if (errors is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors is not null) throw new AggregateException(errors);
        }

        private static bool HasInfinity(Size s) => double.IsInfinity(s.Width) || double.IsInfinity(s.Height);

        private static Rect Intersect(Rect a, Rect b)
        {
            // Hack fix for https://github.com/AvaloniaUI/Avalonia/issues/15075
            var newLeft = (a.X > b.X) ? a.X : b.X;
            var newTop = (a.Y > b.Y) ? a.Y : b.Y;
            var newRight = (a.Right < b.Right) ? a.Right : b.Right;
            var newBottom = (a.Bottom < b.Bottom) ? a.Bottom : b.Bottom;

            if ((newRight >= newLeft) && (newBottom >= newTop))
            {
                return new Rect(newLeft, newTop, newRight - newLeft, newBottom - newTop);
            }
            else
            {
                return default;
            }
        }

        private struct MeasureViewport
        {
            public int anchorIndex;
            public double anchorU;
            public double viewportUStart;
            public double viewportUEnd;
            public double measuredV;
            public double realizedEndU;
            public int lastIndex;
            public bool viewportIsDisjunct;
        }
    }
}
