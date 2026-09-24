using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using UICell = Uno.Controls.Models.TreeDataGrid.ICell;

namespace Uno.Controls.Presentation;

using UI = global::Uno.Controls.Models.TreeDataGrid;

/// <summary>Adapts a public column implementation without copying its Core source.</summary>
internal sealed partial class CellColumnAdapter<TModel> : CellColumn where TModel : class
{
    private readonly ICellColumn<TModel> _inner;
    private bool _disposed;
    public CellColumnAdapter(IColumn model, ICellColumn<TModel> inner) : base(model)
    {
        _inner = inner;
        _inner.SetWidth(Width);
        _inner.SortDirection = model.SortDirection;
        AttachAdapterHandler(_inner, OnInnerChanged);
    }
    public override object? Header { get => _inner.Header; set => throw new NotSupportedException("The custom column owns its header."); }
    public override bool? CanUserResize => _inner.CanUserResize;
    public override double MinimumWidth => GetConstraint(maximum: false);
    public override double MaximumWidth => GetConstraint(maximum: true);
    private double GetConstraint(bool maximum)
    {
        var value = maximum ? _inner.MaxActualWidth : _inner.MinActualWidth;
        if (!double.IsNaN(value) || HasWidthMeasurement || _inner is not CellColumnBase<TModel> column)
            return value;
        // The reference custom-column base reports NaN for unmeasured Auto
        // constraints. Give native layout a discovery interval, without faking
        // a measurement or changing that public contract. Only this known base
        // receives the bridge; invalid arbitrary custom constraints still fail.
        var minimum = column.Options.MinWidth.IsAuto ? 0 : column.Options.MinWidth.Value;
        var limit = column.Options.MaxWidth is { IsAuto: false } bound ? bound.Value : double.PositiveInfinity;
        return Math.Min(limit, Math.Max(minimum, maximum ? double.PositiveInfinity : 0));
    }
    // Match Avalonia's opt-in measurement contract. Unannotated custom
    // columns retain conservative natural measurement for Auto constraints.
    public override bool RequiresUnconstrainedWidthMeasurement =>
        (_inner as UI.IColumnMeasurementOptions)?.RequiresUnconstrainedWidthMeasurement ?? true;
    public override CellValue CreateCell(IRow row)
    {
        var cell = _inner.CreateCell((IRow<TModel>)row) ?? throw new InvalidOperationException("The column returned no cell.");
        return Adapt(cell, true, null);
    }
    // Public view-row realization transfers the actual UI model to its caller.
    // Do not create a native adapter that would need another ownership registry.
    internal override UICell CreateCellModel(IRow row) => _inner.CreateCell((IRow<TModel>)row) ??
        throw new InvalidOperationException("The column returned no cell.");
    internal static CellValue Adapt(UICell cell, bool ownsModel, HashSet<UICell>? ancestors = null)
    {
        if (cell is CellValue native) return native;
        try
        {
            if (cell is UI.IExpanderCellPresentation expander)
            {
                ancestors ??= new(ReferenceEqualityComparer.Instance);
                if (!ancestors.Add(cell)) throw new InvalidOperationException("An expander cell cannot contain itself or an ancestor.");
                try { return new CustomExpanderValue(expander, ownsModel, ancestors); }
                finally { ancestors.Remove(cell); }
            }
            return new CustomCellValue(cell, ownsModel);
        }
        catch (Exception error)
        {
            try { if (ownsModel) (cell as IDisposable)?.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }
    // Third-party columns select each row's actual cell model/kind. Do not
    // overwrite it with this adapter's default column kind.
    internal override void ConfigureCell(CellValue value) { }
    internal override bool SupportsRetainedCellReuse => true;
    internal override bool TryReuseCell(CellValue value, IRow row)
    {
        if (_disposed) return false;
        // Even rejected or throwing reuse can partially mutate a custom cell.
        // Supersede old writes before entering that application callback.
        if (value is CustomCellValue customCell) customCell.InvalidateWrite();
        else if (value is CustomExpanderValue customExpander) customExpander.InvalidateWrite();
        if (!_inner.TryReuseCell(value.PresentationModel, (IRow<TModel>)row) || _disposed) return false;
        if (value is CustomCellValue cell) cell.RefreshAfterRetarget();
        if (value is CustomExpanderValue expander) expander.RefreshAfterRetarget();
        return true;
    }
    internal override bool RecordWidth(double width, int rowIndex = -1)
    {
        _inner.CellMeasured(width, rowIndex);
        return base.RecordWidth(width, rowIndex);
    }
    internal override bool SetActualWidth(double width)
    {
        if (Width.IsStar) _inner.CalculateStarWidth(width, Width.Value > 0 ? Width.Value : 1);
        _inner.CommitActualWidth();
        return base.SetActualWidth(width);
    }
    internal override void ModelChanged(PropertyChangedEventArgs e)
    {
        _inner.SetWidth(Width);
        _inner.SortDirection = Model.SortDirection;
        base.ModelChanged(e);
    }
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ReleaseAdapterModel(_inner, true, OnInnerChanged);
    }
    private void OnInnerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_disposed) RaisePropertyChanged(e);
    }

    private sealed class CustomExpanderValue : ExpanderCellValue
    {
        private readonly UI.IExpanderCellPresentation _model;
        private readonly bool _ownsModel;
        private CellValue _content;
        private object? _rawContent;
        private bool _disposed;

        public CustomExpanderValue(UI.IExpanderCellPresentation model, bool ownsModel, HashSet<UICell> ancestors)
        {
            _model = model;
            _ownsModel = ownsModel;
            _rawContent = model.Content;
            _content = _rawContent is UICell cell ? Adapt(cell, false, ancestors) : new EmptyCell();
            Kind = CellKind.Expander;
            EditGestures = model.EditGestures;
            try
            {
                _content.PropertyChanged += InnerChanged;
                if (model is INotifyPropertyChanged notifications) notifications.PropertyChanged += ModelChanged;
            }
            catch
            {
                _content.PropertyChanged -= InnerChanged;
                try { if (model is INotifyPropertyChanged notifications) notifications.PropertyChanged -= ModelChanged; }
                finally { DisposeAdapter(_content, _rawContent); }
                throw;
            }
        }
        public override UICell PresentationModel => _model;
        public override CellValue Inner => _content;
        public override object? Content => _rawContent;
        internal override bool HasContent => _rawContent is UICell;
        public override IRow Row => _model.Row;
        public override object? Value => _model.Value;
        public override bool CanEdit => _model.CanEdit;
        public override string? DisplayText => _content.DisplayText;
        public override TextCellOptions? TextOptions => _content.TextOptions;
        public override Exception? Error => (_model as UI.IBoundCellState)?.Error ?? _content.Error;
        public override bool IsExpanded { get => _model.IsExpanded; set => _model.IsExpanded = value; }
        public override bool ShowExpander => _model.ShowExpander;
        public override void Write(object? value) => _content.Write(value);
        internal void InvalidateWrite()
        {
            // Propagate only through adapter-owned wrappers. A borrowed native
            // CellValue remains responsible for its own lifetime contract.
            if (_content is CustomCellValue cell) cell.InvalidateWrite();
            else if (_content is CustomExpanderValue expander) expander.InvalidateWrite();
        }
        public void RefreshAfterRetarget()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EditGestures = _model.EditGestures;
            // A custom column may retarget silently while its control is
            // unrealized. Refresh child identity even without an INPC event.
            ModelChanged(_model, new PropertyChangedEventArgs(nameof(Content)));
            if (_content is CustomCellValue cell) cell.RefreshAfterRetarget();
            if (_content is CustomExpanderValue expander) expander.RefreshAfterRetarget();
        }

        private void ModelChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (_disposed) return;
            if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == nameof(Content))
            {
                var raw = _model.Content;
                if (!ReferenceEquals(raw, _rawContent))
                {
                    var ancestors = new HashSet<UICell>(ReferenceEqualityComparer.Instance) { _model };
                    var next = raw is UICell cell ? Adapt(cell, false, ancestors) : new EmptyCell();
                    var previous = _content;
                    var previousRaw = _rawContent;
                    previous.PropertyChanged -= InnerChanged;
                    _rawContent = raw;
                    _content = next;
                    next.PropertyChanged += InnerChanged;
                    EditGestures = _model.EditGestures;
                    try
                    {
                        RaisePropertyChanged(nameof(Content));
                        if (!_disposed) RaisePropertyChanged(new CellContentChangedEventArgs(this));
                    }
                    finally { DisposeAdapter(previous, previousRaw); }
                    if (_disposed) return;
                    // An all-properties notification may also change expansion,
                    // but must not publish the content value a second time.
                    if (string.IsNullOrEmpty(args.PropertyName))
                    {
                        RaisePropertyChanged(nameof(IsExpanded));
                        if (!_disposed) RaisePropertyChanged(nameof(ShowExpander));
                        if (!_disposed) RaisePropertyChanged(nameof(Row));
                    }
                    return;
                }
            }
            if (args.PropertyName == nameof(Value) && _rawContent is not INotifyPropertyChanged && _content is CustomCellValue adapter)
                adapter.Refresh();
            else RaisePropertyChanged(args);
        }
        private void InnerChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (!_disposed && ReferenceEquals(sender, _content)) RaisePropertyChanged(args);
        }
        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_model is INotifyPropertyChanged notifications) notifications.PropertyChanged -= ModelChanged;
            }
            finally
            {
                _content.PropertyChanged -= InnerChanged;
                try { DisposeAdapter(_content, _rawContent); }
                finally { if (_ownsModel) (_model as IDisposable)?.Dispose(); }
            }
        }
        private static void DisposeAdapter(CellValue adapter, object? model)
        {
            // The original expander owns/disposes its content. Only dispose
            // wrappers/subscriptions here, never its borrowed native cell.
            if (!ReferenceEquals(adapter, model)) adapter.Dispose();
        }
        private sealed class EmptyCell : CellValue
        {
            public override object? Value => null;
            public override bool CanEdit => false;
            public override void Write(object? value) => throw new InvalidOperationException("The expander has no cell content.");
        }
    }
}
