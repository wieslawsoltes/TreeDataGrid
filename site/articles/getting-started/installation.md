---
title: "Installation"
---

# Installation

This guide configures TreeDataGrid in an Avalonia project and verifies that it renders correctly.

## 1. Add NuGet Package

```bash
dotnet add package TreeDataGrid
```

If you use `PackageReference` directly:

```xml
<ItemGroup>
  <PackageReference Include="TreeDataGrid" Version="*" />
</ItemGroup>
```

## 2. Add TreeDataGrid Theme

Add the style include to `App.axaml`:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="AvaloniaApplication.App">
  <Application.Styles>
    <FluentTheme/>
    <StyleInclude Source="avares://Avalonia.Controls.TreeDataGrid/Themes/Fluent.axaml"/>
  </Application.Styles>
</Application>
```

Note: keep the URI as `avares://Avalonia.Controls.TreeDataGrid/...`.

## 3. Verify Rendering

Add a minimal control instance to any window:

```xml
<TreeDataGrid />
```

Run the app. If styling is loaded correctly, the control template is applied (not a plain empty rectangle).

## 4. Verify with a Bound Source

When available, bind `Source` to your view model:

```xml
<TreeDataGrid Source="{Binding Source}" />
```

Then proceed to:

- [Quickstart: Flat TreeDataGrid](quickstart-flat.md)
- [Quickstart: Hierarchical TreeDataGrid](quickstart-hierarchical.md)

## Troubleshooting

If setup still fails after installation, use the checks below.

### Control appears unstyled

Cause: missing `StyleInclude` in `App.axaml`.

Fix: ensure this exists:

```xml
<StyleInclude Source="avares://Avalonia.Controls.TreeDataGrid/Themes/Fluent.axaml"/>
```

### Build succeeds but control does not appear

Cause: `DataContext` or `Source` binding is null.

Fix: verify:

- view `DataContext` is set
- `Source` property exists and is initialized
- `TreeDataGrid Source="{Binding Source}"` path matches property name

## API Coverage Checklist

- <xref:Avalonia.Controls.TreeDataGrid>

## Related

- [Quickstart: Flat TreeDataGrid](quickstart-flat.md)
- [Quickstart: Hierarchical TreeDataGrid](quickstart-hierarchical.md)
- [Validation Snippets](../guides/validation-snippets.md)
- [Troubleshooting Guide](../guides/troubleshooting.md)
