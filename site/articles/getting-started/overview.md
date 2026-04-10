---
title: "Getting Started with TreeDataGrid"
---

# Getting Started with TreeDataGrid

TreeDataGrid combines tree navigation and tabular columns in a single control. In the v12-style API there are two supported ways to configure it:

- `ItemsSource` plus declarative `TreeDataGrid*Column` elements in XAML
- `Source` plus a `FlatTreeDataGridSource<TModel>` or `HierarchicalTreeDataGridSource<TModel>` built in code

## Choose an Approach

- Use `ItemsSource` when you want the control definition to stay in XAML and you do not need advanced source-only features.
- Use `Source` when you need filtering, programmatic expand/collapse, or more involved runtime composition.

## Useful Properties

- `ItemsSource`: binds a collection for declarative XAML columns
- `Source`: binds a `FlatTreeDataGridSource<TModel>` or `HierarchicalTreeDataGridSource<TModel>`
- `SelectionMode`: chooses row or cell selection, with optional multiple selection
- `CanUserResizeColumns`: controls column resizing, default `false`
- `CanUserSortColumns`: controls header-click sorting, default `true`

## Recommended Path

1. [Installation](installation.md)
2. [Quickstart: Flat](quickstart-flat.md)
3. [Quickstart: Hierarchical](quickstart-hierarchical.md)
4. [Breaking Changes v12](../breaking-changes-v12.md)
5. [Column Types](../column-types.md)

## Next

- If you are migrating older code, read [Breaking Changes v12](../breaking-changes-v12.md) before changing your view models.
- If you are starting from markup, continue with [XAML Overview](../xaml/overview.md).
- If you need filtering or programmatic tree operations, continue with [Filtering](../filtering.md) and [Expand and Collapse](../expand-and-collapse.md).
