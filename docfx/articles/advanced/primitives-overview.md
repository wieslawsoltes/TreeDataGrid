# Primitives Overview

This article covers the low-level visual primitives used by TreeDataGrid templates.

## Primitive Roles

- [TreeDataGridRow](xref:Avalonia.Controls.Primitives.TreeDataGridRow): row container and row-level interaction surface.
- [TreeDataGridCell](xref:Avalonia.Controls.Primitives.TreeDataGridCell): base for all cell visuals and edit lifecycle.
- [TreeDataGridTextCell](xref:Avalonia.Controls.Primitives.TreeDataGridTextCell): text rendering and text edit presentation.
- [TreeDataGridCheckBoxCell](xref:Avalonia.Controls.Primitives.TreeDataGridCheckBoxCell): bool/tri-state cell UI.
- [TreeDataGridTemplateCell](xref:Avalonia.Controls.Primitives.TreeDataGridTemplateCell): template-driven cell with editing template support.
- [TreeDataGridExpanderCell](xref:Avalonia.Controls.Primitives.TreeDataGridExpanderCell): indentation and expand/collapse affordance.
- [TreeDataGridColumnHeader](xref:Avalonia.Controls.Primitives.TreeDataGridColumnHeader): header visual with sorting/resizing behavior.

## Important State and Pseudoclasses

- `TreeDataGridRow`: `:selected`
- `TreeDataGridCell`: `:selected`, `:editing`
- `TreeDataGridColumnHeader`: `:resizable`

These are the key hooks for high-performance styling.

## Editing Lifecycle at Cell Level

`TreeDataGridCell` manages:

- begin edit (gesture-dependent)
- commit (`EndEdit`) and cancel (`CancelEdit`)
- value synchronization to cell model
- `CellValueChanged` signal through owner grid

Begin-edit behavior is controlled by [BeginEditGestures](xref:Avalonia.Controls.Models.TreeDataGrid.BeginEditGestures).

## Styling Example

```xml
<Style Selector="TreeDataGridRow:selected">
  <Setter Property="Background" Value="#1F3A5F"/>
</Style>

<Style Selector="TreeDataGridCell:editing">
  <Setter Property="Background" Value="#FFF6D5"/>
</Style>

<Style Selector="TreeDataGridColumnHeader:resizable">
  <Setter Property="Cursor" Value="SizeWestEast"/>
</Style>
```

## Interaction Notes

- [TreeDataGridRow](xref:Avalonia.Controls.Primitives.TreeDataGridRow) detects pointer movement threshold and can start row drag.
- [TreeDataGridExpanderCell](xref:Avalonia.Controls.Primitives.TreeDataGridExpanderCell) mirrors expansion state from [IExpanderCell](xref:Avalonia.Controls.Models.TreeDataGrid.IExpanderCell).
- [TreeDataGridTemplateCell](xref:Avalonia.Controls.Primitives.TreeDataGridTemplateCell) manages focus transitions for editing content.

## Troubleshooting

- Editing template loses focus too early
Cause: editing content is hosted in popup or descendant focus chain not tracked by custom template.

- Checkbox cell value appears stale
Cause: model does not implement expected property change notifications.

- Row drag starts unexpectedly
Cause: pointer movement exceeds drag threshold while selection gestures are active.

## API Coverage Checklist

- <xref:Avalonia.Controls.Primitives.TreeDataGridRow>
- <xref:Avalonia.Controls.Primitives.TreeDataGridCell>
- <xref:Avalonia.Controls.Primitives.TreeDataGridTextCell>
- <xref:Avalonia.Controls.Primitives.TreeDataGridCheckBoxCell>
- <xref:Avalonia.Controls.Primitives.TreeDataGridTemplateCell>
- <xref:Avalonia.Controls.Primitives.TreeDataGridExpanderCell>
- <xref:Avalonia.Controls.Primitives.TreeDataGridColumnHeader>

## Related

- [Performance, Virtualization, and Realization](performance-virtualization-and-realization.md)
- [Templates and Styling](../guides/templates-and-styling.md)
- [Editing and Begin Edit Gestures](../guides/editing-and-begin-edit-gestures.md)
- [TreeDataGrid Glossary](../concepts/glossary.md)
- [Namespace: Avalonia.Controls.Primitives](../reference/namespace-primitives.md)
