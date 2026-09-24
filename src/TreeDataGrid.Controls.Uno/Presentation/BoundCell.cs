using System;
using System.ComponentModel;
using System.Globalization;
using TreeDataGridCore.Models;

namespace Uno.Controls.Presentation;

internal class BoundCell<TModel, TValue> : CellValue where TModel : class
{
    // Immutable notification payloads; names and publication order are unchanged.
    private static readonly PropertyChangedEventArgs ValueChanged = new(nameof(Value));
    private static readonly PropertyChangedEventArgs ErrorChanged = new(nameof(Error));
    private readonly CellBinding<TModel, TValue> _binding;
    private readonly bool _canPool;
    private readonly CultureInfo? _culture;
    public BoundCell(ValueColumn<TModel, TValue> column, IRow row, bool canPool, CultureInfo? culture = null,
        ColumnBindingSnapshot<TModel, TValue>? bindingSnapshot = null)
    {
        _canPool = canPool;
        _culture = culture;
        // A cell ignores an error result without a fallback, just like the
        // reference scalar observer. Merely accessing Binding must not change
        // that behavior. Internal non-cell bindings retain their own policy.
        _binding = bindingSnapshot is null
            ? new(column, Changed, retainValueOnError: true)
            : new(bindingSnapshot, Changed);
        try { _binding.Retarget((TModel)row.Model!); }
        catch (Exception error)
        {
            try { _binding.Dispose(); }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }
    public override object? Value => _binding.Value;
    public override Exception? Error => _binding.Error;
    public override bool CanEdit => Kind != CellKind.CheckBox && _binding.CanWrite;
    public override bool CanWrite => _binding.CanWrite;
    // Native TextColumn options are mutable. Only the internal facade cell
    // overrides this scalar read; conversion still runs inside CellBinding's
    // lifetime/write transaction, never before its retirement checks.
    protected virtual CultureInfo? ConversionCulture => _culture;
    internal bool UsesColumn(ValueColumn<TModel, TValue> column) => _binding.UsesColumn(column);
    public override void Write(object? value) => _binding.WriteConverted(value, ConversionCulture ?? CultureInfo.CurrentCulture);
    public override void Dispose() => _binding.Dispose();
    internal override bool TryRetarget(IRow row)
    {
        if (!_canPool || row.Model is not TModel model) return false;
        _binding.Retarget(model);
        return true;
    }
    internal override bool TrySuspend()
    {
        if (!_canPool) return false;
        _binding.Suspend();
        return true;
    }
    private void Changed()
    {
        RaisePropertyChanged(ValueChanged);
        RaisePropertyChanged(ErrorChanged);
    }
}
