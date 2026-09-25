# Current Uno completion checklist

Updated 2026-09-25 UTC. Tested implementation **611f5439**, product **f06ceeb0**.
**Functional/platform validation passes after one Ubuntu retry. Full API,
behavioral and performance parity are not established.**

[Viewport snapshot review](uno-column-viewport-review-2026-09-25.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-611f5439.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-611f5439.md)

## Revisions and scope

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
Sources, rows, hierarchy and selection remain owned by the actual shared Core
assembly. No merge, public release, renderer change or Core copy is introduced.

Starting head: `ad72ec8f3b1c24e59d5483be6abde543ff27fd8c`.
Initial test commit: `6dabcbdfa7d550d8b5c342d48fd328c924be9236`.
Product commit: `f06ceeb0536f94f7417d58e81b40d06d08b3a7f4`.
Consumer/test correction: `611f543959eea0e6cedf00f694b27af5960ed2a4`.
Implementation tree: `9aa2a7a72f02ea91c2dbcee549f38c2487c750ae`.
CI merge: `09e61d966cb84512e90a6b8b97386d5f0e83df3c`, with the same tree,
verified through the Git commit API. Final documentation follows completed
implementation validation and changes no product, test, build or workflow sources.
Prior observer-lifetime work is preserved and revalidated, not recounted as new.

## Implemented coherent viewport fallback

The primary `GetColumnAt` already used cached binary search. Its fallback viewport
estimator still rescanned live widths without revision checks, potentially mixing
retired/current columns after an application getter mutated the collection. Healthy
warm fallback calls also repeated reads after the hit-test had built a snapshot.

`ColumnListBase.Viewport.cs` now publishes nonnegative cumulative hit-test ends,
raw strictly-positive viewport-prefix widths, and the mean of all positive measured
widths from one revision-checked pass. Stale scratch values are discarded after
callbacks; newer nested geometry publications remain authoritative. Both destination
capacities are reserved before publication. Getter errors preserve identity and
leave reconstruction retryable.

Warm zero-origin searches use O(log p) binary lookup. Shifted origins retain O(p)
sequential additions over cached raw widths, preserving floating-point order.
For `[1e16, 1, 1]` at origin `-1e16`, sequential ends must remain `0, 1, 2`;
translating cumulative sums or subtracting adjacent ends would lose unit columns.
No approximation or tolerance change is used to conceal that difference.

The fallback retains positive-width mean semantics, rejects invalid estimate
scales, clamps indexes before integer conversion, bounds caller counts against
current columns, and saturates overflowing estimated positions. Empty and near-zero
origin shortcuts avoid width getters. These are intentional robustness changes for
invalid/stale internal inputs, not blanket equivalence waivers.

The additional retained raw prefix costs one double per usable entry plus list
capacity overhead. Rebuild scratch uses two doubles per column: stack storage for
small snapshots and one returned pool lease for larger snapshots. Warm queries
need no scratch allocation or live width reads. Primary hit-testing, constrained
width solving, shared Core state and observer transactions are otherwise unchanged.
This is a focused correctness/lookup improvement, not a measured whole-grid speedup.

## Authored coverage and initial fixture correction

This continuation adds **31 Uno unit cases**, **one native consumer scenario**,
**zero direct-framework cases**, and **zero registered native suites**. The native
registry remains 65. Three theory cases exercise **3,588 ordinary numeric-oracle
combinations**, not 3,588 separately registered or cross-framework tests.

Coverage includes cold/warm snapshots, structural and width reentry, nested
stack/pool queries, throwing getters, shifted rounding, unknown/zero/negative
prefixes, invalid estimate scales, stale counts, overflow and allocation/getter
counts. Two- and 300-column fixtures assert zero managed allocation across 8,192
measured warm queries and no repeated width reads.

`ColumnViewportRuntimeChecks` invokes production protected anchor dispatch through
an actual native `TreeDataGridColumnHeadersPresenter` subclass, checking 4,096 warm
fallbacks, notifications, nonfinite means, nested replacement, geometry and cleanup.
Its marker is `UNO_RUNTIME_COLUMN_VIEWPORT_SNAPSHOT_PASSED`. The probe presenter is
not attached to a visual tree; the enclosing existing `builtin-column-comparison`
suite retains actual rendering/editing/sorting/virtualization assertions.

The initial test-only commit did not establish an executed failing baseline:
its Uno tests failed compilation because xUnit could not infer a unique enumerable
element type. `611f5439` adds explicit `Assert.All<ProbeColumn>` type arguments.
The product commit also corrects one newly authored expectation: viewport 25 divided
by mean width 20 anchors at `(1, 20)`, not `(2, 40)`. Tests predating this continuation
and their thresholds remain unchanged.

## Completed functional, native and browser execution

[Functional run 36160233485](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36160233485),
job `108155075408`, passed all fifteen stages on unchanged input:
**228 Core + 861 Uno + 536 Avalonia + 41 sample-state + 171 direct-framework =
1,837 .NET cases**, zero failed/skipped; **65/65 native suites**; sequential native
smoke; both native builds with zero warnings/errors; five Activity Monitor sections
and lifetime; Python review/metadata/normalization checks 12/39/57. Report artifact
`10875358660` and source artifact `10875505656` preserve this checkpoint.

[Platform run 36160233552](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36160233552)
now concludes **success**, with all six latest job summaries successful. This covers
Windows/macOS/Linux build/test jobs, Linux native runtime/NuGet consumers, Windows
App SDK build/publication and published trimmed-browser consumers. Full native log
`108154872250` contains the new viewport marker in both sequential and native
NuGet-consumer execution; artifact `10875431675` retains native evidence.

Full browser log `108154872217` confirms **four successful execution routes**:
showcase, Activity Monitor, and browser-dispatched pointer/keyboard input at scales
1 and 2, with Chromium 143.0.7499.4 and Playwright 1.57.0. The new scenario is wired
into that passed showcase; its individual browser marker was not extracted from
an artifact. Existing UnoSplashScreen warnings remain. Windows package publication
is not Windows OS runtime execution, and browser-driver input is not physical,
all-browser, IME, external screen-reader or universal DPI acceptance.

The checkpoint keeps artifact observations tied to their provenance. The inspected
browser job log identifies artifact `10875843019`; a later run-level listing returns
`10876080941`. No byte equivalence or exact attempt relationship between those
objects is asserted. Execution results are separate from archive verification.

Repository Build, dependency snapshot, reference packs, trimmed binding contract
and contract reproducibility workflows also passed for implementation `611f5439`.

## Original Ubuntu allocation failure retained

The first Ubuntu platform job `108154871827` passed 860/861 Uno cases, including
all 31 new cases, but failed the unchanged
`ColumnLayoutReentrancyTests.Warm_layout_and_geometry_queries_allocate_no_managed_storage(count: 1)`:
**32,664 bytes observed versus zero expected**. Its recorded artifact is
`10875158471`. No assertion was removed, skipped or relaxed.

That case passed in the independent 861/861 Linux validation above. One accepted
request to retry the Ubuntu job then completed as `108159261385`: **228 Core,
861 Uno and 41 sample-state cases passed**, zero failures/skips, and the solution
build reported zero warnings/errors on the same `09e61d9` checkout. Retry artifact
from its log: `10875896904`. No source changes occurred between those attempts.

**The original intermittent allocation observation is not explained or fixed.**
The successful independent run and retry do not erase it or establish its cause.
The zero-allocation assertion and performance thresholds are unchanged. The latest
run-level listing returns new IDs for all six jobs; this continuation requested only
the targeted Ubuntu rerun action and records the returned observations separately.

## Independent performance gate remains failed

[Paired run 36160233549](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36160233549),
job `108154705890`, completed both builds and all four alternating AB/BA hosts.
The unchanged **1.10 median timing/allocation ratio budget remains failed**.
Artifact `10874349811` preserves raw data, including p95 and settlement.

| Workload | Avalonia median ms | Uno median ms | Time ratio | Allocation ratio |
| --- | ---: | ---: | ---: | ---: |
| Horizontal scroll | 0.28585 | 1.77755 | 6.218 | 1.063 |
| Vertical scroll | 0.84880 | 1.83215 | 2.159 | 1.957 |
| Distant diagonal scroll | 1.90765 | 6.24865 | 3.276 | 1.928 |
| Replace visible row | 1.20795 | 2.17270 | 1.799 | 2.668 |
| Resize visible column | 2.00865 | 2.79770 | 1.393 | 1.489 |
| Sort | 19.29585 | 41.91680 | 2.172 | 0.620 |

These measure synchronous UI/source/layout work and verified settlement, not GPU
completion or frame rate. They are not a controlled before/after experiment for
this change. Different-runner timing differences are not proof of a speedup.

## Audit and remaining acceptance

The unchanged audit remains **1,845 baseline / 1,842 target declarations; 1,011
exact matches; 834 missing-or-different baseline and 831 additional-or-different
target entries**. Dependencies resolve and strict self-comparison has no differences.
Supplemental metadata remains 13,465 / 15,793 entries, 3,838 exact, 9,627 missing
and 11,955 additional/different. No public declaration was added by private cache
helpers. Counts are not completion percentages or accepted Core/native mappings.

Remaining gates include genuine API/signature/inheritance/attribute contracts and
tested Core/native mappings; broader custom-source callbacks; the unexplained
allocation observation; unchanged performance budgets and hierarchy/variable-height
workloads; physical input/drag, Unicode/IME, external accessibility and cross-head
lifecycle/scaling acceptance. The entire port is not marked complete.

Local execution tools returned ClientError. No local compilation, artifact extraction
or independently recomputed archive digest is claimed. Execution evidence comes from
GitHub Actions on committed input, inspected logs/artifact metadata and verified
Git tree identities.
