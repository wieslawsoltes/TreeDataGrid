using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using Windows.Foundation;

namespace Uno.Controls.Models.TreeDataGrid
{
    internal interface IColumnViewportEstimator
    {
        (int index, double position) GetOrEstimateColumnAt(
            double viewportStart,
            double viewportEnd,
            int itemCount,
            double realizedStart,
            int firstRealizedIndex,
            ref double estimatedElementSize);

        double EstimateElementSize();
    }

    /// <summary>
    /// An implementation of <see cref="IColumns"/> that stores its columns in a list.
    /// </summary>
    public class ColumnListBase<TColumn> : NotifyingListBase<TColumn>, IColumns,
        IColumnLayoutBatch, IColumnViewportEstimator where TColumn : class, IColumn
    {
        private int _actualWidthBatchDepth;
        private bool _actualWidthBatchNeedsFinalMeasure;
        private bool _initialized;
        private bool _columnWidthsDirty = true;
        private readonly List<(double min, double max)> _committedConstraints = new();
        private double _viewportWidth;
        private readonly List<double> _columnEnds = new();
        private bool _geometryDirty = true;
        private double _estimatedElementSize = -1;
        // Every application-controlled callback must return to the same layout
        // transaction before it can publish into aligned collection storage.
        private int _layoutRevision;
        private int _completedLayoutRevision = -1;
        private int _geometryRevision;

        public event EventHandler? LayoutInvalidated;

        public void AddRange(IEnumerable<TColumn> items)
        {
            foreach (var item in items)
                Add(item);
        }

        public Size CellMeasured(int columnIndex, int rowIndex, Size size)
        {
            var column = (IUpdateColumnLayout)this[columnIndex];
            var committed = _committedConstraints[columnIndex];
            var revision = unchecked(++_layoutRevision);
            _initialized = true;
            try
            {
                var measuredWidth = column.CellMeasured(size.Width, rowIndex);
                var result = new Size(measuredWidth, size.Height);
                if (revision != _layoutRevision) return result;
                var actual = column.ActualWidth;
                if (revision != _layoutRevision) return result;
                if (!WidthsEqual(measuredWidth, actual))
                {
                    _columnWidthsDirty = true;
                    return result;
                }
                var minimum = column.MinActualWidth;
                if (revision != _layoutRevision) return result;
                if (!WidthsEqual(committed.min, minimum))
                {
                    _columnWidthsDirty = true;
                    return result;
                }
                var maximum = column.MaxActualWidth;
                if (revision != _layoutRevision) return result;
                if (!WidthsEqual(committed.max, maximum))
                    _columnWidthsDirty = true;

                return result;
            }
            catch
            {
                // A throwing measurement may already have changed natural width.
                // Do not poison the next commit or overwrite a newer nested one.
                if (revision == _layoutRevision || _completedLayoutRevision != _layoutRevision) _columnWidthsDirty = true;
                throw;
            }
        }

        public (int index, double x) GetColumnAt(double x)
        {
            EnsureGeometry();

            // Upper bound skips zero-width columns and preserves exclusive right edges.
            var low = 0;
            var high = _columnEnds.Count;
            while (low < high)
            {
                var middle = low + ((high - low) / 2);
                if (_columnEnds[middle] <= x)
                    low = middle + 1;
                else
                    high = middle;
            }

            if (x >= 0 && low < _columnEnds.Count && x < _columnEnds[low])
                return (low, low == 0 ? 0 : _columnEnds[low - 1]);

            return (-1, -1);
        }

        (int index, double position) IColumnViewportEstimator.GetOrEstimateColumnAt(
            double viewportStartU,
            double viewportEndU,
            int itemCount,
            double startU,
            int firstIndex,
            ref double estimatedElementSizeU)
        {
            // We have no elements, nothing to do here.
            if (itemCount <= 0)
                return (-1, 0);

            // If we're at 0 then display the first item.
            if (LayoutMath.IsZero(viewportStartU))
                return (0, 0);

            var u = startU;

            for (var i = 0; i < Count; ++i)
            {
                var size = this[i].ActualWidth;

                // A zero-width Auto column has not provided a useful viewport anchor yet.
                // Falling back to the measured-width estimate keeps large horizontal jumps
                // accurate while still allowing zero to reserve its configured minimum width.
                if (double.IsNaN(size) || size <= 0)
                    break;

                var endU = u + size;

                if (endU > viewportStartU && u < viewportEndU)
                    return (firstIndex + i, u);

                u = endU;
            }

            // We don't have any realized elements in the requested viewport, or can't rely on
            // StartU being valid. Estimate the index using only the estimated size. First,
            // estimate the element size, using defaultElementSizeU if we don't have any realized
            // elements.
            var estimatedSize = ((IColumnViewportEstimator)this).EstimateElementSize() switch
            {
                -1 => estimatedElementSizeU,
                var v => v,
            };

            // Store the estimated size for the next layout pass.
            estimatedElementSizeU = estimatedSize;

            // Estimate the element at the start of the viewport.
            var index = Math.Min((int)(viewportStartU / estimatedSize), itemCount - 1);
            return (index, index * estimatedSize);
        }

        double IColumnViewportEstimator.EstimateElementSize()
        {
            EnsureGeometry();
            return _estimatedElementSize;
        }

        private void EnsureGeometry()
        {
            while (_geometryDirty) RebuildGeometry();
        }

        private void RebuildGeometry()
        {
            var revision = _geometryRevision;
            var count = Count;
            double[]? rented = null;
            Span<double> ends = count <= 128 ? stackalloc double[count] :
                (rented = ArrayPool<double>.Shared.Rent(count)).AsSpan(0, count);
            try
            {
                var end = 0.0;
                var total = 0.0;
                var measuredCount = 0;
                var prefixCount = 0;
                var knownPrefix = true;
                for (var i = 0; i < count; ++i)
                {
                    var width = this[i].ActualWidth;
                    if (revision != _geometryRevision) return;
                    // Measure first, publish later. An ActualWidth getter can
                    // replace the collection and recursively query its geometry.
                    knownPrefix &= !double.IsNaN(width) && width >= 0;
                    if (knownPrefix)
                    {
                        end += width;
                        ends[prefixCount++] = end;
                    }
                    if (!double.IsNaN(width) && width > 0)
                    {
                        total += width;
                        ++measuredCount;
                    }
                }
                // No application callbacks occur while publishing. Reserve
                // storage before clearing so allocation failure keeps the old
                // committed prefix intact and the dirty flag retryable.
                if (_columnEnds.Capacity < prefixCount) _columnEnds.Capacity = prefixCount;
                _columnEnds.Clear();
                for (var i = 0; i < prefixCount; ++i) _columnEnds.Add(ends[i]);
                _estimatedElementSize = measuredCount > 0 ? total / measuredCount : -1;
                _geometryDirty = false;
                unchecked { ++_geometryRevision; }
            }
            finally { if (rented is not null) ArrayPool<double>.Shared.Return(rented); }
        }

        private void InvalidateGeometry()
        {
            unchecked { ++_geometryRevision; }
            _geometryDirty = true;
        }

        // One weak owner per observed column, never a global subscription table.
        // Duplicate column entries share a subscription until their last removal.
        private readonly Dictionary<IColumn, (ColumnSubscription Subscription, int Count)> _subscriptions =
            new(ReferenceEqualityComparer.Instance);

        private void SubscribeColumn(IColumn column)
        {
            if (_subscriptions.TryGetValue(column, out var existing))
                _subscriptions[column] = (existing.Subscription, existing.Count + 1);
            else
                _subscriptions.Add(column, (new ColumnSubscription(this, column), 1));
        }

        private void UnsubscribeColumn(IColumn column)
        {
            if (!_subscriptions.TryGetValue(column, out var existing)) return;
            if (existing.Count > 1) _subscriptions[column] = (existing.Subscription, existing.Count - 1);
            else
            {
                _subscriptions.Remove(column);
                existing.Subscription.Dispose();
            }
        }

        private sealed class ColumnSubscription : IDisposable
        {
            private readonly WeakReference<ColumnListBase<TColumn>> _owner;
            private readonly IColumn _column;
            public ColumnSubscription(ColumnListBase<TColumn> owner, IColumn column)
            {
                _owner = new(owner);
                _column = column;
                try { column.PropertyChanged += Changed; }
                catch { column.PropertyChanged -= Changed; throw; }
            }
            private void Changed(object? sender, PropertyChangedEventArgs args)
            {
                if (_owner.TryGetTarget(out var owner)) owner.OnColumnPropertyChanged(sender, args);
                else Dispose();
            }
            public void Dispose() => _column.PropertyChanged -= Changed;
        }

        private void OnColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(IColumn.ActualWidth))
                InvalidateGeometry();
        }

        public double GetEstimatedWidth(double constraint)
        {
            var hasStar = false;
            var totalMeasured = 0.0;
            var measuredCount = 0;
            var unmeasuredCount = 0;

            for (var i = 0; i < Count; ++i)
            {
                var column = (IUpdateColumnLayout)this[i];

                if (column.Width.IsStar)
                {
                    hasStar = true;
                    totalMeasured += column.MinActualWidth;
                }
                else if (!double.IsNaN(column.ActualWidth))
                {
                    totalMeasured += column.ActualWidth;
                    ++measuredCount;
                }
                else
                    ++unmeasuredCount;
            }

            // If there are star columns, and all measured columns fit within the available space
            // then we will fill the available space.
            if (hasStar && !double.IsInfinity(constraint) && totalMeasured < constraint)
                return constraint;

            // If there are a mix of measured and unmeasured columns then use the measured columns
            // to estimate the size of the unmeasured columns.
            if (measuredCount > 0 && unmeasuredCount > 0)
            {
                var estimated = (totalMeasured / measuredCount) * unmeasuredCount;
                return totalMeasured + estimated;
            }

            return totalMeasured;
        }

        public void CommitActualWidths()
        {
            if (_actualWidthBatchDepth == 0)
                UpdateColumnSizes();
        }

        // The grid retains its existing constrained-width solver. Adopt its
        // completed commit so public layout calls use the same viewport and
        // constraints rather than recomputing stars against an initial zero.
        internal void AcceptNativeWidths(double viewportWidth)
        {
            var revision = unchecked(++_layoutRevision);
            _columnWidthsDirty = true;
            InvalidateGeometry();
            // Capture custom getters before marking this transaction committed.
            // A nested collection change/commit owns its replacement snapshots.
            if (!CaptureConstraints(revision)) return;
            _viewportWidth = viewportWidth;
            _initialized = true;
            _columnWidthsDirty = false;
            _completedLayoutRevision = revision;
        }

        public void SetColumnWidth(int columnIndex, GridLength width)
        {
            var column = this[columnIndex];
            var revision = _layoutRevision;
            var previous = column.Width;
            if (revision != _layoutRevision || width == previous) return;

            revision = unchecked(++_layoutRevision);
            _columnWidthsDirty = true;
            InvalidateGeometry();
            ((IUpdateColumnLayout)column).SetWidth(width);
            if (revision != _layoutRevision) return;
            LayoutInvalidated?.Invoke(this, EventArgs.Empty);
            if (revision != _layoutRevision) return;
            UpdateColumnSizes();
        }

        public void ViewportChanged(Rect viewport)
        {
            if (!LayoutMath.AreClose(_viewportWidth, viewport.Width))
            {
                unchecked { ++_layoutRevision; }
                _viewportWidth = viewport.Width;
                _columnWidthsDirty = true;
                if (_initialized)
                    UpdateColumnSizes();
            }
        }

        IColumn IReadOnlyList<IColumn>.this[int index] => this[index];
        IEnumerator<IColumn> IEnumerable<IColumn>.GetEnumerator() => GetEnumerator();

        bool IColumnLayoutBatch.IsActualWidthCommitDeferred => _actualWidthBatchDepth > 0;

        void IColumnLayoutBatch.BeginActualWidthBatch()
        {
            if (_actualWidthBatchDepth++ == 0)
                _actualWidthBatchNeedsFinalMeasure = false;
        }

        bool IColumnLayoutBatch.EndActualWidthBatch()
        {
            if (_actualWidthBatchDepth <= 0)
                throw new InvalidOperationException("No column width batch is active.");

            if (--_actualWidthBatchDepth > 0)
                return false;

            try
            {
                UpdateColumnSizes();
                return _actualWidthBatchNeedsFinalMeasure;
            }
            finally
            {
                _actualWidthBatchNeedsFinalMeasure = false;
            }
        }

        void IColumnLayoutBatch.RequestFinalMeasure() =>
            _actualWidthBatchNeedsFinalMeasure = true;

        protected override void ClearItems()
        {
            CheckReentrancy();
            unchecked { ++_layoutRevision; }
            _columnWidthsDirty = true;
            InvalidateGeometry();
            foreach (var column in this)
                UnsubscribeColumn(column);
            _committedConstraints.Clear();
            base.ClearItems();
        }

        protected override void InsertItem(int index, TColumn item)
        {
            CheckReentrancy();
            unchecked { ++_layoutRevision; }
            _columnWidthsDirty = true;
            InvalidateGeometry();
            SubscribeColumn(item);
            _committedConstraints.Insert(index, (double.NaN, double.NaN));
            base.InsertItem(index, item);
        }

        protected override void MoveItem(int oldIndex, int newIndex)
        {
            if ((uint)oldIndex >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(oldIndex));
            if ((uint)newIndex >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(newIndex));

            // Keep the constraint snapshots aligned before the collection-changed event is raised,
            // so synchronous listeners always observe a consistent column list.
            CheckReentrancy();
            unchecked { ++_layoutRevision; }
            InvalidateGeometry();
            var constraints = _committedConstraints[oldIndex];
            _committedConstraints.RemoveAt(oldIndex);
            _committedConstraints.Insert(newIndex, constraints);
            _columnWidthsDirty = true;
            base.MoveItem(oldIndex, newIndex);
        }

        protected override void RemoveItem(int index)
        {
            CheckReentrancy();
            unchecked { ++_layoutRevision; }
            _columnWidthsDirty = true;
            InvalidateGeometry();
            UnsubscribeColumn(this[index]);
            _committedConstraints.RemoveAt(index);
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, TColumn item)
        {
            CheckReentrancy();
            unchecked { ++_layoutRevision; }
            _columnWidthsDirty = true;
            InvalidateGeometry();
            UnsubscribeColumn(this[index]);
            SubscribeColumn(item);
            _committedConstraints[index] = (double.NaN, double.NaN);
            base.SetItem(index, item);
        }

        private void UpdateColumnSizes()
        {
            if (!_columnWidthsDirty) return;

            var revision = unchecked(++_layoutRevision);
            _columnWidthsDirty = false;
            InvalidateGeometry();
            try
            {
                if (!UpdateColumnSizes(revision) && _completedLayoutRevision != _layoutRevision)
                    _columnWidthsDirty = true;
            }
            catch
            {
                // Custom commits cannot be rolled back, but their failure must
                // not mark a partially completed pass clean forever. Preserve
                // any newer nested transaction rather than dirtying its result.
                if (revision == _layoutRevision || _completedLayoutRevision != _layoutRevision) _columnWidthsDirty = true;
                InvalidateGeometry();
                throw;
            }
        }

        private bool UpdateColumnSizes(int revision)
        {
            var totalStars = 0.0;
            var availableSpace = _viewportWidth;
            var invalidated = false;
            var count = Count;

            // Preserve the reference solver's operation order. Only the native
            // callback boundaries change: never continue on a retired column or
            // mix its contribution with a newer collection/viewport transaction.
            for (var i = 0; i < count; ++i)
            {
                var column = (IUpdateColumnLayout)this[i];
                var width = column.Width;
                if (revision != _layoutRevision) return false;
                if (!width.IsStar)
                {
                    invalidated |= column.CommitActualWidth();
                    if (revision != _layoutRevision) return false;
                    var actual = column.ActualWidth;
                    if (revision != _layoutRevision) return false;
                    availableSpace -= NotNaN(actual);
                }
                else totalStars += width.Value;
            }

            if (totalStars > 0)
            {
                var starWidthWasConstrained = false;
                availableSpace = Math.Max(0, availableSpace);
                for (var i = 0; i < count; ++i)
                {
                    var column = (IUpdateColumnLayout)this[i];
                    var width = column.Width;
                    if (revision != _layoutRevision) return false;
                    if (width.IsStar)
                    {
                        column.CalculateStarWidth(availableSpace, totalStars);
                        if (revision != _layoutRevision) return false;
                        var constrained = column.StarWidthWasConstrained;
                        if (revision != _layoutRevision) return false;
                        starWidthWasConstrained |= constrained;
                    }
                }

                if (starWidthWasConstrained && LayoutMath.GreaterThan(availableSpace, 0))
                {
                    var initialAvailableSpace = availableSpace;
                    var initialTotalStars = totalStars;
                    for (var i = 0; i < count; ++i)
                    {
                        var column = (IUpdateColumnLayout)this[i];
                        var constrained = column.StarWidthWasConstrained;
                        if (revision != _layoutRevision) return false;
                        if (constrained)
                        {
                            var width = column.Width;
                            if (revision != _layoutRevision) return false;
                            var minimum = column.MinActualWidth;
                            if (revision != _layoutRevision) return false;
                            var maximum = column.MaxActualWidth;
                            if (revision != _layoutRevision) return false;
                            var proposed = (initialAvailableSpace / initialTotalStars) * width.Value;
                            availableSpace -= Math.Min(Math.Max(proposed, minimum), maximum);
                            totalStars -= width.Value;
                        }
                    }
                    for (var i = 0; i < count; ++i)
                    {
                        var column = (IUpdateColumnLayout)this[i];
                        var width = column.Width;
                        if (revision != _layoutRevision) return false;
                        if (!width.IsStar) continue;
                        var constrained = column.StarWidthWasConstrained;
                        if (revision != _layoutRevision) return false;
                        if (!constrained)
                        {
                            column.CalculateStarWidth(availableSpace, totalStars);
                            if (revision != _layoutRevision) return false;
                        }
                    }
                }

                for (var i = 0; i < count; ++i)
                {
                    var column = (IUpdateColumnLayout)this[i];
                    var width = column.Width;
                    if (revision != _layoutRevision) return false;
                    if (width.IsStar)
                    {
                        invalidated |= column.CommitActualWidth();
                        if (revision != _layoutRevision) return false;
                    }
                }
            }

            if (!CaptureConstraints(revision)) return false;
            InvalidateGeometry();
            _completedLayoutRevision = revision;
            if (invalidated) LayoutInvalidated?.Invoke(this, EventArgs.Empty);
            return true;
        }

        private bool CaptureConstraints(int revision)
        {
            var count = Count;
            for (var i = 0; i < count; ++i)
            {
                var column = (IUpdateColumnLayout)this[i];
                var minimum = column.MinActualWidth;
                if (revision != _layoutRevision) return false;
                var maximum = column.MaxActualWidth;
                if (revision != _layoutRevision) return false;
                _committedConstraints[i] = (minimum, maximum);
            }
            return true;
        }

        private static double NotNaN(double v) => double.IsNaN(v) ? 0 : v;

        private static bool WidthsEqual(double x, double y) =>
            x.Equals(y) || LayoutMath.AreClose(x, y);

        private static class LayoutMath
        {
            private const double Epsilon = 2.2204460492503131e-016;
            public static bool IsZero(double value) => Math.Abs(value) < 10 * Epsilon;
            public static bool AreClose(double x, double y)
            {
                if (x == y) return true;
                var tolerance = (Math.Abs(x) + Math.Abs(y) + 10) * Epsilon;
                var delta = x - y;
                return -tolerance < delta && delta < tolerance;
            }
            public static bool GreaterThan(double x, double y) => x > y && !AreClose(x, y);
        }
    }
}
