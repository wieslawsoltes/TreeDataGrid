---
title: "Template Resource Keys from Model"
---

# Template Resource Keys from Model

`TemplateColumn<TModel>` supports a resource-key constructor so model/viewmodel code can reference XAML templates by key.

## Constructor Pattern

```csharp
new TemplateColumn<Country>(
    "Region",
    "RegionCell",
    "RegionEditCell")
```

The sample project uses this pattern in multiple view models (`CountriesPageViewModel`, `FilesPageViewModel`, `WikipediaPageViewModel`).

## Matching XAML Resources

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

## Runtime Resolution Model

`TemplateColumn<TModel>` resolves keys with `anchor.FindResource(key)` when template cells are realized.

Key behavior:

- template lookup is lazy (first need)
- resolved templates are cached
- missing keys throw `KeyNotFoundException`
- editing template is optional (if no editing key/template is supplied, the
  template cell remains read-only)
- if an editing key is supplied but not found, edit entry triggers a
  `KeyNotFoundException`

## Recommended Practices

- keep key names as constants in shared code
- keep templates near owning grid unless cross-grid reuse is intentional
- define explicit `DataType` and stable bindings in each template
- verify editing template exists before enabling edit-centric workflows

## Troubleshooting

- `KeyNotFoundException` for template key
Cause: key does not exist or is not visible in lookup scope.
Fix: place template in `TreeDataGrid.Resources`, parent resources, or `Application.Resources`.

- Cell never enters editing
Cause: no editing template key was provided; `TemplateCell.CanEdit` is false.
Fix: pass editing template key (or direct editing template instance) to `TemplateColumn`.

- Edit starts but fails with `KeyNotFoundException`
Cause: editing key was provided, but no matching keyed `DataTemplate` exists in
resource scope.
Fix: define the editing template key in scope or remove editing-key usage.

## API Coverage Checklist

- <xref:Avalonia.Controls.Models.TreeDataGrid.TemplateColumn`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.TemplateColumnOptions`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.TemplateCell>
- <xref:Avalonia.Controls.Primitives.TreeDataGridTemplateCell>

## Related

- [XAML Samples Walkthrough](samples-walkthrough.md)
- [Theme Usage](theme-usage.md)
- [Template Column Guide](../guides/column-template.md)
- [Working With ItemsSourceView](../guides/working-with-itemssourceview.md)
