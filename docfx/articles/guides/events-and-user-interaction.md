# Events and User Interaction

`TreeDataGrid` exposes lifecycle and interaction hooks for advanced behaviors.

## Cell Lifecycle Events

- `CellPrepared`: realized and ready for customization
- `CellClearing`: being unrealized/recycled
- `CellValueChanged`: value committed from edit interaction

Example:

```csharp
grid.CellPrepared += (_, e) =>
{
    // e.Cell, e.ColumnIndex, e.RowIndex
};

grid.CellValueChanged += (_, e) =>
{
    // react to committed edits
};
```

## Row Lifecycle Events

- `RowPrepared`
- `RowClearing`

Useful for row-level instrumentation.

## Selection Intercept Hook

`SelectionChanging` can cancel selection updates:

```csharp
grid.SelectionChanging += (_, e) =>
{
    if (ShouldBlockSelection())
        e.Cancel = true;
};
```

## Drag/Drop Events

- `RowDragStarted`
- `RowDragOver`
- `RowDrop`

See [Drag and Drop Rows](drag-and-drop-rows.md).

## Retrieval Helpers

Use these APIs to resolve TreeDataGrid visuals or models from controls and indexes.

## Resolve from Visual Element

- `TryGetRow(Control, out TreeDataGridRow?)`
- `TryGetCell(Control, out TreeDataGridCell?)`
- `TryGetRowModel<TModel>(Control, out TModel?)`

## Resolve by Visible Index

- `TryGetRow(int rowIndex)`
- `TryGetCell(int columnIndex, int rowIndex)`

Example:

```csharp
if (grid.TryGetRow(10) is { } row)
{
    row.Focus();
}
```

## Interaction Properties

Common behavior toggles:

- `CanUserSortColumns`
- `CanUserResizeColumns`
- `ShowColumnHeaders`
- `AutoDragDropRows`

## Routed Events and Template Parts

Internally, control syncs row/header scroll viewers and delegates keyboard/pointer/text input to active selection interaction.

This means changing `Source.Selection` can materially change keyboard/pointer behavior without changing UI markup.

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.TreeDataGrid>
- <xref:Avalonia.Controls.TreeDataGridCellEventArgs>
- <xref:Avalonia.Controls.TreeDataGridRowEventArgs>

## Related

- [Selection Row](selection-row.md)
- [Selection Cell](selection-cell.md)
- [Drag and Drop Rows](drag-and-drop-rows.md)
- [Troubleshooting Guide](troubleshooting.md)
