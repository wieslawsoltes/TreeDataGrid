using System;
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
    public partial class ColumnListBase<TColumn> : NotifyingListBase<TColumn>, IColumns,
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
            ref double estimatedElementSizeU) =>
            GetViewportAnchor(viewportStartU, viewportEndU, itemCount, startU, firstIndex, ref estimatedElementSizeU);

        double IColumnViewportEstimator.EstimateElementSize()
        {
            EnsureGeometry();
            return _estimatedElementSize;
        }

        private void EnsureGeometry()
        {
            while (_geometryDirty) BuildGeometrySnapshot();
        }

        private void InvalidateGeometry()
        {
            unchecked { ++_geometryRevision; }
            _geometryDirty = true;
        }

        private void OnColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(IColumn.ActualWidth))
                InvalidateGeometry();
        }

        public double GetEstimatedWidth(double constraint)
        {
            // Like geometry reconstruction, an estimate must describe one
            // coherent collection/layout revision. A custom property getter can
            // replace columns, complete a nested commit or invalidate an earlier
            // width. Retry without retaining stale columns or partial totals.
            while (true)
            {
                var layout = _layoutRevision;
                var geometry = _geometryRevision;
                if (TryEstimateWidth(constraint, layout, geometry, out var result))
                    return result;
            }
        }

        private bool TryEstimateWidth(double constraint, int layout, int geometry, out double result)
        {
            result = 0;
            var hasStar = false;
            var totalMeasured = 0.0;
            var measuredCount = 0;
            var unmeasuredCount = 0;
            var count = Count;

            for (var i = 0; i < count; ++i)
            {
                var column = (IUpdateColumnLayout)this[i];
                var width = column.Width;
                if (layout != _layoutRevision || geometry != _geometryRevision) return false;

                if (width.IsStar)
                {
                    var minimum = column.MinActualWidth;
                    if (layout != _layoutRevision || geometry != _geometryRevision) return false;
                    hasStar = true;
                    totalMeasured += minimum;
                }
                else
                {
                    // ActualWidth may be an application getter. Read it once,
                    // both to avoid duplicate work and to use the validated value.
                    var actual = column.ActualWidth;
                    if (layout != _layoutRevision || geometry != _geometryRevision) return false;
                    if (!double.IsNaN(actual))
                    {
                        totalMeasured += actual;
                        ++measuredCount;
                    }
                    else ++unmeasuredCount;
                }
            }

            // Preserve the reference estimator's arithmetic and priority:
            // viewport fill first, then measured/unmeasured extrapolation.
            if (hasStar && !double.IsInfinity(constraint) && totalMeasured < constraint)
                result = constraint;
            else if (measuredCount > 0 && unmeasuredCount > 0)
                result = totalMeasured + (totalMeasured / measuredCount) * unmeasuredCount;
            else
                result = totalMeasured;
            return true;
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
