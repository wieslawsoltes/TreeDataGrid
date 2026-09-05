using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls.Experimental.Data.Core;
using Avalonia.Data;
using Avalonia.Experimental.Data;
using Avalonia.Experimental.Data.Core;

namespace Avalonia.Controls.Models.TreeDataGrid
{
    internal class ShowExpanderObservable<TModel> : SingleSubscriberObservableBase<bool>,
        IObserver<BindingValue<bool>>,
        IObserver<BindingValue<IEnumerable<TModel>?>>
            where TModel : class
    {
        private readonly Func<TModel, IEnumerable<TModel>?> _childSelector;
        private readonly TypedBinding<TModel, bool>? _hasChildrenSelector;
        private TModel? _model;
        private IDisposable? _subscription;
        private INotifyCollectionChanged? _incc;
        private INotifyPropertyChanged? _inpc;

        public ShowExpanderObservable(
            Func<TModel, IEnumerable<TModel>?> childSelector,
            TypedBinding<TModel, bool>? hasChildrenSelector,
            TModel model)
        {
            _childSelector = childSelector;
            _hasChildrenSelector = hasChildrenSelector;
            _model = model;
        }

        protected override void Subscribed()
        {
            if (_model is null)
                throw new ObjectDisposedException(nameof(ShowExpanderObservable<TModel>));

            if (_hasChildrenSelector is not null)
                _subscription = _hasChildrenSelector?.Instance(_model).Subscribe(this);
            else
            {
                if (_model is INotifyPropertyChanged inpc)
                {
                    _inpc = inpc;
                    _inpc.PropertyChanged += OnModelPropertyChanged;
                }

                PublishChildren();
            }
        }

        protected override void Unsubscribed()
        {
            _subscription?.Dispose();
            _subscription = null;
            if (_incc is not null)
                _incc.CollectionChanged -= OnCollectionChanged;
            _incc = null;
            if (_inpc is not null)
                _inpc.PropertyChanged -= OnModelPropertyChanged;
            _inpc = null;
            _model = null;
        }

        void IObserver<BindingValue<bool>>.OnNext(BindingValue<bool> value)
        {
            if (value.HasValue)
                PublishNext(value.Value);
        }

        void IObserver<BindingValue<IEnumerable<TModel>?>>.OnNext(BindingValue<IEnumerable<TModel>?> value)
        {
            if (_incc is not null)
                _incc.CollectionChanged -= OnCollectionChanged;
            _incc = null;

            if (value.HasValue && value.Value is not null)
            {
                if (value.Value is INotifyCollectionChanged incc)
                {
                    _incc = incc;
                    _incc.CollectionChanged += OnCollectionChanged;
                }

                PublishNext(value.Value.Any());
            }
            else
            {
                PublishNext(false);
            }
        }

        void IObserver<BindingValue<bool>>.OnCompleted() { }
        void IObserver<BindingValue<IEnumerable<TModel>?>>.OnCompleted() { }
        void IObserver<BindingValue<bool>>.OnError(Exception error) { }
        void IObserver<BindingValue<IEnumerable<TModel>?>>.OnError(Exception error) { }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            PublishNext((sender as IEnumerable<TModel>)?.Any() ?? false);
        }

        private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
            PublishChildren();

        private void PublishChildren()
        {
            if (_model is not null)
            {
                ((IObserver<BindingValue<IEnumerable<TModel>?>>)this).OnNext(
                    new(_childSelector(_model)));
            }
        }
    }
}
