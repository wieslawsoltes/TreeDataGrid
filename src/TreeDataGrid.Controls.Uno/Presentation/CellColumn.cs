using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using BeginEditGestures = Uno.Controls.Models.TreeDataGrid.BeginEditGestures;
using ICell = Uno.Controls.Models.TreeDataGrid.ICell;

namespace Uno.Controls.Presentation;

public enum CellKind { Text, CheckBox, Template, Expander }

/// <summary>Immutable Uno text presentation, separate from framework-neutral column definitions.</summary>
public sealed record TextCellOptions
{
    public TextAlignment TextAlignment { get; init; } = TextAlignment.Left;
    public TextWrapping TextWrapping { get; init; } = TextWrapping.NoWrap;
    public TextTrimming TextTrimming { get; init; } = TextTrimming.CharacterEllipsis;
    public TextAlignment Alignment { get => TextAlignment; init => TextAlignment = value; }
    public TextWrapping Wrapping { get => TextWrapping; init => TextWrapping = value; }
    public TextTrimming Trimming { get => TextTrimming; init => TextTrimming = value; }
    public string StringFormat { get; init; } = "{0}";
    public CultureInfo Culture { get; init; } = CultureInfo.CurrentCulture;
    public bool IsTextSearchEnabled { get; init; }
}

/// <summary>A view-owned column over a shared Core definition.</summary>
public abstract partial class CellColumn : NotifyingBase, IDisposable
{
    private double _measuredWidth;
    internal bool HasWidthMeasurement { get; private set; }
    protected double MeasuredWidth => _measuredWidth;
    protected CellColumn(IColumn model) => Model = model;
    public IColumn Model { get; private set; }
    internal void AttachModel(IColumn model) => Model = model;
    public virtual double MinimumWidth => 30;
    public virtual double MaximumWidth => double.PositiveInfinity;
    public virtual bool RequiresUnconstrainedWidthMeasurement => Model.Width.IsAuto;
    internal double AutoWidth => _measuredWidth;
    internal virtual void ResetWidthMeasurement()
    {
        _measuredWidth = 0;
        HasWidthMeasurement = false;
    }
    internal virtual bool RecordWidth(double width, int rowIndex = -1)
    {
        if (!double.IsFinite(width) || width < 0) return false;
        var changed = !HasWidthMeasurement || width > _measuredWidth;
        HasWidthMeasurement = true;
        _measuredWidth = Math.Max(_measuredWidth, width);
        return changed;
    }
    public virtual CellKind Kind => CellKind.Text;
    public virtual CellKind ContentKind => Kind;
    internal virtual CellColumn? InnerColumn => null;
    public virtual bool IsThreeState => false;
    public virtual bool? CanUserResize => null;
    public virtual bool? CanUserSort => null;
    public Microsoft.UI.Xaml.Controls.DataTemplateSelector? HeaderTemplateSelector { get; init; }
    public DataTemplate? HeaderTemplate { get; init; }
    public BeginEditGestures BeginEditGestures { get; init; } = BeginEditGestures.Default;
    public BeginEditGestures EditGestures { get => BeginEditGestures; init => BeginEditGestures = value; }
    /// <summary>Optional model-to-text selector for template-column incremental search.</summary>
    public Func<object?, string?>? TextSearchValueSelector { get; init; }
    public virtual bool IsTextSearchEnabled => TextSearchValueSelector is not null;
    public virtual string? GetSearchText(object? model) => TextSearchValueSelector?.Invoke(model);
    public virtual string FormatValue(object? value) => TextOptions is { } options
        ? CellTextFormatting.Format(options.Culture, options.StringFormat, value) : value?.ToString() ?? string.Empty;
    public virtual TextCellOptions? TextOptions => null;
    public virtual DataTemplate? GetCellTemplate(Microsoft.UI.Xaml.Controls.Control anchor) => null;
    public virtual DataTemplate? GetCellEditingTemplate(Microsoft.UI.Xaml.Controls.Control anchor) => null;
    public abstract CellValue CreateCell(IRow row);
    internal virtual ICell CreateCellModel(IRow row)
    {
        var cell = CreateCell(row);
        try { ConfigureCell(cell); return cell; }
        catch { cell.Dispose(); throw; }
    }
    internal virtual bool SupportsRetainedCellReuse => false;
    internal virtual bool TryReuseCell(CellValue value, IRow row) => value.TryRetarget(row);
    internal virtual void ConfigureCell(CellValue value) => value.Kind = Kind;
    public virtual void Dispose() { }
}

/// <summary>A realized view value. The Core source never owns this object.</summary>
public abstract class CellValue : NotifyingBase, ICell, IDisposable
{
    /// <summary>The native presentation kind assigned by the owning view column.</summary>
    public CellKind Kind { get; internal set; }
    public virtual CellKind ContentKind => Kind;
    public virtual ICell PresentationModel => this;
    public virtual bool CanWrite => CanEdit;
    public virtual bool? IsThreeState => null;
    public virtual object? EditTarget => null;
    public virtual DataTemplate? GetCellTemplate(Microsoft.UI.Xaml.Controls.Control anchor) => null;
    public virtual DataTemplate? GetCellEditingTemplate(Microsoft.UI.Xaml.Controls.Control anchor) => null;
    public abstract object? Value { get; }
    public abstract bool CanEdit { get; }
    public virtual string? DisplayText => null;
    public virtual TextCellOptions? TextOptions => null;
    public virtual BeginEditGestures EditGestures { get; internal set; } = BeginEditGestures.Default;
    public virtual Exception? Error => null;
    public abstract void Write(object? value);
    public virtual void Dispose() { }
    internal virtual bool TryRetarget(IRow row) => false;
    internal virtual bool TrySuspend() => false;
}

public class ValueCellColumn<TModel, TValue> : CellColumn, ICellColumn<TModel> where TModel : class
{
    private readonly ValueColumn<TModel, TValue> _column;
    private readonly ColumnOptions<TModel> _options;
    private readonly CellKind _kind;
    public ValueCellColumn(ValueColumn<TModel, TValue> column, CellKind kind, TextCellOptions? textOptions = null,
        ColumnOptions<TModel>? viewOptions = null) : base(column)
    {
        _options = viewOptions ?? column.Options;
        if (_options.MinWidth.IsStar || _options.MaxWidth?.IsStar == true)
            throw new ArgumentException("Column minimum and maximum widths must use pixels or Auto.", nameof(column));
        _column = column;
        _kind = kind;
        TextOptions = textOptions;
    }
    public override TextCellOptions? TextOptions { get; }
    public override CellKind Kind => _kind;
    public override bool? CanUserResize => _options.CanUserResizeColumn;
    public override bool? CanUserSort => _options.CanUserSortColumn;
    public override bool IsThreeState => _column is CheckBoxColumn<TModel> check && check.IsThreeState;
    public override double MinimumWidth => _options.MinWidth.IsAuto ? MeasuredWidth : _options.MinWidth.Value;
    public override double MaximumWidth => _options.MaxWidth is { } maximum ? maximum.IsAuto ? MeasuredWidth : maximum.Value : double.PositiveInfinity;
    public override bool RequiresUnconstrainedWidthMeasurement => base.RequiresUnconstrainedWidthMeasurement ||
        _options.MinWidth.IsAuto || _options.MaxWidth?.IsAuto == true;
    internal override bool RecordWidth(double width, int rowIndex = -1)
    {
        if (!double.IsFinite(width) || width < 0) return false;
        if (!_options.MinWidth.IsAuto) width = Math.Max(width, _options.MinWidth.Value);
        if (_options.MaxWidth is { IsAuto: false } maximum) width = Math.Min(width, maximum.Value);
        return base.RecordWidth(width, rowIndex);
    }
    public override bool IsTextSearchEnabled => base.IsTextSearchEnabled || TextOptions?.IsTextSearchEnabled == true;
    public override string? GetSearchText(object? model) => TextSearchValueSelector is not null
        ? base.GetSearchText(model) : model is TModel typed ? FormatValue(_column.GetValue(typed)) : null;
    public override CellValue CreateCell(IRow row)
    {
        CellValue cell = Kind == CellKind.Text
            ? new TextBoundCell<TModel, TValue>(_column, row, TextOptions)
            : new BoundCell<TModel, TValue>(_column, row, canPool: true, TextOptions?.Culture);
        cell.EditGestures = Kind == CellKind.CheckBox ? BeginEditGestures.None : EditGestures;
        cell.Kind = Kind;
        return cell;
    }
    ICell ICellColumn<TModel>.CreateCell(IRow<TModel> row) => CreateCell(row);
    public bool TryReuseCell(ICell cell, IRow<TModel> row) => cell is BoundCell<TModel, TValue> bound &&
        bound.UsesColumn(_column) && bound.TryRetarget(row);
}

internal sealed class TextBoundCell<TModel, TValue> : BoundCell<TModel, TValue>, Uno.Controls.Models.TreeDataGrid.ITextCell where TModel : class
{
    private readonly TextCellOptions? _options;
    public override TextCellOptions? TextOptions => _options;
    public TextBoundCell(ValueColumn<TModel, TValue> column, IRow row, TextCellOptions? options)
        : base(column, row, canPool: true, options?.Culture) => _options = options;
    public string? Text
    {
        get => _options is { } options ? CellTextFormatting.Format(options.Culture, options.StringFormat, Value) : Value?.ToString();
        set => Write(value);
    }
    public TextTrimming TextTrimming => _options?.TextTrimming ?? TextTrimming.CharacterEllipsis;
    public TextWrapping TextWrapping => _options?.TextWrapping ?? TextWrapping.NoWrap;
    public TextAlignment TextAlignment => _options?.TextAlignment ?? TextAlignment.Left;
}

internal sealed class ExpanderCellColumn<TModel> : CellColumn where TModel : class
{
    private readonly HierarchicalExpanderColumn<TModel> _model;
    private readonly CellColumn _inner;
    private bool _disposed;
    public ExpanderCellColumn(HierarchicalExpanderColumn<TModel> model, CellColumn inner) : base(model)
    {
        _model = model;
        _inner = inner;
        HeaderTemplate = inner.HeaderTemplate;
        HeaderTemplateSelector = inner.HeaderTemplateSelector;
        EditGestures = inner.EditGestures;
    }
    public override CellKind Kind => CellKind.Expander;
    public override object? Header { get => _inner.Header; set => _inner.Header = value; }
    public override CellKind ContentKind => _inner.ContentKind;
    public override bool IsThreeState => _inner.IsThreeState;
    public override bool? CanUserResize => _inner.CanUserResize;
    public override bool? CanUserSort => _inner.CanUserSort;
    public override TextCellOptions? TextOptions => _inner.TextOptions;
    public override bool IsTextSearchEnabled => _inner.IsTextSearchEnabled;
    public override string? GetSearchText(object? model) => _inner.GetSearchText(model);
    public override string FormatValue(object? value) => _inner.FormatValue(value);
    public override DataTemplate? GetCellTemplate(Microsoft.UI.Xaml.Controls.Control anchor) => _inner.GetCellTemplate(anchor);
    public override DataTemplate? GetCellEditingTemplate(Microsoft.UI.Xaml.Controls.Control anchor) => _inner.GetCellEditingTemplate(anchor);
    public CellColumn Inner => _inner;
    internal override CellColumn InnerColumn => _inner;
    public override double MinimumWidth => _inner.MinimumWidth;
    public override double MaximumWidth => _inner.MaximumWidth;
    public override bool RequiresUnconstrainedWidthMeasurement => _inner.RequiresUnconstrainedWidthMeasurement;
    internal override bool RecordWidth(double width, int rowIndex = -1) => _inner.RecordWidth(width, rowIndex) | base.RecordWidth(width, rowIndex);
    internal override void ResetWidthMeasurement() { _inner.ResetWidthMeasurement(); base.ResetWidthMeasurement(); }
    internal override bool SetActualWidth(double width) => _inner.SetActualWidth(width) | base.SetActualWidth(width);
    internal override void ModelChanged(PropertyChangedEventArgs e) { _inner.ModelChanged(e); base.ModelChanged(e); }
    public override CellValue CreateCell(IRow row)
    {
        var expanderRow = (IExpanderRow<TModel>)row;
        var inner = _inner.CreateCell(row);
        _inner.ConfigureCell(inner);
        try { return new ExpanderCellValue<TModel>(_model, inner, expanderRow); }
        catch { inner.Dispose(); throw; }
    }
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _inner.Dispose();
    }
}

public abstract class ExpanderCellValue : CellValue, Uno.Controls.Models.TreeDataGrid.IExpanderCell
{
    public abstract CellValue Inner { get; }
    internal virtual bool HasContent => true;
    public virtual object? Content => Inner;
    public abstract IRow Row { get; }
    public override CellKind ContentKind => Inner.ContentKind;
    public override bool CanWrite => Inner.CanWrite;
    public override bool? IsThreeState => Inner.IsThreeState;
    public override object? EditTarget => Inner.EditTarget;
    public abstract bool IsExpanded { get; set; }
    public abstract bool ShowExpander { get; }
}

internal sealed class ExpanderCellValue<TModel> : ExpanderCellValue where TModel : class
{
    private readonly HierarchicalExpanderColumn<TModel> _column;
    private readonly IExpanderRow<TModel> _row;
    private readonly CellBinding<TModel, bool>? _hasChildren;
    private readonly IDisposable? _nativeHasChildren;
    private INotifyCollectionChanged? _children;
    private bool _disposed;
    public ExpanderCellValue(HierarchicalExpanderColumn<TModel> column, CellValue inner, IExpanderRow<TModel> row)
    {
        _column = column;
        _row = row;
        Inner = inner;
        Kind = CellKind.Expander;
        EditGestures = inner.EditGestures;
        try
        {
            if (column.HasChildrenSelector is { } selector)
            {
                _hasChildren = new(new ValueColumn<TModel, bool>("Has children", selector), Changed);
                _hasChildren.Retarget(row.Model);
            }
            if (column is INativeExpanderBindings<TModel> native)
                _nativeHasChildren = native.SubscribeToHasChildren(row.Model, () =>
                {
                    if (_disposed) return;
                    _row.UpdateShowExpander(_column.HasChildren(_row.Model));
                    Changed();
                });
            _row.PropertyChanged += OnRowChanged;
            if (row.Model is INotifyPropertyChanged model) model.PropertyChanged += OnModelChanged;
            Inner.PropertyChanged += OnInnerChanged;
            SubscribeChildren();
        }
        catch { ReleaseSubscriptions(); throw; }
    }
    public override CellValue Inner { get; }
    public override IRow Row => _row;
    public override object? Value => Inner.Value;
    public override string? DisplayText => Inner.DisplayText;
    public override TextCellOptions? TextOptions => Inner.TextOptions;
    public override bool CanEdit => Inner.CanEdit;
    public override Exception? Error => Inner.Error;
    public override void Write(object? value) => Inner.Write(value);
    public override bool IsExpanded { get => _row.IsExpanded; set => _row.IsExpanded = value; }
    public override bool ShowExpander => _row.ShowExpander &&
        (_hasChildren is null || (_hasChildren.Error is null && _hasChildren.Value));
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ReleaseSubscriptions();
        Inner.Dispose();
    }
    private void ReleaseSubscriptions()
    {
        _hasChildren?.Dispose();
        _nativeHasChildren?.Dispose();
        _row.PropertyChanged -= OnRowChanged;
        if (_row.Model is INotifyPropertyChanged model) model.PropertyChanged -= OnModelChanged;
        Inner.PropertyChanged -= OnInnerChanged;
        if (_children is not null) _children.CollectionChanged -= OnChildrenChanged;
        _children = null;
    }
    private void OnInnerChanged(object? sender, PropertyChangedEventArgs e) => RaisePropertyChanged(e);
    private void OnRowChanged(object? sender, PropertyChangedEventArgs e) => RaisePropertyChanged(e);
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) { SubscribeChildren(); Changed(); }
    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e) => Changed();
    private void Changed() => RaisePropertyChanged(nameof(ShowExpander));
    private void SubscribeChildren()
    {
        // An explicit HasChildren binding is the model's lazy-expansion contract.
        // Do not invoke a potentially expensive/unsupported child getter merely
        // to render an expander; its binding already tracks its dependencies.
        var next = _hasChildren is null && _column is not INativeExpanderBindings<TModel> { HasChildrenBinding: true }
            ? _column.GetChildModels(_row.Model) as INotifyCollectionChanged : null;
        if (ReferenceEquals(next, _children)) return;
        if (_children is not null) _children.CollectionChanged -= OnChildrenChanged;
        _children = next;
        if (_children is not null) _children.CollectionChanged += OnChildrenChanged;
    }
}
