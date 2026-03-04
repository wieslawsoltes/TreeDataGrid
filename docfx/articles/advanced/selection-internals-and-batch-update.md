# Selection Internals and Batch Update

This article explains TreeDataGrid selection contracts below the basic row/cell usage layer.

## Selection Stack

The internal stack has three levels:

1. Data model contracts:
   - [ITreeDataGridSelection](xref:Avalonia.Controls.Selection.ITreeDataGridSelection)
   - [ITreeSelectionModel](xref:Avalonia.Controls.Selection.ITreeSelectionModel)
2. Interaction adapter between control input and selection state:
   - [ITreeDataGridSelectionInteraction](xref:Avalonia.Controls.Selection.ITreeDataGridSelectionInteraction)
3. Concrete models:
   - [`TreeSelectionModelBase<T>`](xref:Avalonia.Controls.Selection.TreeSelectionModelBase`1)
   - [TreeDataGridColumnSelectionModel](xref:Avalonia.Controls.Selection.TreeDataGridColumnSelectionModel)

## Batch Update Semantics

Use batching when applying many selection changes.

Two public patterns:

- `BeginBatchUpdate()` / `EndBatchUpdate()` on [ITreeSelectionModel](xref:Avalonia.Controls.Selection.ITreeSelectionModel)
- disposable [`TreeSelectionModelBase<T>.BatchUpdateOperation`](xref:Avalonia.Controls.Selection.TreeSelectionModelBase`1.BatchUpdateOperation) from `BatchUpdate()` on concrete model

### Example: Bulk Row Selection

```csharp
using Avalonia.Controls;
using Avalonia.Controls.Selection;

if (source.RowSelection is TreeDataGridRowSelectionModel<Person> rows)
{
    using var update = rows.BatchUpdate();

    rows.Clear();
    rows.Select(new IndexPath(0));
    rows.Select(new IndexPath(2));
    rows.Select(new IndexPath(4));
}
```

Batching delays expensive notifications and keeps selection-change events coherent.

## Index and Source Change Events

Key event args:

- [TreeSelectionModelIndexesChangedEventArgs](xref:Avalonia.Controls.Selection.TreeSelectionModelIndexesChangedEventArgs)
- [TreeSelectionModelSelectionChangedEventArgs](xref:Avalonia.Controls.Selection.TreeSelectionModelSelectionChangedEventArgs)
- [`TreeSelectionModelSelectionChangedEventArgs<T>`](xref:Avalonia.Controls.Selection.TreeSelectionModelSelectionChangedEventArgs`1)
- [TreeSelectionModelSourceResetEventArgs](xref:Avalonia.Controls.Selection.TreeSelectionModelSourceResetEventArgs)

Important behavior:

- `SelectionChanged` can be skipped by source `Reset` semantics in `INotifyCollectionChanged` providers.
- Always pair `SelectionChanged` handling with `SourceReset` when you need durable consistency.

## Node-Level Infrastructure

[`SelectionNodeBase<T>`](xref:Avalonia.Controls.Selection.SelectionNodeBase`1) manages range state for each tree level and updates selection ranges on source changes.

You usually do not instantiate it directly, but it is the core reason hierarchical selection remains stable across add/remove/replace operations.

## Column Selection Model

[ITreeDataGridColumnSelectionModel](xref:Avalonia.Controls.Selection.ITreeDataGridColumnSelectionModel) and [TreeDataGridColumnSelectionModel](xref:Avalonia.Controls.Selection.TreeDataGridColumnSelectionModel) represent selected columns and are composed by cell-selection logic.

## Troubleshooting

- Duplicate or noisy selection events
Cause: batch boundaries are missing during grouped updates.

- Selection disappears after source reset
Cause: consumer listens only to `SelectionChanged` and ignores `SourceReset`.

- Keyboard/pointer behavior differs after custom selection assignment
Cause: assigned selection does not implement [ITreeDataGridSelectionInteraction](xref:Avalonia.Controls.Selection.ITreeDataGridSelectionInteraction).

## API Coverage Checklist

- <xref:Avalonia.Controls.Selection.ITreeDataGridColumnSelectionModel>
- <xref:Avalonia.Controls.Selection.ITreeDataGridSelection>
- <xref:Avalonia.Controls.Selection.ITreeDataGridSelectionInteraction>
- <xref:Avalonia.Controls.Selection.ITreeSelectionModel>
- <xref:Avalonia.Controls.Selection.SelectionNodeBase`1>
- <xref:Avalonia.Controls.Selection.TreeDataGridColumnSelectionModel>
- <xref:Avalonia.Controls.Selection.TreeSelectionModelBase`1>
- <xref:Avalonia.Controls.Selection.TreeSelectionModelBase`1.BatchUpdateOperation>
- <xref:Avalonia.Controls.Selection.TreeSelectionModelIndexesChangedEventArgs>
- <xref:Avalonia.Controls.Selection.TreeSelectionModelSelectionChangedEventArgs>
- <xref:Avalonia.Controls.Selection.TreeSelectionModelSelectionChangedEventArgs`1>
- <xref:Avalonia.Controls.Selection.TreeSelectionModelSourceResetEventArgs>

## Related

- [Selection Row](../guides/selection-row.md)
- [Selection Cell](../guides/selection-cell.md)
- [Selection Models](../concepts/selection-models.md)
- [Troubleshooting Guide](../guides/troubleshooting.md)
- [Namespace: Avalonia.Controls.Selection](../reference/namespace-selection.md)
