using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Avalonia.Controls.Models.TreeDataGrid
{
    internal sealed class FilteredExpanderColumn<TModel> : IExpanderColumn<TModel>, IUpdateColumnLayout
        where TModel : class
    {
        private readonly IExpanderColumn<TModel> _inner;
        private readonly Func<Func<TModel, bool>?> _filterAccessor;

        public FilteredExpanderColumn(IExpanderColumn<TModel> inner, Func<Func<TModel, bool>?> filterAccessor)
        {
            _inner = inner;
            _filterAccessor = filterAccessor;
            _inner.PropertyChanged += OnInnerPropertyChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public double ActualWidth => _inner.ActualWidth;
        public bool? CanUserResize => _inner.CanUserResize;
        public object? Header => _inner.Header;
        public GridLength Width => _inner.Width;

        public ListSortDirection? SortDirection
        {
            get => _inner.SortDirection;
            set => _inner.SortDirection = value;
        }

        public object? Tag
        {
            get => _inner.Tag;
            set => _inner.Tag = value;
        }

        double IUpdateColumnLayout.MinActualWidth => ((IUpdateColumnLayout)_inner).MinActualWidth;
        double IUpdateColumnLayout.MaxActualWidth => ((IUpdateColumnLayout)_inner).MaxActualWidth;
        bool IUpdateColumnLayout.StarWidthWasConstrained => ((IUpdateColumnLayout)_inner).StarWidthWasConstrained;

        public ICell CreateCell(IRow<TModel> row) => _inner.CreateCell(row);

        public Comparison<TModel?>? GetComparison(ListSortDirection direction) => _inner.GetComparison(direction);

        public bool HasChildren(TModel model)
        {
            var children = GetChildModels(model);
            return children?.Any() ?? false;
        }

        public IEnumerable<TModel>? GetChildModels(TModel model)
        {
            var children = _inner.GetChildModels(model);
            var filter = _filterAccessor();

            return filter is null || children is null ? children : children.Where(filter);
        }

        void IExpanderColumn<TModel>.SetModelIsExpanded(IExpanderRow<TModel> row)
        {
            _inner.SetModelIsExpanded(row);
        }

        double IUpdateColumnLayout.CellMeasured(double width, int rowIndex)
        {
            return ((IUpdateColumnLayout)_inner).CellMeasured(width, rowIndex);
        }

        bool IUpdateColumnLayout.CommitActualWidth()
        {
            return ((IUpdateColumnLayout)_inner).CommitActualWidth();
        }

        void IUpdateColumnLayout.CalculateStarWidth(double availableWidth, double totalStars)
        {
            ((IUpdateColumnLayout)_inner).CalculateStarWidth(availableWidth, totalStars);
        }

        void IUpdateColumnLayout.SetWidth(GridLength width)
        {
            ((IUpdateColumnLayout)_inner).SetWidth(width);
        }

        private void OnInnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }
    }
}
