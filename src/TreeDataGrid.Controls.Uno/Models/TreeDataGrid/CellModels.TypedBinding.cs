using System;
using Uno.Data;

namespace Uno.Controls.Models.TreeDataGrid;

public partial class TextCell<T>
{
    private int _typedReceiveRevision;

    /// <summary>Observes a typed binding value without introducing an Rx dependency or a second cell state.</summary>
    public TextCell(IObservable<BindingValue<T>> binding, bool isReadOnly, ITextCellOptions? options = null)
        : this(binding, binding as IObserver<BindingValue<T>>, isReadOnly, options) { }

    public TextCell(IObservable<BindingValue<T>> binding, IObserver<BindingValue<T>>? writer,
        bool isReadOnly, ITextCellOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (!isReadOnly && writer is null)
            throw new ArgumentException("An editable binding requires an observer for writeback.", nameof(writer));
        _writer = writer is null ? null : new BindingCellWriter<T>(writer);
        _options = options;
        IsReadOnly = isReadOnly;
        var subscription = binding.Subscribe(new TypedObserver(this));
        if (_disposed) subscription.Dispose(); else _subscription = subscription;
    }

    private sealed class TypedObserver(TextCell<T> owner) : IObserver<BindingValue<T>>
    {
        public void OnNext(BindingValue<T> value)
        {
            if (owner._disposed) return;
            var revision = unchecked(++owner._typedReceiveRevision);
            if (value.HasValue) owner.Receive(value.Value);
            // Receive invokes application observers, which may replace this
            // result with a nested publication or dispose the cell entirely.
            if (!owner._disposed && revision == owner._typedReceiveRevision && value.HasError)
                owner.ErrorReceived(value.Error!);
        }
        public void OnError(Exception error) => owner.ErrorReceived(error);
        public void OnCompleted() { }
    }
}

public partial class CheckBoxCell
{
    private int _typedReceiveRevision;

    public CheckBoxCell(IObservable<BindingValue<bool?>> binding, bool isReadOnly, bool isThreeState)
        : this(binding, binding as IObserver<BindingValue<bool?>>, isReadOnly, isThreeState) { }

    public CheckBoxCell(IObservable<BindingValue<bool?>> binding, IObserver<BindingValue<bool?>>? writer,
        bool isReadOnly, bool isThreeState)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (!isReadOnly && writer is null)
            throw new ArgumentException("An editable binding requires an observer for writeback.", nameof(writer));
        _writer = writer is null ? null : new BindingCellWriter<bool?>(writer);
        IsReadOnly = isReadOnly;
        IsThreeState = isThreeState;
        var subscription = binding.Subscribe(new TypedObserver(this));
        if (_disposed) subscription.Dispose(); else _subscription = subscription;
    }

    private sealed class TypedObserver(CheckBoxCell owner) : IObserver<BindingValue<bool?>>
    {
        public void OnNext(BindingValue<bool?> value)
        {
            if (owner._disposed) return;
            var revision = unchecked(++owner._typedReceiveRevision);
            if (value.HasValue) owner.Receive(value.Value);
            if (!owner._disposed && revision == owner._typedReceiveRevision && value.HasError)
                owner.ErrorReceived(value.Error!);
        }
        public void OnError(Exception error) => owner.ErrorReceived(error);
        public void OnCompleted() { }
    }
}

// Only owns the adaptation, not the source/subject. Its normal write path passes
// the BindingValue struct without boxing or allocating a wrapper per assignment.
internal sealed class BindingCellWriter<T>(IObserver<BindingValue<T>> writer) : IObserver<T>
{
    public void OnNext(T value) => writer.OnNext(value);
    public void OnError(Exception error) => writer.OnError(error);
    public void OnCompleted() => writer.OnCompleted();
}
