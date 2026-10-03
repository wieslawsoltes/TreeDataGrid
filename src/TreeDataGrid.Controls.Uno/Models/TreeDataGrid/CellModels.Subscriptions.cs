using System;
using Uno.Data;

namespace Uno.Controls.Models.TreeDataGrid;

public partial class TextCell<T>
{
    // Separate concrete observer routes avoid another generic method instantiation
    // on the constructor path. Neither observer retains a subscription handle:
    // the cell captures it only after synchronous publication has unwound.
    private void AttachSubscription(IObservable<T> binding, RawObserver observer)
    {
        IDisposable? subscription;
        try { subscription = binding.Subscribe(observer); }
        catch
        {
            // A source that throws without returning its lease owns rollback of
            // that registration. Retire its escaped observer's cell immediately.
            Dispose();
            throw;
        }
        if (_disposed) subscription?.Dispose();
        else _subscription = subscription;
    }

    private void AttachSubscription(IObservable<BindingValue<T>> binding, TypedObserver observer)
    {
        IDisposable? subscription;
        try { subscription = binding.Subscribe(observer); }
        catch { Dispose(); throw; }
        if (_disposed) subscription?.Dispose();
        else _subscription = subscription;
    }

    // One owner reference replaces the raw wrapper's two instance delegates.
    private sealed class RawObserver(TextCell<T> owner) : IObserver<T>
    {
        public void OnNext(T value) => owner.Receive(value);
        public void OnError(Exception error) => owner.ErrorReceived(error);
        public void OnCompleted() { }
    }
}

public partial class CheckBoxCell
{
    private void AttachSubscription(IObservable<bool?> binding, RawObserver observer)
    {
        IDisposable? subscription;
        try { subscription = binding.Subscribe(observer); }
        catch { Dispose(); throw; }
        if (_disposed) subscription?.Dispose();
        else _subscription = subscription;
    }

    private void AttachSubscription(IObservable<BindingValue<bool?>> binding, TypedObserver observer)
    {
        IDisposable? subscription;
        try { subscription = binding.Subscribe(observer); }
        catch { Dispose(); throw; }
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
