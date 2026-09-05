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
- `TreeDataGrid.Controls.Uno` owns cell binding subscriptions, presentation configuration,
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
not a claim of completed parity. Countries, People, Templates, and variable-height Countries run in the desktop
sample. New draft [PR #26](https://github.com/wieslawsoltes/TreeDataGrid/pull/26) is
open; the complete showcase and Activity Monitor are still pending.

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

### Templating and selection checkpoint (2026-09-05)

- The grid and cells now derive from `Control` and use `Themes/Generic.xaml`,
  template parts, theme resources, and selected/current visual states. The default
  themes are discovered without an explicit sample-only merge on macOS/Skia.
- A view-owned selection controller maps visible columns and displayed rows to
  the exact Core row/cell selection model. Its subscriptions suspend on unload;
  existing source selection is not cleared or disposed by unloading a view.
  Source/default, none, single/multiple-row, and single/multiple-cell modes exist.
- Nine additional unit tests cover row ranges after sorting, toggle/right-click
  preservation semantics, hidden-column mapping, column moves, rectangular
  ranges, external selection replacement, suspension, and select-all. Total: 47
  passing Uno tests.
- Native checks verify selected/current cell state, row and rectangular cell
  navigation, scrolling an offscreen selected row into view, hierarchical
  expansion/collapse and parent/child navigation, and retemplating while retaining
  shared selection and releasing the previous presenter.
- Grid source replacement is staged before the working presentation is removed.
  If creation fails, the `Model` dependency property is restored to the previous
  source. A native regression verifies that the old Model/presentation pair remains
  usable. This extends the earlier column-factory failure recovery to the grid.
- The Countries render now includes its selection-mode selector, a highlighted
  Albania row, and the current-cell border; it was visually inspected. The added
  toolbar reduces the initial/scrolled realized cell counts to 108/120.
- `TreeDataGrid.Input.cs` wires pointer press/release/cancel and keyboard events
  to the tested selection/navigation operations. The runtime checks invoke those
  operations programmatically; actual OS pointer/keyboard dispatch is still a
  separate unverified gate, not implied by those passing checks.
- A dedicated Uno CI workflow now builds/tests on Linux, Windows, and macOS and
  runs native X11 regression checks with renders. Remote results are pending;
  these Skia desktop jobs do not establish Windows App SDK or browser coverage.

### Editing and shared showcase checkpoint (2026-09-05)

- Added text/numeric editing, template editors registered by presentation key,
  F2/double-tap/Enter entry, Enter commit and Escape cancel. TextBox creation is
  lazy (only on first edit); its host stays parented after editing and recycling.
  The display template stays attached while the editing template is shown.
- Each edit captures its realized model, including optional `IEditableObject`
  transactions. Recycling/source removal cancels the old edit. Failed conversions
  and setters leave the editor open with a validation border and error tooltip;
  selection cannot leave an invalid edit through the selection API.
- Six new unit tests cover buffered cancellation, exact-once commit, retry after
  write failure, template transaction ownership, synchronous cancellation during
  writeback, and failed BeginEdit cleanup. Total: 53 Uno tests; all 202 Core tests
  also passed again with the local .NET 10 roll-forward setting.
- Native checks exercise real TextBoxes (including two-way template binding),
  focus entry, focus-loss commit, numeric validation/correction, row replacement,
  source removal, and reentrant model replacement during BeginEdit. Uno dispatches
  LostFocus asynchronously; its regression waits for that event rather than
  assuming a synchronous focus callback. No grid-owned deferred recycle closure
  was introduced.
- The showcase now switches between Countries, People, and Templates using the
  same Uno control. `Person`, the People initial data, and `TemplateColumnItem`
  are source-linked from the original demo. Only neutral `ReactiveUI` is added;
  the Uno app does not reference `ReactiveUI.Avalonia` or Avalonia UI.
- People supports nested expansion, editable expander/name/title/age cells,
  native checkbox writeback, add-child and removal. Templates has 200 rows,
  sortable display templates, replacement and removal. Runtime checks assert
  model identity, initial nested expansion, live child changes, retained template
  content on replacement, correct details after scrolling, and source switching.
- Inspected settled native 2048×1280 renders in `artifacts/uno`: `countries.png`,
  `people.png`, `people-validation.png`, and `templates.png`. Capture now flushes
  layout and waits for rendering after mutations; immediate captures had shown
  stale row positions and an unarranged editor. The invalid editor has an explicit
  positive-size/input assertion in the runtime check.
- The earlier PR head `af9ea133` passed the existing Build/Test/Docs/Pack workflow,
  Uno Linux/Windows build/tests, and native Linux X11 regression run. Its hosted
  macOS job remained queued at the last check. These results do not validate this
  new checkpoint until its CI runs finish.

### Column sizing and package identity checkpoint (2026-09-05)

- The controls project, assembly and package are now `TreeDataGrid.Controls.Uno`,
  following the `TreeDataGrid.Controls.Avalonia` package naming pattern. The CLR
  namespace remains `Uno.Controls`, parallel to `Avalonia.Controls`. Sample/test
  project references and documentation use the new path; the existing Uno test
  assembly name is unchanged.
- Auto widths now come from realized native headers/cells, not a 150 px estimate.
  Width measurement is retained per view column and grows monotonically during
  scrolling/mutation. Pixel constraints do not require an unconstrained pass;
  Auto widths or Auto min/max do. Unmeasured zero Auto constraints get a temporary
  discovery slot so native content can initialize them.
- Star widths redistribute around minima/maxima instead of independently clamping
  one proportional pass. Maximum wins conflicting constraints, matching Avalonia.
  The solver recomputes normalized weights after freezing constraints, including
  extreme star-weight ratios. Thirteen sizing tests bring the Uno total to 66,
  including 500 deterministic randomized comparisons with an independent solver.
- A native regression exposed geometry commits during measurement being arranged
  with an earlier unclipped desired size. Presenters now settle changed geometry
  in their measurement pass before arranging. Unchanged geometry does not schedule
  additional viewport invalidations. Expander measurements are also forwarded to
  their inner presentation, including Auto min/max constraints.
- macOS/Skia checks pass for native auto/header width, live text growth, retained
  widths after shorter text, constrained-star redistribution, viewport resizing,
  Auto constraints, and expander inner sizing. The existing recycling, selection,
  editing, showcase, and 1,000-column virtualization checks still pass.
- A showcase selector exposes original, auto, and auto-plus-star widths. The
  updated Countries toolbar/render was inspected.
- Explicit packability was needed: the Uno SDK library otherwise skipped pack.
  A local `12.0.0.7-uno.validation.1` package was packed, inspected, and consumed by
  the same native sample using PackageReferences instead of ProjectReferences.
  The complete native smoke suite passed from those packages; the assets graph
  confirms both controls and Core were packages. This was local validation only,
  not a public release. Post-commit validation.3 also packed matching symbols
  without warnings; package repository metadata and PDB SourceLink both point to
  exact commit `6231391836a4b75a22009078729ddf3b47c7d951`. Its package-consumer
  native smoke suite passed.
- CI now includes a local package-consumer smoke run on Linux X11. The prior
  `44118261` head passed Build/Test/Docs/Pack plus Linux/Windows Uno build/tests and
  native X11 checks; its hosted macOS job remained queued. New-head results remain
  separate evidence.

### Variable-height layout checkpoint (2026-09-05)

- `RowHeight` defaults to Auto (`NaN`); `MinRowHeight` defaults to 28 logical px.
  Positive fixed heights remain available. Invalid dependency-property values
  restore the previous value before throwing, even without an attached source.
- Sparse measured row deviations support logarithmic prefix/offset queries
  without allocating an entry for every unknown row. Five tests cover ten million
  estimated rows, exact boundaries, growth/shrink, mutations, reset validation,
  and 2,000 deterministic updates against an independent dense reference.
- Rows measure native content and share their resulting height across cells.
  Width changes invalidate heights. Fully represented rows can shrink with live
  template content; horizontally virtualized rows conservatively retain the
  maximum known height so offscreen columns are not clipped. This is not complete
  layout/performance parity evidence.
- Insert/remove above the viewport preserve the displayed anchor and intra-row
  offset. Width changes retain offsets inside tall rows until measurement settles.
  Sort/reset preserves display position rather than following the old model.
  Explicit scrolling cancels pending anchors; corrections use one layout-event
  subscription, not a per-recycle dispatcher closure.
- Native macOS/Skia checks pass for wrapping growth/shrink, live template shrink,
  insertion/removal anchors, a 70 px tall-row resize anchor, variable-height and
  last-row bring-into-view, bounded realization, fixed-height mode, invalid values,
  and source removal. All preceding native smoke checks still pass.
- Variable-height Countries shares the existing Country data and uses multiline
  names, with Auto/28/48 row-height choices. Its 2048×1280 native render was
  inspected: multiline names and neighboring values align without row overlap.
- Local validation: 71 Uno tests and 202 unchanged Core tests pass, plus the full
  native smoke suite. This checkpoint has not yet completed remote CI.

### Remaining implementation and verification

- Complete control theme styling and automation, and test actual OS input/focus
  routing (including editing and drag/drop interactions with selection).
- Extend variable-height layout stress and performance coverage, including
  horizontal virtualization and collection mutation combinations.
- Complete editing, drag/drop, text search, selection cancellation, and
  column-resize/reorder gestures. Header-click sorting and checkbox writeback are
  present, but do not cover the full editing/interaction API.
- Complete XAML/declarative columns, source extensions, public lifecycle events,
  template-editing contracts, and dependency-property model binding as required.
- Add runtime failure-path, unload/GC, nested expansion, and lifecycle tests;
  measure recycling allocations/timing against the current Avalonia benchmarks.
  The current binding/geometry unit tests alone do not prove these requirements.
- Port all showcase scenarios and Activity Monitor with shared model source where
  practical. Countries, People, Templates, and variable-height Countries are not
  the requested full suite.
- Restore browser and Windows App SDK heads, finish solution/package/CI lanes, verify real
  heads, update public README, review the full diff, and finish draft PR #26.

### Reproduction

Run from the `codex/uno-core-port` worktree:

```sh
dotnet test tests/TreeDataGrid.Uno.Tests/TreeDataGrid.Uno.Tests.csproj -c Release -p:TreeDataGridUnoTargetFrameworks=net10.0 -p:DisableSourceLink=true
DOTNET_ROLL_FORWARD=Major dotnet test tests/TreeDataGrid.Core.Tests/TreeDataGrid.Core.Tests.csproj -c Release -p:DisableSourceLink=true
dotnet run --project samples/TreeDataGridUnoSample/TreeDataGridUnoSample.csproj -c Release -f net10.0-desktop -p:DisableSourceLink=true -- --smoke --screenshot-dir "$PWD/artifacts/uno"
```

The runtime command must exit zero and print
`UNO_RUNTIME_RECYCLING_PASSED`, `UNO_RUNTIME_SELECTION_PASSED`,
`UNO_RUNTIME_EDITING_PASSED`, `UNO_RUNTIME_SHOWCASE_PASSED`, and
`UNO_RUNTIME_COLUMN_SIZING_PASSED`, and `UNO_RUNTIME_ROW_SIZING_PASSED`, followed by `UNO_CORE_SAMPLE_SMOKE_PASSED`. Omit `--smoke`
to leave the showcase window open for interaction. The optional screenshot
switch writes a generated artifact, not a checked-in source file.
