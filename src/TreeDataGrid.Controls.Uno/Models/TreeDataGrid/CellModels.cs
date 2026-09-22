using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore.Models;

namespace Uno.Controls.Models.TreeDataGrid;

// Native observable bindings carry typed values, not Avalonia BindingValue<T>.
// A separate observer is supported when the observable itself is not a subject.
internal interface IBoundCellState
{
    Exception? Error { get; }
}
internal interface ITextCellState : IBoundCellState
{
    ITextCellOptions? Options { get; }
}

internal sealed class CellObserver<T>(Action<T> next, Action<Exception> error) : IObserver<T>
{
    public void OnNext(T value) => next(value);
    public void OnError(Exception value) => error(value);
    public void OnCompleted() { }
}

public class TextCell<T> : NotifyingBase, ITextCell, IDisposable, IEditableObject, ITextCellState
{
    private readonly IObserver<T>? _writer;
    private readonly ITextCellOptions? _options;
    private IDisposable? _subscription;
    private T? _value;
    private string? _editText;
    private bool _editing;
    private bool _disposed;
    private bool _writeFailed;
    private int _receiving;
    private int _valueRevision;
    private int _writeRevision;
    private int _editRevision;
    public TextCell(T? value) { _value = value; IsReadOnly = true; }
    public TextCell(IObservable<T> binding, bool isReadOnly, ITextCellOptions? options = null)
        : this(binding, binding as IObserver<T>, isReadOnly, options) { }
    public TextCell(IObservable<T> binding, IObserver<T>? writer, bool isReadOnly, ITextCellOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (!isReadOnly && writer is null) throw new ArgumentException("An editable binding requires an observer for writeback.", nameof(writer));
        _writer = writer;
        _options = options;
        IsReadOnly = isReadOnly;
        _subscription = binding.Subscribe(new CellObserver<T>(Receive, ErrorReceived));
    }
    public bool CanEdit => !IsReadOnly;
    public bool IsReadOnly { get; }
    public BeginEditGestures EditGestures => _options?.BeginEditGestures ?? BeginEditGestures.Default;
    public TextTrimming TextTrimming => _options?.TextTrimming ?? TextTrimming.None;
    public TextWrapping TextWrapping => _options?.TextWrapping ?? TextWrapping.NoWrap;
    public TextAlignment TextAlignment => _options?.TextAlignment ?? TextAlignment.Left;
    public Exception? Error { get; private set; }
    ITextCellOptions? ITextCellState.Options => _options;
    public T? Value
    {
        get => _value;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var revision = unchecked(++_valueRevision);
            var previous = _value;
            var changed = !EqualityComparer<T?>.Default.Equals(previous, value);
            if (_disposed || revision != _valueRevision) return;
            if (!changed && !_writeFailed) return;
            if (changed)
            {
                _value = value;
                // Keep the established Value -> Text -> writer notification order.
                // Application callbacks can supersede the pending write.
                RaisePropertyChanged(nameof(Value));
                if (_disposed || revision != _valueRevision) return;
                RaisePropertyChanged(nameof(Text));
                if (_disposed || revision != _valueRevision) return;
            }
            if (IsReadOnly || _editing || _receiving != 0) return;
            var write = unchecked(++_writeRevision);
            _writeFailed = false;
            try { _writer!.OnNext(value!); }
            catch (Exception error)
            {
                // A subject may publish a normalized/accepted value before another
                // observer throws. Retain that newer source value, but retry a
                // rejected equal-valued write unless a later write superseded it.
                if (!_disposed && write == _writeRevision) _writeFailed = true;
                if (!_disposed && revision == _valueRevision && changed)
                {
                    _value = previous;
                    try
                    {
                        RaisePropertyChanged(nameof(Value));
                        if (!_disposed && revision == _valueRevision) RaisePropertyChanged(nameof(Text));
                    }
                    catch (Exception rollback) { throw new AggregateException(error, rollback); }
                }
                throw;
            }
        }
    }
    object? ICell.Value => Value;
    public string? Text
    {
        get => _editing ? _editText : _options?.StringFormat is { } format
            ? string.Format(_options.Culture, format, _value) : _value?.ToString();
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_editing) { _editText = value; return; }
            var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            Value = value is null && (!typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) is not null)
                ? default : (T?)(type.IsEnum ? Enum.Parse(type, value!) : Convert.ChangeType(value, type, _options?.Culture ?? CultureInfo.CurrentCulture));
        }
    }
    public void BeginEdit()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_editing || IsReadOnly) return;
        var edit = unchecked(++_editRevision);
        var text = Convert.ToString(_value, _options?.Culture ?? CultureInfo.CurrentCulture);
        if (_disposed || edit != _editRevision) return;
        _editText = text;
        _editing = true;
    }
    public void CancelEdit()
    {
        unchecked { ++_editRevision; }
        _editing = false;
        _editText = null;
        _writeFailed = false;
    }
    public void EndEdit()
    {
        if (!_editing) return;
        var edit = _editRevision;
        var text = _editText;
        _editing = false;
        try
        {
            Text = text;
            if (!_disposed && edit == _editRevision) _editText = null;
        }
        catch
        {
            if (!_disposed && edit == _editRevision) _editing = true;
            throw;
        }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        unchecked { ++_valueRevision; ++_writeRevision; }
        CancelEdit();
        var subscription = _subscription;
        _subscription = null;
        subscription?.Dispose();
        GC.SuppressFinalize(this);
    }
    private void Receive(T value)
    {
        if (_disposed) return;
        ++_receiving;
        try
        {
            Error = null;
            _writeFailed = false;
            Value = value;
            if (!_disposed) RaisePropertyChanged(nameof(Error));
        }
        finally { --_receiving; }
    }
    private void ErrorReceived(Exception error) { if (!_disposed) { Error = error; RaisePropertyChanged(nameof(Error)); } }
}

public class CheckBoxCell : NotifyingBase, ICell, IDisposable, IBoundCellState
{
    private readonly IObserver<bool?>? _writer;
    private IDisposable? _subscription;
    private bool? _value;
    private bool _disposed;
    private bool _writeFailed;
    private int _receiving;
    private int _valueRevision;
    private int _writeRevision;
    public CheckBoxCell(bool? value) { _value = value; IsReadOnly = true; }
    public CheckBoxCell(IObservable<bool?> binding, bool isReadOnly, bool isThreeState)
        : this(binding, binding as IObserver<bool?>, isReadOnly, isThreeState) { }
    public CheckBoxCell(IObservable<bool?> binding, IObserver<bool?>? writer, bool isReadOnly, bool isThreeState)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (!isReadOnly && writer is null) throw new ArgumentException("An editable binding requires an observer for writeback.", nameof(writer));
        _writer = writer;
        IsReadOnly = isReadOnly;
        IsThreeState = isThreeState;
        _subscription = binding.Subscribe(new CellObserver<bool?>(Receive, ErrorReceived));
    }
    public bool CanEdit => false;
    public bool SingleTapEdit => false;
    public BeginEditGestures EditGestures => BeginEditGestures.None;
    public bool IsReadOnly { get; }
    public bool IsThreeState { get; }
    public Exception? Error { get; private set; }
    public bool? Value
    {
        get => _value;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var revision = unchecked(++_valueRevision);
            var previous = _value;
            var changed = previous != value;
            if (!changed && !_writeFailed) return;
            if (changed)
            {
                _value = value;
                RaisePropertyChanged(nameof(Value));
                if (_disposed || revision != _valueRevision) return;
            }
            if (IsReadOnly || _receiving != 0) return;
            var write = unchecked(++_writeRevision);
            _writeFailed = false;
            try { _writer!.OnNext(value); }
            catch (Exception error)
            {
                if (!_disposed && write == _writeRevision) _writeFailed = true;
                if (!_disposed && revision == _valueRevision && changed)
                {
                    _value = previous;
                    try { RaisePropertyChanged(nameof(Value)); }
                    catch (Exception rollback) { throw new AggregateException(error, rollback); }
                }
                throw;
            }
        }
    }
    object? ICell.Value => Value;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        unchecked { ++_valueRevision; ++_writeRevision; }
        _writeFailed = false;
        var subscription = _subscription;
        _subscription = null;
        subscription?.Dispose();
        GC.SuppressFinalize(this);
    }
    private void Receive(bool? value)
    {
        if (_disposed) return;
        ++_receiving;
        try
        {
            Error = null;
            _writeFailed = false;
            Value = value;
            if (!_disposed) RaisePropertyChanged(nameof(Error));
        }
        finally { --_receiving; }
    }
    private void ErrorReceived(Exception error) { if (!_disposed) { Error = error; RaisePropertyChanged(nameof(Error)); } }
}

public class TemplateCell : ICell, IEditableObject
{
    private readonly ITemplateCellOptions? _options;
    public TemplateCell(object? value, Func<Control, DataTemplate> getCellTemplate,
        Func<Control, DataTemplate>? getCellEditingTemplate, ITemplateCellOptions? options)
    {
        Value = value;
        GetCellTemplate = getCellTemplate ?? throw new ArgumentNullException(nameof(getCellTemplate));
        GetCellEditingTemplate = getCellEditingTemplate;
        _options = options;
    }
    public object? Value { get; private set; }
    public bool CanEdit => GetCellEditingTemplate is not null;
    public BeginEditGestures EditGestures => _options?.BeginEditGestures ?? BeginEditGestures.Default;
    public Func<Control, DataTemplate> GetCellTemplate { get; }
    public Func<Control, DataTemplate>? GetCellEditingTemplate { get; }
    internal void SetValue(object? value) => Value = value;
    void IEditableObject.BeginEdit() => (Value as IEditableObject)?.BeginEdit();
    void IEditableObject.CancelEdit() => (Value as IEditableObject)?.CancelEdit();
    void IEditableObject.EndEdit() => (Value as IEditableObject)?.EndEdit();
}
