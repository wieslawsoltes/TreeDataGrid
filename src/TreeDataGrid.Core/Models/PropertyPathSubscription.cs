using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace TreeDataGridCore.Models
{
    /// <summary>
    /// Parses a nested member-access expression once and creates lightweight row subscriptions.
    /// </summary>
    internal sealed class PropertyPathSubscriptionFactory<TSource>
    {
        private readonly MemberInfo[] _members;

        private PropertyPathSubscriptionFactory(MemberInfo[] members)
        {
            _members = members;
        }

        public static PropertyPathSubscriptionFactory<TSource>? TryCreate(
            Expression<Func<TSource, bool>> expression)
        {
            var members = GetMemberPath(expression);
            return members is { Length: > 1 } ? new PropertyPathSubscriptionFactory<TSource>(members) : null;
        }

        public IDisposable Subscribe(TSource source, Action changed) =>
            new PropertyPathSubscription<TSource>(source, _members, changed);

        private static MemberInfo[]? GetMemberPath(Expression<Func<TSource, bool>> expression)
        {
            var result = new List<MemberInfo>();
            Expression? current = StripConvert(expression.Body);

            while (current is MemberExpression member)
            {
                result.Add(member.Member);
                current = StripConvert(member.Expression);
            }

            if (!ReferenceEquals(current, expression.Parameters[0]) || result.Count == 0)
                return null;

            result.Reverse();
            return result.ToArray();
        }

        private static Expression? StripConvert(Expression? expression)
        {
            while (expression is UnaryExpression unary &&
                (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            {
                expression = unary.Operand;
            }

            return expression;
        }
    }

    /// <summary>
    /// Tracks notifying objects along a cached member path and rebuilds only the affected tail.
    /// </summary>
    internal sealed class PropertyPathSubscription<TSource> : IDisposable
    {
        private readonly Action _changed;
        private readonly PropertyChangedEventHandler _handler;
        private readonly MemberInfo[] _members;
        private readonly TSource _source;
        private readonly object?[] _values;
        private bool _disposed;

        internal PropertyPathSubscription(TSource source, MemberInfo[] members, Action changed)
        {
            _source = source;
            _members = members;
            _changed = changed;
            _handler = OnPropertyChanged;
            _values = new object?[members.Length];
            Rebuild(0);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Unsubscribe(0);
        }

        private static object? GetValue(MemberInfo member, object owner) => member switch
        {
            PropertyInfo property => property.GetValue(owner),
            FieldInfo field => field.GetValue(owner),
            _ => throw new NotSupportedException($"Member '{member.Name}' is not a field or property."),
        };

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_disposed)
                return;

            for (var i = 0; i < _members.Length; ++i)
            {
                var owner = i == 0 ? _source : _values[i - 1];
                if (ReferenceEquals(owner, sender) &&
                    (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == _members[i].Name))
                {
                    Rebuild(i);
                    _changed();
                    return;
                }
            }
        }

        private void Rebuild(int startIndex)
        {
            Unsubscribe(startIndex);
            object? owner = startIndex == 0 ? _source : _values[startIndex - 1];

            for (var i = startIndex; i < _members.Length; ++i)
            {
                if (owner is INotifyPropertyChanged notifier)
                {
                    notifier.PropertyChanged += _handler;
                }

                owner = owner is null ? null : GetValue(_members[i], owner);
                _values[i] = owner;
            }
        }

        private void Unsubscribe(int startIndex)
        {
            for (var i = _members.Length - 1; i >= startIndex; --i)
            {
                var owner = i == 0 ? _source : _values[i - 1];
                if (owner is INotifyPropertyChanged notifier)
                    notifier.PropertyChanged -= _handler;

                _values[i] = null;
            }
        }
    }
}
