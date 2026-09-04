// Layout algorithm derived from TreeDataGrid ColumnBase<TModel> (MIT).
// Copyright (c) .NET Foundation and Contributors.
// This base owns view layout only; sorting and row identity belong to Core.
using System;
using System.ComponentModel;
using Avalonia.Utilities;
using Avalonia.Controls;
using Avalonia.Controls.Models;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Presentation;
using CoreModels = TreeDataGridCore.Models;

namespace Avalonia.Controls.Presentation
{
    public abstract class CellColumnBase<TModel> : NotifyingBase, ICellColumn<TModel>, IColumnMeasurementOptions
    {
        private double _actualWidth = double.NaN;
        private GridLength _width;
        private double _autoWidth = double.NaN;
        private double _starWidth = double.NaN;
        private bool _starWidthWasConstrained;
        private object? _header;
        private ListSortDirection? _sortDirection;
        protected CellColumnBase(
            object? header,
            GridLength? width,
            CellColumnOptions options)
        {
            _header = header;
            Options = options;
            SetWidth(width ?? GridLength.Auto);
        }
        public double ActualWidth
        {
            get => _actualWidth;
            private set => RaiseAndSetIfChanged(ref _actualWidth, value);
        }
        public GridLength Width
        {
            get => _width;
            private set => RaiseAndSetIfChanged(ref _width, value);
        }
        public object? Header
        {
            get => _header;
            set => RaiseAndSetIfChanged(ref _header, value);
        }
        public CellColumnOptions Options { get; }
        public ListSortDirection? SortDirection
        {
            get => _sortDirection;
            set => RaiseAndSetIfChanged(ref _sortDirection, value);
        }
        public object? Tag { get; set; }

        bool? IColumn.CanUserResize => Options.CanUserResizeColumn;
        double IUpdateColumnLayout.MinActualWidth => CoerceActualWidth(0);
        double IUpdateColumnLayout.MaxActualWidth => CoerceActualWidth(double.PositiveInfinity);
        bool IUpdateColumnLayout.StarWidthWasConstrained => _starWidthWasConstrained;
        bool IColumnMeasurementOptions.RequiresUnconstrainedWidthMeasurement =>
            Width.IsAuto || Options.MinWidth.IsAuto || Options.MaxWidth?.IsAuto == true;

        public abstract ICell CreateCell(CoreModels.IRow<TModel> row);

        double IUpdateColumnLayout.CellMeasured(double width, int rowIndex)
        {
            _autoWidth = Math.Max(NonNaN(_autoWidth), CoerceMeasuredWidth(width));

            if (Width.GridUnitType == GridUnitType.Auto)
                return _autoWidth;

            if (!double.IsNaN(ActualWidth))
                return ActualWidth;

            // If we're measuring a star column before its actual width has been calculated,
            // return the minimum width so we can continue measuring the remaining columns.
            if (Width.IsStar)
                return ((IUpdateColumnLayout)this).MinActualWidth;

            return _autoWidth;
        }

        void IUpdateColumnLayout.CalculateStarWidth(double availableWidth, double totalStars)
        {
            if (!Width.IsStar)
                throw new InvalidOperationException("Attempt to calculate star width on a non-star column.");

            var width = (availableWidth / totalStars) * Width.Value;
            _starWidth = CoerceActualWidth(width);
            _starWidthWasConstrained = !MathUtilities.AreClose(_starWidth, width);
        }

        bool IUpdateColumnLayout.CommitActualWidth()
        {
            var width = Width.GridUnitType switch
            {
                GridUnitType.Auto => double.IsNaN(_autoWidth) ? CoerceActualWidth(0) : _autoWidth,
                GridUnitType.Pixel => CoerceActualWidth(Width.Value),
                GridUnitType.Star => _starWidth,
                _ => throw new NotSupportedException(),
            };

            var oldWidth = ActualWidth;
            ActualWidth = width;
            _starWidthWasConstrained = false;

            // MathUtilites.AreClose will return true for this condition.
            // If the user has auto columns that are not yet realized, then the
            // _autoWidth will remain NaN.
            // This will lead to an endless layout cycle causing the whole UI
            // to have degraded performance, until all columns have an actual value
            // set for _autoWidth.
            if (double.IsNaN(oldWidth) && double.IsNaN(ActualWidth))
            {
                return false;
            }

            return !MathUtilities.AreClose(oldWidth, ActualWidth);
        }

        void IUpdateColumnLayout.SetWidth(GridLength width) => SetWidth(width);

        private double CoerceActualWidth(double width)
        {
            width = Options.MinWidth.GridUnitType switch
            {
                GridUnitType.Auto => Math.Max(width, _autoWidth),
                GridUnitType.Pixel => Math.Max(width, Options.MinWidth.Value),
                GridUnitType.Star => throw new NotImplementedException(),
                _ => width
            };

            return Options.MaxWidth?.GridUnitType switch
            {
                GridUnitType.Auto => Math.Min(width, _autoWidth),
                GridUnitType.Pixel => Math.Min(width, Options.MaxWidth.Value.Value),
                GridUnitType.Star => throw new NotImplementedException(),
                _ => width
            };
        }

        private double CoerceMeasuredWidth(double width)
        {
            // Auto min/max constraints are derived from the measured width itself. Applying them
            // while _autoWidth is still NaN creates a circular NaN that can never initialize.
            width = Options.MinWidth.GridUnitType switch
            {
                GridUnitType.Auto => width,
                GridUnitType.Pixel => Math.Max(width, Options.MinWidth.Value),
                GridUnitType.Star => throw new NotImplementedException(),
                _ => width
            };

            return Options.MaxWidth?.GridUnitType switch
            {
                GridUnitType.Auto => width,
                GridUnitType.Pixel => Math.Min(width, Options.MaxWidth.Value.Value),
                GridUnitType.Star => throw new NotImplementedException(),
                _ => width
            };
        }

        private void SetWidth(GridLength width)
        {
            _width = width;

            if (width.IsAbsolute)
                ActualWidth = width.Value;
        }

        private static double NonNaN(double v) => double.IsNaN(v) ? 0 : v;
    }
}
