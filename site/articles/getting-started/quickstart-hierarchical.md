---
title: "Quickstart: Hierarchical TreeDataGrid"
---

# Quickstart: Hierarchical TreeDataGrid

This quickstart shows both hierarchical setup styles. The key rule is unchanged: a hierarchical grid needs one expander column.

## Model

```csharp
using System.Collections.ObjectModel;

public class Person
{
    public string? Name { get; set; }
    public int Age { get; set; }
    public bool IsExpanded { get; set; }
    public ObservableCollection<Person> Children { get; } = new();
    public bool HasChildren => Children.Count > 0;
}
```

## XAML `ItemsSource` Setup

```xml
<TreeDataGrid ItemsSource="{Binding People}">
  <TreeDataGridHierarchicalExpanderColumn Header="Name"
                                          ChildrenBinding="{Binding Children}"
                                          HasChildrenBinding="{Binding HasChildren}"
                                          IsExpandedBinding="{Binding IsExpanded}">
    <TreeDataGridTextColumn Binding="{Binding Name}"/>
  </TreeDataGridHierarchicalExpanderColumn>
  <TreeDataGridTextColumn Header="Age"
                          Binding="{Binding Age}"/>
</TreeDataGrid>
```

## Code `Source` Setup

Use the fluent source API when you need runtime-only features such as filtering or programmatic expand/collapse:

```csharp
Source = new HierarchicalTreeDataGridSource<Person>(People)
    .WithHierarchicalExpanderTextColumn(x => x.Name, x => x.Children, o =>
    {
        o.HasChildren = x => x.HasChildren;
        o.IsExpanded = x => x.IsExpanded;
    })
    .WithTextColumn(x => x.Age);

Source.Expand(new IndexPath(0));
```

## Next

- Programmatically expand rows: [Expand and Collapse](../expand-and-collapse.md)
- Switch selection behavior: [Selection Modes](../selection-modes.md)
- Migrate older expander code: [Breaking Changes v12](../breaking-changes-v12.md)
