---
title: "Theme Resource Keys Reference"
---

# Theme Resource Keys Reference

This reference lists TreeDataGrid XAML resource keys defined by shipped themes and where they are used.

## Brush Keys

| Key | Used For |
|---|---|
| `TreeDataGridGridLinesBrush` | grid lines and header separator visuals |
| `TreeDataGridHeaderBackgroundPointerOverBrush` | header pointer-over background |
| `TreeDataGridHeaderBackgroundPressedBrush` | header pressed background |
| `TreeDataGridHeaderBorderBrushPointerOverBrush` | header pointer-over border |
| `TreeDataGridHeaderBorderBrushPressedBrush` | header pressed border |
| `TreeDataGridHeaderForegroundPointerOverBrush` | header pointer-over foreground |
| `TreeDataGridHeaderForegroundPressedBrush` | header pressed foreground |
| `TreeDataGridSelectedCellBackgroundBrush` | selected-row/cell background |

## Geometry Keys

| Key | Used For |
|---|---|
| `TreeDataGridSortIconAscendingPath` | ascending sort icon in headers |
| `TreeDataGridSortIconDescendingPath` | descending sort icon in headers |
| `TreeDataGridItemCollapsedChevronPathData` | collapsed expander chevron |
| `TreeDataGridItemExpandedChevronPathData` | expanded expander chevron |

## ControlTheme Keys

| Key | TargetType |
|---|---|
| `{x:Type TreeDataGrid}` | `TreeDataGrid` |
| `{x:Type TreeDataGridColumnHeader}` | `TreeDataGridColumnHeader` |
| `{x:Type TreeDataGridRow}` | `TreeDataGridRow` |
| `{x:Type TreeDataGridCheckBoxCell}` | `TreeDataGridCheckBoxCell` |
| `{x:Type TreeDataGridExpanderCell}` | `TreeDataGridExpanderCell` |
| `{x:Type TreeDataGridTextCell}` | `TreeDataGridTextCell` |
| `{x:Type TreeDataGridTemplateCell}` | `TreeDataGridTemplateCell` |
| `TreeDataGridExpandCollapseChevron` | `ToggleButton` theme used by expander cells |

## Override Example

```xml
<Application.Resources>
  <SolidColorBrush x:Key="TreeDataGridHeaderBackgroundPointerOverBrush" Color="#22007ACC"/>
  <StreamGeometry x:Key="TreeDataGridSortIconAscendingPath">M0,10 L5,0 10,10 Z</StreamGeometry>
</Application.Resources>
```

## Troubleshooting

- Resource key override has no effect
Cause: key name mismatch or override is defined in a scope that is not used by
the target control instance.
Fix: confirm exact key spelling and verify resource-scope precedence
(`TreeDataGrid.Resources` -> parent logical resources -> `Application.Resources`).

- Sort/chevron icon disappears
Cause: replacement geometry path is invalid.
Fix: validate geometry syntax and test with a known-good path first.

## API Coverage Checklist

- <xref:Avalonia.Controls.TreeDataGrid>
- <xref:Avalonia.Controls.Primitives.TreeDataGridColumnHeader>
- <xref:Avalonia.Controls.Primitives.TreeDataGridExpanderCell>
- <xref:Avalonia.Controls.Primitives.TreeDataGridTemplateCell>
- <xref:Avalonia.Controls.Primitives.TreeDataGridTextCell>
- <xref:Avalonia.Controls.Primitives.TreeDataGridCheckBoxCell>
- <xref:Avalonia.Controls.Primitives.TreeDataGridRow>

## Related

- [Theme Usage](theme-usage.md)
- [Theme Customization](theme-customization.md)
- [Templates and Styling](../guides/templates-and-styling.md)
- [Primitives Overview](../advanced/primitives-overview.md)
