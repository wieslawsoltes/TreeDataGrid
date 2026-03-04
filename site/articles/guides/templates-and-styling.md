---
title: "Templates and Styling"
---

# Templates and Styling

TreeDataGrid supports standard Avalonia styling and templating patterns.

## Data Templates for `TemplateColumn`

Define templates in `TreeDataGrid.Resources` and reference by key.

```xml
<TreeDataGrid Source="{Binding Source}">
  <TreeDataGrid.Resources>
    <DataTemplate x:Key="RegionCell" DataType="m:Country">
      <TextBlock Text="{Binding Region}"/>
    </DataTemplate>

    <DataTemplate x:Key="RegionEditCell" DataType="m:Country">
      <ComboBox ItemsSource="{x:Static m:Countries.Regions}"
                SelectedItem="{Binding Region}"/>
    </DataTemplate>
  </TreeDataGrid.Resources>
</TreeDataGrid>
```

## Styling Rows, Cells, Headers

```xml
<TreeDataGrid.Styles>
  <Style Selector="TreeDataGrid TreeDataGridRow:nth-child(2n)">
    <Setter Property="Background" Value="#20808080"/>
  </Style>

  <Style Selector="TreeDataGrid :is(TreeDataGridCell):nth-last-child(1)">
    <Setter Property="TextBlock.FontWeight" Value="Bold"/>
  </Style>

  <Style Selector="TreeDataGrid TreeDataGridColumnHeader:nth-last-child(1)">
    <Setter Property="TextBlock.FontWeight" Value="Bold"/>
  </Style>
</TreeDataGrid.Styles>
```

## Template Column with Rich Cell Content

You can compose images, icons, and editors in template cells.

Pattern used in sample:

- display template for read mode
- edit template for edit mode
- multibinding converter for dynamic icon state

## Indentation and Hierarchy Visuals

`Avalonia.Controls.Converters.IndentConverter` can be used when creating custom hierarchical templates that need indentation thickness from depth.

## Theme Requirement

TreeDataGrid style include must be present in `App.axaml`:

```xml
<StyleInclude Source="avares://Avalonia.Controls.TreeDataGrid/Themes/Fluent.axaml"/>
```

## Performance Tips

- keep templates shallow for large datasets
- avoid expensive bindings/converters in hot cells
- prefer built-in text/checkbox columns when rich templates are unnecessary

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.Converters.IndentConverter>

## Related

- [Template Column Guide](column-template.md)
- [Events and User Interaction](events-and-user-interaction.md)
- [Primitives Overview](../advanced/primitives-overview.md)
- [TreeDataGrid Glossary](../concepts/glossary.md)
- [Troubleshooting Guide](troubleshooting.md)
