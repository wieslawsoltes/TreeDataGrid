using System;
using System.ComponentModel;

namespace Uno.Controls.Presentation;

/// <summary>An edit belongs to one realization, never to a reusable control.</summary>
internal sealed class CellEditSession : IDisposable
{
    private CellValue? _cell;
    private IEditableObject? _editable;
    private readonly bool _writeValue;
    public CellEditSession(CellValue cell, object? model, bool writeValue)
    {
        _cell = cell;
        _editable = model as IEditableObject;
        _writeValue = writeValue;
        try { _editable?.BeginEdit(); }
        catch (Exception error)
        {
            try { Cancel(); }
            catch (Exception cancelError) { throw new AggregateException(error, cancelError); }
            throw;
        }
    }
    public bool IsActive => _cell is not null;
    public Exception? Error { get; private set; }
    public bool Commit(object? value)
    {
        if (_cell is not { } cell) return false;
        try
        {
            if (_writeValue) cell.Write(value);
            // A setter can synchronously replace/remove its row. Its edit was
            // cancelled by recycling; do not finish an edit on the next model.
            if (!ReferenceEquals(_cell, cell)) return false;
            _editable?.EndEdit();
            if (!ReferenceEquals(_cell, cell)) return false;
            _cell = null;
            _editable = null;
            Error = null;
            return true;
        }
        catch (Exception error) { Error = error; return false; }
    }
    public void Cancel()
    {
        if (_cell is null) return;
        var editable = _editable;
        _cell = null;
        _editable = null;
        Error = null;
        editable?.CancelEdit();
    }
    public void Dispose() => Cancel();
}
