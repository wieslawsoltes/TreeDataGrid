# PR Summary: Expand Uno TreeDataGrid port with Avalonia 12 parity and shared core reuse

## Branch

- `feat/uno-treedatagrid-port`

## Overview

This branch adds a full Uno Platform port of `TreeDataGrid`, wires in Uno sample applications, closes a substantial set of Avalonia 12 parity gaps, and then refactors the Uno implementation to consume the Avalonia TreeDataGrid core from a shared authoritative source instead of maintaining a growing fork.

The result is a thinner Uno-specific surface: the WinUI/Uno control shell, binding/runtime adapters, presenters, automation, and compatibility shims stay local, while the reusable model, source, selection, and helper core is now implemented once.

## Main changes

### 1. Add the Uno TreeDataGrid library

Introduces `src/TreeDataGrid.Uno/` with:

- Uno control shell and dependency-property surface
- Uno themes and templates
- row, cell, and header presenters under `Primitives/`
- Avalonia-compatible model, source, and selection architecture adapted for Uno
- compatibility shims for Avalonia-style contracts used by the shared core
- Uno automation peer support

### 2. Add Uno sample applications and repo wiring

Adds two Uno samples:

- `samples/TreeDataGridUnoSample/`
- `samples/TreeDataGridUnoActivityMonitor/`

Also updates the solutions, CI workflow scope, and README so the Uno port can build and be smoke-tested alongside the Avalonia implementation.

### 3. Address review feedback on the initial Uno port

The branch includes several follow-up fixes on top of the first port, including:

- header theming fixes
- sort glyph alignment with Avalonia
- compact Uno checkbox column sizing
- drag indicator alignment
- row rebinding after source swaps
- sample selection-model cleanup
- additional Uno-specific behavior fixes found during review

### 4. Bring Uno closer to Avalonia 12 APIs and behavior

This branch adds the first major Avalonia 12 parity slice for Uno:

- declarative/XAML column support in `TreeDataGrid.V12.cs`
- Uno `ColumnDefinitions` support for text, checkbox, template, row-header, and hierarchical expander columns
- Uno `TreeDataGridSourceExtensions` parity surface
- source behavior parity for `ClearSort`, `Filter`, and `RefreshFilter`
- filtered hierarchical expander behavior aligned with Avalonia
- selection and row event parity via `TreeDataGridSelectionChangedEventArgs`, `TreeDataGridRowModel`, and `TreeDataGridRowModelEventArgs`
- a dedicated Uno parity test project covering the new surface

### 5. Fix follow-up issues found during parity work

The latest parity fixes also address review findings uncovered while testing the newer Uno surface:

- filtered hierarchical root model lookup now resolves correctly under active filters
- Uno XAML binding write-back now respects `BindingMode` instead of writing through one-way bindings

### 6. Refactor Uno to share the Avalonia TreeDataGrid core

The branch now shifts from copied Uno core files to Avalonia-authored shared source:

- adds `src/TreeDataGrid.Uno/TreeDataGrid.Uno.LinkedAvalonia.props` to link the reusable Avalonia core into the Uno build
- removes the duplicated Uno copies for the linked source set
- moves shared member-path, declarative-source, column-option, and non-visual control logic into Avalonia-owned helpers
- links the Avalonia `TreeDataGridCellSelectionModel` into Uno and splits `TreeDataGridRowSelectionModel<TModel>` into shared core plus platform-specific interaction partials
- keeps only the true Uno adapter boundary local: control shell, binding/runtime adapters, presenters, automation, and compatibility shims

This improves reuse by making shared behavior changes land in one place instead of parallel Avalonia and Uno edits.

### 7. Add committed sharing analysis and plan docs

Adds:

- `plan/uno-avalonia-code-sharing-analysis.md`
- `plan/uno-avalonia-code-sharing-plan.md`

These documents capture the current sharing architecture and the Phase 4 evaluation. The conclusion is that a separate `TreeDataGrid.Core` assembly is not justified yet because the linked-source model already centralizes the reusable core without adding package and assembly-identity churn.

## Validation performed

The following checks were run successfully during this branch work:

```bash
dotnet build Avalonia.Controls.TreeDataGrid.CI.slnx
dotnet build src/Avalonia.Controls.TreeDataGrid/Avalonia.Controls.TreeDataGrid.csproj
dotnet build src/TreeDataGrid.Uno/TreeDataGrid.Uno.csproj
dotnet test tests/TreeDataGrid.Uno.Tests/TreeDataGrid.Uno.Tests.csproj
dotnet build samples/TreeDataGridUnoSample/TreeDataGridUnoSample.csproj -p:TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop
dotnet build samples/TreeDataGridUnoActivityMonitor/TreeDataGridUnoActivityMonitor.csproj -p:TreeDataGridUnoActivityMonitorTargetFrameworks=net10.0-desktop
```

Validation results:

- CI solution build passed
- Avalonia library build passed
- Uno library build passed
- Uno parity tests passed
- Uno sample desktop build passed
- Uno Activity Monitor desktop build passed

## Remaining note

The local machine used for this work does not have the .NET 8 runtime required to execute `tests/Avalonia.Controls.TreeDataGrid.Tests`, so the Avalonia `net8.0` test host was not run in this lane.
