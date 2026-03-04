---
title: "ControlTheme Full Replacement"
---

# ControlTheme Full Replacement

This guide covers advanced TreeDataGrid theming where you replace built-in `ControlTheme` templates with fully new templates.

Use this when `BasedOn` overrides cannot express the visual tree or interaction behavior you need.

## Required Template Parts

When replacing templates, preserve required part names used by control code.

| Control | Required part(s) |
|---|---|
| `TreeDataGrid` | `PART_HeaderScrollViewer`, `PART_ColumnHeadersPresenter`, `PART_ScrollViewer`, `PART_RowsPresenter` |
| `TreeDataGridColumnHeader` | `PART_Resizer` |
| `TreeDataGridRow` | `PART_CellsPresenter` |
| `TreeDataGridExpanderCell` | `PART_Content` |
| `TreeDataGridTextCell` | `PART_Edit` (editing template) |
| `TreeDataGridTemplateCell` | `PART_EditingContentPresenter` (editing template) |

If a required part is missing, features such as resizing, editing focus, realization, or expansion content rendering will break.

## Example: Fully New Column Header Theme

This example replaces the entire `TreeDataGridColumnHeader` theme and keeps sorting and resize behavior by preserving `SortIcon` and `PART_Resizer`.

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Application.Resources>
    <ControlTheme x:Key="NeoTreeDataGridColumnHeaderTheme"
                  TargetType="TreeDataGridColumnHeader">
      <Setter Property="Padding" Value="12 6" />
      <Setter Property="Template">
        <ControlTemplate>
          <Border Name="DataGridBorder"
                  Background="{TemplateBinding Background}"
                  BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}">
            <Grid ColumnDefinitions="*,Auto,Auto">
              <ContentPresenter Name="PART_ContentPresenter"
                                Grid.Column="0"
                                Content="{TemplateBinding Header}"
                                ContentTemplate="{TemplateBinding ContentTemplate}"
                                Padding="{TemplateBinding Padding}"
                                VerticalContentAlignment="{TemplateBinding VerticalContentAlignment}" />

              <Path Name="SortIcon"
                    Grid.Column="1"
                    Margin="6 0"
                    Width="10"
                    Height="10"
                    IsVisible="False"
                    Fill="{TemplateBinding Foreground}"
                    Stretch="Uniform" />

              <Thumb Name="PART_Resizer"
                     Grid.Column="2"
                     Width="6"
                     Background="Transparent"
                     Cursor="SizeWestEast"
                     IsVisible="{TemplateBinding CanUserResize}" />
            </Grid>
          </Border>
        </ControlTemplate>
      </Setter>

      <Style Selector="^:pointerover /template/ Border#DataGridBorder">
        <Setter Property="Background" Value="#22000000" />
      </Style>

      <Style Selector="^:pressed /template/ Border#DataGridBorder">
        <Setter Property="Background" Value="#44000000" />
      </Style>

      <Style Selector="^[SortDirection=Ascending] /template/ Path#SortIcon">
        <Setter Property="IsVisible" Value="True" />
        <Setter Property="Data" Value="{DynamicResource TreeDataGridSortIconAscendingPath}" />
      </Style>

      <Style Selector="^[SortDirection=Descending] /template/ Path#SortIcon">
        <Setter Property="IsVisible" Value="True" />
        <Setter Property="Data" Value="{DynamicResource TreeDataGridSortIconDescendingPath}" />
      </Style>
    </ControlTheme>
  </Application.Resources>

  <Application.Styles>
    <FluentTheme/>
    <StyleInclude Source="avares://Avalonia.Controls.TreeDataGrid/Themes/Fluent.axaml"/>

    <Style Selector="TreeDataGridColumnHeader">
      <Setter Property="Theme" Value="{DynamicResource NeoTreeDataGridColumnHeaderTheme}" />
    </Style>
  </Application.Styles>
</Application>
```

## Example: Fully New Text Cell Theme with Editing

When replacing `TreeDataGridTextCell`, keep a dedicated editing template with `PART_Edit` so edit activation and initial selection still work.

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Application.Resources>
    <ControlTheme x:Key="NeoTreeDataGridTextCellTheme"
                  TargetType="TreeDataGridTextCell">
      <Setter Property="Padding" Value="8 4" />
      <Setter Property="Template">
        <ControlTemplate>
          <Border Background="{TemplateBinding Background}"
                  BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}"
                  Padding="{TemplateBinding Padding}">
            <TextBlock Text="{TemplateBinding Value}"
                       TextTrimming="{TemplateBinding TextTrimming}"
                       TextWrapping="{TemplateBinding TextWrapping}"
                       TextAlignment="{TemplateBinding TextAlignment}"
                       VerticalAlignment="Center" />
          </Border>
        </ControlTemplate>
      </Setter>

      <Style Selector="^:editing">
        <Setter Property="Template">
          <ControlTemplate>
            <Border Background="{TemplateBinding Background}"
                    BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}"
                    Padding="{TemplateBinding Padding}">
              <TextBox Name="PART_Edit"
                       Text="{TemplateBinding Value, Mode=TwoWay}" />
            </Border>
          </ControlTemplate>
        </Setter>
      </Style>
    </ControlTheme>
  </Application.Resources>

  <Application.Styles>
    <FluentTheme/>
    <StyleInclude Source="avares://Avalonia.Controls.TreeDataGrid/Themes/Fluent.axaml"/>

    <Style Selector="TreeDataGridTextCell">
      <Setter Property="Theme" Value="{DynamicResource NeoTreeDataGridTextCellTheme}" />
    </Style>
  </Application.Styles>
</Application>
```

## Replacement Strategy

1. Start from shipped `Generic.axaml` templates and copy only the control(s) you need to replace.
2. Keep required part names unchanged.
3. Preserve state selectors (`:editing`, `SortDirection=...`) for behavior-critical visuals.
4. Validate pointer, keyboard, sorting, resizing, expansion, and editing interactions.

## Troubleshooting

- Column header resize no longer works
Cause: `PART_Resizer` missing or renamed.
Fix: restore `Thumb Name="PART_Resizer"` in the header template.

- Text editing starts but focus is not inside editor
Cause: editing template does not include `TextBox Name="PART_Edit"`.
Fix: restore `PART_Edit` in `:editing` template.

- Expander cell content disappears
Cause: `TreeDataGridExpanderCell` template is missing `Decorator Name="PART_Content"`.
Fix: restore `PART_Content` host in the expander cell template.

## API Coverage Checklist

- <xref:Avalonia.Controls.TreeDataGrid>
- <xref:Avalonia.Controls.Primitives.TreeDataGridColumnHeader>
- <xref:Avalonia.Controls.Primitives.TreeDataGridRow>
- <xref:Avalonia.Controls.Primitives.TreeDataGridExpanderCell>
- <xref:Avalonia.Controls.Primitives.TreeDataGridTextCell>
- <xref:Avalonia.Controls.Primitives.TreeDataGridTemplateCell>

## Related

- [Theme Usage](theme-usage.md)
- [Theme Customization](theme-customization.md)
- [ControlTheme Overrides with BasedOn](control-theme-overrides-basedon.md)
- [Primitives Overview](../advanced/primitives-overview.md)
