# Current Uno completion checklist

2026-09-26 UTC. Tested source **b38ca0f7**, runtime implementation **879e75ad**.
**Draft: 784 declared API differences remain and the native performance gate fails.**

[Implementation and reproduction review](uno-declarative-policy-unformatted-text-2026-09-26.md) ·
[Exact execution checkpoint](uno-raw-text-checkpoint-b38ca0f7.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-b38ca0f7.md)

## Implemented on the existing shared Core

This continuation starts at `4f2dc4cc96043fb33dc157d76b96d63e8c62b366`.
PR #26 remains on `codex/uno-core-port`, based on master `3ca47316`.
Sources, rows, hierarchy and selection are still owned by the actual shared Core.
No merge or public release is requested.

TreeDataGridColumn now directly declares six portable policies: CanUserResize,
CanUserSortColumn, AllowTriStateSorting, CompareAscending, CompareDescending and
BeginEditGestures. Every accessor forwards to existing ColumnCreateOptions state.
This restores declaration owners, not six new runtime features. There are no
additional policy fields, delegates, event stores or subscriptions. Existing
captured scalar options versus live configured comparison callbacks are unchanged.

LiveTextCell and TextBoundCell use their existing by-value TypedValue accessor for
parameterless ToString instead of boxing through the public object-valued Value.
Mutable struct ToString runs on a copy, original exceptions propagate, and nested
source updates affect the next query without corrupting the current read snapshot.
The public virtual Value/FormatValue APIs, custom dispatch and binding ownership
remain unchanged.

Immutable Core-backed text cells and CellColumn.FormatValue now also recognize a
null StringFormat as no format, matching the established scalar/live behavior.
Previously an existing options object with a null format called string.Format(null)
and threw. Raw Text preserves null; the existing FormatValue API preserves its
empty-string result for null. Parameterless formatting follows CurrentCulture,
not an explicitly supplied display provider. No formatted string or culture cache
is introduced. No public nullability annotations change.

Only TreeDataGridColumn.cs, Models/TreeDataGrid/TextColumn.cs and
Presentation/CellColumn.cs change in the runtime library. Shared Core, original
Avalonia sources, renderer and layout algorithms are unchanged.

## Commits and exact validation input

Runtime and tests: `879e75ad5cf2da5f14b657d408a58e28b685be14`.
Corrected new fixture: `b38ca0f74446621fba43474c9759c130b7f852bb`.
Tested tree: `835d089f89d6d0843b612369909c8fd8ed3f847b`.
CI merge: `5683f9497e378083eca938765f67ec389a8fae38`, with that exact tree.
Tree identity was checked through the Git commit API, not local reconstruction.

The first run compiled and executed 293 paired-framework cases: 292 passed and a
new defaults assertion failed. It incorrectly compared GridLength.Auto.Value
payloads (Avalonia 0, Uno 1). The corrected test independently asserts IsAuto and
retains all pixel minimum, gesture, policy and declared-shape checks. No preexisting
assertion, layout rule or normalization mapping changes. Runtime files are identical
between the two commits. Failed functional run `36266049705` and failed comparison
`36266046811` remain recorded; the latter never reached measured benchmark hosts.

The final four-path documentation-only commit uses `[skip ci]` to avoid replacing
active implementation validation under the existing PR concurrency rule. Product
and test commits were not skipped. Workflow acceptance rules and the 1.10 native
budget are unchanged; pending documentation-head checks are not called green.

## Executed baseline proof and authored coverage

The same corrected 49 new cases run against both exact implementations:

| Input plus new fixtures | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Original 4f2dc4cc runtime | 32 | **17** | 0 |
| Retained b38ca0f7 runtime | **49** | 0 | 0 |

The baseline failures are six declared-owner checks, nine immutable-null-format
cases and two allocation assertions. Only new fixtures are copied to the old test
projects; no baseline runtime source is edited. Coverage comprises eleven native
unit cases and 38 paired-framework cases. Tests protect nullable/accessor metadata,
one policy store, callback identity, null-format culture behavior, mutable value
copies, errors/recovery, reentrant ToString, retargeting, writeback and cleanup.

One new composite public-consumer scenario extends existing value-column-base.
It uses native cell models over actual Core rows; its enclosing suite retains
loaded-grid rendering, editing, sorting and virtualization. It is not a new
registered suite or another independently attached visual tree. Marker
`UNO_RUNTIME_UNFORMATTED_TEXT_POLICIES_PASSED` was explicitly inspected in both
native sequential and native NuGet-consumer logs, job `108471544769`.

## Canonical functional validation

[Run 36266232264](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36266232264)
passes every one of its fifteen stages on unchanged committed input.

| Gate | Result |
| --- | ---: |
| Core | 228 passed |
| Uno | **1,027 passed** |
| Original Avalonia | 536 passed |
| Sample-state | 41 passed |
| Paired-framework | **293 passed** |
| **Total .NET cases** | **2,125; zero failed/skipped** |
| Registered native suites | **65/65 passed** |

Sequential native checks, both native sample builds (zero warnings/errors), and
Activity Monitor's five sections and lifetime checks pass. Existing signature-reader,
interface-ordering and Python integrity checks pass. Report artifact `10914342820`
contains 127 files; source artifact `10913354948` retains the exact input. The
artifact ZIPs were downloaded, but not locally extracted or independently hashed.
Counters were read from completed execution logs.

[Platform run 36266232255](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36266232255)
currently has **four completed successful jobs**: Windows/Ubuntu builds and tests,
Linux native/NuGet consumers, and Windows App SDK builds/package publication.
Browser builds and packing pass, publication is running, and actual published-
consumer execution is pending. macOS job `108471544933` is queued with no steps.
Neither incomplete job is counted as passed; older-head results are not substituted.
Windows package publication does not establish Windows OS runtime acceptance.

Supporting Build, dependency snapshot, trimmed-binding, reference-pack and contract-
reproducibility workflows all pass on b38ca0f7. Earlier intermittent allocation
observations remain unexplained; a successful run does not resolve that investigation.

## Declared API: 790 to 784, with raw differences retained

The unchanged production API reader independently reconciles six exact new target
shapes against the complete reference inventory. Every previous raw target record
and every reference record remains unchanged. No newly missing declaration,
normalization-policy change, unresolved dependency or collision is permitted.
The canonical native-target audit independently confirms the same declared totals:

| Scope | Reference | Target | Exact | Missing/different | Additional/different |
| --- | ---: | ---: | ---: | ---: | ---: |
| Combined | 1,845 | 1,894 | 1,061 | **784** | 833 |
| Identical Core dependency | 590 | 590 | 590 | 0 | 0 |
| Independent UI assemblies | 1,255 | 1,304 | 471 | **784** | 833 |

The six changes reduce undeclared members on matched types from eighty to 74.
Other categories remain: 208 changed same-identity declarations, 43 absent exported
types, 316 members of absent types and 143 overload/parameter-identity differences.
The 590 Core self-matches are dependency identity, not independent UI-port coverage.

Supplemental metadata does NOT close with these declarations: target entries rise
from 15,880 to **15,916**, and additional/different entries from 11,993 to **12,029**.
The 36 new supplemental differences remain visible. Baseline supplemental entries
stay 13,465, exact matches 3,887, and missing/different 9,578. Strict target
self-comparison is clean across 1,894 declarations and 15,916 supplemental records.
Counts are not feature-completion percentages, ABI certification or compatibility
waivers for native/Core/inheritance adaptations.

## Controlled text-query allocation and timing

[Run 36266229246](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36266229246)
uses one identical external public harness against exact revisions in ABBA order.
All four hosts complete ten workloads, each with fifty measured batches per revision
(8,192 queries per batch after twelve warmup batches per host). Exact strings,
checksums, library fingerprints and unchanged worktrees are checked. Runtime
settings are inherited, without tiering or ReadyToRun overrides.

Environment: .NET 10.0.12, x64 Ubuntu 24.04.5 LTS, workstation GC. All results below
are pooled medians; full samples, per-pass medians, p95, baseline failures and API
inventories remain in artifact `10914227901` (43 files).

| Query | Baseline ns | Candidate ns | Bytes before | Bytes after |
| --- | ---: | ---: | ---: | ---: |
| Live int, raw | 73.16 | 54.41 | 64 | **40** |
| Core int, raw | 31.59 | 26.75 | 64 | **40** |
| Live decimal, raw | 96.62 | 87.26 | 80 | **48** |
| Live nullable int, raw | 49.62 | 31.32 | 64 | **40** |
| Core null nullable | 16.67 | 14.47 | 0 | 0 |
| Live string, raw | 20.15 | 19.90 | 0 | 0 |
| Core object, raw | 30.85 | **32.47** | 40 | 40 |
| Live int, formatted | 84.39 | **85.05** | 80 | 80 |
| Core int, formatted | 82.15 | **83.06** | 80 | 80 |
| Scalar int control | 25.77 | **26.06** | 40 | 40 |

The targeted saving is 24 bytes per measured int/nullable-int query (-37.5%) and
32 bytes per decimal query (-40%). Result strings still allocate; object sizes
are specific to the measured runtime. Null/string controls remain allocation-free,
while object, formatted and scalar-control allocations are unchanged.

Adverse results remain explicit: Core object median +5.26%, live formatted +0.78%,
Core formatted +1.11%, scalar control +1.11%. Core raw-int p95 worsens from **42.20
to 53.15 ns**, despite its lower median. The live-int candidate's two pass medians
also differ (59.01 versus 40.78 ns). These results are not dismissed as proven noise.
No sample/workload was removed and no same-code measurement retry was requested.
Pooled diagnostics do not establish confidence intervals, universal speedup or
complete causal attribution. These are warm cell-model display queries, not
construction, startup, native visual layout, whole-grid sorting or frame rate.

## Independent native gate still fails

[Run 36266232248](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36266232248)
completes both builds and all four hosts but fails the unchanged **1.10** budget.

| Operation | Uno/Avalonia time | Uno/Avalonia allocation |
| --- | ---: | ---: |
| Horizontal scroll | 2.047 | 0.695 |
| Vertical scroll | 2.050 | 1.957 |
| Distant diagonal scroll | 2.330 | 1.917 |
| Visible-row replacement | 1.431 | 2.668 |
| Visible-column resize | 1.573 | 1.516 |
| Sorting | 1.356 | 0.620 |

Artifact `10914332678` retains raw results. This synchronous UI/layout/settlement
comparison is not a controlled before/after whole-grid improvement from the raw-text
patch; do not combine it with microbenchmarks or earlier different-runner ratios.

Remaining: native/Core/type/inheritance API contracts and supplemental metadata;
measured native text/layout and sorting costs; hierarchy/variable-height workloads;
physical input, Unicode/IME, external accessibility and cross-head lifecycle/scaling.
Full API and performance parity are not established. No old assertion, shared Core
rule, renderer setting, normalizer or native acceptance budget was weakened.

Local container/Python tools returned ClientError; all new execution ran in GitHub
Actions. Completed logs, returned artifact metadata and repository tree identities
were inspected. No local compilation, archive extraction or independently recomputed
archive digest is claimed. The checkpoint distinguishes failed, passed and pending
stages rather than representing the full branch as green.
