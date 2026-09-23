using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using TreeDataGridCore.Models;

namespace Uno.Controls.Presentation;

/// <summary>
/// Per-cell subscriptions over a Core value column. Compiled owner accessors are
/// shared, while subscriptions and row references belong only to the presentation.
/// </summary>
internal sealed partial class CellBinding<TModel, TValue> : IDisposable where TModel : class
{
    private static readonly ConditionalWeakTable<Expression<Func<TModel, TValue>>, Accessors> s_accessors = new();
    private static readonly Func<TModel, object?>[] s_rootAccessors = [static model => model];
    private readonly ValueColumn<TModel, TValue> _column;
    private readonly Func<TModel, object?>[] _accessors;
    // Keep the always-present root inline. Flat cells need neither a per-cell
    // singleton owner array nor a per-cell singleton delegate-accessor array.
    // Only additional nested owners require storage; no model enters a static cache.
    private object? _rootOwner;
    private readonly object?[] _owners;
    private readonly PropertyChangedEventHandler _propertyChanged;
    private readonly NotifyCollectionChangedEventHandler _collectionChanged;
    private readonly Action _changed;
    private TModel? _model;
    private bool _disposed;
    private bool _refreshing;
    private bool _refreshAgain;
    private int _revision;

    public CellBinding(ValueColumn<TModel, TValue> column, Action changed)
    {
        _column = column ?? throw new ArgumentNullException(nameof(column));
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
        _accessors = column.GetterExpression is { } expression
            ? s_accessors.GetValue(expression, static x => Accessors.Create(x)).Owners
            : s_rootAccessors;
        _owners = _accessors.Length == 1 ? Array.Empty<object?>() : new object?[_accessors.Length - 1];
        _propertyChanged = OnPropertyChanged;
        _collectionChanged = OnCollectionChanged;
    }

    public TValue? Value { get; private set; }
    public Exception? Error { get; private set; }
    public bool CanWrite => _column.Setter is not null;
    internal bool UsesColumn(ValueColumn<TModel, TValue> column) => ReferenceEquals(_column, column);

    public void Retarget(TModel model)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        unchecked { ++_revision; }
        Refresh();
    }

    public void Write(TValue value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var model = _model ?? throw new InvalidOperationException("A suspended cell cannot write a value.");
        var setter = _column.Setter ?? throw new InvalidOperationException("The column is read-only.");
        // A typed assignment also supersedes an older in-flight conversion.
        unchecked { ++_writeRevision; }
        Exception? writeFailure = null;
        try
        {
            setter(model, value);
        }
        catch (Exception error)
        {
            writeFailure = error;
            throw;
        }
        finally
        {
            // Delegated setters need not raise notifications, and failed setters may
            // have changed part of a model. Re-read in either case, but never hide
            // the original write failure behind a second observer/refresh failure.
            try { Refresh(); }
            catch (Exception refreshFailure) when (writeFailure is not null)
            { throw new AggregateException(writeFailure, refreshFailure); }
        }
    }

    public void Suspend()
    {
        _model = null;
        unchecked { ++_revision; }
        Value = default;
        Error = null;
        // Event accessors can retire/retarget this binding while subscriptions
        // are changing. Serialize cleanup with that in-flight operation so a
        // returning add/remove accessor cannot overwrite newer bookkeeping.
        Refresh();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Suspend();
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Expressions can contain computed accessors; conservatively observe the
        // current owners. Delegate columns can specify a single root property.
        if (_column.GetterExpression is not null || _column.PropertyName is null ||
            string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == _column.PropertyName)
            Refresh();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        if (_refreshing)
        {
            _refreshAgain = true;
            return;
        }
        _refreshing = true;
        Exception? failure = null;
        try
        {
            do
            {
                _refreshAgain = false;
                var revision = _revision;
                if (_disposed || _model is not { } model)
                {
                    for (var i = 0; i < _accessors.Length; ++i)
                        SetOwner(i, null);
                    continue;
                }
                for (var i = 0; i < _accessors.Length; ++i)
                {
                    object? owner;
                    try { owner = _accessors[i](model); }
                    catch (Exception) { owner = null; }
                    if (revision != _revision) break;
                    SetOwner(i, owner);
                    if (revision != _revision) break;
                }
                if (revision != _revision) continue;

                // A selector, equality implementation or event accessor is
                // application code. Never publish its result after Suspend,
                // Dispose or Retarget (including retargeting to the same row).
                TValue? value;
                Exception? error;
                try { value = _column.GetValue(model); error = null; }
                catch (Exception caught) { value = default; error = caught; }
                if (revision != _revision) continue;
                // Distinct exceptions are distinct diagnostics even when the
                // type is unchanged. Avoid application-defined equality/message
                // access; reobserving the exact same exception stays quiet.
                var changed = !EqualityComparer<TValue?>.Default.Equals(Value, value) ||
                    !ReferenceEquals(Error, error);
                if (revision != _revision) continue;
                Value = value;
                Error = error;
                if (changed) _changed();
            }
            while (_refreshAgain);
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            _refreshing = false;
            // A change/subscription callback may retire the binding and then
            // throw. Complete the pending cleanup before propagating failure.
            if (_refreshAgain)
            {
                try { Refresh(); }
                catch (Exception cleanup) when (failure is not null)
                { throw new AggregateException(failure, cleanup); }
            }
        }
    }

    private void SetOwner(int index, object? owner)
    {
        var previous = index == 0 ? _rootOwner : _owners[index - 1];
        if (ReferenceEquals(previous, owner)) return;
        if (index == 0) _rootOwner = null;
        else _owners[index - 1] = null;
        if (previous is not null && !Contains(previous))
        {
            if (previous is INotifyPropertyChanged property) property.PropertyChanged -= _propertyChanged;
            if (previous is INotifyCollectionChanged collection) collection.CollectionChanged -= _collectionChanged;
        }
        var subscribe = owner is not null && !Contains(owner);
        if (index == 0) _rootOwner = owner;
        else _owners[index - 1] = owner;
        if (subscribe)
        {
            if (owner is INotifyPropertyChanged property) property.PropertyChanged += _propertyChanged;
            if (owner is INotifyCollectionChanged collection) collection.CollectionChanged += _collectionChanged;
        }
    }

    private bool Contains(object owner)
    {
        if (ReferenceEquals(_rootOwner, owner)) return true;
        foreach (var value in _owners)
            if (ReferenceEquals(value, owner)) return true;
        return false;
    }

    private sealed class Accessors : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;
        private readonly List<Func<TModel, object?>> _owners = [s_rootAccessors[0]];
        private Accessors(ParameterExpression parameter) => _parameter = parameter;
        public Func<TModel, object?>[] Owners { get; private set; } = [];
        public static Accessors Create(Expression<Func<TModel, TValue>> expression)
        {
            var result = new Accessors(expression.Parameters[0]);
            result.Visit(expression.Body);
            result.Owners = result._owners.Count == 1 ? s_rootAccessors : result._owners.ToArray();
            return result;
        }
        protected override Expression VisitMember(MemberExpression node)
        {
            AddOwner(node.Expression);
            return base.VisitMember(node);
        }
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            AddOwner(node.Object);
            return base.VisitMethodCall(node);
        }
        protected override Expression VisitIndex(IndexExpression node)
        {
            AddOwner(node.Object);
            return base.VisitIndex(node);
        }
        protected override Expression VisitLambda<T>(Expression<T> node) => node;
        private void AddOwner(Expression? expression)
        {
            if (expression is null || expression == _parameter || expression.Type.IsValueType) return;
            var body = Expression.Convert(expression, typeof(object));
            // Compile shared owner accessors on JIT hosts instead of allocating an
            // interpreter frame on every nested-cell retarget. Browser/AOT hosts
            // keep the interpreter; no dynamic-code dependency is introduced there.
            _owners.Add(Expression.Lambda<Func<TModel, object?>>(body, _parameter)
                .Compile(preferInterpretation: !RuntimeFeature.IsDynamicCodeCompiled));
        }
    }
}
