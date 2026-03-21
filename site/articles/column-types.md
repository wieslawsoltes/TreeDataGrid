---
title: "Article Moved"
---

# Column Types

The v12-style API exposes non-generic column types in `Avalonia.Controls`.

## Available Columns

- `TreeDataGridTextColumn`: text display and editing through a binding
- `TreeDataGridCheckBoxColumn`: boolean or nullable-boolean values
- `TreeDataGridTemplateColumn`: custom cell templates, either inline or by resource key
- `TreeDataGridHierarchicalExpanderColumn`: the tree expander wrapper for hierarchical data
- `TreeDataGridRowHeaderColumn`: row headers and row numbers

## XAML Example

```xml
<TreeDataGrid ItemsSource="{Binding People}">
  <TreeDataGridRowHeaderColumn Width="48"/>
  <TreeDataGridTextColumn Header="Name"
                          Binding="{Binding Name}"/>
  <TreeDataGridCheckBoxColumn Header="Active"
                              Binding="{Binding IsActive}"/>
  <TreeDataGridTemplateColumn Header="Status"
                              TextSearchBinding="{Binding Title}">
    <TreeDataGridTemplateColumn.CellTemplate>
      <DataTemplate>
        <TextBlock Text="{Binding Title}"/>
      </DataTemplate>
    </TreeDataGridTemplateColumn.CellTemplate>
  </TreeDataGridTemplateColumn>
</TreeDataGrid>
```

## Code Example

```csharp
Source = new FlatTreeDataGridSource<Person>(People)
    .WithRowHeaderColumn()
    .WithTextColumn(x => x.Name)
    .WithCheckBoxColumn("Active", x => x.IsActive)
    .WithTemplateColumnFromResourceKeys("Status", "StatusCell");
```

## Notes

- Text columns infer two-way editing automatically when the selected member is writable.
- Template columns use `TextSearchBinding` instead of the older lambda-based text-search selector.
- Hierarchical grids should contain exactly one expander column.
