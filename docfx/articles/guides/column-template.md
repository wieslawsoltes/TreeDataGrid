# Template Column Guide

`TemplateColumn<TModel>` renders cells through `DataTemplate` and optional editing template.

Use it when `TextColumn`/`CheckBoxColumn` are not enough.

## Constructor Options

Choose one of the following constructor families based on how templates are provided.

## 1. Template Objects

```csharp
new TemplateColumn<Person>(
    header: "Selected",
    cellTemplate: new FuncDataTemplate<Person>((_, _) => new CheckBox
    {
        [!CheckBox.IsCheckedProperty] = new Binding("IsSelected"),
    }))
```

## 2. Template Resource Keys

```csharp
new TemplateColumn<Person>("Region", "RegionCell", "RegionEditCell")
```

This resolves templates from control resources at runtime.

## XAML Template Example

```xml
<TreeDataGrid Source="{Binding Source}">
  <TreeDataGrid.Resources>
    <DataTemplate x:Key="RegionCell">
      <TextBlock Text="{Binding Region}"/>
    </DataTemplate>

    <DataTemplate x:Key="RegionEditCell">
      <ComboBox ItemsSource="{x:Static m:Countries.Regions}"
                SelectedItem="{Binding Region}"/>
    </DataTemplate>
  </TreeDataGrid.Resources>
</TreeDataGrid>
```

## Editing Support

A template column is editable only if editing template is provided.

- if `cellEditingTemplate` is null: read-only
- if provided: editing can start via configured gestures

## Options

`TemplateColumnOptions<TModel>` supports:

- `IsTextSearchEnabled`
- `TextSearchValueSelector`
- inherited `ColumnOptions<TModel>` (sorting, width, edit gestures, etc.)

Example with text search and custom sort:

```csharp
new TemplateColumn<FileNode>(
    "Name",
    "FileNameCell",
    "FileNameEditCell",
    GridLength.Star,
    new TemplateColumnOptions<FileNode>
    {
        IsTextSearchEnabled = true,
        TextSearchValueSelector = x => x.Name,
        CompareAscending = (a, b) => string.Compare(a?.Name, b?.Name, StringComparison.OrdinalIgnoreCase),
        CompareDescending = (a, b) => string.Compare(b?.Name, a?.Name, StringComparison.OrdinalIgnoreCase),
    })
```

## Runtime Resolution Errors

If resource key is missing, template lookup throws `KeyNotFoundException`.

Recommendation:

- keep template keys as constants
- define templates near the grid using them

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.Models.TreeDataGrid.TemplateColumn`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.TemplateColumnOptions`1>

## Related

- [Templates and Styling](templates-and-styling.md)
- [Editing and Begin Edit Gestures](editing-and-begin-edit-gestures.md)
- [Sorting and Column Widths](sorting-and-column-widths.md)
- [Troubleshooting Guide](troubleshooting.md)
