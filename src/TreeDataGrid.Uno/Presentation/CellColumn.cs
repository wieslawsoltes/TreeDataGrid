using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using TreeDataGridCore.Models;

namespace Uno.Controls.Presentation;

public enum CellKind { Text, CheckBox, Template, Expander }

/// <summary>A view-owned column over a shared Core definition.</summary>
public abstract class CellColumn : IDisposable
{
    protected CellColumn(IColumn model) => Model = model;
    public IColumn Model { get; }
    public virtual double MinimumWidth => 30;
    public virtual double MaximumWidth => double.PositiveInfinity;
    public virtual CellKind Kind => CellKind.Text;
    public virtual CellKind ContentKind => Kind;
    public virtual bool IsThreeState => false;
    public abstract CellValue CreateCell(IRow row);
    public virtual void Dispose() { }
}

/// <summary>A realized view value. The Core source never owns this object.</summary>
public abstract class CellValue : NotifyingBase, IDisposable
{
    public abstract object? Value { get; }
    public abstract bool CanEdit { get; }
    public virtual Exception? Error => null;
    public abstract void Write(object? value);
    public virtual void Dispose() { }
    internal virtual bool TryRetarget(IRow row) => false;
    internal virtual bool TrySuspend() => false;
}

internal sealed class ValueCellColumn<TModel, TValue> : CellColumn where TModel : class
{
    private readonly ValueColumn<TModel, TValue> _column;
    private readonly CellKind _kind;
    public ValueCellColumn(ValueColumn<TModel, TValue> column, CellKind kind) : base(column)
    { _column = column; _kind = kind; }
    public override CellKind Kind => _kind;
    public override bool IsThreeState => _column is CheckBoxColumn<TModel> check && check.IsThreeState;
    public override double MinimumWidth => _column.Options.MinWidth.IsAuto ? 0 : _column.Options.MinWidth.Value;
    public override double MaximumWidth => _column.Options.MaxWidth is { IsAuto: false } maximum ? maximum.Value : double.PositiveInfinity;
    public override CellValue CreateCell(IRow row) => new BoundCell<TModel, TValue>(_column, row, canPool: true);
}

internal sealed class BoundCell<TModel, TValue> : CellValue where TModel : class
{
    private readonly CellBinding<TModel, TValue> _binding;
    private readonly bool _canPool;
    public BoundCell(ValueColumn<TModel, TValue> column, IRow row, bool canPool)
    {
        _canPool = canPool;
        _binding = new(column, Changed);
        try { _binding.Retarget((TModel)row.Model!); }
        catch { _binding.Dispose(); throw; }
    }
    public override object? Value => _binding.Value;
    public override Exception? Error => _binding.Error;
    public override bool CanEdit => _binding.CanWrite;
    public override void Write(object? value)
    {
        var type = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
        var converted = value is null || value is TValue ? value :
            type.IsEnum ? Enum.Parse(type, value.ToString()!) : Convert.ChangeType(value, type, CultureInfo.CurrentCulture);
        _binding.Write((TValue)converted!);
    }
    public override void Dispose() => _binding.Dispose();
    internal override bool TryRetarget(IRow row)
    {
        if (!_canPool || row.Model is not TModel model) return false;
        _binding.Retarget(model);
        return true;
    }
    internal override bool TrySuspend()
    {
        if (!_canPool) return false;
        _binding.Suspend();
        return true;
    }
    private void Changed()
    {
        RaisePropertyChanged(nameof(Value));
        RaisePropertyChanged(nameof(Error));
    }
}

internal sealed class ExpanderCellColumn<TModel> : CellColumn where TModel : class
{
    private readonly HierarchicalExpanderColumn<TModel> _model;
    private readonly CellColumn _inner;
    private bool _disposed;
    public ExpanderCellColumn(HierarchicalExpanderColumn<TModel> model, CellColumn inner) : base(model)
    { _model = model; _inner = inner; }
    public override CellKind Kind => CellKind.Expander;
    public override CellKind ContentKind => _inner.ContentKind;
    public override bool IsThreeState => _inner.IsThreeState;
    public CellColumn Inner => _inner;
    public override double MinimumWidth => _inner.MinimumWidth;
    public override double MaximumWidth => _inner.MaximumWidth;
    public override CellValue CreateCell(IRow row)
    {
        var expanderRow = (IExpanderRow<TModel>)row;
        var inner = _inner.CreateCell(row);
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

public abstract class ExpanderCellValue : CellValue
{
    public abstract CellValue Inner { get; }
    public abstract bool IsExpanded { get; set; }
    public abstract bool ShowExpander { get; }
}

internal sealed class ExpanderCellValue<TModel> : ExpanderCellValue where TModel : class
{
    private readonly HierarchicalExpanderColumn<TModel> _column;
    private readonly IExpanderRow<TModel> _row;
    private readonly CellBinding<TModel, bool>? _hasChildren;
    private INotifyCollectionChanged? _children;
    private bool _disposed;
    public ExpanderCellValue(HierarchicalExpanderColumn<TModel> column, CellValue inner, IExpanderRow<TModel> row)
    {
        _column = column;
        _row = row;
        Inner = inner;
        try
        {
            if (column.HasChildrenSelector is { } selector)
            {
                _hasChildren = new(new ValueColumn<TModel, bool>("Has children", selector), Changed);
                _hasChildren.Retarget(row.Model);
            }
            _row.PropertyChanged += OnRowChanged;
            if (row.Model is INotifyPropertyChanged model) model.PropertyChanged += OnModelChanged;
            Inner.PropertyChanged += OnInnerChanged;
            SubscribeChildren();
        }
        catch { ReleaseSubscriptions(); throw; }
    }
    public override CellValue Inner { get; }
    public override object? Value => Inner.Value;
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
        var next = _column.GetChildModels(_row.Model) as INotifyCollectionChanged;
        if (ReferenceEquals(next, _children)) return;
        if (_children is not null) _children.CollectionChanged -= OnChildrenChanged;
        _children = next;
        if (_children is not null) _children.CollectionChanged += OnChildrenChanged;
    }
}
