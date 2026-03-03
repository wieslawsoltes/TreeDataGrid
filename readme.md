[![NuGet](https://img.shields.io/nuget/v/TreeDataGrid.svg)](https://www.nuget.org/packages/TreeDataGrid/)

# `TreeDataGrid` for Avalonia

## Introduction

`TreeDataGrid` is a control for the [Avalonia](https://github.com/AvaloniaUI/Avalonia) UI framework which displays hierarchical and tabular data together in a single view. It is a combination of a `TreeView` and `DataGrid` control.

The control has two modes of operation:

- Hierarchical: data is displayed in a tree with optional columns
- Flat: data is displayed in a 2D table, similar to other `DataGrid` controls

An example of `TreeDataGrid` displaying hierarchical data:

![TreeDataGrid in hierarchical mode](docs/images/files.png)

An example of `TreeDataGrid` displaying flat data:

![TreeDataGrid in hierarchical mode](docs/images/countries.png)

## Current Status

We accept issues and pull requests but we answer and review only pull requests and issues that are created by our customers. It's a quite big project and servicing all issues and pull requests will require more time than we have. But feel free to open issues and pull requests because they may be useful for us!

## Quick Start

Install the package:

```bash
dotnet add package TreeDataGrid
```

Or add a package reference:

```xml
<ItemGroup>
  <PackageReference Include="TreeDataGrid" Version="x.y.z" />
</ItemGroup>
```

## Getting Started

- [Installation](docs/installation.md)
- [Creating a flat `TreeDataGrid`](docs/get-started-flat.md)
- [Creating a hierarchical `TreeDataGrid`](docs/get-started-hierarchical.md)
- [Supported column types](docs/column-types.md)
- [Selection](docs/selection.md)

## License

- Main project license: [licence.md](licence.md)
- Preserved original upstream license: [LICENSE-AVALONIA](LICENSE-AVALONIA)
