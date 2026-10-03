using System;
using TreeDataGridCore.Models;
using Uno.Data;
using Uno.Experimental.Data;
using Uno.Experimental.Data.Core;

namespace Uno.Controls.Presentation;

public partial class ValueCellColumn<TModel, TValue>
{
    private TypedBinding<TModel, TValue>? _binding;
    private ColumnBindingSnapshot<TModel, TValue>? _bindingSnapshot;

    /// <summary>Gets the mutable descriptor used when creating subsequent cells.</summary>
    /// <remarks>
    /// Existing cells, including retained and pooled cells, keep the descriptor
    /// snapshot captured when they were created. Editing this descriptor neither
    /// changes the Core definition/selector nor replaces existing subscriptions.
    /// Mode and Source are target-attachment settings; cells use their own row and
    /// the reference InstanceForCell default observation mode.
    /// </remarks>
    public TypedBinding<TModel, TValue> Binding => _binding ??= new()
    {
        Read = _column.Getter,
        Write = _column.Setter,
        Links = (Func<TModel, object>[])CellBinding<TModel, TValue>.GetOwnerAccessors(_column).Clone(),
        Mode = _column.Setter is null ? BindingMode.OneWay : BindingMode.TwoWay,
    };

    /// <summary>Creates an independently observed snapshot of this column's binding for a row.</summary>
    protected TypedBindingExpression<TModel, TValue> CreateBindingExpression(TModel model) =>
        Binding.InstanceForCell(model);

    internal ColumnBindingSnapshot<TModel, TValue>? CaptureBindingSnapshot()
    {
        // No descriptor, expression wrapper or snapshot is allocated on the
        // ordinary built-in path unless the public descriptor is requested.
        if (_binding is not { } binding) return null;
        if (_bindingSnapshot is { } snapshot && snapshot.Matches(binding)) return snapshot;
        // Publish only after validation/cloning succeeds. An invalid edit cannot
        // corrupt already realized cells or the last complete snapshot.
        return _bindingSnapshot = new(_column, binding);
    }
}

/// <summary>Immutable binding instructions shared by cells, without realization-owned rows or subscriptions.</summary>
internal sealed class ColumnBindingSnapshot<TModel, TValue> where TModel : class
{
    private readonly int _revision;
    internal ValueColumn<TModel, TValue> Origin { get; }
    internal ValueColumn<TModel, TValue> Definition { get; }
    internal Func<TModel, object?>[] Links { get; }
    internal Optional<TValue> Fallback { get; }

    internal ColumnBindingSnapshot(ValueColumn<TModel, TValue> origin, TypedBinding<TModel, TValue> binding)
    {
        var read = binding.Read ?? throw new InvalidOperationException("Cannot bind TypedBinding: Read is uninitialized.");
        var links = binding.Links ?? throw new InvalidOperationException("Cannot bind TypedBinding: Links is uninitialized.");
        var copy = new Func<TModel, object?>[links.Length];
        for (var i = 0; i < links.Length; ++i)
            copy[i] = links[i] ?? throw new ArgumentException("A binding link cannot be null.", nameof(links));
        Origin = origin;
        // Only immutable accessor instructions are adapted. This definition is
        // never installed in Core's Columns and never owns a source or model.
        Definition = ValueColumn<TModel, TValue>.FromDelegate(null, read, setter: binding.Write);
        Links = copy;
        Fallback = binding.FallbackValue;
        _revision = binding.CellRevision;
    }

    internal bool Matches(TypedBinding<TModel, TValue> binding)
    {
        if (_revision != binding.CellRevision || binding.Links is not { } links || links.Length != Links.Length)
            return false;
        for (var i = 0; i < links.Length; ++i)
            if (!ReferenceEquals(links[i], Links[i])) return false;
        return true;
    }
}
