# PR Summary: Add Uno TreeDataGrid source port and sample app

## Branch

- `feat/uno-treedatagrid-port`

## Commits

- `432eff0` — `feat: add Uno TreeDataGrid library`
- `f69e632` — `feat: add Uno TreeDataGrid sample app`
- `d49d8a4` — `docs: document Uno TreeDataGrid port`

## Overview

This branch adds a new Uno Platform source port of `TreeDataGrid` alongside the existing Avalonia implementation. The port keeps the shared model, row, column, hierarchical, and selection architecture close to the Avalonia original while adapting the control shell, templating, scrolling, layout, and input integration to Uno/WinUI mechanics.

The branch also adds a full Uno sample app with parity-focused demo tabs for `Countries`, `Files`, `Wikipedia`, and `Drag/Drop`, and updates the repository README with Uno build and smoke-test instructions.

## What changed

### 1. New Uno TreeDataGrid library

Added `src/Uno.Controls.TreeDataGrid/` as a new source port of the Avalonia control, including:

- Uno control shell and dependency-property surface in `TreeDataGrid.cs`
- WinUI/Uno themes and templates in `Themes/Generic.xaml`
- row/cell/header presenters and element factory under `Primitives/`
- shared model, source, row, column, and selection logic under `Shared/`
- Avalonia compatibility shims under `Compatibility/`
- automation peer coverage under `Automation/Peers/`

### 2. Uno-specific behavior work needed for parity

The port includes the Uno-side integration work required to match Avalonia behavior as closely as possible, including:

- viewport-driven row virtualization via `TreeDataGridRowsPresenter` and `RealizedStackElements`
- model-backed hierarchical expansion owned by `HierarchicalRow<TModel>` so `Files` expansion does not depend on cell realization timing
- rebuild requeue logic for row changes that occur during realization/rebuild passes
- explicit hidden-tab / visibility layout recovery for Uno tab and visibility lifecycles
- manual drag/drop indicator visuals via template parts instead of Avalonia adorners
- width calculations driven from the real rows `ScrollViewer` viewport to avoid false horizontal overflow
- restored keyboard selection/navigation pipeline, including Up/Down, Left/Right, Home/End, and PageUp/PageDown handling
- expander toggle styling to avoid default WinUI checked-state background leakage

### 3. New Uno sample app

Added `samples/TreeDataGridUnoSample/` with:

- main sample shell and tab host
- `Countries`, `Files`, `Wikipedia`, and `Drag/Drop` sample pages/view models
- Uno assets for file/folder icons
- Android, iOS, WebAssembly, and desktop heads
- desktop smoke-test path via `--exit`

### 4. Sample-specific fixes included in the port

The sample and port also include fixes discovered while bringing behavior in line with the Avalonia sample:

- cached file/folder `ImageSource` instances to stop icon flashing on expand/collapse
- updated file-cell bindings to use the cached icon source path
- Uno-side handling for sample scrolling, expansion, and drag/drop feedback behavior

### 5. Documentation updates

Updated `readme.md` to document:

- the new Uno library location
- the new Uno sample location
- desktop build/run commands for the Uno source port
- platform prerequisite notes for Android and WebAssembly builds

## Validation performed

The following commands were run successfully on this branch:

```bash
dotnet build Avalonia.Controls.TreeDataGrid.slnx -c Release
dotnet test tests/Avalonia.Controls.TreeDataGrid.Tests/Avalonia.Controls.TreeDataGrid.Tests.csproj -c Release --no-build
dotnet build samples/TreeDataGridUnoSample/TreeDataGridUnoSample.csproj -c Release -f net9.0-desktop
cd samples/TreeDataGridUnoSample && dotnet run -c Release -f net9.0-desktop -- --exit
```

Validation results:

- solution build passed
- Avalonia test suite passed: `329` tests
- Uno desktop sample build passed
- Uno desktop sample smoke run passed

## Commit breakdown rationale

The commit history is intentionally split into three layers:

1. library port
2. sample app and solution wiring
3. README/docs

That structure should make review easier by separating the reusable control implementation from the demo surface and then from documentation.

## Notes for reviewers

- The architectural alignment tracker and related analysis docs under `plan/` were maintained locally during implementation, but `plan/` is gitignored in this repository and therefore not part of this branch.
- The largest intentional adaptation zones remain the Uno control/template/scroll/layout layers; the shared model and selection architecture were kept as close to Avalonia as possible.
- A possible future parity follow-up is explicit text-input/type-to-search forwarding if full character-input parity is required beyond the restored row keyboard navigation path.
