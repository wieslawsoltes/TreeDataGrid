---
title: "Namespace: Avalonia.Controls.Models.TreeDataGrid"
---

# Namespace: Avalonia.Controls.Models.TreeDataGrid

This namespace contains the model-layer contracts and implementations for columns, cells, rows, hierarchy, sorting, and layout cooperation.

## Functional Groups

Use these groups to navigate the model-layer API by responsibility.

### Column Contracts and Bases

- [IColumn](xref:Avalonia.Controls.Models.TreeDataGrid.IColumn)
- [`IColumn<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.IColumn`1)
- [`ColumnBase<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.ColumnBase`1)
- [`ColumnBase<TModel,TValue>`](xref:Avalonia.Controls.Models.TreeDataGrid.ColumnBase`2)
- [IColumns](xref:Avalonia.Controls.Models.TreeDataGrid.IColumns)
- [`ColumnList<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.ColumnList`1)
- [IUpdateColumnLayout](xref:Avalonia.Controls.Models.TreeDataGrid.IUpdateColumnLayout)

### Built-In Column Types and Options

- [`TextColumn<TModel,TValue>`](xref:Avalonia.Controls.Models.TreeDataGrid.TextColumn`2)
- [`TemplateColumn<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.TemplateColumn`1)
- [`CheckBoxColumn<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.CheckBoxColumn`1)
- [`HierarchicalExpanderColumn<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.HierarchicalExpanderColumn`1)
- [`TextColumnOptions<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.TextColumnOptions`1)
- [`TemplateColumnOptions<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.TemplateColumnOptions`1)
- [`CheckBoxColumnOptions<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.CheckBoxColumnOptions`1)
- [`ColumnOptions<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.ColumnOptions`1)

### Cell Contracts and Implementations

- [ICell](xref:Avalonia.Controls.Models.TreeDataGrid.ICell)
- [ITextCell](xref:Avalonia.Controls.Models.TreeDataGrid.ITextCell)
- [IExpanderCell](xref:Avalonia.Controls.Models.TreeDataGrid.IExpanderCell)
- [CheckBoxCell](xref:Avalonia.Controls.Models.TreeDataGrid.CheckBoxCell)
- [`TextCell<T>`](xref:Avalonia.Controls.Models.TreeDataGrid.TextCell`1)
- [TemplateCell](xref:Avalonia.Controls.Models.TreeDataGrid.TemplateCell)
- [`ExpanderCell<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.ExpanderCell`1)

### Row and Hierarchy Contracts

- [IRow](xref:Avalonia.Controls.Models.TreeDataGrid.IRow)
- [`IRow<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.IRow`1)
- [IRows](xref:Avalonia.Controls.Models.TreeDataGrid.IRows)
- [IIndentedRow](xref:Avalonia.Controls.Models.TreeDataGrid.IIndentedRow)
- [IModelIndexableRow](xref:Avalonia.Controls.Models.TreeDataGrid.IModelIndexableRow)
- [IExpander](xref:Avalonia.Controls.Models.TreeDataGrid.IExpander)
- [`IExpanderRow<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.IExpanderRow`1)
- [`IExpanderRowController<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.IExpanderRowController`1)
- [`IExpanderColumn<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.IExpanderColumn`1)
- [`HierarchicalRow<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.HierarchicalRow`1)
- [`HierarchicalRows<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.HierarchicalRows`1)

### Selection/Search/Edit Helpers

- [BeginEditGestures](xref:Avalonia.Controls.Models.TreeDataGrid.BeginEditGestures)
- [ICellOptions](xref:Avalonia.Controls.Models.TreeDataGrid.ICellOptions)
- [ITextCellOptions](xref:Avalonia.Controls.Models.TreeDataGrid.ITextCellOptions)
- [ITemplateCellOptions](xref:Avalonia.Controls.Models.TreeDataGrid.ITemplateCellOptions)
- [`ITextSearchableColumn<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.ITextSearchableColumn`1)

### Sorting, Drag, and Event Helpers

- [`SortableRowsBase<TModel,TRow>`](xref:Avalonia.Controls.Models.TreeDataGrid.SortableRowsBase`2)
- [`AnonymousSortableRows<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.AnonymousSortableRows`1)
- [DragInfo](xref:Avalonia.Controls.Models.TreeDataGrid.DragInfo)
- [RowEventArgs](xref:Avalonia.Controls.Models.TreeDataGrid.RowEventArgs)
- [`RowEventArgs<TRow>`](xref:Avalonia.Controls.Models.TreeDataGrid.RowEventArgs`1)

## Complete Type Index

| Type | Kind | Primary Article |
|---|---|---|
| <xref:Avalonia.Controls.Models.TreeDataGrid.AnonymousSortableRows`1> | Class | [advanced/custom-rows-columns-pipeline.md](../advanced/custom-rows-columns-pipeline.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.BeginEditGestures> | Enum | [guides/editing-and-begin-edit-gestures.md](../guides/editing-and-begin-edit-gestures.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.CheckBoxCell> | Class | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.CheckBoxColumn`1> | Class | [guides/column-checkbox.md](../guides/column-checkbox.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.CheckBoxColumnOptions`1> | Class | [guides/column-checkbox.md](../guides/column-checkbox.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.ColumnBase`1> | Class | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.ColumnBase`2> | Class | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.ColumnList`1> | Class | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.ColumnOptions`1> | Class | [guides/sorting-and-column-widths.md](../guides/sorting-and-column-widths.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.DragInfo> | Class | [guides/drag-and-drop-rows.md](../guides/drag-and-drop-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.ExpanderCell`1> | Class | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.HierarchicalExpanderColumn`1> | Class | [guides/column-expander.md](../guides/column-expander.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.HierarchicalRow`1> | Class | [guides/expansion-and-programmatic-navigation.md](../guides/expansion-and-programmatic-navigation.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.HierarchicalRows`1> | Class | [guides/expansion-and-programmatic-navigation.md](../guides/expansion-and-programmatic-navigation.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.ICell> | Interface | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.ICellOptions> | Interface | [guides/editing-and-begin-edit-gestures.md](../guides/editing-and-begin-edit-gestures.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.IColumn`1> | Interface | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.IColumn> | Interface | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.IColumns> | Interface | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.IExpander> | Interface | [guides/expansion-and-programmatic-navigation.md](../guides/expansion-and-programmatic-navigation.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.IExpanderCell> | Interface | [guides/expansion-and-programmatic-navigation.md](../guides/expansion-and-programmatic-navigation.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.IExpanderColumn`1> | Interface | [guides/expansion-and-programmatic-navigation.md](../guides/expansion-and-programmatic-navigation.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.IExpanderRow`1> | Interface | [guides/expansion-and-programmatic-navigation.md](../guides/expansion-and-programmatic-navigation.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.IExpanderRowController`1> | Interface | [guides/expansion-and-programmatic-navigation.md](../guides/expansion-and-programmatic-navigation.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.IIndentedRow> | Interface | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.IModelIndexableRow> | Interface | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.IRow`1> | Interface | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.IRow> | Interface | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.IRows> | Interface | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.ITemplateCellOptions> | Interface | [guides/editing-and-begin-edit-gestures.md](../guides/editing-and-begin-edit-gestures.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.ITextCell> | Interface | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.ITextCellOptions> | Interface | [guides/editing-and-begin-edit-gestures.md](../guides/editing-and-begin-edit-gestures.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.ITextSearchableColumn`1> | Interface | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.IUpdateColumnLayout> | Interface | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.RowEventArgs`1> | Class | [guides/expansion-and-programmatic-navigation.md](../guides/expansion-and-programmatic-navigation.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.RowEventArgs> | Class | [guides/expansion-and-programmatic-navigation.md](../guides/expansion-and-programmatic-navigation.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.SortableRowsBase`2> | Class | [advanced/custom-rows-columns-pipeline.md](../advanced/custom-rows-columns-pipeline.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.TemplateCell> | Class | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.TemplateColumn`1> | Class | [guides/column-template.md](../guides/column-template.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.TemplateColumnOptions`1> | Class | [guides/column-template.md](../guides/column-template.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.TextCell`1> | Class | [concepts/columns-cells-rows.md](../concepts/columns-cells-rows.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.TextColumn`2> | Class | [guides/column-text.md](../guides/column-text.md) |
| <xref:Avalonia.Controls.Models.TreeDataGrid.TextColumnOptions`1> | Class | [guides/column-text.md](../guides/column-text.md) |

## Related

- [Concepts: Columns, Cells, Rows](../concepts/columns-cells-rows.md)
- [Guides: Expansion and Programmatic Navigation](../guides/expansion-and-programmatic-navigation.md)
- [Advanced: Custom Rows and Columns Pipeline](../advanced/custom-rows-columns-pipeline.md)
- [API Coverage Index](api-coverage-index.md)
