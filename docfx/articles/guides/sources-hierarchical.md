# Hierarchical Source Guide

Use `HierarchicalTreeDataGridSource<TModel>` when rows can contain children.

It provides expand/collapse behavior, hierarchical index mapping, and row expansion lifecycle events.

## When to Use Hierarchical Source

Use hierarchical source when:

- model has child collections
- you need expand/collapse navigation
- you need parent/child row drag/drop positions (`Before`, `After`, `Inside`)

## Basic Setup

```csharp
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;

public class Node
{
    public string Name { get; set; } = string.Empty;
    public ObservableCollection<Node> Children { get; } = new();
}

public class ExplorerViewModel
{
    private readonly ObservableCollection<Node> _roots;

    public ExplorerViewModel(ObservableCollection<Node> roots)
    {
        _roots = roots;

        Source = new HierarchicalTreeDataGridSource<Node>(_roots)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<Node>(
                    new TextColumn<Node, string>("Name", x => x.Name),
                    x => x.Children),
            },
        };
    }

    public HierarchicalTreeDataGridSource<Node> Source { get; }
}
```

## Expander Column Requirement

A hierarchical source requires exactly one expander column (`IExpanderColumn<TModel>`).

Constraints enforced by source:

- only one expander column allowed
- expander column cannot be removed/replaced/moved after being added
- accessing row pipeline without expander column throws

## Expansion APIs

Programmatic expansion/collapse:

```csharp
Source.Expand(new IndexPath(0));
Source.Collapse(new IndexPath(0, 2));
Source.ExpandAll();
Source.CollapseAll();
```

Predicate-based recursion:

```csharp
Source.ExpandCollapseRecursive(model => ShouldExpand(model));
```

From a specific row:

```csharp
Source.ExpandCollapseRecursive(row, model => model.Name.StartsWith("A"));
```

## Expansion Lifecycle Events

Useful hooks:

- `RowExpanding`
- `RowExpanded`
- `RowCollapsing`
- `RowCollapsed`

Example:

```csharp
Source.RowExpanded += (_, e) =>
{
    var row = e.Row;
    // telemetry, lazy-load marker updates, etc.
};
```

## Resolve Model by Index

```csharp
if (Source.TryGetModelAt(new IndexPath(0, 1, 3), out var model))
{
    // model resolved
}
```

Use this for commands that operate from selection/index paths.

## Sorting

```csharp
using System.ComponentModel;

Source.SortBy(Source.Columns[0], ListSortDirection.Ascending);
```

Behavior:

- hierarchical rows are re-flattened after sort
- `Sorted` event is raised
- row drag/drop move is disabled while sorted

## Hierarchical Drag/Drop Semantics

`DragDropRows(...)` supports:

- `Before`
- `After`
- `Inside`

Constraints:

- move-only effects
- unsorted source
- target collection must be mutable `IList<TModel>`

When dropping `Inside`, target row children collection is used.

## Persisting Expanded State

`HierarchicalExpanderColumn<TModel>` can bind expanded state to model property by passing `isExpandedSelector`.

```csharp
new HierarchicalExpanderColumn<FileNode>(
    inner: new TextColumn<FileNode, string>("Name", x => x.Name),
    childSelector: x => x.Children,
    hasChildrenSelector: x => x.HasChildren,
    isExpandedSelector: x => x.IsExpanded)
```

This pattern is ideal for file tree/explorer scenarios.

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.HierarchicalTreeDataGridSource`1>

## Related

- [Hierarchical Expander Column](column-expander.md)
- [Expansion and Programmatic Navigation](expansion-and-programmatic-navigation.md)
- [Indexing and Addressing](../concepts/indexing-and-addressing.md)
- [Troubleshooting Guide](troubleshooting.md)
