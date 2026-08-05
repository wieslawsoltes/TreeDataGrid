using System;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Utils;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Utilities;
using Avalonia.VisualTree;

namespace Avalonia.Controls.Primitives
{
    public class TreeDataGridRowsPresenter : TreeDataGridPresenterBase<IRow>, IChildIndexProvider
    {
        public static readonly StyledProperty<double> CacheLengthProperty =
            AvaloniaProperty.Register<TreeDataGridRowsPresenter, double>(
                nameof(CacheLength),
                validate: value => value is >= 0 and <= 2);

        public static readonly DirectProperty<TreeDataGridRowsPresenter, IColumns?> ColumnsProperty =
            AvaloniaProperty.RegisterDirect<TreeDataGridRowsPresenter, IColumns?>(
                nameof(Columns),
                o => o.Columns,
                (o, v) => o.Columns = v);

        private IColumns? _columns;
        private bool _isAttachedToVisualTree;
        private IColumns? _layoutInvalidatedColumns;
        private bool _isMeasuringColumnLayoutBatch;
        private bool _pendingColumnLayoutInvalidation;

        public event EventHandler<ChildIndexChangedEventArgs>? ChildIndexChanged;

        public IColumns? Columns
        {
            get => _columns;
            set => SetAndRaise(ColumnsProperty, ref _columns, value);
        }

        /// <summary>
        /// Gets or sets the additional row realization space before and after the viewport,
        /// expressed as a multiple of the viewport height.
        /// </summary>
        public double CacheLength
        {
            get => GetValue(CacheLengthProperty);
            set => SetValue(CacheLengthProperty, value);
        }

        protected override Orientation Orientation => Orientation.Vertical;

        protected override Rect GetMeasureViewport(Rect viewport)
        {
            if (CacheLength <= 0 || viewport.Height <= 0)
                return viewport;

            var buffer = viewport.Height * CacheLength;
            var extent = Bounds.Height > 0 ? Bounds.Height : double.PositiveInfinity;
            var start = Math.Max(0, viewport.Top - buffer);
            var end = Math.Min(extent, viewport.Bottom + buffer);
            var missingBefore = buffer - (viewport.Top - start);
            var missingAfter = buffer - (end - viewport.Bottom);

            if (missingBefore > 0)
                end = Math.Min(extent, end + missingBefore);
            else if (missingAfter > 0)
                start = Math.Max(0, start - missingAfter);

            return new Rect(viewport.X, start, viewport.Width, end - start);
        }

        protected override bool NeedsMeasureForViewportChange(Rect measureViewport, Rect viewport)
        {
            if (CacheLength <= 0)
            {
                return !MathUtilities.AreClose(measureViewport.Width, viewport.Width) ||
                    !IsViewportCoveredByRealizedElements(viewport);
            }

            return viewport.Top < measureViewport.Top || viewport.Bottom > measureViewport.Bottom;
        }

        protected override (int index, double position) GetElementAt(double position)
        {
            return ((IRows)Items!).GetRowAt(position);
        }

        protected override void RealizeElement(Control element, IRow rowModel, int index)
        {
            var row = (TreeDataGridRow)element;
            row.Realize(ElementFactory, GetSelection(), Columns, (IRows?)Items, index);
            ChildIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(element, index));
        }

        protected override void UpdateElementIndex(Control element, int oldIndex, int newIndex)
        {
            ((TreeDataGridRow)element).UpdateIndex(newIndex);
            ChildIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(element, newIndex));
        }

        protected override void UnrealizeElement(Control element)
        {
            ((TreeDataGridRow)element).Unrealize();
            ChildIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(element, ((TreeDataGridRow)element).RowIndex));
        }

        protected override void UnrealizeElementOnItemRemoved(Control element)
        {
            ((TreeDataGridRow)element).UnrealizeOnItemRemoved();
            ChildIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(element, ((TreeDataGridRow)element).RowIndex));
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var batch = Columns as IColumnLayoutBatch;
            var needsFinalMeasure = false;
            Size result;

            _isMeasuringColumnLayoutBatch = true;
            batch?.BeginActualWidthBatch();

            try
            {
                result = base.MeasureOverride(availableSize);
            }
            finally
            {
                try
                {
                    needsFinalMeasure = batch?.EndActualWidthBatch() ?? false;
                }
                finally
                {
                    _isMeasuringColumnLayoutBatch = false;
                }
            }

            // If we have no rows, then get the width from the columns.
            if (Columns is not null && (Items is null || Items.Count == 0))
                result = result.WithWidth(Columns.GetEstimatedWidth(availableSize.Width));

            if (needsFinalMeasure || _pendingColumnLayoutInvalidation)
            {
                _pendingColumnLayoutInvalidation = false;

                foreach (var element in RealizedElements)
                {
                    if (element is TreeDataGridRow row)
                        row.CellsPresenter?.InvalidateMeasure();
                }

                // Apply the widths gathered from all realized rows immediately. This avoids
                // scheduling a complete parent layout pass just to give the same rows their final
                // column constraints. Do not defer commits during this pass: a newly realized wider
                // cell must still request the normal fallback below.
                result = base.MeasureOverride(availableSize);

                if (Columns is not null && (Items is null || Items.Count == 0))
                    result = result.WithWidth(Columns.GetEstimatedWidth(availableSize.Width));

                if (_pendingColumnLayoutInvalidation)
                {
                    _pendingColumnLayoutInvalidation = false;
                    InvalidateMeasure();

                    foreach (var element in RealizedElements)
                    {
                        if (element is TreeDataGridRow row)
                            row.CellsPresenter?.InvalidateMeasure();
                    }
                }
            }

            return result;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Columns?.CommitActualWidths();
            return base.ArrangeOverride(finalSize);
        }

        protected override void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
        {
            base.OnEffectiveViewportChanged(sender, e);
            Columns?.ViewportChanged(Viewport);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == ColumnsProperty)
            {
                var oldValue = change.GetOldValue<IColumns>();
                var newValue = change.GetNewValue<IColumns>();

                if (oldValue is object)
                    UnsubscribeFromColumnLayoutInvalidated(oldValue);
                if (newValue is object)
                    SubscribeToColumnLayoutInvalidated(newValue);

                // When for existing Presenter Columns would be recreated they won't get Viewport set so we need to track that
                // and pass Viewport for a newly created object.
                if (oldValue != null && newValue != null)
                {
                    newValue.ViewportChanged(Viewport);
                }
            }
            else if (change.Property == CacheLengthProperty)
            {
                InvalidateMeasure();
            }

            base.OnPropertyChanged(change);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _isAttachedToVisualTree = true;

            if (Columns is { } columns)
                SubscribeToColumnLayoutInvalidated(columns);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (_layoutInvalidatedColumns is { } columns)
                UnsubscribeFromColumnLayoutInvalidated(columns);

            _isAttachedToVisualTree = false;
            base.OnDetachedFromVisualTree(e);
        }

        internal void UpdateSelection(ITreeDataGridSelectionInteraction? selection)
        {
            foreach (var element in RealizedElements)
            {
                if (element is TreeDataGridRow { RowIndex: >= 0 } row)
                    row.UpdateSelection(selection);
            }
        }

        private void SubscribeToColumnLayoutInvalidated(IColumns columns)
        {
            if (!_isAttachedToVisualTree || ReferenceEquals(_layoutInvalidatedColumns, columns))
                return;

            if (_layoutInvalidatedColumns is { } oldColumns)
                UnsubscribeFromColumnLayoutInvalidated(oldColumns);

            columns.LayoutInvalidated += OnColumnLayoutInvalidated;
            _layoutInvalidatedColumns = columns;
        }

        private void UnsubscribeFromColumnLayoutInvalidated(IColumns columns)
        {
            if (!ReferenceEquals(_layoutInvalidatedColumns, columns))
                return;

            columns.LayoutInvalidated -= OnColumnLayoutInvalidated;
            _layoutInvalidatedColumns = null;
        }

        private void OnColumnLayoutInvalidated(object? sender, EventArgs e)
        {
            if (IsInLayout || _isMeasuringColumnLayoutBatch)
            {
                _pendingColumnLayoutInvalidation = true;
                return;
            }

            InvalidateMeasure();

            foreach (var element in RealizedElements)
            {
                if (element is TreeDataGridRow row)
                    row.CellsPresenter?.InvalidateMeasure();
            }
        }

        private ITreeDataGridSelectionInteraction? GetSelection()
        {
            return this.FindAncestorOfType<TreeDataGrid>()?.SelectionInteraction;
        }

        public int GetChildIndex(ILogical child)
        {
            if (child is TreeDataGridRow row)
            {
                return row.RowIndex;
            }
            return -1;

        }

        public bool TryGetTotalCount(out int count)
        {
            if (Items != null)
            {
                count = Items.Count;
                return true;
            }
            count = 0;
            return false;
        }
    }
}
