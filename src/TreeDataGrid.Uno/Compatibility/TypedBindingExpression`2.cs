using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Subjects;
using Avalonia.Data;
using Avalonia.Utilities;

namespace Avalonia.Experimental.Data.Core
{
    public class TypedBindingExpression<TIn, TOut> : LightweightObservableBase<BindingValue<TOut>>,
        ISubject<BindingValue<TOut>>
        where TIn : class
    {
        private readonly TIn? _source;
        private readonly Func<TIn, TOut> _read;
        private readonly Action<TIn, TOut>? _write;
        private readonly Func<TIn, object>[] _links;
        private readonly Optional<TOut> _fallbackValue;
        private readonly List<IDisposable> _subscriptions = new();
        private int _publishCount;

        public TypedBindingExpression(
            TIn? source,
            Func<TIn, TOut> read,
            Action<TIn, TOut>? write,
            Func<TIn, object>[] links,
            Optional<TOut> fallbackValue)
        {
            _source = source;
            _read = read;
            _write = write;
            _links = links;
            _fallbackValue = fallbackValue;
        }

        public void OnNext(BindingValue<TOut> value)
        {
            if (value.HasValue && _write is not null && _source is not null)
            {
                var publishCount = _publishCount;
                _write(_source, value.Value);

                if (_publishCount == publishCount)
                    PublishValue();
            }
        }

        void IObserver<BindingValue<TOut>>.OnCompleted()
        {
        }

        void IObserver<BindingValue<TOut>>.OnError(Exception error)
        {
        }

        protected override void Initialize()
        {
            Resubscribe();
            PublishValue();
        }

        protected override void Deinitialize()
        {
            ClearSubscriptions();
        }

        protected override void Subscribed(IObserver<BindingValue<TOut>> observer, bool first)
        {
            if (!first)
            {
                var result = GetResult();
                if (result.HasValue)
                    observer.OnNext(result);
            }
        }

        private BindingValue<TOut> GetResult()
        {
            if (_source is null)
                return BindingValue<TOut>.BindingError(new NullReferenceException(), _fallbackValue);

            try
            {
                return new BindingValue<TOut>(_read(_source));
            }
            catch (Exception e)
            {
                return BindingValue<TOut>.BindingError(e, _fallbackValue);
            }
        }

        private void PublishValue()
        {
            unchecked { ++_publishCount; }
            PublishNext(GetResult());
        }

        private void Resubscribe()
        {
            ClearSubscriptions();

            if (_source is null)
                return;

            SubscribeToChanges(_source);

            foreach (var link in _links)
            {
                object? target = null;

                try
                {
                    target = link(_source);
                }
                catch
                {
                    break;
                }

                if (target is not null)
                    SubscribeToChanges(target);
            }
        }

        private void SubscribeToChanges(object source)
        {
            if (source is INotifyPropertyChanged inpc)
            {
                PropertyChangedEventHandler handler = (_, _) =>
                {
                    Resubscribe();
                    PublishValue();
                };
                inpc.PropertyChanged += handler;
                _subscriptions.Add(Disposable.Create(() => inpc.PropertyChanged -= handler));
            }

            if (source is INotifyCollectionChanged incc)
            {
                NotifyCollectionChangedEventHandler handler = (_, _) =>
                {
                    Resubscribe();
                    PublishValue();
                };
                incc.CollectionChanged += handler;
                _subscriptions.Add(Disposable.Create(() => incc.CollectionChanged -= handler));
            }
        }

        private void ClearSubscriptions()
        {
            foreach (var subscription in _subscriptions)
                subscription.Dispose();

            _subscriptions.Clear();
        }
    }
}
