# Cell Selection Guide

Use cell selection when interactions should target individual cells rather than whole rows.

## Enable Cell Selection

```csharp
using Avalonia.Controls.Selection;

Source.Selection = new TreeDataGridCellSelectionModel<Person>(Source)
{
    SingleSelect = false,
};
```

When active, source exposes:

- `Source.CellSelection`

## Single Cell Selection

```csharp
Source.CellSelection!.SelectedIndex = new CellIndex(
    columnIndex: 2,
    rowIndex: new IndexPath(0, 1));
```

Read selection:

```csharp
var current = Source.CellSelection!.SelectedIndex;
var all = Source.CellSelection.SelectedIndexes;
```

## Range Selection

```csharp
Source.CellSelection!.SetSelectedRange(
    start: new CellIndex(1, new IndexPath(0)),
    columnCount: 3,
    rowCount: 5);
```

This clears existing selection and selects a rectangular range.

## Selection Events

```csharp
Source.CellSelection!.SelectionChanged += (_, e) =>
{
    // event fired when selected cells change
};
```

For control-level visuals, `TreeDataGrid` routes selection interaction updates to realized rows/cells.

## Checking Membership

```csharp
var isSelected = Source.CellSelection!.IsSelected(new CellIndex(0, new IndexPath(2)));
```

## Keyboard and Pointer Behavior

Cell model handles:

- arrow navigation across row/column axis
- Shift range extension
- pointer press/release selection behavior

Behavior is implemented through `ITreeDataGridSelectionInteraction`.

## Common Pitfalls

- using cell selection but reading `RowSelection`
- assigning `CellIndex` with model index that is not currently represented in source
- expecting additive range API behavior: `SetSelectedRange` replaces selection

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.Selection.ITreeDataGridCellSelectionModel`1>
- <xref:Avalonia.Controls.Selection.ITreeDataGridCellSelectionModel>
- <xref:Avalonia.Controls.Selection.TreeDataGridCellSelectionChangedEventArgs`1>
- <xref:Avalonia.Controls.Selection.TreeDataGridCellSelectionChangedEventArgs>
- <xref:Avalonia.Controls.Selection.TreeDataGridCellSelectionModel`1>

## Related

- [Row Selection Guide](selection-row.md)
- [Selection Models](../concepts/selection-models.md)
- [Indexing and Addressing](../concepts/indexing-and-addressing.md)
- [Troubleshooting Guide](troubleshooting.md)
