---
title: "Package and Assembly"
---

# Package and Assembly

TreeDataGrid ships as a single NuGet package and a single runtime assembly. This page maps the install identity, assembly name, theme/resource URI root, and generated API route so package setup, XAML theme wiring, and API navigation all point to the same source of truth.

## Distribution Identity

- NuGet package: `TreeDataGrid`
- runtime assembly: `Avalonia.Controls.TreeDataGrid.dll`
- theme resource URI root: `avares://Avalonia.Controls.TreeDataGrid/`
- generated API route: `/api`

Use the package name for installation and the assembly name for theme/resource URIs.

## What the Package Contains

The current generated API exposes `10` public namespaces and `105` public types.

| Namespace | Public Types | Reference Page |
|---|---:|---|
| `Avalonia.Controls` | 14 | [namespace-avalonia-controls.md](namespace-avalonia-controls.md) |
| `Avalonia.Controls.Automation.Peers` | 6 | [namespace-automation-peers.md](namespace-automation-peers.md) |
| `Avalonia.Controls.Converters` | 1 | [namespace-converters.md](namespace-converters.md) |
| `Avalonia.Controls.Models` | 3 | [namespace-models.md](namespace-models.md) |
| `Avalonia.Controls.Models.TreeDataGrid` | 43 | [namespace-models-treedatagrid.md](namespace-models-treedatagrid.md) |
| `Avalonia.Controls.Primitives` | 13 | [namespace-primitives.md](namespace-primitives.md) |
| `Avalonia.Controls.Selection` | 20 | [namespace-selection.md](namespace-selection.md) |
| `Avalonia.Data.Core.Parsers` | 1 | [namespace-data-core-parsers.md](namespace-data-core-parsers.md) |
| `Avalonia.Experimental.Data` | 2 | [namespace-experimental-data.md](namespace-experimental-data.md) |
| `Avalonia.Experimental.Data.Core` | 2 | [namespace-experimental-data-core.md](namespace-experimental-data-core.md) |

## Source and Docs Layout

- package source project: `src/Avalonia.Controls.TreeDataGrid/Avalonia.Controls.TreeDataGrid.csproj`
- authored guides and concepts: `site/articles/**`
- merged API namespace/type docs: `src/Avalonia.Controls.TreeDataGrid/apidocs/**`
- generated API reference: `site/.lunet/build/www/api/**`

## Guidance

- Use [Getting Started: Installation](../getting-started/installation/) for package installation and theme setup.
- Use the generated [API Documentation](../../api/) for member-level reference.
- Use the namespace pages in this section when you want the public surface grouped by responsibility rather than by type name.

## Related

- [API Coverage Index](api-coverage-index/)
- [Lunet Docs Pipeline](lunet-docs-pipeline/)
- [Compatibility page: Build and Package](../build-and-package/)
