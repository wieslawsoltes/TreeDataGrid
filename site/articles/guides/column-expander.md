---
title: "Hierarchical Expander Column Guide"
---

# Hierarchical Expander Column Guide

`HierarchicalExpanderColumn<TModel>` wraps another column and adds tree expansion affordances.

Use it only with `HierarchicalTreeDataGridSource<TModel>`.

## Basic Constructor

```csharp
new HierarchicalExpanderColumn<Person>(
    inner: new TextColumn<Person, string>("Name", x => x.Name),
    childSelector: x => x.Children)
```

Parameters:

- `inner`: how the column content is rendered
- `childSelector`: returns children for each model

## Advanced Constructor Selectors

You can provide optional selectors to optimize behavior and persist expansion state.

```csharp
new HierarchicalExpanderColumn<FileNode>(
    inner: new TemplateColumn<FileNode>("Name", "FileNameCell", "FileNameEditCell"),
    childSelector: x => x.Children,
    hasChildrenSelector: x => x.HasChildren,
    isExpandedSelector: x => x.IsExpanded)
```

`hasChildrenSelector` avoids expensive child materialization for expander visibility checks.

`isExpandedSelector` syncs UI expanded state with model property.

## Sorting and Width

The expander column delegates width and sorting behavior to its inner column.

That means:

- sorting capability is inherited from `inner.GetComparison(...)`
- width changes operate on inner column layout

## Common Pitfalls

- Creating hierarchical source without an expander column causes runtime errors.
- Adding more than one expander column is not allowed by source.
- Removing/replacing/moving expander column after add is not allowed.

## Best Practices

- Use `TemplateColumn` as inner column for rich file/tree visuals.
- Provide `hasChildrenSelector` when child loading is lazy or expensive.
- Bind `isExpandedSelector` if expanded state should persist across refresh/rebind.

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.Models.TreeDataGrid.HierarchicalExpanderColumn`1>

## Related

- [Hierarchical Source Guide](sources-hierarchical.md)
- [Expansion and Programmatic Navigation](expansion-and-programmatic-navigation.md)
- [Template Column Guide](column-template.md)
- [Troubleshooting Guide](troubleshooting.md)
