---
title: "Theme Customization"
---

# Theme Customization

This guide shows safe customization layers for TreeDataGrid theme behavior in XAML.

## Level 1: Override Built-In Resource Keys

For most visual tweaks, override TreeDataGrid brush keys instead of replacing control templates.

```xml
<Application.Resources>
  <SolidColorBrush x:Key="TreeDataGridGridLinesBrush" Color="#60FFB000"/>
  <SolidColorBrush x:Key="TreeDataGridSelectedCellBackgroundBrush" Color="#66984AFB"/>
  <SolidColorBrush x:Key="TreeDataGridHeaderBackgroundPointerOverBrush" Color="#22007ACC"/>
</Application.Resources>
```

This is low-risk and survives future control-template changes better than full template replacement.

## Level 2: Per-Grid Styling with Selectors

Use `TreeDataGrid.Styles` to apply local, scenario-specific styles:

```xml
<TreeDataGrid>
  <TreeDataGrid.Styles>
    <Style Selector="TreeDataGrid TreeDataGridRow:nth-child(2n)">
      <Setter Property="Background" Value="#12808080"/>
    </Style>
    <Style Selector="TreeDataGrid TreeDataGridColumnHeader:nth-last-child(1)">
      <Setter Property="TextBlock.FontWeight" Value="Bold"/>
    </Style>
  </TreeDataGrid.Styles>
</TreeDataGrid>
```

## Level 3: Replace Specific ControlTheme Keys

Advanced customization can replace `ControlTheme` entries from `Generic.axaml` (for example, expander chevron theme key `TreeDataGridExpandCollapseChevron`).

Prefer replacing one focused theme key rather than rewriting all TreeDataGrid control templates.

For deeper guidance:

- use [ControlTheme Overrides with BasedOn](control-theme-overrides-basedon.md) when you want to inherit shipped templates
- use [ControlTheme Full Replacement](control-theme-full-replacement.md) when you need a new visual tree

## Theme Override Order

Recommended setup:

1. Include TreeDataGrid theme file in `Application.Styles`.
2. Define overrides in the intended scope (`Application.Resources` for global,
   `TreeDataGrid.Resources`/`TreeDataGrid.Styles` for local behavior).
3. Validate both Light and Dark variants.

## Troubleshooting

- Override works in Light but not Dark
Cause: only one theme dictionary branch is overridden.
Fix: define overrides for both variants or use variant-aware resources.

- Custom selector does not apply
Cause: selector does not match actual visual tree/control type.
Fix: inspect runtime tree and adjust selector specificity.

- Global override does not affect one grid
Cause: grid-local resource/style definitions are shadowing app-level values.
Fix: inspect nearest resource scope and remove or adjust local overrides.

## API Coverage Checklist

- <xref:Avalonia.Controls.TreeDataGrid>
- <xref:Avalonia.Controls.Primitives.TreeDataGridColumnHeader>
- <xref:Avalonia.Controls.Primitives.TreeDataGridRow>
- <xref:Avalonia.Controls.Primitives.TreeDataGridExpanderCell>
- <xref:Avalonia.Controls.Converters.IndentConverter>

## Related

- [Theme Usage](theme-usage.md)
- [ControlTheme Overrides with BasedOn](control-theme-overrides-basedon.md)
- [ControlTheme Full Replacement](control-theme-full-replacement.md)
- [Theme Resource Keys Reference](theme-resource-keys-reference.md)
- [Templates and Styling](../guides/templates-and-styling.md)
- [Troubleshooting Guide](../guides/troubleshooting.md)
