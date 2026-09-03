using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
namespace TreeDataGridCore.Models
{
    public interface IColumn : INotifyPropertyChanged
    {
        string Id { get; }
        object? Header { get; }
        GridLength Width { get; set; }
        bool IsVisible { get; set; }
        string? PresentationKey { get; set; }
        ListSortDirection? SortDirection { get; set; }
        object? Tag { get; set; }
    }
    public interface IColumn<TModel> : IColumn
    {
        Comparison<TModel?>? GetComparison(ListSortDirection direction);
        TResult Accept<TResult>(IColumnVisitor<TModel, TResult> visitor);
    }
    public interface IColumnVisitor<TModel, out TResult>
    {
        TResult Visit<TValue>(ValueColumn<TModel, TValue> column);
        TResult Visit(HierarchicalExpanderColumn<TModel> column);
    }
    public class ColumnOptions<TModel>
    {
        public bool? CanUserResizeColumn { get; set; }
        public bool? CanUserSortColumn { get; set; }
        public GridLength MinWidth { get; set; } = new GridLength(30);
        public GridLength? MaxWidth { get; set; }
        public Comparison<TModel?>? CompareAscending { get; set; }
        public Comparison<TModel?>? CompareDescending { get; set; }
    }
    public class ColumnList<TModel> : NotifyingListBase<IColumn<TModel>>
    {
        public void SetColumnWidth(int columnIndex, GridLength width) => this[columnIndex].Width = width;
        public void Move(int oldIndex, int newIndex)
        {
            if ((uint)oldIndex >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(oldIndex));
            if ((uint)newIndex >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(newIndex));
            if (oldIndex != newIndex)
                MoveItem(oldIndex, newIndex);
        }
    }
    public abstract class ColumnBase<TModel> : NotifyingBase, IColumn<TModel>
    {
        private GridLength _width;
        private bool _isVisible = true;
        private string? _presentationKey;
        private ListSortDirection? _sortDirection;
        protected ColumnBase(object? header, GridLength? width, ColumnOptions<TModel>? options, string? id)
        { Header = header; Id = id ?? header?.ToString() ?? ""; _width = width ?? GridLength.Auto; Options = options ?? new ColumnOptions<TModel>(); }
        public string Id { get; }
        public object? Header { get; }
        public ColumnOptions<TModel> Options { get; }
        public GridLength Width { get => _width; set => RaiseAndSetIfChanged(ref _width, value); }
        public bool IsVisible { get => _isVisible; set => RaiseAndSetIfChanged(ref _isVisible, value); }
        public string? PresentationKey { get => _presentationKey; set => RaiseAndSetIfChanged(ref _presentationKey, value); }
        public ListSortDirection? SortDirection { get => _sortDirection; set => RaiseAndSetIfChanged(ref _sortDirection, value); }
        public object? Tag { get; set; }
        public abstract Comparison<TModel?>? GetComparison(ListSortDirection direction);
        public abstract TResult Accept<TResult>(IColumnVisitor<TModel, TResult> visitor);
    }
    public class ValueColumn<TModel, TValue> : ColumnBase<TModel>
    {
        private Func<TModel, TValue>? _getter;
        public ValueColumn(object? header, Expression<Func<TModel, TValue>> getter,
            Action<TModel, TValue>? setter = null, GridLength? width = null,
            ColumnOptions<TModel>? options = null, string? id = null) : base(header, width, options, id)
        { GetterExpression = getter ?? throw new ArgumentNullException(nameof(getter)); Setter = setter; }
        private ValueColumn(object? header, Func<TModel, TValue> getter, string? propertyName,
            Action<TModel, TValue>? setter, GridLength? width, ColumnOptions<TModel>? options, string? id)
            : base(header, width, options, id)
        { _getter = getter ?? throw new ArgumentNullException(nameof(getter)); PropertyName = propertyName; Setter = setter; }

        /// <summary>Uses an existing accessor without building or compiling an expression.
        /// Default view bindings observe changes on the row model; custom views may use PropertyName.</summary>
        public static ValueColumn<TModel, TValue> FromDelegate(object? header, Func<TModel, TValue> getter,
            string? propertyName = null, Action<TModel, TValue>? setter = null, GridLength? width = null,
            ColumnOptions<TModel>? options = null, string? id = null) => new(header, getter, propertyName, setter, width, options, id);
        public string? PropertyName { get; }
        public Expression<Func<TModel, TValue>>? GetterExpression { get; }
        // UI bindings can consume the expression directly. Compile a Core accessor only
        // when neutral sorting or value access needs it, then reuse it.
        public Func<TModel, TValue> Getter => _getter ??= GetterExpression!.Compile();
        public Action<TModel, TValue>? Setter { get; }
        public TValue GetValue(TModel model) => Getter(model);
        public void SetValue(TModel model, TValue value)
        {
            if (Setter is null)
                throw new InvalidOperationException("Column is read-only.");
            Setter(model, value);
        }
        public override Comparison<TModel?>? GetComparison(ListSortDirection direction)
        {
            if (Options.CanUserSortColumn == false)
                return null;
            var custom = direction == ListSortDirection.Ascending ? Options.CompareAscending : Options.CompareDescending;
            if (custom is not null)
                return custom;
            return (x, y) => direction == ListSortDirection.Ascending ? Compare(x, y) : Compare(y, x);
        }
        private int Compare(TModel? x, TModel? y)
        {
            if (x is null)
                return y is null ? 0 : -1;
            if (y is null)
                return 1;
            return Comparer<TValue>.Default.Compare(Getter(x), Getter(y));
        }
        public override TResult Accept<TResult>(IColumnVisitor<TModel, TResult> visitor) => visitor.Visit(this);
    }
    public class TextColumn<TModel, TValue> : ValueColumn<TModel, TValue>
    {
        public TextColumn(object? header, Expression<Func<TModel, TValue>> getter, GridLength? width = null,
            ColumnOptions<TModel>? options = null, string? id = null) : base(header, getter, null, width, options, id) { }
        public TextColumn(object? header, Expression<Func<TModel, TValue>> getter, Action<TModel, TValue> setter,
            GridLength? width = null, ColumnOptions<TModel>? options = null, string? id = null) : base(header, getter, setter, width, options, id) { }
    }
    public class CheckBoxColumn<TModel> : ValueColumn<TModel, bool?>
    {
        public Expression<Func<TModel, bool>>? BooleanGetter { get; }
        public Action<TModel, bool>? BooleanSetter { get; }
        public bool IsThreeState => BooleanGetter is null;
        public CheckBoxColumn(object? header, Expression<Func<TModel, bool>> getter, Action<TModel, bool>? setter = null,
            GridLength? width = null, ColumnOptions<TModel>? options = null, string? id = null)
            : base(header, Expression.Lambda<Func<TModel, bool?>>(Expression.Convert(getter.Body, typeof(bool?)), getter.Parameters),
                setter is null ? null : (model, value) => setter(model, value ?? false), width, options, id)
        { BooleanGetter = getter; BooleanSetter = setter; PresentationKey = "CheckBox"; }

        public CheckBoxColumn(object? header, Expression<Func<TModel, bool?>> getter, Action<TModel, bool?>? setter = null,
            GridLength? width = null, ColumnOptions<TModel>? options = null, string? id = null) : base(header, getter, setter, width, options, id)
        { PresentationKey = "CheckBox"; }
    }
    public class TemplateColumn<TModel> : ValueColumn<TModel, TModel>
    {
        public TemplateColumn(object? header, string presentationKey, GridLength? width = null,
            ColumnOptions<TModel>? options = null, string? id = null) : base(header, x => x, null, width, options, id)
        { PresentationKey = presentationKey ?? throw new ArgumentNullException(nameof(presentationKey)); }
        public override Comparison<TModel?>? GetComparison(ListSortDirection direction) =>
            Options.CanUserSortColumn == false ? null :
            direction == ListSortDirection.Ascending ? Options.CompareAscending : Options.CompareDescending;
    }
    public class HierarchicalExpanderColumn<TModel> : IExpanderColumn<TModel>, IModelExpansionObserver<TModel>
    {
        private readonly Func<TModel, IEnumerable<TModel>?> _children;
        private readonly Func<TModel, bool>? _hasChildren;
        private readonly Action<TModel, bool>? _setExpanded;
        private readonly Func<TModel, bool>? _getExpanded;
        private readonly Utils.PropertyPathObserver<TModel>? _expandedObserver;
        public Expression<Func<TModel, bool>>? HasChildrenSelector { get; }
        public Expression<Func<TModel, bool>>? IsExpandedSelector { get; }
        public HierarchicalExpanderColumn(IColumn<TModel> inner, Func<TModel, IEnumerable<TModel>?> childSelector,
            Expression<Func<TModel, bool>>? hasChildrenSelector = null,
            Expression<Func<TModel, bool>>? isExpandedSelector = null, Action<TModel, bool>? setIsExpanded = null)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _children = childSelector ?? throw new ArgumentNullException(nameof(childSelector));
            HasChildrenSelector = hasChildrenSelector;
            IsExpandedSelector = isExpandedSelector;
            _hasChildren = hasChildrenSelector?.Compile();
            _getExpanded = isExpandedSelector?.Compile();
            _expandedObserver = isExpandedSelector is null ? null :
                Utils.PropertyPathObserver<TModel>.Create(isExpandedSelector);
            _setExpanded = setIsExpanded;
            if (isExpandedSelector is not null && _setExpanded is null)
            {
                var value = Expression.Parameter(typeof(bool), "value");
                _setExpanded = Expression.Lambda<Action<TModel, bool>>(Expression.Assign(isExpandedSelector.Body, value), isExpandedSelector.Parameters[0], value).Compile();
            }
        }
        public bool? GetModelIsExpanded(TModel model) => _getExpanded?.Invoke(model);
        Utils.PropertyPathObserver<TModel>? IModelExpansionObserver<TModel>.ExpansionObserver => _expandedObserver;
        public IColumn<TModel> Inner { get; }
        public string Id => Inner.Id;
        public object? Header => Inner.Header;
        public GridLength Width { get => Inner.Width; set => Inner.Width = value; }
        public bool IsVisible { get => Inner.IsVisible; set => Inner.IsVisible = value; }
        public string? PresentationKey { get => Inner.PresentationKey; set => Inner.PresentationKey = value; }
        public ListSortDirection? SortDirection { get => Inner.SortDirection; set => Inner.SortDirection = value; }
        public object? Tag { get => Inner.Tag; set => Inner.Tag = value; }
        public event PropertyChangedEventHandler? PropertyChanged { add => Inner.PropertyChanged += value; remove => Inner.PropertyChanged -= value; }
        public bool HasChildren(TModel model) => _hasChildren?.Invoke(model) ?? System.Linq.Enumerable.Any(_children(model) ?? Array.Empty<TModel>());
        public IEnumerable<TModel>? GetChildModels(TModel model) => _children(model);
        public void SetModelIsExpanded(IExpanderRow<TModel> row) => _setExpanded?.Invoke(row.Model, row.IsExpanded);
        public Comparison<TModel?>? GetComparison(ListSortDirection direction) => Inner.GetComparison(direction);
        public TResult Accept<TResult>(IColumnVisitor<TModel, TResult> visitor) => visitor.Visit(this);
    }
}
