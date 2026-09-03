using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;

namespace TreeDataGridCore.Utils
{
    // Compiled once per column. Each row owns only its current subscriptions.
    internal sealed class PropertyPathObserver<TModel> : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;
        private readonly List<(Func<TModel, object?> Owner, string Property)> _paths = new();

        private PropertyPathObserver(Expression<Func<TModel, bool>> expression)
        {
            _parameter = expression.Parameters[0];
            // Preserve root notifications for computed selectors whose dependencies
            // cannot be inferred from member access alone.
            _paths.Add((static model => model, ""));
            Visit(expression.Body);
        }

        public static PropertyPathObserver<TModel>? Create(Expression<Func<TModel, bool>> expression)
        {
            var observer = new PropertyPathObserver<TModel>(expression);
            // Ordinary root bindings use the existing direct row subscription.
            return observer._paths.Count > 1 ? observer : null;
        }

        public IDisposable Subscribe(TModel model, Action changed) => new Subscription(this, model, changed);

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is { } owner && owner != _parameter)
            {
                // A null intermediate object has no notification source. The selector itself
                // still determines its value (and can explicitly handle null).
                var read = Expression.TryCatch(Expression.Convert(owner, typeof(object)),
                    Expression.Catch(typeof(NullReferenceException), Expression.Constant(null, typeof(object))));
                _paths.Add((Expression.Lambda<Func<TModel, object?>>(read, _parameter).Compile(), node.Member.Name));
            }
            return base.VisitMember(node);
        }

        // Parameters inside a nested lambda belong to that lambda, not to the row.
        protected override Expression VisitLambda<T>(Expression<T> node) => node;

        private sealed class Subscription : IDisposable
        {
            private readonly PropertyPathObserver<TModel> _path;
            private readonly TModel _model;
            private readonly Action _changed;
            private readonly PropertyChangedEventHandler _handler;
            private readonly INotifyPropertyChanged?[] _owners;
            private bool _disposed;

            public Subscription(PropertyPathObserver<TModel> path, TModel model, Action changed)
            {
                _path = path;
                _model = model;
                _changed = changed;
                _handler = OnPropertyChanged;
                _owners = new INotifyPropertyChanged?[path._paths.Count];
                Refresh();
            }

            private void Refresh()
            {
                for (var i = 0; i < _owners.Length; ++i)
                {
                    var next = _path._paths[i].Owner(_model) as INotifyPropertyChanged;
                    var old = _owners[i];
                    if (ReferenceEquals(old, next)) continue;
                    _owners[i] = null;
                    if (old is not null && !Contains(old)) old.PropertyChanged -= _handler;
                    if (next is not null && !Contains(next)) next.PropertyChanged += _handler;
                    _owners[i] = next;
                }
            }

            private bool Contains(INotifyPropertyChanged owner)
            {
                foreach (var existing in _owners)
                    if (ReferenceEquals(existing, owner)) return true;
                return false;
            }

            private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (_disposed) return;
                for (var i = 0; i < _owners.Length; ++i)
                {
                    if (ReferenceEquals(sender, _owners[i]) &&
                        (string.IsNullOrEmpty(e.PropertyName) || _path._paths[i].Property.Length == 0 ||
                            e.PropertyName == _path._paths[i].Property))
                    {
                        Refresh();
                        _changed();
                        break;
                    }
                }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                for (var i = 0; i < _owners.Length; ++i)
                {
                    var old = _owners[i];
                    _owners[i] = null;
                    if (old is not null && !Contains(old)) old.PropertyChanged -= _handler;
                }
            }
        }
    }
}

