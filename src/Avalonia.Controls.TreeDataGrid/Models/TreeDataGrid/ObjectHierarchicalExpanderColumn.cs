using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Experimental.Data;

#if TREE_DATAGRID_UNO

namespace Uno.Controls.Models.TreeDataGrid

#else

namespace Avalonia.Controls.Models.TreeDataGrid

#endif
{
    internal class ObjectHierarchicalExpanderColumn : NotifyingBase, IColumn<object>, IExpanderColumn<object>, IUpdateColumnLayout
    {
        private readonly IColumn<object> _inner;
        private readonly Func<object, IEnumerable<object>?> _childSelector;
        private readonly Func<object, bool>? _hasChildrenSelector;
        private readonly Func<object, object>[]? _hasChildrenLinks;
        private readonly Func<object, bool>? _isExpandedGetter;
        private readonly Action<object, bool>? _isExpandedSetter;
        private readonly Func<object, object>[]? _isExpandedLinks;
        private double _actualWidth = double.NaN;

        public ObjectHierarchicalExpanderColumn(
            IColumn<object> inner,
            Func<object, IEnumerable<object>?> childSelector,
            Func<object, bool>? hasChildrenSelector,
            Func<object, bool>? isExpandedGetter,
            Action<object, bool>? isExpandedSetter,
            Func<object, object>[]? hasChildrenLinks,
            Func<object, object>[]? isExpandedLinks)
        {
            _inner = inner;
            _inner.PropertyChanged += OnInnerPropertyChanged;
            _childSelector = childSelector;
            _hasChildrenSelector = hasChildrenSelector;
            _hasChildrenLinks = hasChildrenLinks;
            _isExpandedGetter = isExpandedGetter;
            _isExpandedSetter = isExpandedSetter;
            _isExpandedLinks = isExpandedLinks;
            _actualWidth = inner.ActualWidth;
        }

        public double ActualWidth
        {
            get => _actualWidth;
            private set => RaiseAndSetIfChanged(ref _actualWidth, value);
        }

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

        public IColumn<object> Inner => _inner;
        double IUpdateColumnLayout.MinActualWidth => ((IUpdateColumnLayout)_inner).MinActualWidth;
        double IUpdateColumnLayout.MaxActualWidth => ((IUpdateColumnLayout)_inner).MaxActualWidth;
        bool IUpdateColumnLayout.StarWidthWasConstrained => ((IUpdateColumnLayout)_inner).StarWidthWasConstrained;

        public ICell CreateCell(IRow<object> row)
        {
            if (row is HierarchicalRow<object> hierarchicalRow)
            {
                var showExpander = new ShowExpanderObservable<object>(
                    _childSelector,
                    _hasChildrenSelector is null ? null : CreateBinding(_hasChildrenSelector, null, _hasChildrenLinks),
                    hierarchicalRow.Model);
                var isExpanded = _isExpandedGetter is null
                    ? null
                    : CreateBinding(_isExpandedGetter, _isExpandedSetter, _isExpandedLinks).Instance(hierarchicalRow.Model);
                return new ExpanderCell<object>(_inner.CreateCell(hierarchicalRow), hierarchicalRow, showExpander, isExpanded);
            }

            throw new NotSupportedException();
        }

        public bool HasChildren(object model)
        {
            return _hasChildrenSelector?.Invoke(model) ?? _childSelector(model) is { } children && System.Linq.Enumerable.Any(children);
        }

        public IEnumerable<object>? GetChildModels(object model)
        {
            return _childSelector(model);
        }

        public Comparison<object?>? GetComparison(ListSortDirection direction)
        {
            return _inner.GetComparison(direction);
        }

        void IExpanderColumn<object>.SetModelIsExpanded(IExpanderRow<object> row)
        {
            _isExpandedSetter?.Invoke(row.Model, row.IsExpanded);
        }

        double IUpdateColumnLayout.CellMeasured(double width, int rowIndex)
        {
            return ((IUpdateColumnLayout)_inner).CellMeasured(width, rowIndex);
        }

        bool IUpdateColumnLayout.CommitActualWidth()
        {
            var result = ((IUpdateColumnLayout)_inner).CommitActualWidth();
            ActualWidth = _inner.ActualWidth;
            return result;
        }

        void IUpdateColumnLayout.CalculateStarWidth(double availableWidth, double totalStars)
        {
            ((IUpdateColumnLayout)_inner).CalculateStarWidth(availableWidth, totalStars);
            ActualWidth = _inner.ActualWidth;
        }

        void IUpdateColumnLayout.SetWidth(GridLength width)
        {
            ((IUpdateColumnLayout)_inner).SetWidth(width);

            if (width.IsAbsolute)
                ActualWidth = width.Value;
        }

        private static TypedBinding<object, bool> CreateBinding(
            Func<object, bool> read,
            Action<object, bool>? write,
            Func<object, object>[]? links)
        {
            return new TypedBinding<object, bool>
            {
                Read = read,
                Write = write,
                Links = links is { Length: > 0 } ? links : new Func<object, object>[] { x => x },
            };
        }

        private void OnInnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CanUserResize) ||
                e.PropertyName == nameof(Header) ||
                e.PropertyName == nameof(SortDirection) ||
                e.PropertyName == nameof(Width))
            {
                RaisePropertyChanged(e);
            }
        }
    }
}
