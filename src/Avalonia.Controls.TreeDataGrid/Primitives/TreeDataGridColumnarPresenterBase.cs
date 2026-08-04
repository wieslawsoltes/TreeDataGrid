using System;
using System.Collections.Generic;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Utils;
using Avalonia.Layout;
using Avalonia.Utilities;

namespace Avalonia.Controls.Primitives
{
    /// <summary>
    /// Base class for presenters which display data in virtualized columns.
    /// </summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <remarks>
    /// Implements common layout functionality between <see cref="TreeDataGridCellsPresenter"/>
    /// and <see cref="TreeDataGridColumnHeadersPresenter"/>.
    /// </remarks>
    public abstract class TreeDataGridColumnarPresenterBase<TItem> : TreeDataGridPresenterBase<TItem>,
        IFinalMeasureSelector
    {
        private double _lastEstimatedElementSizeU = 25;

        protected IColumns? Columns => Items as IColumns;

        protected sealed override Size GetInitialConstraint(Control element, int index, Size availableSize)
        {
            var column = (IUpdateColumnLayout)Columns![index];
            var width = column is IColumnMeasurementOptions
                { RequiresUnconstrainedWidthMeasurement: false } &&
                double.IsFinite(column.ActualWidth) ?
                column.ActualWidth :
                column.MaxActualWidth;
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
            return !MathUtilities.AreClose(measureViewport.Height, viewport.Height) ||
                !IsViewportCoveredByRealizedElements(viewport);
        }

        protected sealed override bool NeedsFinalMeasurePass(int firstIndex, IReadOnlyList<Control?> elements)
        {
            var columns = Columns!;

            columns.CommitActualWidths();

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
            var previous = LayoutInformation.GetPreviousMeasureConstraint(element)!.Value;

            return previous.Width > column.ActualWidth ||
                (column.Width.GridUnitType == GridUnitType.Auto &&
                    !MathUtilities.AreClose(previous.Width, column.ActualWidth));
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

        protected Size MeasureColumnElement(
            int index,
            int rowIndex,
            Control element,
            Size availableSize)
        {
            var columns = (IColumns)Items!;
            var previousConstraint = LayoutInformation.GetPreviousMeasureConstraint(element);

            if (element.IsMeasureValid)
            {
                if (double.IsPositiveInfinity(availableSize.Width) &&
                    element is INaturalWidthMeasureCache { NaturalDesiredSize: { } naturalSize })
                {
                    return columns.CellMeasured(index, rowIndex, naturalSize);
                }

                if (previousConstraint == availableSize)
                    return columns.CellMeasured(index, rowIndex, element.DesiredSize);
            }

            element.Measure(availableSize);

            if (double.IsPositiveInfinity(availableSize.Width) &&
                element is INaturalWidthMeasureCache cache)
            {
                cache.NaturalDesiredSize = element.DesiredSize;
            }

            return columns.CellMeasured(index, rowIndex, element.DesiredSize);
        }

        protected sealed override double CalculateSizeU(Size availableSize)
        {
            return Columns?.GetEstimatedWidth(availableSize.Width) ?? 0;
        }
    }
}
