---
title: "XAML Usage Overview"
---

# XAML Usage Overview

TreeDataGrid can now be configured directly in XAML with `ItemsSource` and declarative columns. This is the recommended path when your columns, templates, and bindings naturally belong in markup.

## Theme Setup

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
<TreeDataGrid ItemsSource="{Binding People}">
  <TreeDataGridTextColumn Header="First Name"
                          Binding="{Binding FirstName}"/>
  <TreeDataGridTextColumn Header="Last Name"
                          Binding="{Binding LastName}"/>
</TreeDataGrid>
```

## When to Use `Source` Instead

Use the code-behind `Source` approach when you need:

- `Filter` and `RefreshFilter`
- `Expand`, `Collapse`, `ExpandAll`, and `CollapseAll`
- more complex runtime composition of columns

## Article Map

- [Getting Started Overview](../getting-started/overview.md)
- [Column Types](../column-types.md)
- [Selection Modes](../selection-modes.md)
- [Sorting](../sorting.md)
- [Theme Usage](theme-usage.md)
- [Theme Customization](theme-customization.md)
- [ControlTheme Overrides with BasedOn](control-theme-overrides-basedon.md)
- [ControlTheme Full Replacement](control-theme-full-replacement.md)
- [Template Resource Keys from Model](template-resource-keys-from-model.md)
- [Theme Resource Keys Reference](theme-resource-keys-reference.md)
