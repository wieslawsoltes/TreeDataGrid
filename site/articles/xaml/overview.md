---
title: "XAML Usage Overview"
---

# XAML Usage Overview

This section documents TreeDataGrid from a XAML-first perspective: declaring controls, wiring templates, loading themes, and customizing visuals.

## What You Will Learn

- how sample projects structure `TreeDataGrid` in XAML
- how to include and use TreeDataGrid themes
- how model code (`TemplateColumn<TModel>`) resolves XAML template resource keys
- how to customize theme resources and selectors safely

## Typical XAML-First Setup

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="MyApp.App">
  <Application.Styles>
    <FluentTheme/>
    <StyleInclude Source="avares://Avalonia.Controls.TreeDataGrid/Themes/Fluent.axaml"/>
  </Application.Styles>
</Application>
```

```xml
<TreeDataGrid Source="{Binding Source}"/>
```

## Article Map

- [XAML Samples Walkthrough](samples-walkthrough.md)
- [Theme Usage](theme-usage.md)
- [Theme Customization](theme-customization.md)
- [ControlTheme Overrides with BasedOn](control-theme-overrides-basedon.md)
- [ControlTheme Full Replacement](control-theme-full-replacement.md)
- [Template Resource Keys from Model](template-resource-keys-from-model.md)
- [Theme Resource Keys Reference](theme-resource-keys-reference.md)

## Troubleshooting

- Control renders but appears unstyled
Cause: TreeDataGrid theme include is missing.
Fix: include `Fluent.axaml` or `Generic.axaml` from `Avalonia.Controls.TreeDataGrid/Themes`.

- Template column content is blank
Cause: template key not found or wrong `DataType`/binding path in XAML template.
Fix: validate template key names and inspect [Template Resource Keys from Model](template-resource-keys-from-model.md).

## API Coverage Checklist

- <xref:Avalonia.Controls.TreeDataGrid>
- <xref:Avalonia.Controls.Models.TreeDataGrid.TemplateColumn`1>
- <xref:Avalonia.Controls.Primitives.TreeDataGridTemplateCell>

## Related

- [Getting Started Overview](../getting-started/overview.md)
- [Template Column Guide](../guides/column-template.md)
- [Templates and Styling](../guides/templates-and-styling.md)
- [Troubleshooting Guide](../guides/troubleshooting.md)
