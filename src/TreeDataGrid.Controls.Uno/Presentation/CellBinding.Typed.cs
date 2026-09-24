using System;
using TreeDataGridCore.Models;

namespace Uno.Controls.Presentation;

internal sealed partial class CellBinding<TModel, TValue> where TModel : class
{
    private readonly bool _publishEveryRefresh;
    private readonly ColumnBindingSnapshot<TModel, TValue>? _descriptorSnapshot;

    internal CellBinding(ColumnBindingSnapshot<TModel, TValue> snapshot, Action changed)
        : this(snapshot.Definition, changed, snapshot.Links, publishEveryRefresh: false)
        => _descriptorSnapshot = snapshot;

    // Explicit generated links reuse the same subscription/retirement machinery.
    // The descriptor owns an immutable array; no root/model is shared between cells.
    internal CellBinding(ValueColumn<TModel, TValue> column, Action changed,
        Func<TModel, object?>[] accessors, bool publishEveryRefresh) : this(column, changed)
    {
        ArgumentNullException.ThrowIfNull(accessors);
        _accessors = accessors;
        _owners = accessors.Length <= 1 ? Array.Empty<object?>() : new object?[accessors.Length - 1];
        _publishEveryRefresh = publishEveryRefresh;
    }

    internal void RefreshCurrent()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Refresh();
    }
}
