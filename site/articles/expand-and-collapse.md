---
title: "Expand and Collapse"
---

# Expand and Collapse

Programmatic tree operations live on `HierarchicalTreeDataGridSource<TModel>`.

## Basic Operations

```csharp
Source.Expand(new IndexPath(0));
Source.Collapse(new IndexPath(0));
Source.ExpandAll();
Source.CollapseAll();
```

You can also expand or collapse recursively:

```csharp
Source.ExpandCollapseRecursive(x => x.IsExpandedByDefault);
```

## Row Events

Hierarchical sources raise:

- `RowExpanding`
- `RowExpanded`
- `RowCollapsing`
- `RowCollapsed`

These events now use `TreeDataGridRowModelEventArgs`:

```csharp
Source.RowExpanded += (_, e) =>
{
    var model = (Person?)e.Row.Model;
    var path = e.Row.ModelIndexPath;
};
```

## When to Use This API

Use the `Source` workflow when you need programmatic control over the expansion state. Declarative `ItemsSource` grids are useful for markup-driven tree layouts, but the explicit source remains the path for advanced runtime tree management.
