---
title: "TreeDataGrid Glossary"
---

# TreeDataGrid Glossary

This glossary defines core terms used across TreeDataGrid documentation.

## A

### `AutoDragDropRows`

A `TreeDataGrid` control property that enables built-in row drag/drop behavior when supported by the source and current sort state.

## B

### `BeginEditGestures`

A flags enum that controls how a cell enters edit mode, for example `F2`, `Tap`, `DoubleTap`, and `WhenSelected`.

## C

### Cell Model

An object implementing [ICell](xref:Avalonia.Controls.Models.TreeDataGrid.ICell) (or specialized interfaces such as [ITextCell](xref:Avalonia.Controls.Models.TreeDataGrid.ITextCell) and [IExpanderCell](xref:Avalonia.Controls.Models.TreeDataGrid.IExpanderCell)) representing a cell's data/edit state.

### `CellIndex`

A struct that addresses a cell by `(ColumnIndex, RowIndex)` where `RowIndex` is an [IndexPath](xref:Avalonia.Controls.IndexPath).

### Column Model

An object implementing [IColumn](xref:Avalonia.Controls.Models.TreeDataGrid.IColumn), typically one of the built-in column types (`TextColumn`, `TemplateColumn`, `CheckBoxColumn`, `HierarchicalExpanderColumn`).

## E

### Element Factory

[TreeDataGridElementFactory](xref:Avalonia.Controls.Primitives.TreeDataGridElementFactory), responsible for creating and recycling row/cell/header controls.

### Expander Column

A hierarchical column implementing [`IExpanderColumn<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.IExpanderColumn`1), usually [`HierarchicalExpanderColumn<TModel>`](xref:Avalonia.Controls.Models.TreeDataGrid.HierarchicalExpanderColumn`1), used to display expand/collapse affordances and navigate child models.

## I

### `IndexPath`

A hierarchical model index path (for example `0`, `0,1`, `0,1,2`) used as the canonical model address in selection, expansion, drag/drop, and model lookup operations.

### `ItemsSourceView`

[TreeDataGridItemsSourceView](xref:Avalonia.Controls.TreeDataGridItemsSourceView) and [`TreeDataGridItemsSourceView<T>`](xref:Avalonia.Controls.TreeDataGridItemsSourceView`1), wrappers that normalize item collection access and notifications.

## M

### Model Index

A position in the source hierarchy represented by `IndexPath`. This remains the canonical identity even when visible row positions change.

## P

### Presenter

A virtualized visual host (rows, cells, headers) responsible for realizing and recycling controls for the current viewport.

## R

### Realization

The process of materializing a visual control for a model row/cell/header in the current viewport.

### Row Model

An object implementing [IRow](xref:Avalonia.Controls.Models.TreeDataGrid.IRow), accessed through [IRows](xref:Avalonia.Controls.Models.TreeDataGrid.IRows).

### Row Index (Visible Index)

An `int` index into currently visible rows. It can differ from model index when sorting or hierarchy expansion changes layout.

## S

### Selection Interaction

The input-behavior adapter interface [ITreeDataGridSelectionInteraction](xref:Avalonia.Controls.Selection.ITreeDataGridSelectionInteraction), used by `TreeDataGrid` to delegate keyboard/pointer/text selection handling.

### Source

A data source implementing [ITreeDataGridSource](xref:Avalonia.Controls.ITreeDataGridSource), typically [`FlatTreeDataGridSource<TModel>`](xref:Avalonia.Controls.FlatTreeDataGridSource`1) or [`HierarchicalTreeDataGridSource<TModel>`](xref:Avalonia.Controls.HierarchicalTreeDataGridSource`1).

## T

### Typed Binding

Strongly typed binding APIs in `Avalonia.Experimental.Data*`, including [`TypedBinding<TIn>`](xref:Avalonia.Experimental.Data.TypedBinding`1) and [`TypedBinding<TIn,TOut>`](xref:Avalonia.Experimental.Data.TypedBinding`2).

## V

### Virtualization

The strategy of realizing only visible rows/cells and recycling visuals outside viewport to reduce memory/CPU costs.

## Troubleshooting

- Term usage differs between API and article
Cause: one concept can appear under different names across layers (model vs visual).
Fix: use xref links in this glossary as canonical anchors.

- New API term is missing here
Cause: glossary lags behind API/reference updates.
Fix: add the term and link it to the namespace reference article.

## API Coverage Checklist

- <xref:Avalonia.Controls.TreeDataGrid>
- <xref:Avalonia.Controls.IndexPath>
- <xref:Avalonia.Controls.CellIndex>

## Related

- [Architecture and Data Flow](architecture-and-data-flow.md)
- [Indexing and Addressing](indexing-and-addressing.md)
- [Columns, Cells, and Rows](columns-cells-rows.md)
- [Troubleshooting Guide](../guides/troubleshooting.md)
