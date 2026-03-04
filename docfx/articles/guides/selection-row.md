# Row Selection Guide

Row selection is the default selection mode for both flat and hierarchical sources.

## Default and Multi-Select

By default:

- row selection model is created lazily
- `SingleSelect = true`

Enable multi-select:

```csharp
Source.RowSelection!.SingleSelect = false;
```

## Selecting by IndexPath

```csharp
Source.RowSelection!.SelectedIndex = new IndexPath(0, 1);
```

For flat sources, implicit int conversion is available:

```csharp
Source.RowSelection!.SelectedIndex = 3;
```

## Read Selected Items

```csharp
var selection = Source.RowSelection!;
var first = selection.SelectedItem;
var all = selection.SelectedItems;
var indexes = selection.SelectedIndexes;
```

## Programmatic Operations

Available operations (through `ITreeSelectionModel`):

- `Select(IndexPath)`
- `Deselect(IndexPath)`
- `Clear()`
- `IsSelected(IndexPath)`

Batch updates for advanced scenarios:

```csharp
selection.BeginBatchUpdate();
try
{
    selection.Select(new IndexPath(0));
    selection.Select(new IndexPath(1));
}
finally
{
    selection.EndBatchUpdate();
}
```

## Events

Typed event:

```csharp
Source.RowSelection!.SelectionChanged += (_, e) =>
{
    // e.SelectedIndexes, e.DeselectedIndexes
    // e.SelectedItems, e.DeselectedItems
};
```

Base model events (advanced):

- `IndexesChanged`
- `SourceReset`

`SourceReset` is important when the underlying collection raises Reset.

## UI Integration

From `TreeDataGrid` control:

- `RowSelection` property returns active row selection model when in row mode
- keyboard and pointer interactions route through selection interaction contract

## Common Pitfalls

- assigning a custom selection model whose `Source` differs from source `Items`
- persisting visible row indexes instead of `IndexPath`
- forgetting to disable `SingleSelect` for Ctrl/Shift style multi-selection workflows

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.Selection.ITreeDataGridRowSelectionModel`1>
- <xref:Avalonia.Controls.Selection.ITreeDataGridRowSelectionModel>
- <xref:Avalonia.Controls.Selection.TreeDataGridRowSelectionModel`1>

## Related

- [Selection Models](../concepts/selection-models.md)
- [Cell Selection Guide](selection-cell.md)
- [Indexing and Addressing](../concepts/indexing-and-addressing.md)
- [Troubleshooting Guide](troubleshooting.md)
