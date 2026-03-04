---
title: "Getting Started with TreeDataGrid"
---

# Getting Started with TreeDataGrid

TreeDataGrid is an Avalonia control that combines:

- DataGrid-style columns
- Tree-style hierarchical expansion
- Strongly typed source and column models

Use TreeDataGrid when you need column-based presentation with optional parent/child data and large datasets.

## What You Will Build

By the end of Getting Started, you will have:

- A working `TreeDataGrid` in XAML
- A view model exposing a `FlatTreeDataGridSource<TModel>` or `HierarchicalTreeDataGridSource<TModel>`
- Strongly typed columns (`TextColumn`, `TemplateColumn`, `CheckBoxColumn`, `HierarchicalExpanderColumn`)
- Working row or cell selection

## Learning Path

1. [Installation](installation.md)
2. [Quickstart: Flat TreeDataGrid](quickstart-flat.md)
3. [Quickstart: Hierarchical TreeDataGrid](quickstart-hierarchical.md)
4. [Architecture and Data Flow](../concepts/architecture-and-data-flow.md)
5. [Columns, Cells, and Rows](../concepts/columns-cells-rows.md)
6. [Selection Models](../concepts/selection-models.md)
7. [TreeDataGrid Glossary](../concepts/glossary.md)

## Flat vs Hierarchical

Choose your source type first:

- `FlatTreeDataGridSource<TModel>`: table-like data, no children
- `HierarchicalTreeDataGridSource<TModel>`: rows may contain nested children

You can still use multiple columns in both modes.

## Key Idea

TreeDataGrid separates what to show from how to show it:

- Source model (`ITreeDataGridSource`) defines rows, columns, selection, sorting
- `TreeDataGrid` control realizes models into visual rows/cells with virtualization

This keeps view models strongly typed and UI behavior predictable.

## Next

- Start with [Installation](installation.md)
- If package/theme are already configured, jump directly to [Quickstart: Flat TreeDataGrid](quickstart-flat.md)
- Use [Troubleshooting Guide](../guides/troubleshooting.md) if setup diverges from expected behavior.

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.TreeDataGrid>
- <xref:Avalonia.Controls.FlatTreeDataGridSource`1>
- <xref:Avalonia.Controls.HierarchicalTreeDataGridSource`1>

## Related

- [Installation](installation.md)
- [Quickstart: Flat TreeDataGrid](quickstart-flat.md)
- [Quickstart: Hierarchical TreeDataGrid](quickstart-hierarchical.md)
- [TreeDataGrid Glossary](../concepts/glossary.md)
