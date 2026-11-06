using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace TreeDataGridCore.Models
{
    /// <summary>Projects an expander column's children through a source filter.</summary>
    internal sealed class FilteredExpanderColumn<TModel> : IExpanderColumn<TModel>,
        IModelExpansionObserver<TModel>,
        IModelChildrenObserver<TModel>
    {
        private readonly IExpanderColumn<TModel> _inner;
        private readonly Func<Func<TModel, bool>?> _filterAccessor;

        public FilteredExpanderColumn(IExpanderColumn<TModel> inner, Func<Func<TModel, bool>?> filterAccessor)
        {
            _inner = inner;
            _filterAccessor = filterAccessor;
        }

        public IExpanderColumn<TModel> Inner => _inner;
        public string Id => _inner.Id;
        public object? Header => _inner.Header;
        public GridLength Width { get => _inner.Width; set => _inner.Width = value; }
        public bool IsVisible { get => _inner.IsVisible; set => _inner.IsVisible = value; }
        public string? PresentationKey { get => _inner.PresentationKey; set => _inner.PresentationKey = value; }
        public ListSortDirection? SortDirection { get => _inner.SortDirection; set => _inner.SortDirection = value; }
        public object? Tag { get => _inner.Tag; set => _inner.Tag = value; }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add => _inner.PropertyChanged += value;
            remove => _inner.PropertyChanged -= value;
        }

        public bool HasChildren(TModel model)
        {
            var filter = _filterAccessor();
            if (filter is null)
                return _inner.HasChildren(model);
            return _inner.HasChildren(model) && (_inner.GetChildModels(model)?.Any(filter) ?? false);
        }

        public bool? GetModelIsExpanded(TModel model) => _inner.GetModelIsExpanded(model);

        public IEnumerable<TModel>? GetChildModels(TModel model)
        {
            var children = _inner.GetChildModels(model);
            var filter = _filterAccessor();
            return filter is null || children is null ? children : children.Where(filter).ToList();
        }

        public void SetModelIsExpanded(IExpanderRow<TModel> row) => _inner.SetModelIsExpanded(row);

        public Comparison<TModel?>? GetComparison(ListSortDirection direction) => _inner.GetComparison(direction);

        public TResult Accept<TResult>(IColumnVisitor<TModel, TResult> visitor) => _inner.Accept(visitor);

        IDisposable? IModelExpansionObserver<TModel>.SubscribeToExpansion(TModel model, Action changed) =>
            (_inner as IModelExpansionObserver<TModel>)?.SubscribeToExpansion(model, changed);

        IDisposable? IModelChildrenObserver<TModel>.SubscribeToChildren(TModel model, Action changed) =>
            (_inner as IModelChildrenObserver<TModel>)?.SubscribeToChildren(model, changed);
    }
}
