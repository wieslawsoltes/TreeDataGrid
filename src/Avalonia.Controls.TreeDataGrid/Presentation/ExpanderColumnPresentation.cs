using Avalonia.Controls.Models;
using System;
using System.ComponentModel;
using Avalonia.Controls.Adapters;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Experimental.Data;
using Core = global::TreeDataGridCore;
namespace Avalonia.Controls.Presentation
{
    internal sealed class ExpanderColumnPresentation<T> : ICellColumn<T>, IUpdateColumnLayout,
        IColumnMeasurementOptions, ITextSearchableColumn<T>, IDisposable where T : class
    {
        private readonly Core.Models.HierarchicalExpanderColumn<T> _model;
        private readonly ICellColumn<T> _inner;
        private readonly TypedBinding<T, bool>? _hasChildren;
        private bool _disposed;
        public ExpanderColumnPresentation(Core.Models.HierarchicalExpanderColumn<T> model, ICellColumn<T> inner)
        { _model = model; _inner = inner; _hasChildren = model.HasChildrenSelector is { } expr ? TypedBinding<T>.OneWay(expr) : null; }
        public object? Header => _model.Header;
        public GridLength Width => _inner.Width;
        public double ActualWidth => _inner.ActualWidth;
        public bool? CanUserResize => _inner.CanUserResize;
        public object? Tag { get => _model.Tag; set => _model.Tag = value; }
        public ListSortDirection? SortDirection { get => _inner.SortDirection; set => _inner.SortDirection = value; }
        public event PropertyChangedEventHandler? PropertyChanged { add => _inner.PropertyChanged += value; remove => _inner.PropertyChanged -= value; }
        public ICell CreateCell(Core.Models.IRow<T> row) => new CoreExpanderCell<T>(_inner.CreateCell(row), (Core.Models.IExpanderRow<T>)row,
            new ShowExpanderObservable<T>(_model.GetChildModels, _hasChildren, row.Model));
        public ICell CreateCell(IRow<T> row) => CreateCell((Core.Models.IRow<T>)row);
        public Comparison<T?>? GetComparison(ListSortDirection direction) => _model.GetComparison(direction);
        private IUpdateColumnLayout Layout => (IUpdateColumnLayout)_inner;
        public double MinActualWidth => Layout.MinActualWidth;
        public double MaxActualWidth => Layout.MaxActualWidth;
        public bool StarWidthWasConstrained => Layout.StarWidthWasConstrained;
        public bool RequiresUnconstrainedWidthMeasurement => _inner is not IColumnMeasurementOptions options || options.RequiresUnconstrainedWidthMeasurement;
        public double CellMeasured(double width, int rowIndex) => Layout.CellMeasured(width, rowIndex);
        public bool CommitActualWidth() => Layout.CommitActualWidth();
        public void CalculateStarWidth(double availableWidth, double totalStars) => Layout.CalculateStarWidth(availableWidth, totalStars);
        public void SetWidth(GridLength width) => Layout.SetWidth(width);
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            (_inner as IDisposable)?.Dispose();
        }
        bool ITextSearchableColumn<T>.IsTextSearchEnabled => (_inner as ITextSearchableColumn<T>)?.IsTextSearchEnabled ?? false;
        string? ITextSearchableColumn<T>.SelectValue(T model) => (_inner as ITextSearchableColumn<T>)?.SelectValue(model);
    }
    internal sealed class CoreExpanderCell<T> : NotifyingBase, IExpanderCellPresentation, IDisposable where T : class
    {
        private readonly ICell _inner;
        private readonly Core.Models.IExpanderRow<T> _row;
        private readonly IDisposable _showExpander;
        public CoreExpanderCell(ICell inner, Core.Models.IExpanderRow<T> row, IObservable<bool> showExpander)
        { _inner = inner; _row = row; _row.PropertyChanged += OnRowChanged; _showExpander = showExpander.Subscribe(row.UpdateShowExpander); }
        public object? Content => _inner;
        public Core.Models.IRow Row => _row;
        public bool ShowExpander => _row.ShowExpander;
        public bool IsExpanded { get => _row.IsExpanded; set => _row.IsExpanded = value; }
        public object? Value => _inner.Value;
        public bool CanEdit => _inner.CanEdit;
        public BeginEditGestures EditGestures => _inner.EditGestures;
        private void OnRowChanged(object? sender, PropertyChangedEventArgs e) => RaisePropertyChanged(e);
        public void Dispose()
        {
            _row.PropertyChanged -= OnRowChanged;
            _showExpander.Dispose();
            if (_row is Core.Models.HierarchicalRow<T> row)
                row.ClearShowExpander();
            (_inner as IDisposable)?.Dispose();
        }
    }
}
