# Coherent column viewport snapshots

Review date: 2026-09-25 UTC. Product: `f06ceeb0536f94f7417d58e81b40d06d08b3a7f4`.
Consumer/tests: `611f543959eea0e6cedf00f694b27af5960ed2a4`.
[Current checklist](uno-current-work.md) · [Execution checkpoint](uno-ci-checkpoint-611f5439.json)

## Defect and scope

`GetColumnAt` already used a transactionally rebuilt, binary-searched prefix.
The fallback `IColumnViewportEstimator.GetOrEstimateColumnAt` did not: it read
live `ActualWidth` getters again and accumulated them without revision checks.
A getter could replace columns, clear the list, or change an earlier width, while
the fallback continued accumulating retired and current values into one anchor.
Even healthy warm queries repeated width getters after the native presenter had
already built a valid hit-test snapshot.

This change fixes that fallback, not the entire scrolling pipeline. The primary
hit-test, constrained width solver, Core ownership, renderer, existing performance
threshold and trimming diagnostics are unchanged. No whole-grid speedup is claimed.

## One publication, two prefix meanings

`ColumnListBase.Viewport.cs` now stages and publishes three related results from
one pass: nonnegative cumulative ends for hit-testing, raw strictly-positive
prefix widths for viewport anchoring, and the existing mean of all positive
measured widths (including those after the usable prefix).

These prefixes intentionally differ. Hit-testing skips zero-width columns and
can locate later positive columns. Viewport anchoring stops at the first zero,
negative or unknown width and uses its estimate when the prefix cannot locate an
anchor. A zero Auto column is not silently promoted into a measured viewport anchor.

Every application width getter is followed by the existing geometry-revision
check. A changed revision abandons the scratch values; `EnsureGeometry` retries
or accepts the newer nested publication. Storage capacity for both committed
buffers is reserved before either is replaced. Exceptions retain the preceding
committed buffers and a retryable dirty flag. Reentrant snapshots use independent
stack/pool leases, returned in `finally`; no shared scratch array or live column
reference is retained in the new cache.

Small snapshots use stack storage, with two doubles per column. Larger snapshots
rent one buffer split into ends and raw widths. The new retained list costs one
additional double per usable positive-prefix entry, plus list capacity overhead.
The collection is still UI-thread/reentrant code, not a concurrent collection.
An application that invalidates geometry on every read need not reach a fixed point;
this change does not introduce a bounded retry/cancellation policy.

## Lookup and numerical semantics

For an exactly zero realized origin, the strictly-positive prefix can be searched
by upper bound over the same committed cumulative ends. The warm search is
O(log p), where p is the usable prefix length. A nonzero origin deliberately uses
O(p) sequential additions over cached raw widths. Both avoid application getter
calls and per-query scratch allocations when geometry is clean.

The shifted-origin distinction is essential. With widths `[1e16, 1, 1]` and origin
`-1e16`, sequential addition produces ends `0, 1, 2`. Translating a prefix summed
from zero, or recovering widths by subtracting adjacent cumulative ends, loses
the unit-width columns. The regression asserts the original operation order,
not approximate equality or an increased tolerance.

Ordinary positive finite fallback scales retain the old mean/previous-estimate
priority. Invalid measured means fall back to a positive finite prior estimate;
if that too is invalid, the presenter's existing initial scale of 25 is used.
The quotient is bounded before integer conversion. NaN/negative origins select
index zero, overflowing positive origins select the last allowed index, and an
overflowing estimated position saturates to `double.MaxValue`. The caller's count
is bounded by current collection count after snapshot callbacks. A shifted known
prefix cannot overflow `firstRealizedIndex + offset` or escape that count.

These bounds are intentional robustness changes for invalid/stale internal inputs,
not claims of identical behavior for previously negative/out-of-range anchors.
Exact-hit results continue leaving the caller's prior estimate unchanged. Empty
and near-zero-origin shortcuts retain their no-getter behavior, with an empty
current collection no longer returning a stale zero index.

## Coverage and consumer boundary

`ColumnViewportSnapshotTests.cs` adds 31 .NET cases: cold/warm coherence, cached
exact/fallback lookup, replacement and invalidation inside getters, reentrant
Clear, nested geometry queries on stack/pool paths, exception identity/retry,
shifted rounding, unknown/zero/negative prefixes, invalid estimates, count bounds,
nonfinite and overflowing inputs, and warmed allocation/getter counts.

Three theory cases compare 3,588 ordinary input combinations against a local
linear numeric oracle, retaining both anchor and estimate-update semantics. These
are not 3,588 separately registered tests or new cross-framework differential
cases. Warm tests perform 8,192 measured queries after warmup, for two- and
300-column lists, and assert zero managed allocation and no repeated width reads.

`ColumnViewportRuntimeChecks` extends the existing `builtin-column-comparison`
consumer through an actual native `TreeDataGridColumnHeadersPresenter` subclass.
It invokes production protected anchor dispatch, checking 4,096 warm fallbacks
without repeated width reads, width notifications, unusable-mean recovery, nested
replacement geometry, and observer cleanup. The marker is
`UNO_RUNTIME_COLUMN_VIEWPORT_SNAPSHOT_PASSED`.

The consumer runs within the existing isolated/sequential/native-package/published
browser paths. This focused check constructs a native dependency object but does
not attach its probe presenter to a visual tree. The enclosing preexisting suite
retains real rendering/editing/sorting/virtualization assertions. No new registered
native suite or additional physical-input/accessibility acceptance is claimed.

## Validation history

The first fixture commit `6dabcbdf` was published before the product correction,
but its attempted Uno test run failed compilation: xUnit could not infer a single
element type from the collection's multiple generic enumerable interfaces. It is
not a successfully executed failing baseline, and no red/green case count is
claimed from it. Commit `611f5439` supplies explicit fixture type arguments without
relaxing the assertions. The product commit also corrects one newly authored
expected value: `25 / mean(10,20,30)` yields index 1, position 20, not index 2.
All tests predating this continuation are unchanged.

The execution checkpoint distinguishes completed, failed and pending jobs for the
exact implementation. A successful focused allocation assertion does not replace
the independent 1.10 paired native timing/allocation budget. Raw API differences
remain unwaived. The public declared API is not expanded by private cache helpers.
PR #26 remains draft; no merge or public release is part of this continuation.
