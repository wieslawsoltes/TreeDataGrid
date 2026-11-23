---
title: "Theme Usage"
---

# Theme Usage

TreeDataGrid ships with two theme entry files:

- `Themes/Generic.axaml`
- `Themes/Fluent.axaml`

## Include Pattern

Include the TreeDataGrid Fluent styles after your app theme. This example uses Fluent2, the default for new Avalonia 12.2 projects.

```xml
<Application.Styles>
  <Fluent2Theme/>
  <StyleInclude Source="avares://TreeDataGrid.Avalonia/Themes/Fluent.axaml"/>
</Application.Styles>
```

If your project uses `Avalonia.Themes.Fluent`, use `<FluentTheme/>` with the same `StyleInclude`.

## Fluent vs Generic

`Fluent.axaml` includes `Generic.axaml`, then overrides selected resource keys with Fluent system colors.

In source, this is implemented via `StyleInclude Source="/Themes/Generic.axaml"`
inside `Fluent.axaml`.

This means:

- Generic defines control templates and baseline resources.
- Fluent keeps templates and replaces values for key brushes.

## Theme Dictionaries

Both files define `Default` and `Dark` dictionaries for TreeDataGrid keys.

Key groups include:

- grid/header/selection brushes
- sort icon geometries
- expander chevron geometries
- control themes for grid primitives and cells

## Control Themes Defined in Generic

`Generic.axaml` defines `ControlTheme` entries for:

- `TreeDataGrid`
- `TreeDataGridColumnHeader`
- `TreeDataGridRow`
- `TreeDataGridCheckBoxCell`
- `TreeDataGridExpanderCell`
- `TreeDataGridTextCell`
- `TreeDataGridTemplateCell`

It also defines a keyed `ToggleButton` theme for the expander chevron.

## Dynamic vs Static Resources

The templates use `DynamicResource` for most override points (`TreeDataGrid...Brush` keys), so app-level overrides propagate without replacing full control themes.

## Troubleshooting

- Header/cell colors do not change after overriding key
Cause: wrong key name or override is defined in an unreachable resource scope.
Fix: verify exact key spelling and move override to `Application.Resources` to confirm reachability.

- Theme looks incomplete
Cause: Grid is loaded without TreeDataGrid style include.
Fix: include TreeDataGrid `Fluent.axaml` or `Generic.axaml` explicitly.

## API Coverage Checklist

- <xref:Avalonia.Controls.TreeDataGrid>
- <xref:Avalonia.Controls.Primitives.TreeDataGridColumnHeader>
- <xref:Avalonia.Controls.Primitives.TreeDataGridRow>
- <xref:Avalonia.Controls.Primitives.TreeDataGridTextCell>
- <xref:Avalonia.Controls.Primitives.TreeDataGridTemplateCell>
- <xref:Avalonia.Controls.Primitives.TreeDataGridExpanderCell>
- <xref:Avalonia.Controls.Primitives.TreeDataGridCheckBoxCell>

## Related

- [XAML Usage Overview](overview.md)
- [Theme Customization](theme-customization.md)
- [ControlTheme Overrides with BasedOn](control-theme-overrides-basedon.md)
- [ControlTheme Full Replacement](control-theme-full-replacement.md)
- [Theme Resource Keys Reference](theme-resource-keys-reference.md)
- [Templates and Styling](../guides/templates-and-styling.md)
