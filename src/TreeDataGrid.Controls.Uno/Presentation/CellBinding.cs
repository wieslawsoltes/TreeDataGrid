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
internal sealed class CellBinding<TModel, TValue> : IDisposable where TModel : class
{
    private static readonly ConditionalWeakTable<Expression<Func<TModel, TValue>>, Accessors> s_accessors = new();
    private readonly ValueColumn<TModel, TValue> _column;
    private readonly Func<TModel, object?>[] _accessors;
    private readonly object?[] _owners;
    private readonly PropertyChangedEventHandler _propertyChanged;
    private readonly NotifyCollectionChangedEventHandler _collectionChanged;
    private readonly Action _changed;
    private TModel? _model;
    private bool _disposed;
    private bool _refreshing;
    private bool _refreshAgain;

    public CellBinding(ValueColumn<TModel, TValue> column, Action changed)
    {
        _column = column ?? throw new ArgumentNullException(nameof(column));
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
        _accessors = column.GetterExpression is { } expression
            ? s_accessors.GetValue(expression, static x => Accessors.Create(x)).Owners
            : [static model => model];
        _owners = new object?[_accessors.Length];
        _propertyChanged = OnPropertyChanged;
        _collectionChanged = OnCollectionChanged;
    }

    public TValue? Value { get; private set; }
    public Exception? Error { get; private set; }
    public bool CanWrite => _column.Setter is not null;

    public void Retarget(TModel model)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        Refresh();
    }

    public void Write(TValue value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_model is null)
            throw new InvalidOperationException("A suspended cell cannot write a value.");
        if (_column.Setter is null)
            throw new InvalidOperationException("The column is read-only.");
        try
        {
            _column.Setter(_model, value);
        }
        finally
        {
            // Delegated setters need not raise notifications, and failed setters may
            // have changed part of a model. Re-read in either case.
            Refresh();
        }
    }

    public void Suspend()
    {
        _model = null;
        Value = default;
        Error = null;
        for (var i = 0; i < _owners.Length; ++i)
            SetOwner(i, null);
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
        if (_disposed || _model is null) return;
        if (_refreshing)
        {
            _refreshAgain = true;
            return;
        }
        _refreshing = true;
        try
        {
            do
            {
                _refreshAgain = false;
                if (_disposed || _model is not { } model) break;
                for (var i = 0; i < _accessors.Length; ++i)
                {
                    object? owner;
                    try { owner = _accessors[i](model); }
                    catch (Exception) { owner = null; }
                    SetOwner(i, owner);
                }
                var oldValue = Value;
                var oldError = Error;
                try { Value = _column.GetValue(model); Error = null; }
                catch (Exception error) { Value = default; Error = error; }
                if (!EqualityComparer<TValue?>.Default.Equals(oldValue, Value) ||
                    oldError?.GetType() != Error?.GetType())
                    _changed();
            }
            while (_refreshAgain);
        }
        finally { _refreshing = false; }
    }

    private void SetOwner(int index, object? owner)
    {
        if (ReferenceEquals(_owners[index], owner)) return;
        var previous = _owners[index];
        _owners[index] = null;
        if (previous is not null && !Contains(previous))
        {
            if (previous is INotifyPropertyChanged property) property.PropertyChanged -= _propertyChanged;
            if (previous is INotifyCollectionChanged collection) collection.CollectionChanged -= _collectionChanged;
        }
        if (owner is not null && !Contains(owner))
        {
            if (owner is INotifyPropertyChanged property) property.PropertyChanged += _propertyChanged;
            if (owner is INotifyCollectionChanged collection) collection.CollectionChanged += _collectionChanged;
        }
        _owners[index] = owner;
    }

    private bool Contains(object owner)
    {
        foreach (var value in _owners)
            if (ReferenceEquals(value, owner)) return true;
        return false;
    }

    private sealed class Accessors : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;
        private readonly List<Func<TModel, object?>> _owners = [static model => model];
        private Accessors(ParameterExpression parameter) => _parameter = parameter;
        public Func<TModel, object?>[] Owners { get; private set; } = [];
        public static Accessors Create(Expression<Func<TModel, TValue>> expression)
        {
            var result = new Accessors(expression.Parameters[0]);
            result.Visit(expression.Body);
            result.Owners = result._owners.ToArray();
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
            _owners.Add(Expression.Lambda<Func<TModel, object?>>(body, _parameter).Compile(preferInterpretation: true));
        }
    }
}
