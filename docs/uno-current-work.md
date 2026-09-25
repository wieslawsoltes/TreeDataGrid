# Current Uno completion checklist

Updated 2026-09-25 UTC. Tested implementation **6b8d43b2**;
product correction **c92c78ca**.
**Full API, behavioral and performance parity are not established.**

[Column observer review](uno-column-observer-review-2026-09-25.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-6b8d43b2.json) ·
[Typed column guide](uno-typed-column-contract.md) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-6b8d43b2.md)

## Revisions and architecture

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
Sources, rows, hierarchy and selection remain owned by the actual shared Core
assembly. No copied Core state, merge or public release was introduced.

Starting head: `757d1c0db5bd88a6386b774a29672daf9741b556`.
Test-first checkpoint: `72383243ec91b9e31256691c6cf55121f79fd268`.
Product correction: `c92c78ca08bcfccd3f2c0c46bfb1e8b7e7f36787`.
Tested consumer checkpoint: `6b8d43b26e25038f82cac2b60b9306873e9ad907`.
Tested tree: `c960f4d795f7387653472e94b38b492fc8e80ca6`.
CI merge: `fa9d5fa3f542f297b4fb4d05c907e6d3c59f8932`, with the same tree,
verified through the Git commit API. Final documentation is recorded after all
implementation platform jobs completed and does not modify product, test, build
or workflow sources. Earlier typed-column and automation work is preserved and
revalidated, not counted again as newly authored work.

## Implemented observer lifetime correction

`ColumnListBase<TColumn>` stages event attachment before owning a new column,
acquires a replacement observer before retiring the original, and maintains one
weak-owner subscription per distinct column with reference-counted duplicates.
A separate structural revision rejects stale inserts/replacements after reentrant
collection mutation while allowing layout-only reentry.

Constraints and subscription ownership are aligned before the base collection
publishes its structural change. Retired subscriptions are inactive before custom
remove accessors execute. A throwing removal therefore cannot leave an original
entry silently unobserved; a remove callback may repopulate the collection without
being overwritten by an outer stale mutation. Publishers retaining old handlers
cannot invalidate replacement geometry. Same-object replacement and duplicate
removal avoid redundant subscriptions. Caller-owned columns are never disposed.

Clear attempts every retired observer independently. InsertRange, RemoveRange and
Reset preserve the real shared base's batching rules, but defer retired-accessor
execution until the outer batch unwinds. An unsubscribe error cannot interrupt an
otherwise successful range removal or hide its complete collection notification.
The cleanup queue is detached before callbacks, allowing a reentrant batch to own
a separate queue. A single exception retains its identity; multiple failures are
reported in attempted order, with the primary operation/notification error first.

**Exception boundary:** validation/attachment failure preserves original membership
and observation unless the application itself performed a newer nested mutation.
Notification or cleanup failure occurs after structural commit and does not roll
it back. Blindly retrying removal at an old index can therefore remove a different
column. Arbitrary throwing batch actions retain the shared base's partial-mutation
semantics; this is neither a rollback engine nor a concurrent collection. A
publisher refusing removal may retain an inert subscription object, but that object
holds only a weak reference to the collection and accepts no retired events.

The implementation is in `ColumnListBase.Subscriptions.cs`; the existing width
solver and geometry-query implementation are otherwise unchanged. Core sources,
renderer settings, trimming diagnostics, existing assertions and the independent
performance threshold remain unchanged. No blanket API waiver or speedup is claimed.

## Test-first evidence and new coverage

This continuation adds **20 Uno unit cases**, **one composite native consumer
scenario**, **zero differential-framework cases**, and **zero registered suites**.
The native suite count remains 65.

The test-only baseline `72383243` executed with **18 new cases failing and two
passing**, while all preceding 810 Uno cases passed: 812 passed / 18 failed / 830
total. Linux job `108128927575` in
[run 36152484026](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36152484026)
retains the failures in artifact `10873085067`. This deliberately failing baseline
is not final acceptance. The correction passes all 20 cases without changing their
assertions.

Coverage includes failed attachment/rollback, duplicate ownership, same-object
replacement, independent Clear cleanup, complete range/reset notification, invalid
arguments, retired handlers, structural and layout-only reentry, current geometry,
subsequent reuse, and primary/cleanup exception identity.

`TypedColumnContractRuntimeChecks.Observers.cs` extends the existing
`builtin-column-comparison` consumer using actual Core rows and native cell values.
It verifies failed replacement recovery, two independently owned values through
typed/legacy factories, throwing Clear with a reentrant replacement, duplicate
observers, inert retained handlers, same-factory readdition and range notification.
Its marker is `UNO_RUNTIME_COLUMN_OBSERVER_LIFETIME_PASSED`. The enclosing suite
retains rendering, editing, sorting and virtualization assertions. The new fault
injection itself targets collections/native values, not every attached-grid source
replacement or external OS input path.

## Completed functional and platform evidence

[Functional run 36153026239](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36153026239),
job `108131037887`, passed all fifteen stages on unchanged committed input:
**228 Core + 830 Uno + 536 Avalonia + 41 sample-state + 171 direct-framework =
1,806 .NET cases**, zero failures/skips; **65/65 native suites**; sequential native
checks; both native sample builds with zero warnings/errors; and all five Activity
Monitor sections plus lifetime checks. Python review/metadata-semantic/normalization
checks remain 12/39/57. Artifact `10871959785` contains full logs/TRX/raw inventories;
source snapshot artifact `10871963910` preserves the input.

[Platform run 36153026174](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36153026174)
completed **all six jobs successfully**: Windows/macOS/Linux builds and tests,
Linux native runtime and NuGet consumers, Windows App SDK builds/package publication,
and published trimmed-browser consumers. Linux native job `108131105745` includes
the new observer marker in both sequential and NuGet-consumer execution. Native
artifact `10872223307` retains results, packages and rendered samples.

Browser job `108131105980` passed **all four execution routes**: showcase, Activity
Monitor, and browser-dispatched pointer/keyboard input at device scales 1 and 2.
The new scenario is wired into the passed published showcase; its individual marker
was inspected in native logs, not separately extracted from the browser artifact.
Chromium is pinned to 143.0.7499.4 with Playwright 1.57.0. These results are not
physical-hardware, all-browser, IME, external screen-reader or universal DPI acceptance.
Windows App SDK publication is not Windows OS runtime execution. Existing browser
UnoSplashScreen warnings remain; the zero-warning statement above applies to the
native sample builds, not all platform steps.

The final artifact listing identifies browser artifact `10873281184`. The browser
job log and artifact listing returned different artifact IDs/digests; both
observations are retained in the execution checkpoint. Archive-byte equivalence is
not independently verified. Execution success is established separately from the
completed job and its four route results.

Repository Build, dependency snapshot, reference packs, trimmed binding contract
and contract reproducibility workflows also passed for the tested implementation.

## Independent performance gate remains failed

[Paired run 36153026273](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36153026273),
job `108130735752`, completed both builds and all four alternating AB/BA hosts with
valid measurements. The unchanged **1.10 median timing/allocation ratio budget**
remains failed. Artifact `10872197632` retains raw allocations, p95 and settlement.

| Workload | Avalonia median ms | Uno median ms | Time ratio | Allocation ratio |
| --- | ---: | ---: | ---: | ---: |
| Horizontal scroll | 0.12870 | 1.09150 | 8.481 | 1.063 |
| Vertical scroll | 0.44400 | 1.25770 | 2.833 | 1.930 |
| Distant diagonal scroll | 1.05320 | 4.26495 | 4.050 | 1.928 |
| Replace visible row | 0.87345 | 1.62695 | 1.863 | 2.668 |
| Resize visible column | 1.56390 | 1.81005 | 1.157 | 1.537 |
| Sort | 19.81450 | 32.64545 | 1.648 | 0.619 |

The scope is synchronous UI-thread source/layout work and verified settlement,
not GPU completion, frame rate, physical input or full-feature performance. This
is not a controlled before/after experiment for the observer correction. Absolute
timing changes from different runners must not be described as product speedups.

## Audit and remaining acceptance

The unchanged auditor reports **1,845 baseline / 1,842 target declarations,
1,011 exact normalized matches, 834 missing-or-different baseline and 831
additional-or-different target entries**. Three declared overrides of previously
inherited batch methods remain visible in the raw inventory. Dependencies resolve
and strict self-comparison has no differences. Supplemental metadata:
13,465 / 15,793 entries; 3,838 exact; 9,627 missing-or-different and 11,955
additional-or-different. Counts are not completion percentages or accepted mappings.

Remaining gates are genuine API/signature/inheritance/attribute contracts and tested
Core/native mappings; broader custom-source/mixed-callback behavior; unchanged
performance budgets and hierarchy/variable-height workloads; physical input/drag,
Unicode/IME, external accessibility and cross-head lifecycle/scaling acceptance.
The entire port is not marked complete.

Local execution tools returned ClientError. No local compilation, extraction or
byte-for-byte archive verification is claimed. Evidence comes from GitHub Actions
on unchanged committed input, inspected job logs/artifact metadata and Git tree
identities verified through the repository API.
