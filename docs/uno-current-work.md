# Current Uno completion checklist

Updated 2026-09-22. This supersedes the 2026-09-21 24/28-suite checkpoint.
The latest Windows and binding-lifetime continuation is recorded in
[Windows and binding validation](uno-windows-binding-validation-2026-09-22.md).
**Complete API, functional and performance parity is not yet certified.**
The detailed implementation and measurement report is
[Uno parity validation: 2026-09-22](uno-validation-2026-09-22.md).

## Branch and architecture

PR #26 uses `codex/uno-core-port`, based on master `3ca47316`. Changes are
committed directly to that branch; no merge or package release is implied.
`TreeDataGrid.Controls.Uno` references the actual `TreeDataGrid.Core` assembly
shared with Avalonia. Model, hierarchy, selection and source ownership remain in
Core; native presentation, binding, layout and input remain in the view.

## Latest Windows and binding continuation

- Native Windows App SDK library and both samples build, packages are produced,
  and both PackageReference sample consumers publish successfully in run
  `35747491998`, job `106812743366`, at `a69db731`.
- The Activity Monitor identity template now has an initialized compiled resource
  dictionary. Its retained x:Bind content still retargets across immutable telemetry
  snapshots. MetricSeries has a genuinely initialized, XAML-activatable empty
  constructor; required declarations remain, and three new tests pass.
- `CellBinding` now serializes reentrant lifetime operations and validates a
  revision after application getters, subscription accessors and value equality.
  A retired result/error is never published, including same-row retargets.
  Eleven reentrancy tests and a zero-allocation warmed-retarget regression pass.
- Local validation records 989 passed unit cases (Core 210, Uno 227, Avalonia 520,
  sample state 32), zero failed/skipped, all 30 TreeDataGrid native suites and
  Activity Monitor checks passing. The separate bare-Uno recovery test and
  sequential integration still fail. The first local all-suite invocation was
  interrupted by the host; all seven uncompleted suites were executed separately.
  See the linked report for the exact environment and evidence boundaries.

These local results cover product implementation `021489e6` plus the added
allocation test. The completed Windows CI evidence covers `a69db731`; do not
silently promote an earlier CI result to a later revision. No complete CI result
for the binding correction was available when this report was recorded.

## Current verified implementation

- Retained row/cell/template identity across replacement, sorting and scrolling;
  precise visible-column mutations and pre-published selection mappings.
  The native recycling suite includes 1,000-column virtualization.
- Declarative null-intermediate-owner expansion recovery, live theme/foreground
  propagation and cache shrink/regrowth now pass their complete isolated suites.
- Grid-level and standalone variable-height bring-into-view pass. The standalone
  path corrects geometry after native effective-viewport delivery, retaining
  native routing/cancellation and rejecting obsolete requests/source generations.
  New tests cover distant/final/reverse targets, tall-row TargetRect, supersession,
  source removal, unload/reattach, bounded realization and model identity.
- Cross-column recycling now reuses factory-compatible, same-parent native
  controls rather than creating another complete visual tree for each horizontal
  column window. Previous column models are released, never transferred to a
  different column. Legacy delegates retain their own compatibility behavior.
  A 128-column regression verifies six disjoint/reverse windows, no additional
  controls after boundary priming, zero unload/reparent events and correct formats.
- Default text cells have a specialized smaller visual tree without unused
  checkbox/expander/display trees. Native editing, template editing, independent
  selection/current/validation states, borders and theme resources are retained.
- Reuse/measurement callbacks check ownership generations before accessing a
  possibly retired presentation. Custom, expander and element factories pass
  reentrancy/cleanup checks, including row-factory fallback and legacy delegates.
- Focus tests use native forward/reverse Tab traversal rooted in XamlRoot content
  and verify two-axis focused-container retention. Physical input is separate.
- Windows-only sample SkiaSharp references are aligned to 3.119.2. Subsequent
  Activity Monitor XBF/required-member corrections close the Windows sample
  build and package-consumer publishing gates at the checkpoint above.

## Earlier completed functional evidence

Implementation `502f5ab9` was tested as merge `6b8c179a` in
[run 35740892721](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35740892721),
job `106790044799`, artifact `10700020208`: **974 unit tests passed**, zero
failed/skipped (Core 210, Uno 215, Avalonia 520, sample state 29), both native
samples built with zero warnings/errors, all Activity Monitor demo/lifetime
checks passed, and **30/30 isolated TreeDataGrid suites passed**.

The later test-only commit `60dbd59b` adds a framework-only exception-recovery
probe. [Run 35742390581](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35742390581),
job `106795206950`, artifact `10700431491`, tested merge `84de2cb7` and again
passed all 974 unit tests, both native builds, Activity Monitor and the same 30
TreeDataGrid suites. The new probe fails: the aggregate is now **30/31**, not 31/31.
This is a newly exposed dependency failure, not a skipped or accepted test.

Sequential integration still fails after the intentionally throwing custom-reuse
callback. The next single-row source retains the previous 5,600-pixel native
extent and has zero realized rows/cells. Later sequential assertions are unrun,
even though their individual suites pass. CI correctly remains failed.

An earlier `74418d7` run also recorded a delayed Wikipedia-image assertion and
an X11/GLX teardown error after automation assertions. Both passed on the later
checkpoints above. Do not infer that repeated environment/render reliability is
fully established from one successful isolated run.

## Independently reproduced native framework defect

`NativeLayoutRecoveryRuntimeChecks` uses only a bare native `Control`, with no
TreeDataGrid source, presenter, factory, template or binding. It measures once,
throws deliberately from the next MeasureOverride, catches the exception, then
invalidates and measures with the **same constraint** and a new natural width.

The executed result is:

```text
framework=Uno.UI, Version=255.255.255.255
exception=System.InvalidOperationException
before=2; after=2; actualWidth=40; expectedWidth=80; constraint=300,200
```

The native framework never calls the override after the handled failure. The
inspected Uno 6.6.166 source sets `MeasuringSelf` before `MeasureCore` but clears
it only on normal return. Upstream has an exception-safe helper clearing it in
`finally`; see the linked source and commit in the detailed report.

A dependency with verified exception-safe measurement is required to close this
acceptance boundary. Do not hide it with a changed constraint/delay, swallowed
exceptions, reflection into native private flags or forced visual-tree rebuilding.
The framework repository has not been modified by this port.

## Paired native performance evidence

The paired benchmark now builds and runs both native hosts against the same Core
source. [Run 35740892949](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35740892949),
job `106790049143`, artifact `10699900110`, measures implementation `502f5ab9`
in two AB/BA pairs with 64 columns and 25 iterations per workload/process.
Geometry, model/text identity, viewport and bounded realization are checked.

| Workload | Avalonia median ms | Uno median ms | Uno/Avalonia time | Uno median allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| Horizontal scroll | 0.50705 | 1.56590 | 3.09 | 29,940 |
| Vertical scroll | 1.35360 | 3.31270 | 2.45 | 212,584 |
| Distant diagonal scroll | 2.54945 | 13.99850 | 5.49 | 1,075,688 |
| Replace visible row | 2.05135 | 4.27800 | 2.09 | 110,352 |
| Resize visible column | 3.73280 | 4.77600 | 1.28 | 138,224 |
| Sort | 34.89330 | 47.40700 | 1.36 | 1,097,952 |

Before the recycling/template fixes, measured Uno median allocations were
3,690,312 bytes for horizontal scroll, 17,138,928 for distant diagonal scroll,
and 63,116,936 for sort. Latest values are approximately 99.2%, 93.7% and 98.3%
lower. Different revision runs use different hosted runners; only each run's
Uno-versus-Avalonia comparison is paired on one machine. Raw measurements and
p95/settlement metrics are retained in the artifacts and detailed report.

**The unchanged 1.10 median time/allocation ratio budget still fails.** Sorting
allocates less than Avalonia in this workload, but remains slower. This benchmark
covers synchronous UI-thread work and verified layout-settlement latency, not
GPU completion, frame rate, physical input, variable-height or all-feature parity.

## Remaining platform and API gates

The three-OS desktop build/unit matrix is distinct from Windows App SDK and
browser package consumers. Windows App SDK sample builds, all framework package
assets and both package-consuming sample publications now pass at `a69db731`.
Native Windows runtime input, accessibility, scaling and rendering acceptance
remain separate; a successful build/publish does not execute those checks.

Both browser samples build and Core/Uno packages are produced. Trimmed package
publishing still fails on model/interface/property/indexer discovery, TypeDescriptor
conversion, and reflection in ReactiveUI/Rx/Uno dependencies. Explicit preservation
and generated/registered-accessor contracts remain necessary; trimming diagnostics
have not been disabled. Browser runtime automation remains a separate gate.

The compiled metadata audit resolves both dependency sets and its strict self-diff
passes. It reports 1,748 baseline/1,525 target shapes, 823 exact namespace-normalized
matches, 925 missing-or-different and 702 additional-or-different entries. These
are not feature-completion percentages. Relocated Core contracts, native types,
inheritance/accessibility and true omissions need explicit classification and
compatibility tests. `completeApiParityProven` correctly remains false.

Remaining acceptance also includes real pointer/keyboard/drag-drop, Unicode/IME,
accessibility, scaling and multi-head rendering checks, plus closing the measured
rebind/layout/selection performance gaps. No budget or assertion was weakened.

## Reproduction

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop \
  python3 build/validate-uno-linux.py

# After building the desktop showcase:
python3 build/run-uno-native-suites.py \
  --suite bring-into-view --suite cross-column-recycling
python3 build/run-uno-native-suites.py --suite native-layout-recovery

python3 build/run-native-parity.py \
  --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

The read-only workflows execute their unchanged checkout and preserve revisions,
toolchains, TRX files, native logs, screenshots and raw metrics, including failures.
Later completed run artifacts take precedence over this dated checkpoint; a job
step displaying success is not proof that the aggregate or all assertions passed.
