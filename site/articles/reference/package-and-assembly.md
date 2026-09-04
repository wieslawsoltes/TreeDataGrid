---
title: "Package and Assembly"
---

# Package and Assembly

TreeDataGrid ships framework-neutral Core, preferred Avalonia UI, and compatibility UI packages. This page maps the install identities, assembly names, theme/resource URI roots, and generated API route.

## Distribution Identity

- preferred NuGet package: `TreeDataGrid.Controls.Avalonia`
- preferred runtime assembly: `TreeDataGrid.Avalonia.dll`
- preferred theme resource URI root: `avares://TreeDataGrid.Avalonia/`
- compatibility package: `TreeDataGrid`
- compatibility assembly: `Avalonia.Controls.TreeDataGrid.dll`
- compatibility theme root: `avares://Avalonia.Controls.TreeDataGrid/`
- framework-neutral package/assembly: `TreeDataGrid.Core`
- generated API route: `/api`

The two UI packages source-link the same implementation and expose the same API.
Reference only one UI package. New applications should use `TreeDataGrid.Controls.Avalonia`;
existing applications may retain `TreeDataGrid` unchanged.

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

- compatibility package source project: `src/Avalonia.Controls.TreeDataGrid/Avalonia.Controls.TreeDataGrid.csproj`
- preferred package source project: `src/TreeDataGrid.Avalonia/TreeDataGrid.Avalonia.csproj`
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
