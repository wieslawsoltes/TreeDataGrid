---
title: "Columns, Cells, and Rows"
---

# Columns, Cells, and Rows

TreeDataGrid uses explicit column/row/cell model abstractions instead of directly binding visuals to raw data.

## Column Model

Main contracts:

- `IColumn` and `IColumn<TModel>`
- `IColumns`

Built-in column types:

- `TextColumn<TModel, TValue>`
- `CheckBoxColumn<TModel>`
- `TemplateColumn<TModel>`
- `HierarchicalExpanderColumn<TModel>`

Column responsibilities:

- map model values into cell models (`CreateCell`)
- provide sorting comparison (`GetComparison`)
- expose sizing metadata (`Width`, `ActualWidth`)

## Row Model

Main contracts:

- `IRow` and `IRow<TModel>`
- `IRows`

Row responsibilities:

- expose model instance for each visible row
- map between model indexes (`IndexPath`) and visible indexes (`int`)
- realize/unrealize cell models for presenters

Hierarchical row models additionally track:

- expand/collapse state
- indentation depth
- child row collections

## Cell Model

Main contracts:

- `ICell`
- `ITextCell`
- `IExpanderCell`

Cell responsibilities:

- expose current value
- describe edit capability (`CanEdit`, `EditGestures`)
- propagate value changes back to model when editable

## Column Options and Editing

Common options come from `ColumnOptions<TModel>`:

- sorting behavior and custom comparers
- width constraints
- edit gestures (`BeginEditGestures`)

Specialized options:

- `TextColumnOptions<TModel>`: format, wrapping, trimming, alignment, text-search
- `TemplateColumnOptions<TModel>`: text-search selector

## Realization Lifecycle (Important)

The UI does not create a permanent visual for every row/cell.

- Presenters request row/cell models for visible range.
- Controls are realized and recycled as you scroll.
- Lifecycle hooks (`CellPrepared`, `CellClearing`, `RowPrepared`, `RowClearing`) let you integrate custom behavior safely.

Treat visuals as ephemeral; treat source/model state as canonical.

## User-Facing vs Infrastructure APIs

Typically app code uses:

- source types (`FlatTreeDataGridSource`, `HierarchicalTreeDataGridSource`)
- built-in column types
- selection models

Infrastructure-level contracts are still public and useful for advanced customization:

- `IUpdateColumnLayout`
- presenter primitives and element factory
- custom row/cell pipelines

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.Models.TreeDataGrid.CheckBoxCell>
- <xref:Avalonia.Controls.Models.TreeDataGrid.ColumnBase`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.ColumnBase`2>
- <xref:Avalonia.Controls.Models.TreeDataGrid.ColumnList`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.ExpanderCell`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.ICell>
- <xref:Avalonia.Controls.Models.TreeDataGrid.IColumn`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.IColumn>
- <xref:Avalonia.Controls.Models.TreeDataGrid.IColumns>
- <xref:Avalonia.Controls.Models.TreeDataGrid.IIndentedRow>
- <xref:Avalonia.Controls.Models.TreeDataGrid.IModelIndexableRow>
- <xref:Avalonia.Controls.Models.TreeDataGrid.IRow`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.IRow>
- <xref:Avalonia.Controls.Models.TreeDataGrid.IRows>
- <xref:Avalonia.Controls.Models.TreeDataGrid.ITextCell>
- <xref:Avalonia.Controls.Models.TreeDataGrid.ITextSearchableColumn`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.IUpdateColumnLayout>
- <xref:Avalonia.Controls.Models.TreeDataGrid.TemplateCell>
- <xref:Avalonia.Controls.Models.TreeDataGrid.TextCell`1>

## Related

- [Text Column](../guides/column-text.md)
- [CheckBox Column](../guides/column-checkbox.md)
- [Template Column](../guides/column-template.md)
- [Hierarchical Expander Column](../guides/column-expander.md)
- [Sorting and Column Widths](../guides/sorting-and-column-widths.md)
- [TreeDataGrid Glossary](glossary.md)
