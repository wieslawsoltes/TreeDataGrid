---
title: "TreeDataGrid v12 Breaking Changes"
---

# TreeDataGrid v12 Breaking Changes

TreeDataGrid v12 introduces a new XAML-first workflow and a new fluent source API. The most important changes are:

- Generic column types were replaced by non-generic `TreeDataGrid*Column` types.
- `ItemsSource` plus declarative columns is now a first-class setup path.
- Code-based sources now use fluent builders such as `WithTextColumn` and `WithHierarchicalExpanderColumn`.
- Column option classes moved to top-level `*CreateOptions` types and `CanUserResizeColumn` became `CanUserResize`.
- Text-column text search now defaults to `true`.
- `SelectionMode` and control-level `SelectionChanged` were added to `TreeDataGrid`.
- Hierarchical row events now use `TreeDataGridRowModelEventArgs`.
- Template-column text search now uses `TextSearchBinding`.
- `FlatTreeDataGridSource<TModel>` and `HierarchicalTreeDataGridSource<TModel>` are sealed.

## Typical Migrations

### Text columns

```csharp
source.WithTextColumn("Name", x => x.Name, o => o.Width = GridLength.Star);
```

### Check box columns

```csharp
source.WithCheckBoxColumn("Active", x => x.IsActive);
```

### Template columns

```csharp
source.WithTemplateColumnFromResourceKeys("Status", "StatusCell", "StatusEditCell", o =>
{
    o.TextSearchBinding = new Avalonia.Data.Binding("Name");
});
```

### Hierarchical expander columns

```csharp
source.WithHierarchicalExpanderTextColumn(x => x.Name, x => x.Children, o =>
{
    o.HasChildren = x => x.HasChildren;
    o.IsExpanded = x => x.IsExpanded;
});
```

## XAML Migration

Older code typically bound only `Source`. In v12-style code you can declare columns directly on the control:

```xml
<TreeDataGrid ItemsSource="{Binding People}">
  <TreeDataGridTextColumn Header="Name"
                          Binding="{Binding Name}"/>
</TreeDataGrid>
```

## Related

- [Getting Started Overview](getting-started/overview.md)
- [Column Types](column-types.md)
- [Selection Modes](selection-modes.md)
