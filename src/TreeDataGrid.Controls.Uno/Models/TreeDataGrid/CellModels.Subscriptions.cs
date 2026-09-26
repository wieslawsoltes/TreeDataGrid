using System;

namespace Uno.Controls.Models.TreeDataGrid;

public partial class TextCell<T>
{
    // Constructors are the only callers. There is no owned lease until Subscribe
    // returns; publication and synchronous application callbacks may precede it.
    private void AttachSubscription<TValue>(IObservable<TValue> binding, IObserver<TValue> observer)
    {
        IDisposable? subscription;
        try { subscription = binding.Subscribe(observer); }
        catch
        {
            // A throwing publisher can retain the observer without returning a
            // lease. Retire the escaped cell so later callbacks are inert. The
            // publisher remains responsible for its inaccessible registration.
            Dispose();
            throw;
        }
        if (_disposed) subscription?.Dispose();
        else _subscription = subscription;
    }

    // The raw overload needs one owner reference, not a wrapper containing two
    // separately allocated instance delegates. TypedObserver remains unchanged.
    private sealed class RawObserver(TextCell<T> owner) : IObserver<T>
    {
        public void OnNext(T value) => owner.Receive(value);
        public void OnError(Exception error) => owner.ErrorReceived(error);
        public void OnCompleted() { }
    }
}

public partial class CheckBoxCell
{
    private void AttachSubscription<TValue>(IObservable<TValue> binding, IObserver<TValue> observer)
    {
        IDisposable? subscription;
        try { subscription = binding.Subscribe(observer); }
        catch
        {
            Dispose();
            throw;
        }
        if (_disposed) subscription?.Dispose();
        else _subscription = subscription;
    }

    private sealed class RawObserver(CheckBoxCell owner) : IObserver<bool?>
    {
        public void OnNext(bool? value) => owner.Receive(value);
        public void OnError(Exception error) => owner.ErrorReceived(error);
        public void OnCompleted() { }
    }
}
