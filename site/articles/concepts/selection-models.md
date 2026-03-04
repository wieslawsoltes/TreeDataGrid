---
title: "Selection Models"
---

# Selection Models

TreeDataGrid supports two primary selection modes:

- row selection
- cell selection

The active behavior comes from `ITreeDataGridSource.Selection`.

## Default Behavior

If you do not assign `Selection` explicitly:

- sources create a row selection model by default
- selection is single-select by default

This makes basic keyboard/mouse selection work immediately.

## Row Selection Model

Main API:

- `TreeDataGridRowSelectionModel<TModel>`
- `ITreeDataGridRowSelectionModel<TModel>`

Common operations:

- set `SelectedIndex` (`IndexPath`)
- inspect `SelectedIndexes`, `SelectedItem`, `SelectedItems`
- enable multi-select via `SingleSelect = false`

Example:

```csharp
Source.RowSelection!.SingleSelect = false;
Source.RowSelection.SelectedIndex = new IndexPath(0, 1);
```

## Cell Selection Model

Main API:

- `TreeDataGridCellSelectionModel<TModel>`
- `ITreeDataGridCellSelectionModel<TModel>`

Common operations:

- set `SelectedIndex` (`CellIndex`)
- inspect `SelectedIndexes`
- select ranges via `SetSelectedRange(...)`

Example:

```csharp
Source.Selection = new TreeDataGridCellSelectionModel<Person>(Source)
{
    SingleSelect = false,
};
```

## Switching Modes at Runtime

Switch by replacing `Source.Selection`:

```csharp
if (useCellSelection)
    Source.Selection = new TreeDataGridCellSelectionModel<Person>(Source);
else
    Source.Selection = new TreeDataGridRowSelectionModel<Person>(Source);
```

Requirement: assigned selection model must point to the same `Items` source.

## Base Contracts and Advanced Behavior

Core contracts:

- `ITreeSelectionModel`
- `ITreeDataGridSelection`
- `ITreeDataGridSelectionInteraction`

Advanced events from selection base model:

- `SelectionChanged`
- `IndexesChanged`
- `SourceReset`

`SourceReset` is important when source collections raise reset notifications.

## Anchors and Ranges

For keyboard/range logic, selection models track:

- `AnchorIndex`
- `RangeAnchorIndex`

This supports shift-range operations and consistent keyboard navigation behavior.

## Practical Recommendations

- Prefer row selection for list-style workflows and commands on full records.
- Prefer cell selection for spreadsheet-like editing.
- Persist model addresses (`IndexPath`, `CellIndex`) rather than visible row numbers.

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.Selection.ITreeDataGridSelection>
- <xref:Avalonia.Controls.Selection.ITreeDataGridRowSelectionModel`1>
- <xref:Avalonia.Controls.Selection.ITreeDataGridCellSelectionModel`1>
- <xref:Avalonia.Controls.Selection.ITreeSelectionModel>

## Related

- [Indexing and Addressing](indexing-and-addressing.md)
- [Row Selection Guide](../guides/selection-row.md)
- [Cell Selection Guide](../guides/selection-cell.md)
- [Selection Internals and Batch Update](../advanced/selection-internals-and-batch-update.md)
- [TreeDataGrid Glossary](glossary.md)
