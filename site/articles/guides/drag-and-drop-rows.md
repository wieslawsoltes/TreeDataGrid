---
title: "Drag and Drop Rows"
---

# Drag and Drop Rows

TreeDataGrid supports row drag/drop with automatic data movement and event hooks.

## Enable Auto Drag/Drop

```xml
<TreeDataGrid Source="{Binding Source}" AutoDragDropRows="True" />
```

With this enabled, grid can perform internal move operations when allowed.

## Routed Events

- `RowDragStarted`
- `RowDragOver`
- `RowDrop`

Attach in XAML or code-behind.

```xml
<TreeDataGrid
    AutoDragDropRows="True"
    RowDragStarted="DragDrop_RowDragStarted"
    RowDragOver="DragDrop_RowDragOver" />
```

## Event Args Essentials

`TreeDataGridRowDragStartedEventArgs`:

- `Models`: dragged model set
- `AllowedEffects`: set to permit/deny effects

`TreeDataGridRowDragEventArgs`:

- `TargetRow`: row being hovered/dropped
- `Position`: `None`, `Before`, `After`, `Inside`
- `Inner`: underlying `DragEventArgs`

## Common Event Logic

Example from sample pattern:

```csharp
private void DragDrop_RowDragStarted(object? sender, TreeDataGridRowDragStartedEventArgs e)
{
    foreach (DragDropItem i in e.Models)
    {
        if (!i.AllowDrag)
            e.AllowedEffects = DragDropEffects.None;
    }
}

private void DragDrop_RowDragOver(object? sender, TreeDataGridRowDragEventArgs e)
{
    if (e.Position == TreeDataGridRowDropPosition.Inside &&
        e.TargetRow?.Model is DragDropItem i &&
        !i.AllowDrop)
        e.Inner.DragEffects = DragDropEffects.None;
}
```

## Source-Side Move Constraints

Automatic move operation is blocked when:

- source is sorted
- drag effect is not `Move`
- source collections are not mutable `IList<T>`

Flat source supports `Before` / `After`.

Hierarchical source supports `Before` / `After` / `Inside`.

## Drop Position Semantics

- `Before`: insert before target row
- `After`: insert after target row
- `Inside`: insert as child of target row (hierarchical only)

## UX Behavior

Grid shows drag adorner and supports auto-scroll near edges while dragging.

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.Models.TreeDataGrid.DragInfo>
- <xref:Avalonia.Controls.TreeDataGridRowDragEventArgs>
- <xref:Avalonia.Controls.TreeDataGridRowDragStartedEventArgs>
- <xref:Avalonia.Controls.TreeDataGridRowDropPosition>

## Related

- [Events and User Interaction](events-and-user-interaction.md)
- [Hierarchical Source Guide](sources-hierarchical.md)
- [Flat Source Guide](sources-flat.md)
- [Troubleshooting Guide](troubleshooting.md)
