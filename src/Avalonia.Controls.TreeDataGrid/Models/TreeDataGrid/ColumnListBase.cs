using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Utilities;

namespace Avalonia.Controls.Models.TreeDataGrid
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

        public event EventHandler? LayoutInvalidated;

        public void AddRange(IEnumerable<TColumn> items)
        {
            foreach (var item in items)
                Add(item);
        }

        public Size CellMeasured(int columnIndex, int rowIndex, Size size)
        {
            var column = (IUpdateColumnLayout)this[columnIndex];
            _initialized = true;
            var measuredWidth = column.CellMeasured(size.Width, rowIndex);
            var committed = _committedConstraints[columnIndex];

            if (!WidthsEqual(measuredWidth, column.ActualWidth) ||
                !WidthsEqual(committed.min, column.MinActualWidth) ||
                !WidthsEqual(committed.max, column.MaxActualWidth))
            {
                _columnWidthsDirty = true;
            }

            return new Size(measuredWidth, size.Height);
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
            if (MathUtilities.IsZero(viewportStartU))
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
            if (!_geometryDirty)
                return;

            _columnEnds.Clear();
            var end = 0.0;
            var total = 0.0;
            var measuredCount = 0;
            var knownPrefix = true;
            for (var i = 0; i < Count; ++i)
            {
                var width = this[i].ActualWidth;
                // Positions beyond an unmeasured column are unknown. The average still
                // includes measured columns beyond that point, as in the uncached estimator.
                knownPrefix &= !double.IsNaN(width) && width >= 0;
                if (knownPrefix)
                {
                    end += width;
                    _columnEnds.Add(end);
                }
                if (!double.IsNaN(width) && width > 0)
                {
                    total += width;
                    ++measuredCount;
                }
            }

            _estimatedElementSize = measuredCount > 0 ? total / measuredCount : -1;
            _geometryDirty = false;
        }

        private void SubscribeColumn(IColumn column) =>
            WeakEventHandlerManager.Subscribe<INotifyPropertyChanged, PropertyChangedEventArgs, ColumnListBase<TColumn>>(
                column, nameof(column.PropertyChanged), OnColumnPropertyChanged);

        private void UnsubscribeColumn(IColumn column) =>
            WeakEventHandlerManager.Unsubscribe<PropertyChangedEventArgs, ColumnListBase<TColumn>>(
                column, nameof(column.PropertyChanged), OnColumnPropertyChanged);

        private void OnColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(IColumn.ActualWidth))
                _geometryDirty = true;
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

        public void SetColumnWidth(int columnIndex, GridLength width)
        {
            var column = this[columnIndex];

            if (width != column.Width)
            {
                ((IUpdateColumnLayout)column).SetWidth(width);
                _columnWidthsDirty = true;
                LayoutInvalidated?.Invoke(this, EventArgs.Empty);
                UpdateColumnSizes();
            }
        }

        public void ViewportChanged(Rect viewport)
        {
            if (!MathUtilities.AreClose(_viewportWidth, viewport.Width))
            {
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
            _columnWidthsDirty = true;
            _geometryDirty = true;
            foreach (var column in this)
                UnsubscribeColumn(column);
            _committedConstraints.Clear();
            base.ClearItems();
        }

        protected override void InsertItem(int index, TColumn item)
        {
            _columnWidthsDirty = true;
            _geometryDirty = true;
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
            _geometryDirty = true;
            var constraints = _committedConstraints[oldIndex];
            _committedConstraints.RemoveAt(oldIndex);
            _committedConstraints.Insert(newIndex, constraints);
            _columnWidthsDirty = true;
            base.MoveItem(oldIndex, newIndex);
        }

        protected override void RemoveItem(int index)
        {
            _columnWidthsDirty = true;
            _geometryDirty = true;
            UnsubscribeColumn(this[index]);
            _committedConstraints.RemoveAt(index);
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, TColumn item)
        {
            _columnWidthsDirty = true;
            _geometryDirty = true;
            UnsubscribeColumn(this[index]);
            SubscribeColumn(item);
            _committedConstraints[index] = (double.NaN, double.NaN);
            base.SetItem(index, item);
        }

        private void UpdateColumnSizes()
        {
            if (!_columnWidthsDirty)
                return;

            _columnWidthsDirty = false;
            // Custom columns may commit widths without raising PropertyChanged.
            _geometryDirty = true;
            var totalStars = 0.0;
            var availableSpace = _viewportWidth;
            var invalidated = false;

            // First commit the actual width for all non-star width columns and get a total of the
            // number of stars for star width columns.
            for (var i = 0; i < Count; ++i)
            {
                var column = (IUpdateColumnLayout)this[i];

                if (!column.Width.IsStar)
                {
                    invalidated |= column.CommitActualWidth();
                    availableSpace -= NotNaN(column.ActualWidth);
                }
                else
                    totalStars += column.Width.Value;
            }

            if (totalStars > 0)
            {
                // Size the star columns.
                var starWidthWasConstrained = false;

                availableSpace = Math.Max(0, availableSpace);

                // Do a first pass to calculate star column widths.
                for (var i = 0; i < Count; ++i)
                {
                    var column = (IUpdateColumnLayout)this[i];

                    if (column.Width.IsStar)
                    {
                        column.CalculateStarWidth(availableSpace, totalStars);
                        starWidthWasConstrained |= column.StarWidthWasConstrained;
                    }
                }

                // If the width of any star columns was constrained by their min/max size, and we
                // actually had any space to distribute between star columns, then we need to update
                // the star width for the non-constrained columns.
                if (starWidthWasConstrained && MathUtilities.GreaterThan(availableSpace, 0))
                {
                    var initialAvailableSpace = availableSpace;
                    var initialTotalStars = totalStars;

                    for (var i = 0; i < Count; ++i)
                    {
                        var column = (IUpdateColumnLayout)this[i];

                        if (column.StarWidthWasConstrained)
                        {
                            availableSpace -= GetConstrainedStarWidth(
                                column,
                                initialAvailableSpace,
                                initialTotalStars);
                            totalStars -= column.Width.Value;
                        }
                    }

                    for (var i = 0; i < Count; ++i)
                    {
                        var column = (IUpdateColumnLayout)this[i];
                        if (column.Width.IsStar && !column.StarWidthWasConstrained)
                            column.CalculateStarWidth(availableSpace, totalStars);
                    }
                }

                // Finally commit the star column widths.
                for (var i = 0; i < Count; ++i)
                {
                    var column = (IUpdateColumnLayout)this[i];

                    if (column.Width.IsStar)
                    {
                        invalidated |= column.CommitActualWidth();
                    }
                }
            }

            for (var i = 0; i < Count; ++i)
            {
                var column = (IUpdateColumnLayout)this[i];
                _committedConstraints[i] = (column.MinActualWidth, column.MaxActualWidth);
            }

            _geometryDirty = true;
            if (invalidated)
            {
                LayoutInvalidated?.Invoke(this, EventArgs.Empty);
            }
        }

        private static double NotNaN(double v) => double.IsNaN(v) ? 0 : v;

        private static double GetConstrainedStarWidth(
            IUpdateColumnLayout column,
            double availableSpace,
            double totalStars)
        {
            var width = (availableSpace / totalStars) * column.Width.Value;
            return Math.Min(Math.Max(width, column.MinActualWidth), column.MaxActualWidth);
        }

        private static bool WidthsEqual(double x, double y) =>
            x.Equals(y) || MathUtilities.AreClose(x, y);
    }
}
