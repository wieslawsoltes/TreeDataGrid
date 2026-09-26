# Uno parity validation: 2026-09-22

## Scope and tested revision

PR #26, branch `codex/uno-core-port`, keeps the actual `TreeDataGrid.Core`
assembly shared with Avalonia. No copied model implementation, Avalonia shims,
framework repository modification, release, or merge is part of this checkpoint.

The implementation checkpoint is `502f5ab9e445eb2d7c5b2e527ac3288c7e609e2b`.
GitHub validated its merge with master `3ca47316` as
`6b8c179ad93dbb13294eda0c519575c2da160fd8`. Later documentation or test commits
are not retroactively covered by those runs.

**Full API, functional and performance parity is not yet certified.** Individual
successful suites and lower allocation counts must not conceal the remaining
sequential, package-consumer, platform-input and measured-performance failures.

## Implementation changes

### Native variable-height bring-into-view

`TreeDataGridPresenterBase.BringIntoView.cs` now corrects a target rectangle after
native effective-viewport delivery has allowed preceding rows to obtain measured
heights. Repeating the original estimated rectangle synchronously was insufficient:
the target shifted after that request and could be recycled out of the viewport.

Correction is bounded to eight dispatcher passes and only continues after target
geometry changes. A monotonically increasing request identifier supersedes older
explicit requests; presenter-generation checks reject source replacement. The
callback holds a weak presenter reference and no source or cell/row container.
Native `StartBringIntoView` routing remains in use, preserving native cancellation,
clipping, nested viewers and explicit local target rectangles rather than issuing
unconditional `ScrollViewer.ChangeView` operations.

`BringIntoViewRuntimeChecks` validates distant, final and reverse row targets,
rectangles inside oversized rows, superseded requests, source retirement,
unload/reattach, correct Core model identity and bounded realization. The original
standalone generic-presenter regression and this new suite both pass.

### Horizontal control recycling

The cells presenter previously checked only its pool keyed by the incoming column.
A distant horizontal window therefore created a new native template tree for each
cell even when compatible controls from the previous window remained parented to
the same row. This also overflowed bounded pools and amplified later diagonal
scrolling and sorting allocations.

The presenter now prefers a same-column pooled cell, then performs a bounded,
allocation-free scan for another column's factory-compatible native control.
It releases the old column's model ownership: **only the native control transfers
across columns, never another column's binding/model**. Generation checks run after
application factory callbacks. The legacy cell-factory delegate keeps its original
per-column behavior because it supplies no compatibility-key contract.

`CrossColumnRecyclingRuntimeChecks` exercises 128 differently formatted columns
and six disjoint/reverse horizontal windows. It checks zero additional native
controls after boundary priming, no unload/reparent events, correct column format
and row identity, visible-row replacement, and source cleanup. This suite passes.

### Smaller default text-cell visual tree

The native text-cell style no longer instantiates the general-purpose checkbox,
expander, display ContentPresenter and nested column grids. It retains the text
part, lazy editor host, editing-template presenter, border/padding, independent
selection/current/validation states and semantic theme resources. Generic,
checkbox, template, expander and application-supplied templates remain separate.
The complete isolated suite set passes with the specialized style.

### Build and diagnostic corrections

The paired Avalonia host explicitly accepts `TreeDataGridCore.FlatTreeDataGridSource`
rather than resolving the same-named legacy source type from the Avalonia namespace.
Both benchmark hosts now execute against the intended shared Core source.

Windows-only sample SkiaSharp references use 3.119.2, satisfying the transitive
HotDesign requirement without suppressing the NU1605 downgrade. The native Windows
showcase now builds; Activity Monitor still has a separate XBF compilation failure.

The column-sizing fixture commits its new extent before requesting its initial
scroll position and reports actual source/viewport/presenter state on failure.
This improves diagnosis; it does not fix or accept the sequential recovery failure.

## Functional evidence

[Validation run 35740892721](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35740892721),
job `106790044799`, artifact `10700020208` (`uno-validation-report`):

| Validation | Result |
| --- | --- |
| Shared Core unit tests | 210 passed |
| Uno unit tests | 215 passed |
| Avalonia unit tests | 520 passed |
| Uno sample-state tests | 29 passed |
| Total unit tests | 974 passed; zero failed; zero skipped |
| Native desktop showcase and Activity Monitor builds | Both passed; zero warnings/errors |
| Activity Monitor demo/lifetime checks | Passed for CPU, Memory, Energy, Disk and Network |
| Isolated native suites at this revision | 30 of 30 passed |
| Sequential native showcase | Failed after the throwing custom-reuse test |
| API inventory dependency resolution | Zero unresolved baseline or target types |
| Strict identical-assembly API self-diff | Passed |

The isolated checks include source ownership, binding lifetime, declarative null-owner
recovery, selection, focus, editing, factory/reuse reentrancy, templates, hierarchy,
appearance/RTL, automation providers, column/row sizing, viewport caching, scrolling
and the two new regressions. Their scope remains the actual assertions, not all
possible OS input, screen-reader or rendering behavior.

The immediately preceding run at `74418d7` had a delayed Wikipedia-image assertion
and a GLX `BadAccess` failure during teardown after the automation assertions.
Those failures were retained as failures. The later 30/30 run is a successful
checkpoint, not evidence that repeated native/environmental reliability is settled.

## Paired native performance

[Performance run 35740892949](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35740892949),
job `106790049143`, artifact `10699900110` (`native-parity`), measured the same
implementation revision. Both builds and all four native executions succeeded.
Two AB/BA pairs used 64 columns, 25 iterations per workload per process, the same
machine/runtime/architecture, exact DejaVu Sans font and matched viewports.
Each sample verifies geometry, visible model/text identity and bounded realization.

The unchanged hard diagnostic budget is **Uno/Avalonia <= 1.10 for both median
synchronous UI-thread duration and allocation in every workload**. It still fails.

| Operation | Avalonia median ms | Uno median ms | Time ratio | Avalonia median allocated bytes | Uno median allocated bytes |
| --- | ---: | ---: | ---: | ---: | ---: |
| Horizontal scroll | 0.50705 | 1.56590 | 3.09 | 21,184 | 29,940 |
| Vertical scroll | 1.35360 | 3.31270 | 2.45 | 99,352 | 212,584 |
| Distant diagonal scroll | 2.54945 | 13.99850 | 5.49 | 503,312 | 1,075,688 |
| Replace visible row | 2.05135 | 4.27800 | 2.09 | 37,952 | 110,352 |
| Resize visible column | 3.73280 | 4.77600 | 1.28 | 87,416 | 138,224 |
| Sort | 34.89330 | 47.40700 | 1.36 | 1,675,960 | 1,097,952 |

The first runnable checkpoint, before cross-column recycling, recorded Uno median
allocations of 3,690,312 bytes for horizontal scroll, 17,138,928 for distant diagonal
scroll, and 63,116,936 for sort in
[run 35737519783](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35737519783).
The corresponding latest values are 29,940, 1,075,688 and 1,097,952 bytes: approximately
99.2%, 93.7% and 98.3% lower respectively. These are measurements from distinct
revision runs on different hosted runners; each Uno-versus-Avalonia pair itself
uses one machine. Do not interpret between-run timing changes as a controlled
single-machine optimization experiment.

The raw artifact also preserves p95 and layout-settlement timings. These tests
measure synchronous UI-thread source/layout work and verified layout settlement,
not GPU completion, frame rate, physical input latency, variable-height workloads,
hierarchical workloads or complete feature parity. Sorting allocation is lower
than Avalonia here, but sorting duration and the overall acceptance gate still fail.

## Remaining acceptance work

### Native exception recovery

After the intentionally throwing custom-reuse callback is caught, the next
single-row source has no realized cells. Native geometry remains the retired
200-row extent: `sourceRows=1`, `rows=0`, `cells=0`, `offset=0,0`,
`viewport=618,439`, `extent=618,5600`, `presenter=618,5600`.
The integration test remains a failure.

Uno [6.6.166 native measurement source](https://github.com/unoplatform/uno/blob/6.6.166/src/Uno.UI/UI/Xaml/UIElement.Layout.crossruntime.cs)
sets `MeasuringSelf`, calls `MeasureCore`, and clears the flag only after normal
return. The inspected upstream implementation contains an exception-safe helper
with `finally` clearing that flag
([upstream change e5e238e](https://github.com/unoplatform/uno/commit/e5e238eb764b85e080c9d742c0928c8491c84e95)).
The release-source defect is consistent with the observed freeze; exact runtime
confirmation is provided by the separately added framework-only
`native-layout-recovery` probe. This probe uses only a bare native Control,
repeats the identical measure constraint and never accesses framework-private
flags. Its own result must be checked independently of the 30/30 checkpoint above.

Do not conceal this failure by increasing delays, changing the test's measure
constraint, skipping the expected exception, reflecting into native private state,
or rebuilding the entire visual tree merely to make the fixture pass.

### Platform/package consumers

At [Uno workflow 35740185524](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35740185524):
the desktop solution/unit-test matrix passes on Windows, Linux and macOS. These
are distinct from native Windows App SDK and browser package-consumer gates.

Native Windows builds the library and showcase after the SkiaSharp correction.
Activity Monitor reaches `App.xaml(168,48)` and fails XBF with WMC0612,
`Property Not Found`, at its typed identity template. Its compiled OneWay bindings
also warn that immutable snapshot properties do not raise change notifications.
The later Windows packaging/consumer steps are therefore not validated.

Both browser samples build and Core/Uno packages are produced. Trimmed
PackageReference publishing still fails. Diagnostics identify reflection-based
model/interface/property/indexer discovery, TypeDescriptor conversion in native
binding writeback, and reflection in ReactiveUI/Rx/Uno dependencies. These require
explicit preservation/generated-accessor contracts and validated dependency
handling, not blanket trimming or warning suppression. Browser runtime automation
is a separate, still-required validation dimension.

### API and broader performance

The compiled metadata audit reports 1,748 baseline and 1,525 target shapes:
823 exact namespace-normalized matches, 925 missing-or-different and 702
additional-or-different entries. These are not feature-completion percentages.
Relocated Core contracts, native framework types, inheritance/accessibility and
actual missing members require explicit classification plus compatibility tests.
The report correctly keeps `completeApiParityProven` false.

The native timing gaps above remain measurable. Next optimization boundaries are
rebind/selection/layout invalidation costs, bounded row/cell bookkeeping and cold
hierarchical/variable-height paths. Real pointer, keyboard, drag/drop, Unicode/IME,
accessibility and multi-head input/rendering evidence remains required. No acceptance
threshold was relaxed by this continuation.

## Reproduction

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop \
  python3 build/validate-uno-linux.py

# After building the native showcase:
python3 build/run-uno-native-suites.py \
  --suite bring-into-view --suite cross-column-recycling

python3 build/run-uno-native-suites.py --suite native-layout-recovery

python3 build/run-native-parity.py \
  --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

The read-only validation workflows test their actual checkout and retain failures,
raw measurements, source revisions, toolchain information, TRX files and native logs.
A later tested revision supersedes this checkpoint; a successful job step alone
is not a substitute for checking the aggregate exit code and individual assertions.
