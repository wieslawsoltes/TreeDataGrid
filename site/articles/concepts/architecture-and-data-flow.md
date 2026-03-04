---
title: "Architecture and Data Flow"
---

# Architecture and Data Flow

TreeDataGrid is designed around a model-first architecture:

- Sources define data behavior and interactions
- Row/column/cell models represent visible state
- Presenters and primitives realize models into UI

## Architecture Layers

Use these layers to map responsibilities from data source to visual output.

## 1. Source Layer

Primary contracts:

- `ITreeDataGridSource`
- `FlatTreeDataGridSource<TModel>`
- `HierarchicalTreeDataGridSource<TModel>`

Responsibilities:

- expose `Columns`, `Rows`, and `Selection`
- maintain `Items` input collection
- apply sorting (`SortBy`)
- perform row drag/drop (`DragDropRows`)

## 2. Data Model Layer

Primary contracts:

- columns: `IColumns`, `IColumn`, `IColumn<TModel>`
- rows: `IRows`, `IRow`, `IRow<TModel>`
- cells: `ICell`, `ITextCell`, `IExpanderCell`

Responsibilities:

- type-safe value projection from model to cells
- edit behavior and edit gestures
- width measurement and column layout
- row index translation between model index and visible index

## 3. Control / Realization Layer

Primary types:

- `TreeDataGrid`
- presenters (`TreeDataGridRowsPresenter`, `TreeDataGridCellsPresenter`, `TreeDataGridColumnHeadersPresenter`)
- primitives (`TreeDataGridRow`, `TreeDataGridCell`, specialized cell controls)

Responsibilities:

- virtualize visual elements for current viewport
- realize and recycle row/cell controls through element factory
- route user input to selection interaction models
- raise lifecycle events (`CellPrepared`, `RowPrepared`, `CellClearing`, `RowClearing`)

## End-to-End Data Flow

1. App provides `Items` to source.
2. Source wraps items (`TreeDataGridItemsSourceView`) and exposes row/column model collections.
3. Control binds to source and listens for model/property changes.
4. Presenters realize only visible rows/cells.
5. User actions (sort/select/edit/expand/drag) update source or selection model.
6. Model notifications trigger UI updates.

## Sorting and Selection Interaction

- Sorting is requested by `TreeDataGrid` (header click) via `ITreeDataGridSource.SortBy(...)`.
- Selection behavior depends on current `Selection` object:
  - row selection model
  - cell selection model
- `TreeDataGrid` delegates input handling to `ITreeDataGridSelectionInteraction`.

## Why This Matters

This separation keeps concerns clear:

- View models stay strongly typed and testable.
- UI layer can virtualize aggressively.
- Behavior (sorting/selection/editing) is centralized in models instead of ad-hoc UI code.

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.ITreeDataGridSource>
- <xref:Avalonia.Controls.TreeDataGrid>
- <xref:Avalonia.Controls.Models.TreeDataGrid.IColumns>
- <xref:Avalonia.Controls.Models.TreeDataGrid.IRows>

## Related

- [Columns, Cells, and Rows](columns-cells-rows.md)
- [Selection Models](selection-models.md)
- [Indexing and Addressing](indexing-and-addressing.md)
- [TreeDataGrid Glossary](glossary.md)
- [Troubleshooting Guide](../guides/troubleshooting.md)
