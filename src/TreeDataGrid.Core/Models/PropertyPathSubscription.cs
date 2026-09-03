using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace TreeDataGridCore.Models
{
    /// <summary>
    /// Observes the notifying objects along a member-access expression and rebuilds downstream
    /// subscriptions when an intermediate member changes.
    /// </summary>
    internal sealed class PropertyPathSubscription<TSource> : IDisposable
    {
        private readonly Action _changed;
        private readonly PropertyChangedEventHandler[] _handlers;
        private readonly MemberInfo[] _members;
        private readonly INotifyPropertyChanged?[] _notifiers;
        private readonly TSource _source;
        private readonly object?[] _values;
        private bool _disposed;

        private PropertyPathSubscription(TSource source, MemberInfo[] members, Action changed)
        {
            _source = source;
            _members = members;
            _changed = changed;
            _handlers = new PropertyChangedEventHandler[members.Length];
            _notifiers = new INotifyPropertyChanged?[members.Length];
            _values = new object?[members.Length];

            for (var i = 0; i < members.Length; ++i)
            {
                var index = i;
                _handlers[i] = (_, e) => OnPropertyChanged(index, e);
            }

            Rebuild(0);
        }

        public static IDisposable? TryCreate(
            TSource source,
            Expression<Func<TSource, bool>> expression,
            Action changed)
        {
            var members = GetMemberPath(expression);
            return members is null ? null : new PropertyPathSubscription<TSource>(source, members, changed);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Unsubscribe(0);
        }

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

        private static object? GetValue(MemberInfo member, object owner) => member switch
        {
            PropertyInfo property => property.GetValue(owner),
            FieldInfo field => field.GetValue(owner),
            _ => throw new NotSupportedException($"Member '{member.Name}' is not a field or property."),
        };

        private static Expression? StripConvert(Expression? expression)
        {
            while (expression is UnaryExpression unary &&
                (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            {
                expression = unary.Operand;
            }

            return expression;
        }

        private void OnPropertyChanged(int index, PropertyChangedEventArgs e)
        {
            if (_disposed || (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != _members[index].Name))
                return;

            Rebuild(index);
            _changed();
        }

        private void Rebuild(int startIndex)
        {
            Unsubscribe(startIndex);
            object? owner = startIndex == 0 ? _source : _values[startIndex - 1];

            for (var i = startIndex; i < _members.Length; ++i)
            {
                if (owner is INotifyPropertyChanged notifier)
                {
                    _notifiers[i] = notifier;
                    notifier.PropertyChanged += _handlers[i];
                }

                owner = owner is null ? null : GetValue(_members[i], owner);
                _values[i] = owner;
            }
        }

        private void Unsubscribe(int startIndex)
        {
            for (var i = startIndex; i < _notifiers.Length; ++i)
            {
                if (_notifiers[i] is { } notifier)
                {
                    notifier.PropertyChanged -= _handlers[i];
                    _notifiers[i] = null;
                }

                _values[i] = null;
            }
        }
    }
}
