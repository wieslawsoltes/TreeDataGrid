---
title: "XAML Samples Walkthrough"
---

# XAML Samples Walkthrough

This article explains the TreeDataGrid XAML patterns used in `samples/TreeDataGridDemo`.

## Application-Level Theme Include

`App.axaml` configures Fluent theme and TreeDataGrid styles:

```xml
<Application.Styles>
  <FluentTheme/>
  <StyleInclude Source="avares://Avalonia.Controls.TreeDataGrid/Themes/Fluent.axaml"/>
</Application.Styles>
```

The sample also includes commented resource overrides for TreeDataGrid-specific brush keys.

## Window-Level XAML and Compiled Bindings

`MainWindow.axaml` enables typed compiled bindings:

```xml
<Window x:CompileBindings="True"
        x:DataType="vm:MainWindowViewModel">
```

This keeps XAML bindings strongly typed and easier to refactor.

## Per-Grid Template Resources

Sample tabs define `TreeDataGrid.Resources` with keyed templates:

```xml
<TreeDataGrid.Resources>
  <DataTemplate x:Key="RegionCell" DataType="m:Country">
    <TextBlock Text="{Binding Region}"/>
  </DataTemplate>
  <DataTemplate x:Key="RegionEditCell" DataType="m:Country">
    <ComboBox ItemsSource="{x:Static m:Countries.Regions}"
              SelectedItem="{Binding Region}"/>
  </DataTemplate>
</TreeDataGrid.Resources>
```

View models use the same keys through `TemplateColumn<TModel>` constructors.

## Per-Grid Styles and Selectors

The sample applies style selectors in `TreeDataGrid.Styles` to:

- stripe rows
- make a specific cell column bold
- style a specific header column

This is useful for local visual behavior without replacing global control themes.

## XAML Interaction Wiring

The sample declares interaction options in markup:

```xml
<TreeDataGrid AutoDragDropRows="True"
              RowDragStarted="DragDrop_RowDragStarted"
              RowDragOver="DragDrop_RowDragOver"/>
```

## Resource Keys from Model Layer

Sample view models pass template key names into columns:

```csharp
new TemplateColumn<Country>("Region", "RegionCell", "RegionEditCell")
```

This key is resolved at runtime using the realized template-cell control as the
resource lookup anchor (within the grid's logical/resource scope).

## Troubleshooting

- DataTemplate key exists but still not found
Cause: key is not visible in the lookup scope of the grid/cell anchor.
Fix: place template in `TreeDataGrid.Resources`, parent logical resources, or application resources.

- Template renders but data is empty
Cause: template `DataType` or binding path does not match model shape.
Fix: verify model type and binding members against the row model used by the column.

- Editing starts and throws template-key exception
Cause: editing template key was provided in model code but corresponding
`DataTemplate` key is missing in resources.
Fix: add the keyed editing template or use the non-editing constructor path.

## API Coverage Checklist

- <xref:Avalonia.Controls.TreeDataGrid>
- <xref:Avalonia.Controls.Models.TreeDataGrid.TemplateColumn`1>
- <xref:Avalonia.Controls.TreeDataGridRowDragStartedEventArgs>
- <xref:Avalonia.Controls.TreeDataGridRowDragEventArgs>

## Related

- [XAML Usage Overview](overview.md)
- [Template Resource Keys from Model](template-resource-keys-from-model.md)
- [Drag and Drop Rows](../guides/drag-and-drop-rows.md)
- [Events and User Interaction](../guides/events-and-user-interaction.md)
