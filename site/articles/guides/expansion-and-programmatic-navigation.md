---
title: "Expansion and Programmatic Navigation"
---

# Expansion and Programmatic Navigation

This guide focuses on hierarchical navigation and expansion control APIs.

## Expand/Collapse APIs

`HierarchicalTreeDataGridSource<TModel>` provides:

- `Expand(IndexPath)`
- `Collapse(IndexPath)`
- `ExpandAll()`
- `CollapseAll()`
- `ExpandCollapseRecursive(Func<TModel, bool>)`
- `ExpandCollapseRecursive(HierarchicalRow<TModel>, Func<TModel, bool>)`

Example:

```csharp
Source.Expand(new IndexPath(0));
Source.Expand(new IndexPath(0, 2));
Source.Collapse(new IndexPath(0, 1));
```

## Predicate Expansion

```csharp
Source.ExpandCollapseRecursive(model => model.Name.StartsWith("A"));
```

This is useful for search/filter style expansion.

## Expansion Events

Lifecycle events on source:

- `RowExpanding`
- `RowExpanded`
- `RowCollapsing`
- `RowCollapsed`

Use them for telemetry, lazy loading markers, or command state updates.

## Resolve Model by Path

```csharp
if (Source.TryGetModelAt(new IndexPath(0, 1, 3), out var model))
{
    // model found
}
```

Use this for actions driven from saved `IndexPath` values.

## Scroll to a Hierarchical Row

```csharp
var path = new IndexPath(0, 1, 3);
Source.Expand(path[..^1]);

var rowIndex = Source.Rows.ModelIndexToRowIndex(path);
if (rowIndex >= 0)
    grid.RowsPresenter?.BringIntoView(rowIndex);
```

## Parent/Child Navigation Helpers

`IndexPath` helpers:

- parent: `path[..^1]`
- child append: `path.Append(childIndex)`
- relationships: `IsAncestorOf`, `IsParentOf`

## Persist Expanded State

With `HierarchicalExpanderColumn<TModel>` use `isExpandedSelector` to bind expansion state to model property.

This is recommended when tree state should survive rebind/reload.

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.Models.TreeDataGrid.HierarchicalRow`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.HierarchicalRows`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.IExpander>
- <xref:Avalonia.Controls.Models.TreeDataGrid.IExpanderCell>
- <xref:Avalonia.Controls.Models.TreeDataGrid.IExpanderColumn`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.IExpanderRow`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.IExpanderRowController`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.RowEventArgs`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.RowEventArgs>

## Related

- [Hierarchical Source Guide](sources-hierarchical.md)
- [Hierarchical Expander Column Guide](column-expander.md)
- [Indexing and Addressing](../concepts/indexing-and-addressing.md)
- [Troubleshooting Guide](troubleshooting.md)
