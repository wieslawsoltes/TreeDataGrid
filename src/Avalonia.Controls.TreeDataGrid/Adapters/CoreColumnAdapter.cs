using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls.Models.TreeDataGrid;
using Core = global::TreeDataGridCore;

namespace Avalonia.Controls.Adapters
{
    /// <summary>View-owned factories for named Core column presentations.</summary>
    public sealed class TreeDataGridPresentationOptions<TModel> where TModel : class
    {
        public IDictionary<string, Func<Core.Models.IColumn<TModel>, IColumn<TModel>>> Columns { get; } =
            new Dictionary<string, Func<Core.Models.IColumn<TModel>, IColumn<TModel>>>(StringComparer.Ordinal);
    }

    internal sealed class CoreColumnFactory<TModel> : Core.Models.IColumnVisitor<TModel, IColumn<TModel>> where TModel : class
    {
        private readonly TreeDataGridPresentationOptions<TModel> _options;
        public CoreColumnFactory(TreeDataGridPresentationOptions<TModel> options) => _options = options;
        public IColumn<TModel> Visit<TValue>(Core.Models.ValueColumn<TModel, TValue> column)
        {
            if (column.PresentationKey is { } key && _options.Columns.TryGetValue(key, out var factory)) return factory(column);
            if (column is Core.Models.CheckBoxColumn<TModel> { BooleanGetter: { } booleanGetter } twoState)
                return new CheckBoxColumn<TModel>(column.Header, booleanGetter, twoState.BooleanSetter, column.Width.ToAvalonia(), CopyOptions(column, new CheckBoxColumnOptions<TModel>()));
            if (column is Core.Models.CheckBoxColumn<TModel> checkBox)
                return new CheckBoxColumn<TModel>(column.Header, checkBox.GetterExpression, checkBox.Setter, column.Width.ToAvalonia(), CopyOptions(column, new CheckBoxColumnOptions<TModel>()));
            if (column.PresentationKey is { } missing) throw new InvalidOperationException($"No TreeDataGrid column presentation is registered for '{missing}'.");
            var options = CopyOptions(column, new TextColumnOptions<TModel>());
            return column.Setter is null ?
                new TextColumn<TModel, TValue>(column.Header, column.GetterExpression!, column.Width.ToAvalonia(), options) :
                new TextColumn<TModel, TValue>(column.Header, column.GetterExpression!, column.Setter!, column.Width.ToAvalonia(), options);
        }
        public IColumn<TModel> Visit(Core.Models.HierarchicalExpanderColumn<TModel> column) =>
            new HierarchicalExpanderColumn<TModel>(column.Inner.Accept(this), column.GetChildModels, column.HasChildrenSelector);
        private static TOptions CopyOptions<TValue, TOptions>(Core.Models.ValueColumn<TModel, TValue> column, TOptions options) where TOptions : ColumnOptions<TModel>
        {
            options.CanUserResizeColumn = column.Options.CanUserResizeColumn;
            options.CanUserSortColumn = column.Options.CanUserSortColumn;
            options.MinWidth = column.Options.MinWidth.ToAvalonia();
            options.MaxWidth = column.Options.MaxWidth?.ToAvalonia();
            options.CompareAscending = column.GetComparison(ListSortDirection.Ascending);
            options.CompareDescending = column.GetComparison(ListSortDirection.Descending);
            return options;
        }
    }

    internal sealed class CoreColumnAdapter<TModel> : IColumn<TModel>, IUpdateColumnLayout, IColumnMeasurementOptions, IReusableCellColumn<TModel>, ITextSearchableColumn<TModel>, IDisposable where TModel : class
    {
        public Core.Models.IColumn<TModel> Model { get; }
        public string? PresentationKey { get; }
        private readonly IColumn<TModel> _inner;
        private IUpdateColumnLayout Layout => (IUpdateColumnLayout)_inner;
        public CoreColumnAdapter(Core.Models.IColumn<TModel> model, IColumn<TModel> inner)
        {
            Model = model; _inner = inner; PresentationKey = model.PresentationKey;
            _inner.SortDirection = model.SortDirection;
            _inner.PropertyChanged += OnPropertyChanged;
        }
        public object? Header => Model.Header;
        public GridLength Width => _inner.Width;
        public double ActualWidth => _inner.ActualWidth;
        public bool? CanUserResize => _inner.CanUserResize;
        public object? Tag { get => Model.Tag; set => Model.Tag = value; }
        public ListSortDirection? SortDirection { get => Model.SortDirection; set => Model.SortDirection = value; }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e) => PropertyChanged?.Invoke(this, e);
        public void RefreshSortDirection() { _inner.SortDirection = Model.SortDirection; PropertyChanged?.Invoke(this, new(nameof(SortDirection))); }
        public ICell CreateCell(IRow<TModel> row) => _inner.CreateCell(row);
        public Comparison<TModel?>? GetComparison(ListSortDirection direction) => Model.GetComparison(direction);
        bool IReusableCellColumn<TModel>.TryReuseCell(ICell cell, IRow<TModel> row) => _inner is IReusableCellColumn<TModel> reusable && reusable.TryReuseCell(cell, row);
        private ITextSearchableColumn<TModel>? SearchColumn => (_inner is HierarchicalExpanderColumn<TModel> expander ? expander.Inner : _inner) as ITextSearchableColumn<TModel>;
        bool ITextSearchableColumn<TModel>.IsTextSearchEnabled => SearchColumn?.IsTextSearchEnabled ?? false;
        string? ITextSearchableColumn<TModel>.SelectValue(TModel model) => SearchColumn?.SelectValue(model);
        public double MinActualWidth => Layout.MinActualWidth;
        public double MaxActualWidth => Layout.MaxActualWidth;
        public bool StarWidthWasConstrained => Layout.StarWidthWasConstrained;
        public bool RequiresUnconstrainedWidthMeasurement => _inner is not IColumnMeasurementOptions options || options.RequiresUnconstrainedWidthMeasurement;
        public double CellMeasured(double width, int rowIndex) => Layout.CellMeasured(width, rowIndex);
        public bool CommitActualWidth() => Layout.CommitActualWidth();
        public void CalculateStarWidth(double availableWidth, double totalStars) => Layout.CalculateStarWidth(availableWidth, totalStars);
        public void SetWidth(GridLength width)
        {
            Layout.SetWidth(width);
            Model.Width = width.ToCore();
            PropertyChanged?.Invoke(this, new(nameof(Width)));
        }
        public void Dispose() => _inner.PropertyChanged -= OnPropertyChanged;
    }
}
