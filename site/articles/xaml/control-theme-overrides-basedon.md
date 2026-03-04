---
title: "ControlTheme Overrides with BasedOn"
---

# ControlTheme Overrides with BasedOn

This guide shows how to customize TreeDataGrid visuals by deriving from shipped `ControlTheme` definitions using `BasedOn`.

Use this approach when resource-key overrides are not enough but you still want to inherit the default control templates and behavior.

## When to Use `BasedOn`

Prefer `BasedOn` when you need:

- different sizing, padding, alignment, or border values
- custom pointer/pressed visual states on existing templates
- scoped look changes for one grid or one application theme

Use full template replacement only when you need a different visual tree or behavior contract.

## Global Override Pattern

This pattern derives from built-in themes keyed by control type and applies the derived themes through `Theme` setters.

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Application.Styles>
    <FluentTheme/>
    <StyleInclude Source="avares://Avalonia.Controls.TreeDataGrid/Themes/Fluent.axaml"/>

    <Style Selector="TreeDataGrid">
      <Setter Property="Theme" Value="{DynamicResource BrandTreeDataGridTheme}" />
    </Style>
    <Style Selector="TreeDataGridColumnHeader">
      <Setter Property="Theme" Value="{DynamicResource BrandTreeDataGridColumnHeaderTheme}" />
    </Style>
    <Style Selector="TreeDataGridTextCell">
      <Setter Property="Theme" Value="{DynamicResource BrandTreeDataGridTextCellTheme}" />
    </Style>
  </Application.Styles>

  <Application.Resources>
    <ControlTheme x:Key="BrandTreeDataGridTheme"
                  TargetType="TreeDataGrid"
                  BasedOn="{StaticResource {x:Type TreeDataGrid}}">
      <Setter Property="CornerRadius" Value="6" />
      <Setter Property="BorderThickness" Value="1" />
    </ControlTheme>

    <ControlTheme x:Key="BrandTreeDataGridColumnHeaderTheme"
                  TargetType="TreeDataGridColumnHeader"
                  BasedOn="{StaticResource {x:Type TreeDataGridColumnHeader}}">
      <Setter Property="MinHeight" Value="32" />
      <Setter Property="Padding" Value="10 4" />
      <Style Selector="^:pointerover /template/ Border#DataGridBorder">
        <Setter Property="Background" Value="#1A0A84FF" />
        <Setter Property="BorderBrush" Value="#660A84FF" />
        <Setter Property="TextBlock.Foreground" Value="White" />
      </Style>
    </ControlTheme>

    <ControlTheme x:Key="BrandTreeDataGridTextCellTheme"
                  TargetType="TreeDataGridTextCell"
                  BasedOn="{StaticResource {x:Type TreeDataGridTextCell}}">
      <Setter Property="Padding" Value="8 3" />
    </ControlTheme>
  </Application.Resources>
</Application>
```

## Scoped Override for One Grid

Use this when only one TreeDataGrid instance should look different.

```xml
<TreeDataGrid Source="{Binding Source}">
  <TreeDataGrid.Resources>
    <ControlTheme x:Key="DenseTextCellTheme"
                  TargetType="TreeDataGridTextCell"
                  BasedOn="{StaticResource {x:Type TreeDataGridTextCell}}">
      <Setter Property="Padding" Value="2 1" />
    </ControlTheme>
  </TreeDataGrid.Resources>

  <TreeDataGrid.Styles>
    <Style Selector="TreeDataGridTextCell">
      <Setter Property="Theme" Value="{StaticResource DenseTextCellTheme}" />
    </Style>
  </TreeDataGrid.Styles>
</TreeDataGrid>
```

## Common Override Targets

- `{x:Type TreeDataGrid}`: shell border, corner radius, background
- `{x:Type TreeDataGridColumnHeader}`: header spacing and pointer/pressed visuals
- `{x:Type TreeDataGridRow}`: row-level template and selected state style
- `{x:Type TreeDataGridTextCell}`: text-cell padding/editing template defaults
- `{x:Type TreeDataGridTemplateCell}`: template-host padding and editing presenter styling

## Troubleshooting

- `BasedOn` theme is ignored
Cause: derived theme exists, but no style sets `Theme` for matching controls.
Fix: add a matching style selector with `Theme` setter.

- Override works globally but not for one grid
Cause: resource scope mismatch.
Fix: place the derived `ControlTheme` in `TreeDataGrid.Resources` for local use.

- Sorting icon or resize behavior changed unexpectedly
Cause: nested `Style` selectors in a derived header theme changed template state visuals.
Fix: review selectors against `TreeDataGridColumnHeader` defaults and keep sort/resize selectors intact.

- `BasedOn` cannot resolve `{x:Type TreeDataGrid...}` key
Cause: TreeDataGrid theme file was not included before derived themes are declared.
Fix: include `Fluent.axaml`/`Generic.axaml` in `Application.Styles` before custom `ControlTheme` definitions.

## API Coverage Checklist

- <xref:Avalonia.Controls.TreeDataGrid>
- <xref:Avalonia.Controls.Primitives.TreeDataGridColumnHeader>
- <xref:Avalonia.Controls.Primitives.TreeDataGridRow>
- <xref:Avalonia.Controls.Primitives.TreeDataGridTextCell>
- <xref:Avalonia.Controls.Primitives.TreeDataGridTemplateCell>

## Related

- [Theme Usage](theme-usage.md)
- [Theme Customization](theme-customization.md)
- [ControlTheme Full Replacement](control-theme-full-replacement.md)
- [Theme Resource Keys Reference](theme-resource-keys-reference.md)
