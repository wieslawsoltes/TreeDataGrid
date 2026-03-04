---
title: "Custom Rows and Columns Pipeline"
---

# Custom Rows and Columns Pipeline

This article covers low-level extension points for custom row collections, item notifications, and sorting pipelines.

## When to Use This Layer

Use this layer if you are building custom source internals, not when only configuring columns in app code.

Typical reasons:

- custom row materialization and lifecycle
- custom sorting mechanics over large datasets
- custom `IReadOnlyList`/`INotifyCollectionChanged` implementations with controlled allocation

## Foundation Types

- [NotifyingBase](xref:Avalonia.Controls.Models.NotifyingBase): lightweight property change base class.
- [`NotifyingListBase<T>`](xref:Avalonia.Controls.Models.NotifyingListBase`1): notifying list with range helpers (`InsertRange`, `RemoveRange`, `Reset`).
- [`ReadOnlyListBase<T>`](xref:Avalonia.Controls.Models.ReadOnlyListBase`1): base for read-only list adapters.

These are used heavily by row and column pipeline implementations.

## Row Pipeline Types

- [`SortableRowsBase<TModel,TRow>`](xref:Avalonia.Controls.Models.TreeDataGrid.SortableRowsBase`2): reusable sorted/unsorted row collection core.
- [`AnonymousSortableRows<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.AnonymousSortableRows`1): allocation-light flat-row implementation used by [`FlatTreeDataGridSource<TModel>`](xref:Avalonia.Controls.FlatTreeDataGridSource`1).

`SortableRowsBase` handles:

- source tracking via [`TreeDataGridItemsSourceView<T>`](xref:Avalonia.Controls.TreeDataGridItemsSourceView`1)
- stable sorting map maintenance
- index shift handling on collection changes

## Minimal Custom Rows Example

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Models;
using Avalonia.Controls.Models.TreeDataGrid;

public sealed class PersonRow : NotifyingBase, IRow<Person>, IModelIndexableRow, IDisposable
{
    public PersonRow(int modelIndex, Person model)
    {
        ModelIndex = modelIndex;
        Model = model;
    }

    public int ModelIndex { get; private set; }
    public IndexPath ModelIndexPath => new IndexPath(ModelIndex);
    public Person Model { get; }
    public object? Header => ModelIndex;
    public GridLength Height { get; set; } = GridLength.Auto;

    public void UpdateModelIndex(int delta) => ModelIndex += delta;
    public void Dispose() { }
}

public sealed class PersonRows : SortableRowsBase<Person, PersonRow>, IRows
{
    public PersonRows(TreeDataGridItemsSourceView<Person> items, Comparison<Person>? comparison)
        : base(items, comparison)
    {
    }

    protected override PersonRow CreateRow(int modelIndex, Person model)
        => new PersonRow(modelIndex, model);

    public (int index, double y) GetRowAt(double y)
        => Math.Abs(y) < double.Epsilon ? (0, 0) : (-1, -1);

    public int ModelIndexToRowIndex(IndexPath modelIndex)
        => modelIndex.Count == 1 ? ModelIndexToRowIndex(modelIndex[0]) : -1;

    public IndexPath RowIndexToModelIndex(int rowIndex)
    {
        var modelIndex = base.RowIndexToModelIndex(rowIndex);
        return modelIndex >= 0 ? new IndexPath(modelIndex) : default;
    }

    public ICell RealizeCell(IColumn column, int columnIndex, int rowIndex)
    {
        if (column is IColumn<Person> typed)
            return typed.CreateCell(this[rowIndex]);
        throw new InvalidOperationException("Invalid column type for PersonRows.");
    }

    public void UnrealizeCell(ICell cell, int columnIndex, int rowIndex)
        => (cell as IDisposable)?.Dispose();

    IEnumerator<IRow> IEnumerable<IRow>.GetEnumerator()
        => GetEnumerator();
}
```

## Design Constraints

- Treat `IRow` and `IColumn` instances as transient unless your implementation guarantees persistence.
- Always keep model-index and row-index conversion stable (`ModelIndexToRowIndex` / `RowIndexToModelIndex`).
- In sorted mode, preserve sort stability (equal keys keep deterministic order).
- Dispose row and cell resources when items are removed or reset.

## Troubleshooting

- Row selection appears incorrect after source changes
Cause: model index path is not updated consistently in custom row objects.

- View does not refresh after batch list changes
Cause: missing `NotifyCollectionChanged` events or wrong event action for range operations.

- Drag/drop target index is wrong in sorted mode
Cause: sorted and model indexes are mixed without conversion.

## API Coverage Checklist

- <xref:Avalonia.Controls.Models.NotifyingBase>
- <xref:Avalonia.Controls.Models.NotifyingListBase`1>
- <xref:Avalonia.Controls.Models.ReadOnlyListBase`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.SortableRowsBase`2>
- <xref:Avalonia.Controls.Models.TreeDataGrid.AnonymousSortableRows`1>

## Related

- [Sources Flat](../guides/sources-flat.md)
- [Sources Hierarchical](../guides/sources-hierarchical.md)
- [Validation Snippets](../guides/validation-snippets.md)
- [Namespace: Converters and Base Models](../reference/namespace-converters-and-models.md)
- [Namespace: Avalonia.Controls.Models.TreeDataGrid](../reference/namespace-models-treedatagrid.md)
