# Avalonia/Uno performance audit and measured optimization

Review date: 2026-09-25 UTC. Baseline: `889bfde6`.
Measured runtime candidate: `14be7480`.
[API audit review](uno-api-audit-review-2026-09-25.md) ·
[Current implementation checkpoint](uno-current-work.md)

## Scope and outcome

This review compares the repository's actual Avalonia implementation with the Uno
port, profiles exact pre-review sources, and measures one runtime correction using
unchanged workloads in controlled revision order. It does **not** establish complete
performance parity or claim every optimization in either framework has been ported.
The independent 1.10 timing/allocation acceptance gate remains authoritative.

The controlled experiment observes a substantial reduction in horizontal scrolling:
median synchronous UI time **1.76345 ms to 0.87880 ms (-50.17%)**, allocation
**22,528 to 15,688 bytes (-30.36%)**, and median settlement **1.98690 to 1.11330 ms
(-43.97%)**. Other timings are mixed; vertical scrolling is slower and allocates
more in this run. Raw p95 and per-pass results are preserved instead of presenting
only favorable medians.

## Review against the actual Avalonia paths

Source comparison uses `src/Avalonia.Controls.TreeDataGrid/Primitives` and the
corresponding Uno files at the baseline revision. It is not a comparison against
an unrelated grid package or screenshots of the original application.

| Optimization/contract | Avalonia reference and Uno status | Decision |
| --- | --- | --- |
| Bounded realized viewport and recycling | Both use presenter-owned realized ranges and reusable controls; Uno has row/cell budgets, generation checks and parented pools. | Preserve rather than replace with a second viewport implementation. |
| Cross-column container compatibility | Uno already reuses compatible native controls from other columns while keeping column-specific model ownership separate. | Retain the existing factory compatibility contract and bounded pool scan. |
| Same-column retained models | Uno already attempts retained cell-model reuse, invalidating old writes before custom retargeting callbacks. | Do not recreate bindings just to change visual visibility. |
| Natural-width measurement | Reference columnar layout distinguishes natural-size discovery from constrained measurement; Uno already preserves that distinction. | No replacement of dirty native measurement with a stale desired-size cache. |
| `IsMeasureValid` fast path | Avalonia directly checks native measure validity plus constraint identity and its natural-width cache. The reviewed Uno public path does not expose the same validity contract. | An application-only cache of size/constraints is not equivalent: text, fonts, theme, wrapping, templates and native descendants can invalidate without those keys changing. No reflective/private bypass added. |
| Row visibility during same-pass recycling | Uno already defers row visibility when the original parented row can be reused synchronously, finalizing unused rows before layout returns. | Existing behavior retained. |
| Cell visibility during horizontal recycling | Uno preserved cell content while rebinding, but the generic recycling path still collapsed its native control during same-pass horizontal reuse. | Corrected here using the existing protected visibility-preservation hook. |
| Column geometry and fallback lookup | Uno already has revision-safe cached hit-testing, binary search for zero-origin anchors and raw-width preservation for shifted floating-point sums. | No new live width scans or weakened precision. |
| Repeated managed allocation | Existing delegate caches, immutable boxes and value-type/reused state avoid several hot allocations. | Retain; do not claim these earlier changes as new work. |
| Core ordering and selection | The actual Core assembly already owns sorting, rows, hierarchy and selection in both consumers. | No duplicate sort engine, initial-order copy or per-source cache is introduced. |

The lookup/measurement review is not a claim that all native framework costs are
avoidable inside this library. Conversely, framework differences are not a reason
to ignore avoidable port-side work. Each candidate needs both semantic tests and
measurements under unchanged rendering/realization requirements.

## Baseline profile

[Profile run 36179566554](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36179566554)
checks out exact `889bfde6`, builds both original benchmarks and records 600
operations per framework with pinned dotnet-trace 10.0.745401. Full .nettrace,
Speedscope, inclusive/exclusive reports, frame measurements and environment data
are retained in artifact `10883881462`. No product files are patched for tracing.

The profile shows row retirement, visibility propagation and native layout in the
Uno execution stacks. For example, the sampled inclusive paths include row
unrealization, `VisibilityChanged`, dependency-property propagation and layout.
The Avalonia profile instead exposes its own native measurement and text-layout
paths. These reports are **whole-process sampled residence**, including startup,
waits and other threads. They are not per-operation CPU percentages, allocation
attribution, or uninstrumented acceptance timings. In particular compositor damage
samples on a rendering thread are not added to synchronous UI-operation timings.

The trace identifies a concrete review direction: avoid collapsing a parented
control immediately before making the same control visible with new content.
It does not justify removing arbitrary native callbacks or pretending native
measurement validity can be inferred from a cached rectangle alone.

## Runtime correction: synchronous cell visibility ownership

`TreeDataGridCellsPresenter.RecyclingVisibility.cs` extends the existing
`PreserveRecycledElementVisibility` hook. During the cells presenter's own guarded
measure, an already-deferred native cell may remain visible while the same control
is reused. Cell identity, model subscriptions, editing state, lifecycle events,
column compatibility and retained-model ownership still retire/rebind through the
unchanged paths. Only the intermediate visual-collapse decision is deferred.

The pending set is owned by the presenter, not a global cache or source-sized map.
It contains controls whose decision must finish in this measure. A `finally` path
removes each pending decision before invoking native setters and collapses every
control still unrealized. A reused control's current identity is not collapsed.
Source retirement, exceptions and callback-driven newer layouts cannot leave the
old measure with authority over a newer realized cell. Independent cleanup failures
are attempted and preserved, with an earlier operation failure first.

The existing row-rebind path remains covered by its parent's synchronous visibility
ownership. Ordinary collection removals, public unrealization and dispatcher turns
are not granted arbitrary visibility deferral. Native `Measure` still runs with the
same natural-width/constrained-size rules, and the same geometry is returned.

This adds a small retained `HashSet` and its capacity per cells presenter. Warm
same-pass horizontal reuse avoids many native visibility changes and their managed
allocation, but this data structure has a memory cost. The experiment's increased
vertical allocation is recorded below; its complete allocation-stack cause is not
established by the sampling trace and is not asserted as solely the new set's cost.

## Loaded-native validation

`HorizontalRecyclingVisibilityRuntimeChecks` is called from the existing
`cell-lifecycle` suite. It attaches a real grid with 96 text columns and 100 shared
Core models, custom observed native text cells, a bounded viewport and no row cache.
It crosses five disjoint/reverse horizontal windows and verifies stable controls,
actual text and row identities, and zero intermediate collapses during successful
same-row rebinds. It then shrinks the viewport, checks unused cells are collapsed,
retires the source from a clearing callback, verifies complete cleanup and restores
a working source. The borrowed Core data remains alive.

Marker: `UNO_RUNTIME_HORIZONTAL_RECYCLING_VISIBILITY_PASSED`. The fixture is wired
into isolated, sequential, native package and published-browser consumers. Completed
execution for the final implementation is recorded separately in the checkpoint.
No additional registered suite or xUnit case is claimed for this composite scenario.

The first fixture used `grid.CacheLength`, but the property belongs to
`grid.RowsPresenter`. This produced a compile failure in the new sample, corrected
in `f3dc28a5` without changing any assertion or runtime library source. The library
and benchmark themselves compiled, so exact candidate `14be7480` remains the valid
runtime input to the independent controlled benchmark. That benchmark is not used
as proof that the initially broken sample compiled.

## Controlled exact-revision comparison

[Comparison run 36181683208](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36181683208)
uses one runner and exact unchanged baseline `889bfde6` / candidate `14be7480` in
baseline/candidate/candidate/baseline order. Each revision pass alternates
Avalonia/Uno and Uno/Avalonia. All **16 framework hosts** succeed; each Uno operation
has 100 samples per revision. All **eight Uno frame sequences** match exactly in
operation/iteration, realized geometry, row/cell counts and validity. The environment,
source revision, framework identity, sample count and original gate outcomes are
checked before the workflow reports collection success.

Workload: 10,000 rows, 64 columns, 800x480 viewport, 32-pixel rows, 128-pixel columns,
DejaVu Sans 14, headers disabled, hidden scrollbars, zero cache, five warmups and
25 iterations per host, .NET 10.0.12, X64, workstation GC, Ubuntu 24.04.5.

| Operation | Baseline median ms | Candidate median ms | Time change | Baseline bytes | Candidate bytes | Allocation change |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Horizontal scroll | 1.76345 | 0.87880 | -50.17% | 22,528 | 15,688 | -30.36% |
| Vertical scroll | 1.88985 | 1.97305 | +4.40% | 191,904 | 196,608 | +2.45% |
| Distant diagonal scroll | 7.03115 | 6.50295 | -7.51% | 971,672 | 971,672 | unchanged |
| Visible-row replacement | 2.37080 | 2.26040 | -4.66% | 101,248 | 101,248 | unchanged |
| Visible-column resize | 3.46815 | 3.29395 | -5.02% | 132,528 | 132,528 | unchanged |
| Sort | 45.36360 | 42.34140 | -6.66% | 1,038,616 | 1,038,616 | unchanged |

These are pooled medians, not confidence intervals or a universal speedup claim.
Horizontal allocation is 15,688 bytes in both candidate passes versus 22,528 in
both baseline passes. Candidate vertical allocation differs by pass (196,608 and
192,180-byte medians), with a pooled median of 196,608; the regression must not be
hidden by selecting only the more favorable pass. Diagonal synchronous p95 worsens
from **9.787 to 11.238 ms** despite its lower median. Horizontal p95 improves from
3.0243 to 1.1984 ms. Raw p95 and settlement results remain in the artifact.

Artifact `10884114103` retains 54 files, including `before-after.json`, all pass
summaries, logs and measured frames. Its workflow-reported frame-sequence SHA-256 is
`2e4b9c5f5b2c008b9844b95984b4e0ba5b9a62bc1db34a1144192f72c97bf5ef`.
The four original 1.10 framework-parity passes still report failure; the comparison
workflow's green state means valid controlled collection, not accepted parity.

## Remaining performance work and acceptance

Horizontal time is still above Avalonia: the two candidate framework-pair medians
are 2.639x and 3.626x its reference time. Allocation for that operation is now
0.741x Avalonia in both passes. Other operations retain larger allocation/time gaps.
The complete independent current-head 1.10 gate is recorded in the checkpoint;
this controlled experiment must not be mixed with a different runner's results
to claim further improvement.

Remaining work includes vertical/diagonal lifecycle and layout costs, the observed
vertical allocation increase, source-sort/reset native work, and hierarchical/
variable-height workloads. The existing fixed-text benchmark cannot establish
all-feature parity, IME behavior, accessibility, physical input, GPU completion
or frame rate. Removing native measurement or callback correctness checks to make
this benchmark green would not meet acceptance.

No Core implementation, existing unit assertion, renderer setting, trimming
diagnostic, benchmark workload, viewport size or performance threshold was changed.
Full Avalonia-equivalent performance is **not** established. Exact revisions,
completed functional/platform results and remaining failures are documented in the
current checkpoint rather than inferred from one favorable measurement.
