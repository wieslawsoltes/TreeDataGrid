# TreeDataGrid for Avalonia

**TreeDataGrid** is an Avalonia control for tabular and hierarchical data in a single view. It combines familiar DataGrid-style columns with tree expansion behavior.

## Getting Started

### Install

```bash
dotnet add package TreeDataGrid
```

```xml
<PackageReference Include="TreeDataGrid" Version="..." />
```

### Add Theme

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

### Use in XAML

```xml
<TreeDataGrid Source="{Binding Source}" />
```

## Documentation Sections

- **[Articles](articles/intro.md)**: Usage guides and practical walkthroughs.
- **[Installation](articles/installation.md)**: Package and theme setup.
- **[Flat Mode Guide](articles/get-started-flat.md)**: End-to-end flat source setup.
- **[Hierarchical Mode Guide](articles/get-started-hierarchical.md)**: End-to-end hierarchical source setup.
- **[API Documentation](api/index.md)**: Full public API reference.

## Repository

- Source code and issues: [github.com/wieslawsoltes/TreeDataGrid](https://github.com/wieslawsoltes/TreeDataGrid)

## License

TreeDataGrid is licensed under the MIT License. See [license article](articles/license.md).
