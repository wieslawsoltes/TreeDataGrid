using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using TreeDataGridCore.Models;

namespace Uno.Controls.Models.TreeDataGrid;

/// <summary>A public expander cell whose state belongs to the actual shared Core row.</summary>
/// <remarks>
/// The cell owns its content and observable subscriptions, but borrows the row.
/// Observable values replace framework-specific binding values; expansion writes
/// still use the row's controller, preserving Core model synchronization.
/// </remarks>
public class ExpanderCell<TModel> : NotifyingBase, IExpanderCell, IDisposable, IBoundCellState
    where TModel : class
{
    private IDisposable? _showSubscription;
    private IDisposable? _expandedSubscription;
    private bool _disposed;

    public ExpanderCell(ICell inner, IExpanderRow<TModel> row,
        IObservable<bool> showExpander, IObservable<bool>? isExpanded)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(showExpander);
        Content = inner;
        Row = row;
        try
        {
            row.PropertyChanged += RowPropertyChanged;
            _showSubscription = showExpander.Subscribe(new CellObserver<bool>(
                value => { if (!_disposed) Row.UpdateShowExpander(value); }, ReceiveError));
            if (isExpanded is not null)
                _expandedSubscription = isExpanded.Subscribe(new CellObserver<bool>(
                    value => { if (!_disposed) Row.IsExpanded = value; }, ReceiveError));
        }
        catch (Exception error)
        {
            try { Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }

    public bool CanEdit => Content.CanEdit;
    public ICell Content { get; }
    public BeginEditGestures EditGestures => Content.EditGestures;
    public IExpanderRow<TModel> Row { get; }
    public bool ShowExpander => Row.ShowExpander;
    public object? Value => Content.Value;
    public Exception? Error { get; private set; }
    public bool IsExpanded { get => Row.IsExpanded; set => Row.IsExpanded = value; }

    object? IExpanderCellPresentation.Content => Content;
    IRow IExpanderCellPresentation.Row => Row;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var show = _showSubscription;
        var expanded = _expandedSubscription;
        _showSubscription = null;
        _expandedSubscription = null;
        List<Exception>? errors = null;
        // A custom event accessor or disposable must not prevent release of the
        // remaining ownership. Retired callbacks are rejected before touching Row.
        try { Row.PropertyChanged -= RowPropertyChanged; }
        catch (Exception error) { (errors ??= new()).Add(error); }
        try { show?.Dispose(); }
        catch (Exception error) { (errors ??= new()).Add(error); }
        try { expanded?.Dispose(); }
        catch (Exception error) { (errors ??= new()).Add(error); }
        try { (Content as IDisposable)?.Dispose(); }
        catch (Exception error) { (errors ??= new()).Add(error); }
        if (errors is { Count: 1 }) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is not null) throw new AggregateException(errors);
    }

    private void RowPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed || !ReferenceEquals(sender, Row)) return;
        if (string.IsNullOrEmpty(args.PropertyName) ||
            args.PropertyName is nameof(IsExpanded) or nameof(ShowExpander))
            RaisePropertyChanged(args);
    }

    private void ReceiveError(Exception error)
    {
        if (_disposed) return;
        Error = error;
        RaisePropertyChanged(nameof(Error));
    }
}
