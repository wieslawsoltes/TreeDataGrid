using System;
using System.Collections.Generic;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;



namespace Uno.Controls.Primitives
{
    /// <summary>
    /// Base class for presenters which display data in virtualized columns.
    /// </summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <remarks>
    /// Provides the reference column measurement and two-pass layout contract.
    /// </remarks>
    public abstract partial class TreeDataGridColumnarPresenterBase<TItem> : TreeDataGridPresenterBase<TItem>,
        IFinalMeasureSelector
    {
        private double _lastEstimatedElementSizeU = 25;
        private IColumns? _observedColumns;
        private bool _detached;

        public TreeDataGridColumnarPresenterBase()
        {
            Loaded += (_, _) => { _detached = false; ObserveColumns(); };
            Unloaded += (_, _) => { _detached = true; ObserveColumns(); };
        }

        protected override void OnItemsChanged(IReadOnlyList<TItem>? oldItems, IReadOnlyList<TItem>? newItems)
        {
            base.OnItemsChanged(oldItems, newItems);
            ObserveColumns();
        }

        private void ObserveColumns()
        {
            if (_observedColumns is not null) _observedColumns.LayoutInvalidated -= OnColumnLayoutInvalidated;
            _observedColumns = _detached ? null : Columns;
            if (_observedColumns is not null) _observedColumns.LayoutInvalidated += OnColumnLayoutInvalidated;
        }

        private void OnColumnLayoutInvalidated(object? sender, EventArgs e) => InvalidateMeasure();

        protected override Size MeasureOverride(Size availableSize)
        {
            Columns?.ViewportChanged(GetViewportForMeasure(availableSize));
            Columns?.CommitActualWidths();
            return base.MeasureOverride(availableSize);
        }

        protected IColumns? Columns => Items as IColumns;

        protected sealed override Size GetInitialConstraint(Control element, int index, Size availableSize)
        {
            var column = (IUpdateColumnLayout)Columns![index];
            // Native Auto-valued bounds depend on the natural measurement. Do
            // not cap that first pass using their previous computed value (which
            // can be zero before the first cell/header is measured).
            var width = column is IColumnMeasurementOptions { RequiresUnconstrainedWidthMeasurement: true }
                ? double.PositiveInfinity
                : column is IColumnMeasurementOptions && double.IsFinite(column.ActualWidth)
                    ? column.ActualWidth : column.MaxActualWidth;
            return new Size(Math.Min(availableSize.Width, width), availableSize.Height);
        }

        protected override (int index, double position) GetOrEstimateAnchorElementForViewport(
            double viewportStart,
            double viewportEnd,
            int itemCount)
        {
            if (Columns?.GetColumnAt(viewportStart) is (var index and >= 0, var position))
                return (index, position);

            if (Columns is IColumnViewportEstimator estimator &&
                estimator.GetOrEstimateColumnAt(
                    viewportStart,
                    viewportEnd,
                    itemCount,
                    StartU,
                    FirstIndex,
                    ref _lastEstimatedElementSizeU) is { index: >= 0 } res)
                return res;

            return base.GetOrEstimateAnchorElementForViewport(viewportStart, viewportEnd, itemCount);
        }

        protected override double EstimateElementSizeU()
        {
            if (Columns is not IColumnViewportEstimator estimator)
                return _lastEstimatedElementSizeU;

            var result = estimator.EstimateElementSize();
            if (result >= 0)
                _lastEstimatedElementSizeU = result;

            return _lastEstimatedElementSizeU;
        }

        protected override bool NeedsMeasureForViewportChange(Rect measureViewport, Rect viewport)
        {
            return !PresenterMath.AreClose(measureViewport.Height, viewport.Height) ||
                !IsViewportCoveredByRealizedElements(viewport);
        }

        protected sealed override bool NeedsFinalMeasurePass(int firstIndex, IReadOnlyList<Control?> elements)
        {
            var columns = Columns!;

            columns.CommitActualWidths();

            // A rows presenter can defer the shared column-width commit until all realized rows
            // have contributed their natural sizes. Measuring against an earlier row's width here
            // would only make the row invalid again when the batch commits its final maximum.
            if (columns is IColumnLayoutBatch { IsActualWidthCommitDeferred: true } batch)
            {
                for (var i = 0; i < elements.Count; ++i)
                {
                    if (elements[i] is { } element &&
                        ((IFinalMeasureSelector)this).NeedsFinalMeasure(element, i + firstIndex))
                    {
                        batch.RequestFinalMeasure();
                        break;
                    }
                }

                return false;
            }

            // We need to do a second measure pass if any of the controls were measured with a width
            // that is greater than the final column width.
            for (var i = 0; i < elements.Count; i++)
            {
                var e = elements[i];
                if (e is not null)
                {
                    if (((IFinalMeasureSelector)this).NeedsFinalMeasure(e, i + firstIndex))
                        return true;
                }
            }

            return false;
        }

        bool IFinalMeasureSelector.NeedsFinalMeasure(Control element, int index)
        {
            var column = Columns![index];
            var previous = GetPreviousMeasureConstraint(element)!.Value;

            return previous.Width > column.ActualWidth ||
                (column.Width.GridUnitType == GridUnitType.Auto &&
                    !PresenterMath.AreClose(previous.Width, column.ActualWidth));
        }

        protected sealed override (int index, double position) GetElementAt(double position)
        {
            return ((IColumns)Items!).GetColumnAt(position);
        }

        protected sealed override Size GetFinalConstraint(Control element, int index, Size availableSize)
        {
            var column = Columns![index];
            return new(column.ActualWidth, double.PositiveInfinity);
        }

        protected Size MeasureColumnElement(int index, int rowIndex, Control element, Size availableSize)
        {
            // Native Measure performs valid-constraint caching. Do not emulate
            // Avalonia's private measure-validity state with stale desired sizes.
            var generation = PresenterGeneration;
            var columns = Columns!;
            MeasureNative(element, availableSize);
            // Template construction and custom MeasureOverride are user code.
            // They may retire this layout and clear its column collection.
            EnsureGeneration(generation);
            return columns.CellMeasured(index, rowIndex, element.DesiredSize);
        }

        protected sealed override double CalculateSizeU(Size availableSize)
        {
            return Columns?.GetEstimatedWidth(availableSize.Width) ?? 0;
        }
    }
}
