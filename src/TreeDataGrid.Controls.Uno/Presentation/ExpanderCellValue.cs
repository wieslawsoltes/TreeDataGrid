using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using TreeDataGridCore.Models;

namespace Uno.Controls.Presentation;

/// <summary>Owns one inner value and subscriptions, never the Core row or child collection.</summary>
internal sealed class ExpanderCellValue<TModel> : ExpanderCellValue where TModel : class
{
    private static readonly PropertyChangedEventArgs ShowExpanderChanged = new(nameof(ShowExpander));
    private static readonly ConditionalWeakTable<HierarchicalExpanderColumn<TModel>, ValueColumn<TModel, bool>> Definitions = new();
    private readonly HierarchicalExpanderColumn<TModel> _column;
    private readonly IExpanderRow<TModel> _row;
    private TModel? _model;
    private CellBinding<TModel, bool>? _hasChildren;
    private IDisposable? _nativeHasChildren;
    private INotifyCollectionChanged? _children;
    private bool _observingRow;
    private bool _observingModel;
    private bool _observingInner;
    private bool _disposed;
    private bool _initializing = true;
    private bool _cleanupStarted;
    private bool _changingChildren;
    private bool _childrenAgain;
    private int _childrenRevision;
    private int _nativeRevision;

    internal static ValueColumn<TModel, bool>? GetHasChildrenDefinition(HierarchicalExpanderColumn<TModel> column) =>
        column.HasChildrenSelector is null ? null : Definitions.GetValue(column,
            static definition => new ValueColumn<TModel, bool>("Has children", definition.HasChildrenSelector!));

    public ExpanderCellValue(HierarchicalExpanderColumn<TModel> column, CellValue inner, IExpanderRow<TModel> row)
    {
        _column = column ?? throw new ArgumentNullException(nameof(column));
        _row = row ?? throw new ArgumentNullException(nameof(row));
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        Kind = CellKind.Expander;
        Exception? failure = null;
        try
        {
            EditGestures = inner.EditGestures;
            if (_disposed) return;
            _model = row.Model;
            if (_disposed) return;
            if (GetHasChildrenDefinition(column) is { } definition)
            {
                _hasChildren = new(definition, Changed);
                _hasChildren.Retarget(_model);
                if (_disposed) return;
            }
            if (column is INativeExpanderBindings<TModel> native)
            {
                _nativeHasChildren = native.SubscribeToHasChildren(_model, OnNativeHasChildren);
                if (_disposed) return;
            }
            // Set ownership before calling event accessors: an accessor may
            // attach and then throw, or dispose us before finishing its add.
            _observingRow = true;
            _row.PropertyChanged += OnRowChanged;
            if (_disposed) return;
            if (_model is INotifyPropertyChanged model)
            {
                _observingModel = true;
                model.PropertyChanged += OnModelChanged;
                if (_disposed) return;
            }
            _observingInner = true;
            Inner.PropertyChanged += OnInnerChanged;
            if (!_disposed) SubscribeChildren();
        }
        catch (Exception error)
        {
            failure = error;
            _disposed = true;
            throw;
        }
        finally
        {
            _initializing = false;
            if (_disposed)
            {
                try { ReleaseAll(); }
                catch (Exception cleanup) when (failure is not null)
                { throw new AggregateException(failure, cleanup); }
            }
        }
    }

    public override CellValue Inner { get; }
    public override IRow Row => _row;
    public override object? Value => Inner.Value;
    public override string? DisplayText => Inner.DisplayText;
    public override TextCellOptions? TextOptions => Inner.TextOptions;
    public override bool CanEdit => !_disposed && Inner.CanEdit;
    public override bool CanWrite => !_disposed && Inner.CanWrite;
    public override Exception? Error => Inner.Error;
    public override void Write(object? value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Inner.Write(value);
    }
    public override bool IsExpanded
    {
        get => _row.IsExpanded;
        set { ObjectDisposedException.ThrowIf(_disposed, this); _row.IsExpanded = value; }
    }
    public override bool ShowExpander => !_disposed && _row.ShowExpander &&
        (_hasChildren is null || (_hasChildren.Error is null && _hasChildren.Value));

    public override void Dispose()
    {
        _disposed = true;
        // Event add accessors and source getters must unwind before removal.
        // Their returning work can otherwise reattach a retired observer.
        if (!_initializing && !_changingChildren) ReleaseAll();
    }

    private void ReleaseAll()
    {
        if (_cleanupStarted) return;
        _cleanupStarted = true;
        List<Exception>? errors = null;
        var hasChildren = _hasChildren;
        _hasChildren = null;
        try { hasChildren?.Dispose(); }
        catch (Exception error) { (errors ??= new()).Add(error); }
        var native = _nativeHasChildren;
        _nativeHasChildren = null;
        try { native?.Dispose(); }
        catch (Exception error) { (errors ??= new()).Add(error); }
        if (_observingRow)
        {
            _observingRow = false;
            try { _row.PropertyChanged -= OnRowChanged; }
            catch (Exception error) { (errors ??= new()).Add(error); }
        }
        var model = _model;
        _model = null;
        if (_observingModel)
        {
            _observingModel = false;
            try { if (model is INotifyPropertyChanged notifying) notifying.PropertyChanged -= OnModelChanged; }
            catch (Exception error) { (errors ??= new()).Add(error); }
        }
        if (_observingInner)
        {
            _observingInner = false;
            try { Inner.PropertyChanged -= OnInnerChanged; }
            catch (Exception error) { (errors ??= new()).Add(error); }
        }
        var children = _children;
        _children = null;
        try { if (children is not null) children.CollectionChanged -= OnChildrenChanged; }
        catch (Exception error) { (errors ??= new()).Add(error); }
        try { Inner.Dispose(); }
        catch (Exception error) { (errors ??= new()).Add(error); }
        if (errors is { Count: 1 }) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is not null) throw new AggregateException(errors);
    }

    private void OnInnerChanged(object? sender, PropertyChangedEventArgs e) { if (!_disposed) RaisePropertyChanged(e); }
    private void OnRowChanged(object? sender, PropertyChangedEventArgs e) { if (!_disposed) RaisePropertyChanged(e); }
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed) return;
        unchecked { ++_childrenRevision; }
        SubscribeChildren();
        Changed();
    }
    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _children)) Changed();
    }
    private void Changed() { if (!_disposed) RaisePropertyChanged(ShowExpanderChanged); }
    private void OnNativeHasChildren()
    {
        if (_disposed || _model is not { } model) return;
        var revision = unchecked(++_nativeRevision);
        var value = _column.HasChildren(model);
        if (_disposed || revision != _nativeRevision) return;
        _row.UpdateShowExpander(value);
        if (!_disposed && revision == _nativeRevision) Changed();
    }

    private void SubscribeChildren()
    {
        if (_disposed) return;
        if (_changingChildren) { _childrenAgain = true; return; }
        _changingChildren = true;
        Exception? failure = null;
        try
        {
            do
            {
                _childrenAgain = false;
                var revision = _childrenRevision;
                // Explicit HasChildren bindings are the lazy-expansion contract.
                // They must never force enumeration of the actual children.
                var next = _hasChildren is null && _column is not INativeExpanderBindings<TModel> { HasChildrenBinding: true }
                    ? _column.GetChildModels(_model!) as INotifyCollectionChanged : null;
                if (_disposed) return;
                if (revision != _childrenRevision) continue;
                if (ReferenceEquals(next, _children)) continue;
                var previous = _children;
                _children = null;
                if (previous is not null) previous.CollectionChanged -= OnChildrenChanged;
                if (_disposed) return;
                if (revision != _childrenRevision) continue;
                _children = next;
                if (next is not null) next.CollectionChanged += OnChildrenChanged;
                if (_disposed) return;
            }
            while (_childrenAgain);
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            _changingChildren = false;
            if (_disposed && !_initializing)
            {
                try { ReleaseAll(); }
                catch (Exception cleanup) when (failure is not null)
                { throw new AggregateException(failure, cleanup); }
            }
        }
    }
}
