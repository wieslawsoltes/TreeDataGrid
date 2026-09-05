# Uno port on the shared Core

## Scope and baseline

The replacement for PR #12 starts from master commit
`3ca47316d724e5e040ab0281a880e8df999b25fc` (stable v12.0.0.7).
The reference Uno implementation is PR #12 at
`9a5737226b5c26617da362e28ee3337812b88707`.
The working branch is `codex/uno-core-port`.

The requested outcome is a new PR containing a working Uno port of the current
TreeDataGrid, using the real Core assembly and carrying the fixes and optimizations
made since the original Uno port. The old PR is a reference for Uno templates,
input, automation, bootstrapping, and sample behavior. Its copied model sources and
Avalonia compatibility shims are not the model architecture for this port.

## Architecture

- `TreeDataGrid.Core` owns source objects, rows, sorting, filtering, expansion, and
  row/cell selection. Uno refers to those objects directly.
- `TreeDataGrid.Uno` owns cell binding subscriptions, presentation configuration,
  layout, template selection, recycling, input, and accessibility.
- The Uno grid accepts a Core `ITreeDataGridSource` through `Model`. View options
  register templates/custom presentation columns by the Core presentation key.
- Shared source/model data for the samples should remain shared with the Avalonia
  Core sample where practical. Both the showcase and Activity Monitor must run.

## Required parity and evidence

| Area | Required behavior | Verification |
| --- | --- | --- |
| Core identity | Same Core assembly and row/selection objects; no copied model layer | Package graph, identity assertions, Core suite |
| Binding | Nested owner replacement, null recovery, aliases, computed values, writeback | Binding regression tests |
| Lifetimes | Unload/source replacement clears subscriptions and pools; custom expander owns inner view disposal | Disposal and retention tests |
| Recycling | Hidden controls remain parented; template identity preserved; no deferred closure per recycle | Runtime parent/template assertions and allocation measurement |
| Row replacement | Begin/End rebind contract, exception cleanup, selection/index notifications | Lifecycle tests on actual Uno cells |
| Horizontal layout | Cumulative column geometry, bounded realized columns, fixed/auto/star constraints | Geometry tests and runtime wide-grid scroll |
| Vertical layout | Nonuniform row heights, anchoring, bring-into-view, source swaps | Runtime scroll/bring-into-view tests |
| Selection/input | Row and rectangular cell selection, keyboard, pointer, editing, drag/drop | Runtime interaction tests |
| Column changes | Visibility, width, order, presentation-key changes, sorted index mapping | Projection and runtime tests |
| Declarative API | XAML columns, selection events, source extensions | Compile and behavioral tests |
| Samples | All Avalonia showcase scenarios plus Activity Monitor | Launch, interact, screenshots and runtime smoke output |
| Platform heads | Desktop Skia, browser, Windows App SDK lane retained | Builds and runtime evidence where supported |
| Delivery | New PR based on current master, documented scope and validation | GitHub PR and CI |

## Initial audit

- The old Uno `TreeDataGridCellsPresenter.Realize` iterates all columns, so it has no
  horizontal virtualization equivalent to the current Avalonia implementation.
- Its `Unrealize` clears panel children and returns controls to a global factory;
  this requires reattachment and does not preserve the parented recycling invariant.
- The old compatibility `WeakEventHandlerManager` stores sender/delegate pairs in
  a static dictionary. A fresh implementation must use view-owned subscription
  tracking with deterministic cleanup and no global subscription roots.
- All current neutral source fixes will be consumed from Core rather than ported.

## Progress

Implementation and validation are in progress. This file is a completion checklist,
not a claim of completed parity. The initial Countries desktop sample runs; the
complete showcase, Activity Monitor, and the new PR are still pending.

### Verified foundation (2026-09-05)

- Uno.Sdk 6.5.31 library builds for `net10.0` and `net10.0-desktop`, referring to
  the actual `TreeDataGrid.Core` project without Avalonia model source links or
  compatibility shims. The sample source-links the existing Countries data/model.
- 38 Uno tests pass: nested binding owners, aliases, null/index recovery,
  writeback, suspension, pool bounds, column-factory recovery, exact-once custom
  expander disposal, Core expansion, and cumulative column geometry.
- The unchanged Core suite passes all 202 tests. This machine has only .NET 10,
  so the .NET 8 test host was run with `DOTNET_ROLL_FORWARD=Major`; a direct launch
  without that setting aborted because the .NET 8 runtime is not installed.
- Native macOS/Skia runtime checks pass for synchronous row replacement, balanced
  Begin/End hooks (not called for fresh realization), retained template content,
  native-parent identity, no cell unload/reload during replacement/sort/scroll,
  correct sorted models, incremental insertion/removal indices, column reorder
  and resize, source removal, and bounded headers/cells in a 1,000-column grid.
- Countries initially realizes 120 cells and 132 after a two-axis scroll, instead
  of the complete source. This is behavior evidence, **not** a benchmark result.
- A 2048×1280 offscreen render of the running native sample was inspected:
  `artifacts/uno/countries.png`, SHA-256
  `7b7c3bddde56a9c91559a81620f02f0282c554c12624a3eb2eefdd91189c4cad`.
  It shows readable, aligned data. Theme/selection/editor visual parity is not
  covered by this initial image. The native automation inventory did not expose
  the command-line apphost, so the sample's `RenderTargetBitmap` capture was used.

### Findings fixed while building the new port

1. **Failed column presentation replacement lost recovery.** Definitions are now
   observed independently from successfully created views. Replacement factories
   are staged before committing the projection; failures retain the previous
   view, dispose staged replacements, and remain repairable through the key.
2. **Failed expander construction could dispose a custom inner cell twice.** The
   wrapper retains ownership until construction succeeds; constructor failure
   releases its subscriptions but leaves inner disposal to that wrapper.
3. **A Core flat row is not necessarily a persistent row object.** Core's
   `AnonymousSortableRows` intentionally reuses a row object. Native cells capture
   their realized model separately, and runtime checks compare model identities.
   Exact `presentation.Rows == source.Rows` remains the shared-Core contract.
4. **Flat sorting did not refresh templates.** Flat sources publish `Sorted`
   without a collection reset. The presentation now observes that event and
   synchronously rebinds retained cells. Runtime checks assert the actual sorted
   value (`Row 199`), not just successful method return or row-object identity.
5. **Headers initially realized every column.** The header presenter now uses the
   same cumulative geometry and horizontal viewport as the cell presenter, with
   bounded parented header recycling. The 1,000-column runtime check covers both.
6. **Native smoke failures initially exited successfully.** On this desktop head,
   setting `Environment.ExitCode` followed by Uno `Application.Exit()` did not
   preserve the nonzero result. The failure path now explicitly exits with 1.

### Remaining implementation and verification

- Replace the early code-composed `UserControl` shell with the production
  templated-control/theme contract; add styling/automation and visual states.
- Implement actual auto measurement and constrained-star redistribution. Current
  auto widths use an initial fixed estimate; fixed-width geometry checks do not
  establish auto/star parity. Nonuniform row height, anchoring, resizing and
  bring-into-view are also unfinished (current rows are fixed at 28 logical px).
- Complete shared Core row/cell selection projection, pointer/keyboard routing,
  focus, editing, drag/drop, and column-resize/reorder gestures. Header-click sort
  and checkbox writeback are present but do not cover that full interaction API.
- Complete XAML/declarative columns, source extensions, public lifecycle events,
  template-editing contracts, and dependency-property model binding as required.
- Add runtime failure-path, unload/GC, nested expansion, and lifecycle tests;
  measure recycling allocations/timing against the current Avalonia benchmarks.
  The current binding/geometry unit tests alone do not prove these requirements.
- Port all showcase scenarios and Activity Monitor with shared model source where
  practical. Countries alone is not the requested full sample suite.
- Restore browser and Windows heads, wire solution/package/CI lanes, verify real
  heads, update public README, review the full diff, push, and open the new PR.

### Reproduction

Run from the `codex/uno-core-port` worktree:

```sh
dotnet test tests/TreeDataGrid.Uno.Tests/TreeDataGrid.Uno.Tests.csproj -c Release -p:TreeDataGridUnoTargetFrameworks=net10.0 -p:DisableSourceLink=true
DOTNET_ROLL_FORWARD=Major dotnet test tests/TreeDataGrid.Core.Tests/TreeDataGrid.Core.Tests.csproj -c Release -p:DisableSourceLink=true
dotnet run --project samples/TreeDataGridUnoSample/TreeDataGridUnoSample.csproj -c Release -f net10.0-desktop -p:DisableSourceLink=true -- --smoke --screenshot-dir "$PWD/artifacts/uno"
```

The runtime command must exit zero and print both
`UNO_RUNTIME_RECYCLING_PASSED` and `UNO_CORE_SAMPLE_SMOKE_PASSED`. Omit `--smoke`
to leave the Countries window open for interaction. The optional screenshot
switch writes a generated artifact, not a checked-in source file.
