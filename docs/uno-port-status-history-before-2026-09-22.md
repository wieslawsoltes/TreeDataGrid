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

## Latest implementation checkpoint: compatibility integration

See the [current completion checklist](uno-current-work.md) for the finite
remaining implementation review and validation/delivery gates.

The dirty implementation now includes compatible standalone row realization and
public cells-presenter configuration/realization methods. It uses shared Core
models, public column measurement, horizontal viewport realization and bounded
parent-retained control recycling. A native regression fixture is registered but
UNRUN. Generic presenter extension contracts and remaining standalone lifecycle/
input differences remain open; see the parity ledger. No validation or delivery
claim is based on this source-only checkpoint.

The generic stack and columnar presenter bases are also now ported from Avalonia,
with a native custom-subclass regression fixture authored and registered (UNRUN).
Headers now adopt the common columnar base, replacing their separate layout/pool
loop, with compatible public header lifecycle methods and model observation.
The common engine also retires source/factory changes during layout callbacks.
Cells now also adopt the shared columnar engine, retaining their Core model pool,
captured ownership and grid-wide control budget. The separate standalone cells
layout was removed. Rows now also use the common Core.IRow presenter base, with
reference Items/ElementFactory/Columns configuration and actual overridable
lifecycle/layout hooks. The native sparse height geometry, body viewport,
committed column widths and bounded parented pools are retained as overrides;
duplicate row-event forwarding was removed. Extended native checks are still
UNRUN. Remaining API/lifecycle/theme review and all validation gates are open;
source presence is not behavioral or performance parity evidence.

The presentation now also exposes reference-style source/state/selection/sort/
move/notification members. Grid input and row/cell highlights use its selection-
interaction contract, with native default behavior and replace/unload/reload
observation cleanup. New unit/native fixtures are authored but UNRUN. This does
not close the separately documented Skia committed-text transport limitation.

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
not a claim of completed parity. Countries, People, Templates, variable-height Countries,
Wikipedia, Files (tree/flat), and Find Country run in the desktop
sample. New draft [PR #26](https://github.com/wieslawsoltes/TreeDataGrid/pull/26) is
open. Activity Monitor is implemented in local commit `46d867c1`; complete
showcase/API parity remains open. The current work follows the
[implementation-first parity ledger](uno-parity-audit.md): finish missing code
before the comprehensive validation pass. Earlier results below are historical
checkpoints, not evidence for the new, unvalidated parity changes.

### Latest row API checkpoint (2026-09-20; unvalidated)

Added the shared-Core-compatible ITreeDataGridRows view contract. It returns
original Core rows/models and adds caller-owned cell realization/release; normal
grid cells retain their existing pool. Public Rows now refers to a view facade,
as in Avalonia, while Model.Rows remains the exact original Core collection.
Five unit cases and a native realization/writeback/lifetime case are authored,
unrun. Standalone row/presenter realization and generic base customization remain
unfinished; no new validation or PR push is claimed.

### Latest column API checkpoint (2026-09-20; unvalidated)

Added IColumns/ColumnListBase<TColumn> and exposed the contract through the
presentation and row. It retains the actual view columns/Core definitions and
integrates public measurement/width/geometry operations with native layout.
Column listeners are weak and collection-owned; no global or per-cell tracker
was added. Five unit cases plus a native public-width mapping case are authored,
unrun. This supplies a missing prerequisite; standalone row/presenter realization
and generic base customization remain incomplete.

### Latest cell API checkpoint (2026-09-20; unvalidated)

The Avalonia-shaped public virtual cell Realize overload now runs in normal grid
realization and supports borrowed standalone models. Added deterministic adapter
cleanup, the selection query/event contract and EndEdit. New native/unit checks
cover override dispatch, Core flyweight identity, reuse, observation, specialized
values, templates and failure cleanup; all are unrun. Row/presenter/base contracts
and full input compatibility remain implementation work.

### Latest binding implementation checkpoint (2026-09-20; unvalidated)

Local named-source subscriptions now have deterministic ownership and cleanup;
Uno-resolved ordinary relative paths preserve setter failures. Explicit sources
without property-change notifications refresh after writes, and conversion cannot
write through a replaced nested owner. Native regressions and two unit cases are
authored but not run. Remaining advanced-binding and public standalone presenter
contracts are recorded in the API/parity ledgers. This does not advance the
historical validated baseline or establish complete API parity.

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

### Wikipedia and shared feed checkpoint (2026-09-05)

- Extracted the existing Wikipedia DTOs into a UI-neutral partial shared by the
  Avalonia and Uno samples; Avalonia image behavior remains in its own partial.
  The JSON source-generation context and offline file icon are also source-linked.
  The Uno package graph still contains no Avalonia libraries.
- Wikipedia uses real Core rows, three view templates, constrained star widths,
  wrapping extracts, lazy images, sorting, async loading/cancellation, and an
  explicitly synthetic 240-row fallback. Source updates are committed only by the
  current request. Scenario changes and unload cancel pending feed requests.
- Nine new Core-only sample-state tests cover dated/User-Agent requests, shared
  deserialization, missing pages, empty feeds, HTTP/JSON failures, cancellation,
  superseding reloads, and disposal. The legacy Avalonia sample's five tests and
  Core sample's native test pass after the shared model extraction.
- Native macOS/Skia checks pass for variable wrapping, lazy image creation,
  packaged image decode, correct retained template/image/model identities after
  scrolling/sort/reload, and bounded realization. An injected delayed remote
  image verifies its User-Agent and successful decode after its original cell is
  rebound to an article with no image; the late completion does not overwrite or
  detach the reused native Image.
- Live validation loaded 72 articles for 09-05. It exposed a real image failure:
  Wikimedia returned 403 without a User-Agent, versus 200 with the sample's
  identified client. Remote images now use that client followed by native stream
  decoding. All four realized live thumbnails then decoded. Both offline and live
  2048×1280 renders were inspected; the full existing native smoke suite passes.
  Live network checks are optional (`--wikipedia-live`), not deterministic CI.
- Added `solutions/TreeDataGrid.Uno.slnx` for Core/Uno/sample/test development; CI builds that
  solution and runs the new sample-state tests. Browser and Windows App SDK heads
  remain unfinished. The prior `1b5634ac` head passed Build/Test/Docs/Pack, Uno
  Linux/Windows tests, and native Linux X11/package-consumer checks; hosted macOS
  was still queued. Those remote results do not validate this new checkpoint.
- The Uno solution lives below `solutions/`, not beside the root Avalonia solution:
  the existing build/release workflows use implicit root solution discovery.
  A second root solution would make those commands ambiguous.

### Files and Find Country checkpoint (2026-09-05)

- Source-linked the existing `FileTreeNodeModel`; only UI dispatch is implemented
  in platform partials. The shared model now owns recursive watcher disposal,
  ignores queued notifications after disposal, tracks directory names as well as
  files, updates HasChildren after create/delete, and relocates loaded descendants
  and watcher paths after directory rename. Checkbox state now notifies bindings.
- The Uno file view loads its initial directory off-thread and commits only the
  current request. Tree/flat sources share node objects and folder-first sorting.
  Missing folders retain the working source and show an error. Source replacement,
  navigation away, and unload close owned watchers. The UI never modifies files;
  mutation tests use newly created temporary fixture directories with cleanup.
  Nested expansion still uses the shared model's synchronous lazy enumeration;
  broader filesystem latency/performance work is not claimed complete.
- File rendering exposed an eager child-enumeration bug in the Uno expander:
  `SubscribeChildren` invoked the child getter even with an explicit HasChildren
  expression. It now trusts that binding's tracked dependencies and does not open
  children until expansion. Two regressions verify both true/false indicators and
  live binding changes with a child getter that must not be called.
- Find Country uses the complete shared country catalog, a separate filtered Core
  source, and model-to-display index mapping after sort/filter/clear-sort. The
  selected catalog model stays selected when excluded; the status explains that
  it is not displayed. Native ListView/TextBox events and Core selection/scroll
  results are asserted, not merely the helper's return value.
- Validation: 73 Uno tests, 16 sample-state tests, 202 unchanged Core tests, both
  Avalonia sample suites (5 legacy, 1 Core native), and the complete native smoke
  suite pass. File tests cover real create/change/rename/delete events, loaded
  directory renames, queued-event disposal, failed opens, and close during load.
  Native checks verify lazy nested expansion, watcher-driven row changes, shared
  checkbox state, flat/tree identity, bounded realization, watcher cleanup, and
  sorted/filtered Find Country visibility. Three 2048×1280 renders were inspected.
- Prior `f8a624fd` CI passed Build/Test/Docs/Pack, Linux/Windows Uno builds/tests,
  and native X11/package-consumer checks. Hosted macOS remained queued. Master
  was rechecked at `3ca47316`; no rebase is necessary. New-head CI remains separate.

### Activity Monitor checkpoint (2026-09-05)

- Ported the original PR #12 five-tab shell, tables, numeric comparisons, filters,
  inspectors, demo/native telemetry and Skia charts. Sources and selection now
  come from the actual Core assembly. No Avalonia model/presentation shim is used.
- Added immutable Uno `TextCellOptions` and public value-column presentation for
  numeric alignment; expanders forward inner text options. Rebind restores the
  template's defaults when options are absent, without detaching the control.
  Immutable-option identity avoids repeating text dependency-property writes
  on normal row recycling. This is a code-path safeguard, not a measured
  performance claim; the comparative benchmark gate remains open.
  Core column definitions remain UI-neutral. Replacing a column factory may
  intentionally retire controls because its factory may choose another type;
  that is distinct from row recycling/rebind, which retains parents/templates.
- Fixed snapshot selection loss during Core source replacement, overlapping
  capture/disposal, late capture results after unload, provider-error reporting
  and retry, source cleanup, repeat disposal, and timer/storyboard lifetimes.
- Corrected native process-count units, bounded native buffer reads, unsigned
  counter resets/PID-reuse deltas, host-port cleanup, 32-bit interface timeval
  layout and sysctl value widths. Root-volume totals avoid shared APFS capacity
  duplication; unavailable process compression displays `—`, not wired memory.
- A controlled CPU-load comparison against .NET `Process.TotalProcessorTime`
  exposed an additional Apple Silicon error: treating Mach ticks as nanoseconds
  underreported process CPU by about 41.7×. CPU/user deltas now use
  `mach_timebase_info`; the macOS-only numeric sanity check passes after the fix.
- Preserved OneWay compiled template updates across row replacements. Corrected
  chart hover bounds and explicit Skia-resource disposal, and removed a canvas
  clear that erased the compositor background. Compacted the desktop shell and
  made navigation/inspector scrollable to keep all tabs accessible.
- Local validation: 75 Uno, 23 sample-state and 202 Core tests pass. Full showcase
  native smoke passes, including text-option reset/parent retention. Activity
  Monitor demo and live macOS runs pass all five section checks, stable-key
  selection, retained cell/template identity without unload/reload, last-row
  virtualization, and async lifetime/failure checks. Live runs display 280 rows
  in each process table and 29 interfaces on this machine. Native 2048×1280
  renders were inspected. These are desktop checks, not browser/WinAppSDK proof.
- CI now builds Activity Monitor and runs its deterministic native X11 checks
  using both project and package references. Native telemetry is read-only and
  local. Main still points to `3ca47316`; no rebase is needed at this checkpoint.

### Remaining implementation and verification

The current implementation-first work is tracked in
[the parity ledger](uno-parity-audit.md) and
[the API compatibility contract](uno-api-compatibility.md). New working changes
include native headers/drag-drop, real row/cells presenters, RowStyle, row
lifecycle events and TargetRow/lookup APIs. They have not been built or tested;
the earlier checkpoint results in this document do not validate these changes.
The 2026-09-20 working checkpoint additionally restores typed selection deltas
and cell lifecycle/value events, with unrun unit/native regression cases. Remote
master and draft PR #26 were re-read and remain at the baseline/head above.
The same implementation pass adds assignable typed presentation options and a
public generic presentation. Activity Monitor now supplies typed options. Exact
typed UI column factory/facade compatibility remains open; new configuration
unit/native cases have not run.
The public-column step now adds ICellColumn<TModel>, native column/layout/cell
contracts and text/checkbox/template facades. Activity Monitor uses the text
facade. Custom-cell/base-type and remaining layout compatibility are still open;
all new product code and regression cases remain unvalidated.

Fluent source construction now has the matching text/checkbox/template/row-header
and expander overloads over actual Core sources. View metadata uses weak-key
registrations with separate view columns/template caches, preserving source Tag.
The native search-binding probe includes converter/parameter behavior. Model-index
row headers snapshot Core flyweight rows rather than retain them. Template and
row-header descriptors are present, but ItemsSource/ColumnDefinitions and other
declarative binding types still need implementation. Eight unit cases and one
native fluent suite are authored but unrun; no current validation is implied.

The next implementation step adds ItemsSource/ColumnDefinitions, text/checkbox/
hierarchical declarative bindings, actual generated Core source ownership and an
original-list adapter. Optional neutral Core observer interfaces let Uno bindings
drive nested expansion/child-reference changes without copied row algorithms.
Ordinary binding writes explicitly surface setter errors that Uno's native path
otherwise logs/swallow; native bindings still handle observation. Declarative
People is now in the scenario selector with the existing shared model code.
Four source/adapter tests, three Core observer tests and a native declarative
suite are authored but unrun. Advanced binding forms, Source compatibility and
full source-lifetime/reentrancy handling remain implementation work.

Source/SourceProperty and Source-based row/cell selection accessors are now added,
using actual Core sources and matching Source/Model switching. Generated-source
owners survive promotion into either explicit entry point and retire afterward;
caller sources are never disposed. Reentrant row reset now removes only captured
old controls, and cleanup attempts remaining cells/columns after individual errors.
Four cleanup unit cases and a native source/ownership/reentrancy suite are authored
but unrun. This does not close the remaining advanced-binding, unload/GC and
retemplating failure paths or the broader parity ledger.

- Complete control theme styling and automation, and test actual OS input/focus
  routing (including editing and drag/drop interactions with selection).
- Extend variable-height layout stress and performance coverage, including
  horizontal virtualization and collection mutation combinations.
- Complete editing, drag/drop, text search, selection cancellation, and
  column-resize/reorder gestures. Header-click sorting and checkbox writeback are
  present, but do not cover the full editing/interaction API.
- Complete XAML/declarative columns, remaining source-extension/lifecycle compatibility,
  template-editing contracts, and dependency-property model binding as required.
- Add runtime failure-path, unload/GC, nested expansion, and lifecycle tests;
  measure recycling allocations/timing against the current Avalonia benchmarks.
  The current binding/geometry unit tests alone do not prove these requirements.
- Finish all showcase scenarios with shared model source where practical.
  Drag/drop and Declarative People are now implemented but unvalidated. Existing scenarios
  also need their full interaction controls. Activity Monitor desktop is now
  present, while its additional platform heads remain unfinished.
- Restore browser and Windows App SDK heads, finish solution/package/CI lanes, verify real
  heads, update public README, review the full diff, and finish draft PR #26.

The element-factory checkpoint adds Avalonia-named creation/recycling hooks for
rows, headers and cells, lazy default-factory customization, compatible retained
parent reuse and factory replacement. The direct pool preserves same-parent-first
and cross-panel fallback with weak visual-root ownership; native presenter pools
remain bounded and independently owned. Header reset/factory callbacks are guarded
against source replacement. The old cell-only delegate remains supported. A native
factory suite is authored and unrun. Named specialized cell controls and the rest
of the primitive API remain open; this is implementation progress, not parity or
validation completion.

The specialized-cell step now adds named text/checkbox/template/expander controls,
their scalar dependency properties and default styles. Default factories select
by view-column kind, preserving boolean text columns. Public writeback, template
content/context and Core expansion use existing subscriptions and retained native
rendering. Native scalar/retention cases and expander checks are authored, UNRUN.
Expander inner-control factory customization and remaining public model/base/
presenter/template/automation contracts still need implementation review.

The expander-inner step now routes child creation and reuse through ElementFactory,
retains compatible children, and uses a dedicated chevron/Border template. Editing,
validation, selection foreground, lookup and public value events account for the
inner/outer ownership relationship. UI expander interfaces expose shared Core rows.
Native inner-factory/editing/retention/reentrancy cases are authored, UNRUN. This
closes the missing implementation path, not its correctness/performance gates;
remaining public cell-model/base/presenter compatibility still needs review.

The public-cell-model step adds TextCell<T>, CheckBoxCell and TemplateCell with
native typed observable binding and transaction/disposal behavior. Custom-column
adapters preserve checkbox/template kinds, original factory model identity,
per-cell templates/culture and actual template editing content. Checkbox writes
remain enabled independently of text-edit gestures. Six unit cases are authored,
UNRUN. Arbitrary third-party expander adaptation and custom suspension/reuse are
still implementation work; no current native parity/performance evidence is implied.

Third-party IExpanderCellPresentation models now preserve original model/Core-row
identity through recursive adapters. Native content replacement handles compatible
rebind, changed kinds and empty/restored children; disposal follows the original
expander's ownership rather than double-disposing borrowed cells. Three unit cases
and extended native checks are authored and UNRUN. Custom suspension/reuse and
remaining public primitive compatibility are still implementation work.

Custom column TryReuseCell is now used during synchronous retained-model rebind,
matching Avalonia's public extension path without exposing its internal suspension
contract. Original ICell/Core rows are passed through; leftover models are released
at finalization/reset/eviction. Native long-lived pooling still requires safe
suspension and now respects custom column reuse policy. Four unit cases and a
native reuse/subscription/failure suite are authored, UNRUN. Remaining public
primitive contracts and comprehensive validation are still outstanding.

The primitive contract review corrected two non-platform API deviations: public
cell Model/template DataContext/model-change sender now expose the original ICell,
and row lifecycle overrides receive the same row-index arguments as Avalonia.
Added idempotent protected model-subscription hooks, IsEffectivelySelected and
public row update/unrealize entry points. Native internals use the explicitly
named ViewModel adapter. Extended native identity/subscription/row-hook cases are
authored, UNRUN; standalone realization/presenter contracts still need review.

Row viewport caching now has the reference CacheLength property/default/range,
edge compensation and viewport override hooks. Small buffered scrolls preserve
realized containers without measuring again; zero cache replaces the implicit
one-row overscan with exact viewport intersection. Geometry invalidation covers
fixed-height column resize as well as source/variable-height changes. Geometry
and native retention/cache cases are authored, UNRUN. This is implementation
progress, not evidence of runtime or performance parity.

Declarative writeback now rejects malformed paths before any model getter/setter
runs and selects numeric/string indexer overloads deterministically. Ambiguous
fallbacks, private getters, read-only endpoints and copied struct owners fail
explicitly rather than mutating an arbitrary endpoint or reporting success.
Focused path/conversion/failure cases are authored and UNRUN. Advanced native
binding forms and generated metadata/AOT remain open implementation work.

Generated named-property writeback now uses Uno's public metadata provider before
reflection (with a Windows App SDK guard). Native binding lifetime code handles
nested mutations, retirement during getter/converter/setter callbacks, error-state
ownership and cleanup during installation/disposal. Template search shares snapshot
cleanup so explicit Source bindings do not remain subscribed between searches.
Five metadata unit cases and a native lifetime/search-snapshot suite are authored,
UNRUN. This does not establish AOT, advanced binding or runtime parity.

Header/custom-template compatibility now includes the reference Header property,
standard editor/content-presenter part names, editing visual states, live sort
resources and a native resize cursor. Template-bound text edits preserve the
transaction buffer through cancellation/retry and retain native subtrees during
compatible recycling. Exact write-count, template/parent retention and header
checks are authored, UNRUN; broader API/platform work and validation remain open.

Row template properties now expose native Rows/Columns/ElementFactory DPs.
Row-specific factory overrides propagate to expander children and replace only
that row's cell containers; nested overrides coalesce, and final release clears
stored source/factory references. Native identity/localization/reentrancy/fallback
and inner-factory checks are authored, UNRUN. Standalone presenter configuration
is still outstanding; no full API or runtime parity is claimed.

Navigation/edit ownership corrections now cover vetoed hierarchy movement,
empty-selection End, row-selection Left/Right semantics, nested/source-changing
selection callbacks, source retirement during bring-into-view, edit transfer from
commit callbacks and recursive transaction rejection. Native cases and two session
unit cases are authored, UNRUN. Native target-cell focus/Tab behavior is explicitly
still incomplete; this is not a claim of complete keyboard or editing parity.

The subsequent focus checkpoint implements native target-cell focus/system
visuals, focused-cell F2, guarded editor return focus and display-ordered Tab
children in all presenters. Focused offscreen rows/cells/headers retain their
model/parent identity until native focus loss permits recycling. A registered
native focus/Tab/retention suite is authored, UNRUN. OS input/editor keys/focus
rendering remain validation gates; the overall implementation ledger stays open.

Generated indexer writeback now follows Uno metadata before reflection, including
key coercion, read-only rules, retirement and setter error/retry. Nine cases are
authored, UNRUN. Generated-only endpoints retain the native object value contract
because indexer metadata lacks a declared type; full AOT and advanced named/
relative/compiled binding requirements remain outstanding.

### Reproduction

Browser launch/result transport and Files sandbox mode are now implemented but
unrun. Both apps accept allowlisted URL flags and publish complete/passed/error
smoke state; browser pages remain open. Files use the same shared model with
watchers disabled and explicit snapshot refresh, clearly labeled as sandbox data.
Four launch/result and two snapshot cases are authored; actual browser execution,
input/screenshots, AOT/trimming and all remaining parity gates are still pending.

Browser and native Windows App SDK configurations have been restored for both
samples, with separate source-reference/package-consumer CI build lanes authored.
The native Windows Activity Monitor chart surface reuses the same drawing code
through a DPI-aware retained bitmap; desktop/browser retain direct Skia drawing.
None of these new targets or workflows has been built/run/pushed. Browser file
sandbox/watchers, smoke-result transport, trimming/AOT and real platform input
remain implementation/validation work. See `uno-platforms.md` for exact scope.

Appearance implementation now invalidates natural-width/variable-row caches on
font/theme/flow-direction and row/header-style changes, including retained hidden
columns, while keeping controls parented. Default foreground inheritance and
Source-only header sorting were corrected. Three unit cases and a native
appearance/RTL suite are authored and unrun. Native RTL pointer resizing, scaling,
high contrast and custom-theme verification are still required; no validation
or PR push was performed for these edits.

Committed-text search now has the reference prefix/cycling/timeout behavior and
native-control regression code, still unrun. Supported CharacterReceived heads
are wired through a capability guard. Skia's public event is unimplemented in the
inspected Uno source: its actual OS text transport, IME/dead keys/layout handling
remain open and are not covered by the injected-text suite. Nested grid input
routing was isolated while implementing this path. No validation was started.

Accessibility implementation now includes grid/row/header-presenter peers matching
Avalonia's realized-row selection contract, current Core expansion/value actions,
hidden-container filtering and stale/disabled-provider checks. Notifications use
existing lifecycle events and already-created peer fields: Uno's inspected
FromElement implementation may create peers, so it is not used on recycling
notification paths. A native automation suite is authored and in the smoke
sequence, but no compilation, provider execution, OS event delivery or assistive-
technology validation is claimed for this uncommitted implementation.

Run from the `codex/uno-core-port` worktree:

```sh
dotnet test tests/TreeDataGrid.Uno.Tests/TreeDataGrid.Uno.Tests.csproj -c Release -p:TreeDataGridUnoTargetFrameworks=net10.0 -p:DisableSourceLink=true
dotnet test samples/TreeDataGridUnoSample.Tests/TreeDataGridUnoSample.Tests.csproj -c Release -p:DisableSourceLink=true
DOTNET_ROLL_FORWARD=Major dotnet test tests/TreeDataGrid.Core.Tests/TreeDataGrid.Core.Tests.csproj -c Release -p:DisableSourceLink=true
dotnet run --project samples/TreeDataGridUnoSample/TreeDataGridUnoSample.csproj -c Release -f net10.0-desktop -p:DisableSourceLink=true -- --smoke --screenshot-dir "$PWD/artifacts/uno"
```

The runtime command must exit zero and print
`UNO_RUNTIME_RECYCLING_PASSED`, `UNO_RUNTIME_SELECTION_PASSED`,
`UNO_RUNTIME_EDITING_PASSED`, `UNO_RUNTIME_SHOWCASE_PASSED`, and
`UNO_RUNTIME_COLUMN_SIZING_PASSED`, `UNO_RUNTIME_ROW_SIZING_PASSED`, and
`UNO_RUNTIME_WIKIPEDIA_PASSED` and `UNO_RUNTIME_FILES_FIND_PASSED`, followed by
`UNO_CORE_SAMPLE_SMOKE_PASSED`. Omit `--smoke`
to leave the showcase window open for interaction. The optional screenshot
switch writes a generated artifact, not a checked-in source file.
