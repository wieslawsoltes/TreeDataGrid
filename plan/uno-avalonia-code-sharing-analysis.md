# Uno and Avalonia TreeDataGrid Code Sharing Analysis

## Goal

Maximize code reuse between `src/Avalonia.Controls.TreeDataGrid` and `src/TreeDataGrid.Uno` while keeping:

- Avalonia as the authoritative implementation for platform-neutral TreeDataGrid core behavior.
- Uno-specific control, binding, and WinUI interop code local to the Uno project.
- Build and debugging workflows simple enough to maintain inside a single repository.

## Baseline Findings

Before this refactor, the Uno project contained a large copied subset of the Avalonia TreeDataGrid core. That duplication had three concrete costs:

1. Bug fixes had to be ported twice.
2. Avalonia 12 parity work kept landing as forked Uno-only edits instead of converging the codebase.
3. Review cost was inflated because many files differed only by drift, not by real platform needs.

The overlap was concentrated in five areas:

| Area | Pre-refactor state | Sharing suitability |
| --- | --- | --- |
| Data source core | Nearly identical | High |
| Model and column core | Nearly identical | High |
| Selection infrastructure | Mostly identical with a few Uno hooks | High |
| Lightweight observable helpers | Identical or trivially compatible | High |
| Control/primitives/automation | Framework-specific | Low |

## Current Architecture

### Decision

Use **MSBuild linked-source sharing** from the Avalonia project into the Uno project for the platform-neutral core, and keep Avalonia as the single source of truth for that code.

This remains the right boundary after Phases 1-3 because it removes duplication without introducing a third package or changing the public assembly identity of existing types.

### Shared authoritative code

`src/TreeDataGrid.Uno/TreeDataGrid.Uno.LinkedAvalonia.props` now links Avalonia source files directly into the Uno compilation.

Current linked inventory:

- 22 root/core files
- 43 model files
- 22 selection files
- 4 utility files
- 5 experimental data helper files

Total linked files: **96**

The linked set now includes the previously duplicated binding/path helpers, declarative-source helpers, control-logic helpers, `TreeDataGridCellSelectionModel<TModel>`, and the shared core partial for `TreeDataGridRowSelectionModel<TModel>`.

### Shared-core fixes now land once

The filtered root lookup fix in `src/Avalonia.Controls.TreeDataGrid/HierarchicalTreeDataGridSource.cs` is the model for the new boundary:

- fix the shared Avalonia source
- let Uno pick it up automatically through the link

That pattern now applies across the data source core, most model code, most selection code, and the newer member-path/control-shell helpers.

## What Is Shared Now

The following groups compile from the Avalonia source tree into Uno:

### Shared root/core

- `CellIndex.cs`
- `ITreeDataGridSource.cs`
- `IndexPath.cs`
- `TreeDataGridItemsSourceView.cs`
- `FlatTreeDataGridSource.cs`
- `HierarchicalTreeDataGridSource.cs`
- `NullableAttributes.cs`
- `TreeDataGridColumns.cs`
- `TreeDataGridExpressionHelper.cs`
- `TreeDataGridRowModel.cs`
- `TreeDataGridRowModelEventArgs.cs`
- `TreeDataGridSelectionChangedEventArgs.cs`
- `TreeDataGridSelectionMode.cs`
- `TreeDataGridMemberPath.cs`
- `TreeDataGridColumnOptionsFactory.cs`
- `TreeDataGridControlLogic.cs`
- `TreeDataGridDeclarativeHelper.cs`

### Shared model and column core

Most of `src/Avalonia.Controls.TreeDataGrid/Models/**/*.cs`, including the reflection/row-header/filtered-expander files that were still duplicated earlier in the branch.

The remaining local model adapters are only the files that still require Uno-specific UI or compatibility behavior.

### Shared selection infrastructure

Most of `src/Avalonia.Controls.TreeDataGrid/Selection/*.cs`, including `TreeDataGridCellSelectionModel.cs`.

The only row-selection code that remains local is the Uno interaction partial that bridges focus, visibility, and modifier handling onto WinUI/Uno APIs.

### Shared utilities and lightweight observable helpers

- `src/Avalonia.Controls.TreeDataGrid/Utils/*.cs`
- most of `src/Avalonia.Controls.TreeDataGrid/Experimental/Data/Core/**/*.cs`
- `src/Avalonia.Controls.TreeDataGrid/Experimental/Data/ObservableEx.cs`

## What Remains Local to Uno

These files remain local because they are tied to WinUI/Uno APIs or to Uno-only compatibility work.

### Framework control surface

- `src/TreeDataGrid.Uno/TreeDataGrid.cs`
- `src/TreeDataGrid.Uno/TreeDataGrid.V12.cs`
- `src/TreeDataGrid.Uno/TreeDataGridBindingAccessor.cs`
- `src/TreeDataGrid.Uno/TreeDataGridColumn.cs`
- `src/TreeDataGrid.Uno/TreeDataGridSourceExtensions.cs`

### Uno presenter, automation, and theme surface

- `src/TreeDataGrid.Uno/Primitives/**/*.cs`
- `src/TreeDataGrid.Uno/Automation/Peers/**/*.cs`
- `src/TreeDataGrid.Uno/Themes/**/*.cs`
- `src/TreeDataGrid.Uno/Internal/VisualTreeHelpers.cs`

### Remaining Uno model and interaction adapters

- `src/TreeDataGrid.Uno/Models/TreeDataGrid/TemplateColumn.cs`
- `src/TreeDataGrid.Uno/Shared/Models/TreeDataGrid/ExpanderCell.cs`
- `src/TreeDataGrid.Uno/Shared/Models/TreeDataGrid/HierarchicalExpanderColumn.cs`
- `src/TreeDataGrid.Uno/Shared/Models/TreeDataGrid/HierarchicalRow.cs`
- `src/TreeDataGrid.Uno/Shared/Models/TreeDataGrid/IModelExpanderState.cs`
- `src/TreeDataGrid.Uno/Shared/Selection/TreeDataGridRowSelectionModel.Interaction.cs`
- `src/TreeDataGrid.Uno/Support/Selection/TreeDataGridCellSelectionChangedEventArgs.cs`
- `src/TreeDataGrid.Uno/Support/Experimental/Data/TypedBinding\`1.cs`
- `src/TreeDataGrid.Uno/Compatibility/TypedBinding\`2.cs`
- `src/TreeDataGrid.Uno/Compatibility/TypedBindingExpression\`2.cs`

### Uno compatibility layer

- `src/TreeDataGrid.Uno/Compatibility/**/*.cs`

These files are no longer accidental forks. They are the deliberate platform-adapter boundary.

## Phase 4 Suitability Assessment

### Question

Would extracting the current linked set into a formal `TreeDataGrid.Core` project improve reuse enough to justify the move?

### Evidence from the current branch

1. The current linked model already maximizes source reuse for the platform-neutral layer: **96 files** compile from one authoritative source tree.
2. **64** of those shared files define public types. Moving them into a third assembly would change the assembly identity of user-visible APIs that are currently emitted by `TreeDataGrid` and `TreeDataGrid.Uno`.
3. **32** of those shared files define internal types or rely on internal/protected-internal access inside the current assembly boundary. A new core assembly would need either broader public surface area or new `InternalsVisibleTo` wiring to Avalonia, Uno, and their test assemblies.
4. **41** of those shared files reference Avalonia-shaped namespaces directly. Uno consumes them today because `src/TreeDataGrid.Uno/Compatibility/**/*.cs` recreates the necessary Avalonia contracts inside the Uno assembly. A third project would need its own compatibility layer or a multi-targeted source split.
5. There is no existing type-forwarding infrastructure in the repository. Introducing a core assembly would require type forwarding in both outer packages if binary compatibility matters.
6. The package graph is intentionally simple today:
   - `src/Avalonia.Controls.TreeDataGrid/Avalonia.Controls.TreeDataGrid.csproj` is a packable `net8.0` package.
   - `src/TreeDataGrid.Uno/TreeDataGrid.Uno.csproj` is a packable `net9.0` package that imports the linked Avalonia props file.
   - A new core project would add a third project/package without reducing the amount of shared source that must be maintained.

### Cost of implementing Phase 4 now

Implementing a formal shared assembly would require all of the following:

1. Move or forward public shared types out of both existing package assemblies.
2. Add new `InternalsVisibleTo` rules or widen visibility for the current internal shared layer.
3. Decide where the Avalonia-shaped compatibility contracts live for Uno builds.
4. Rework packaging/versioning so `TreeDataGrid`, `TreeDataGrid.Uno`, and the new core assembly stay coherent.
5. Revisit XAML/assembly metadata such as `XmlnsDefinition` ownership once types stop living in the current outer assembly.

That is a large binary-compatibility and packaging refactor, not an incremental code-sharing improvement.

### Verdict

**Do not implement Phase 4 on the current branch.**

At the current boundary, a formal shared assembly would not materially improve code reuse because the shared source is already implemented once. It would mostly trade source-linking simplicity for package, assembly-identity, and compatibility complexity.

## When Phase 4 Should Be Reopened

Revisit a formal `TreeDataGrid.Core` project only if one of these becomes true:

1. The linked shared set needs independent packaging or versioning.
2. Linked compilation starts causing material IDE/build/tooling problems.
3. The remaining shared layer grows enough that a dedicated assembly simplifies ownership more than it complicates packaging.

Until then, the current linked file set is the right candidate input set, but not the right current implementation vehicle.

## Result

The repository now has a clear ownership model:

- **Avalonia project**: owns the shared TreeDataGrid model, selection, data-source, and helper core.
- **Uno project**: owns the WinUI/Uno control shell, binding/runtime adapters, compatibility shims, and visual surface.

That is the highest-yield sharing boundary in this repository today. Phase 4 has been analyzed and intentionally not adopted.
