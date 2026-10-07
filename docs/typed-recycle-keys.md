# Typed recycle keys

Custom factories can derive from `TreeDataGridElementFactory<TKey>` to use structural
recycle keys without formatting strings or boxing values. The generic factory is
assignable to the existing `TreeDataGrid.ElementFactory` property. Existing factories
that override the string key methods continue to work unchanged.

```csharp
public readonly record struct CellKey(string Column, int GroupDepth);

public sealed class CellFactory : TreeDataGridElementFactory<CellKey>
{
    protected override CellKey GetDataRecycleKeyCore(object? data)
    {
        var model = (CellModel)data!;
        return new CellKey(model.Column, model.GroupDepth);
    }

    protected override CellKey GetElementRecycleKeyCore(Control element)
        => ((CellControl)element).RecycleKey;

    protected override Control CreateElement(object? data)
        => new CellControl(GetDataRecycleKeyCore(data));
}
```

`CellModel` and `CellControl` in this example are application types. A control must
retain its key after unrealization, even when its model is cleared. Keys must be
immutable and implement consistent `IEquatable<TKey>` equality and `GetHashCode`.
The example reuses the model's existing column identifier; it does not concatenate
or format a new string for each group depth.

The pool uses `Dictionary<TKey, ...>` and compatibility checks use
`EqualityComparer<TKey>.Default`. A value key implementing `IEquatable<TKey>` stays
unboxed throughout both paths, including when called through a non-generic factory
reference. Override `GetDataRecycleKeyCore` and `GetElementRecycleKeyCore`; the
inherited string-key methods are not used by the generic factory.

This removes allocations for key construction, comparison and lookup when the key
implementation itself does not allocate. Creating new controls, growing pools and
first-use runtime initialization can still allocate. Pool parent preference,
fallback reparenting and weak ownership of checked-out controls are shared with
the existing string-key factory.
