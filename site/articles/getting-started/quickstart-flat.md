---
title: "Quickstart: Flat TreeDataGrid"
---

# Quickstart: Flat TreeDataGrid

This quickstart shows the simplest v12-style setup: bind a collection to `ItemsSource` and declare columns directly in XAML.

## Model

```csharp
public class Person
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int Age { get; set; }
    public bool IsActive { get; set; }
}
```

## View Model

```csharp
using System.Collections.ObjectModel;

public class MainWindowViewModel
{
    public ObservableCollection<Person> People { get; } = new()
    {
        new Person { FirstName = "Eleanor", LastName = "Pope", Age = 32, IsActive = true },
        new Person { FirstName = "Jeremy", LastName = "Navarro", Age = 47, IsActive = true },
        new Person { FirstName = "Lailah", LastName = "Velazquez", Age = 28, IsActive = false },
    };
}
```

## XAML

```xml
<TreeDataGrid ItemsSource="{Binding People}"
              SelectionMode="Row,Multiple">
  <TreeDataGridTextColumn Header="First Name"
                          Binding="{Binding FirstName}"/>
  <TreeDataGridTextColumn Header="Last Name"
                          Binding="{Binding LastName}"/>
  <TreeDataGridTextColumn Header="Age"
                          Binding="{Binding Age}"/>
  <TreeDataGridCheckBoxColumn Header="Active"
                              Binding="{Binding IsActive}"/>
</TreeDataGrid>
```

## Code-Behind Alternative

If you prefer to build the grid in code, create a `FlatTreeDataGridSource<Person>` and use the fluent helpers:

```csharp
Source = new FlatTreeDataGridSource<Person>(People)
    .WithTextColumn("First Name", x => x.FirstName)
    .WithTextColumn("Last Name", x => x.LastName)
    .WithTextColumn(x => x.Age)
    .WithCheckBoxColumn("Active", x => x.IsActive);
```

## Next

- Add a template column: [Column Types](../column-types.md)
- Switch to cell selection: [Selection Modes](../selection-modes.md)
- Add filtering: [Filtering](../filtering.md)
