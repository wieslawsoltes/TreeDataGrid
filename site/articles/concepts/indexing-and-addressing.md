---
title: "Indexing and Addressing"
---

# Indexing and Addressing

TreeDataGrid distinguishes between model indexes and visible row indexes.

Understanding this difference is critical for selection, expansion, sorting, and drag/drop.

## `IndexPath`: Hierarchical Model Address

`IndexPath` identifies a model row in the source hierarchy.

Examples:

- first root item: `new IndexPath(0)`
- second child of first root: `new IndexPath(0, 1)`
- first grandchild under that child: `new IndexPath(0, 1, 0)`

Conveniences:

- implicit conversion from `int` for flat roots (`IndexPath p = 3;`)
- `IndexPath.Unselected` for empty/no selection
- `Append(int)` to build deeper paths

## `CellIndex`: Cell Address

`CellIndex` combines:

- `ColumnIndex` (`int`)
- `RowIndex` (`IndexPath`)

It is used by cell selection APIs.

Example:

```csharp
var cell = new CellIndex(columnIndex: 2, rowIndex: new IndexPath(0, 1));
```

## Model Index vs Visible Row Index

`IndexPath` addresses model position. Visible row index addresses current on-screen order.

They can differ when:

- hierarchical rows are collapsed/expanded
- sorting is active
- virtualization and realization are in effect

Row collections expose conversion APIs:

- `IRows.ModelIndexToRowIndex(IndexPath)`
- `IRows.RowIndexToModelIndex(int)`

## Practical Rules

- Persist selections using `IndexPath` (model index), not visible row index.
- Convert to visible row index only when targeting UI operations (`BringIntoView`, `TryGetRow`).
- After sorting or hierarchy changes, recalculate visible row indexes.

## Working with Hierarchy Paths

Use `IndexPath` helpers when traversing:

- parent path: `path[..^1]`
- direct child path: `path.Append(childIndex)`
- relationship checks:
  - `parent.IsAncestorOf(child)`
  - `parent.IsParentOf(child)`

## Example: Select and Scroll a Hierarchical Row

```csharp
var modelIndex = new IndexPath(0, 1, 0);
Source.RowSelection!.SelectedIndex = modelIndex;

var rowIndex = Source.Rows.ModelIndexToRowIndex(modelIndex);
if (rowIndex >= 0)
    grid.RowsPresenter?.BringIntoView(rowIndex);
```

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.CellIndex>
- <xref:Avalonia.Controls.IndexPath>

## Related

- [Selection Models](selection-models.md)
- [Row Selection Guide](../guides/selection-row.md)
- [Cell Selection Guide](../guides/selection-cell.md)
- [TreeDataGrid Glossary](glossary.md)
- [Troubleshooting Guide](../guides/troubleshooting.md)
