# Uno row lifetime and allocation validation

Date: 2026-09-23 UTC. PR #26, `codex/uno-core-port`.
**Full API, all-feature and performance parity remain unproven.**

## Revision and ownership boundary

This continuation starts at `0116268dbeec92324782f7a365cabbd19fa499a0`.
That starting commit's column-layout transaction correction and 22 added unit
cases were already present. They were preserved, not counted as this work.
The earlier pending row-lifetime proposal is now implemented, extended and run.

| Commit | Implementation/test change |
| --- | --- |
| `647c690e20fb33dc0259dacd326353b435a54b87` | Validate row indexes; guard teardown and complete independent cleanup stages |
| `c9986dcc0426d45a7349f0eca4fa025b475f83a4` | Correct the native regression's factory namespace |
| `c395670f4bb505187ba07f1a6ceb2e168f01345c` | Identity-string formatting and immutable notification payload allocation fixes |
| `4b026abd6ec5c3045517e438746388662b8ad2e7` | Guard construction before application-controlled row-count access |
| `c6f3936c33f3d840f850f7908ee05844cc1e8f61` | Execute both row suites in sequential native/browser consumers |

Tested product: `c6f3936c33f3d840f850f7908ee05844cc1e8f61`.
Product tree: `193ca45f48dae266ed43edc7c99037be54865cc0`.
Tested merge: `79bb0a2685b3de87fad14b48508c7d32c0402192`.
Subsequent documentation commits do not change this code. No merge, draft-status
transition, public package release, dependency upgrade or Core ownership change
was performed. The real shared `TreeDataGrid.Core` assembly remains in use.

Local shell/Python execution returned ClientError. Changes were applied through
the connected GitHub actions and tested on unchanged GitHub Actions checkouts.
All changes authored here are pushed; unrelated unknown local working-tree files
could not be enumerated or certified as pushed. Cached packages/SDKs and downloaded
CI evidence are not repository source changes.

## Row retirement and construction

`UpdateIndex` now rejects negative and past-end indexes before changing live row
state. Previously, assigning -1 made later Unrealize return early while the row
could still retain its model/selection. The updated method captures realization
and Rows identity around custom Count access and rechecks ownership after cell
index changes and the public row-index callback.

Unrealize now has an explicit in-progress guard. Reentrant calls are idempotent,
while the old identity remains available to the outer clearing callback. A new
realization or reindex cannot enter the unfinished retirement operation. Native
cell teardown, DataContext clearing, selection clearing, required visibility
collapse and automation cleanup are attempted independently. A throwing callback
cannot skip subsequent cleanup stages. Single failures retain exception identity
and dispatch information; multiple failures are aggregated in encounter order.
The successful recycling path allocates neither an error list nor a closure.

Construction reserves its existing realization guard before evaluating Rows.Count,
not after it. A custom Count getter cannot recursively publish a second row which
the outer call overwrites. Presenter revision is captured before validation and
checked before ownership publication. The guard is released through finally on
invalid indexes, throwing count/model getters, normal return and cancellation.
The shared Core anonymous-row model is still captured before native property
callbacks, with the existing generation checks retained.

Two public-contract runtime fixtures cover 17 scenarios:

- Eleven `row-lifetime` cases: three invalid-index boundaries; recursive clearing;
  reindex during clearing; realization attempted during DataContext cleanup;
  ordinary cleanup; independent hook, DataContext and selection failures; and
  aggregate failure ordering. Every case verifies retirement and subsequent reuse.
- Six `row-construction` cases: nested realization from Count; a throwing Count;
  invalid-index guard release; throwing model lookup; cancellation during model
  lookup; and a reindex Count callback replacing the current realization.

The fixtures use actual native controls, native property callbacks with explicit
unregistration and public source/presentation APIs. They do not require reflection
into native state, friend-assembly access, sleeps or fabricated internal ownership.
Both suites are registered for isolated native runs and also invoked by the ordinary
sequential showcase, which published browser consumers execute.

The first fixture referenced the factory under the wrong namespace. CI run
`35884333438` caught the sample compile error. Commit `c9986dcc` corrected the
fixture to `Uno.Controls.Primitives.TreeDataGridElementFactory`. The library/unit
build was already successful; the failed fixture run is not counted as acceptance.
A superseded construction validation run was cancelled by newer work and is also
not counted as an aggregate pass. The final run below executes the complete source.

## Guarded string formatting and notification allocations

For the exact composite format `{0}`, a string is already the complete display
result when no custom CultureInfo subclass is involved. The new internal
`CellTextFormatting` helper returns that string directly, or String.Empty for null,
when the provider is null or an exact base CultureInfo instance. Every other case
continues to call String.Format: derived cultures can supply ICustomFormatter even
for string/null values, and non-string IFormattable/ISpanFormattable dispatch,
alignment, escapes, repeated placeholders and formatting failures are preserved.

The helper is used by column rendering/search formatting and TextBoundCell.Text.
It is not a cache of rows, values, cultures or strings. Custom formatters execute
on every read and can change their behavior or throw; they are not memoized.

BoundCell uses immutable cached PropertyChangedEventArgs for its existing Value
and Error notifications instead of allocating new payloads for each change. The
Value-then-Error publication order and subscriber dispatch remain unchanged.
No change is made to the shared Core NotifyingBase or its public API.

Twenty new unit cases compare output and failures against the runtime, exercise
multiple cultures/Unicode/null/alignment/escaping and custom formatting callbacks,
and verify the actual render/search/public-text entry points. Warmed allocation
checks execute 4,096 iterations and verify:

- zero managed allocation for the three string display/search/text queries;
- zero managed allocation for observed bound-cell retargets, with exactly 8,192
  Value/Error notifications and correct final model value.

These are measured thread-local allocation invariants for specific paths, not a
full-grid timing or total-process allocation claim. In particular the current
fixed-text paired benchmark constructs Core columns without explicit view-level
TextCellOptions. Its results are not direct acceptance of the configured composite
formatting path and cannot establish this helper's end-to-end timing contribution.

## Completed functional evidence

[Functional run 35886616210](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35886616210),
job `107268673539`, artifact `10762967481`, completed successfully for the exact
merge input above. Checkout integrity checks passed.

| Gate | Result |
| --- | --- |
| Shared Core tests | 210 passed |
| Uno tests | 450 passed |
| Avalonia tests | 536 passed |
| Sample-state tests | 36 passed |
| Total unit cases | **1,232 passed; zero failed/skipped** |
| Registered native suites | **45/45 passed** |
| Sequential showcase | Passed, including all 17 new lifecycle scenarios |
| Native measurement recovery | Passed |
| Both desktop sample builds | Zero warnings/errors |
| Activity Monitor | All five sections and lifetime checks passed |
| Compiled API dependency resolution | Zero unresolved types |
| Strict identical-assembly self-comparison | Zero differences |

This continuation adds **20 unit cases and two native suites** to its 1,212-unit,
43-suite starting checkpoint. The earlier 22-case column fix is not counted again.

[Platform run 35886616151](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35886616151)
passes the three desktop build/unit jobs, Linux native regression/package-consumer
checks, and Windows App SDK sample/package-consumer checks. Browser publication
and execution must be assessed at their actual completed step, not inferred from
native tests or a preceding revision. The machine-readable checkpoint records the
latest verified browser status; pending work is never recorded as a pass.
The separately published/executed trimmed-binding contract `35886616294` passes.

## Performance budget remains failed

[Paired run 35886616240](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35886616240),
job `107268263896`, artifact `10763500194`, built both frameworks and completed
all four AB/BA host processes with exit code zero and valid geometry/model frames.
The unchanged **1.10 median time/allocation ratio budget fails**.

| Workload | Avalonia median ms | Uno median ms | Uno/Avalonia | Uno allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| Horizontal scroll | 0.20600 | 1.98200 | 9.621 | 26,040 |
| Vertical scroll | 0.71220 | 2.21720 | 3.113 | 196,608 |
| Distant diagonal scroll | 1.34640 | 7.28450 | 5.410 | 995,904 |
| Replace visible row | 1.28145 | 2.13505 | 1.666 | 102,816 |
| Resize visible column | 1.65740 | 2.04475 | 1.234 | 130,128 |
| Sort | 20.97830 | 41.91675 | 1.998 | 1,062,136 |

Sorting allocates less than Avalonia in this workload but remains slower. These
are same-runner framework comparisons, not a controlled before/after experiment
against previous hosted machines. No overall grid speedup is established here.
The measured scope is synchronous UI-thread work and verified layout settlement,
not GPU completion, frame rate, physical input or complete feature performance.
Raw measurements, p95 and settlement values are preserved in the artifact.

## Remaining acceptance

The compiled inventory remains 1,748 baseline/1,599 target declarations, 865 exact
namespace-normalized matches and 883 missing-or-different baseline entries, with
734 additional-or-different target entries. Both dependency sets resolve and the
self-diff is exact. This continuation fixes existing behavior/internal paths rather
than adding exported API shapes. These counts are not feature-completion percentages:
Core relocation, native types, inheritance, generated exports and genuine omissions
still require explicit, tested compatibility decisions.

Outstanding work includes genuine public-contract completion, closing measured
native scrolling/rebinding/layout costs, broader variable-height/mixed-mutation
performance workloads, positive physical drag/input, Unicode/IME, screen-reader
and DPI coverage, and repeated multi-head lifecycle/render reliability. Successful
Chromium execution is not acceptance of every browser or OS input stack.

No test assertion, trimming diagnostic, lifetime ownership rule or performance
threshold was weakened. The PR remains draft and neither full API equivalence nor
performance parity is marked complete.

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite row-lifetime --suite row-construction
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```
