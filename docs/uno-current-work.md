# Current Uno completion checklist

Updated 2026-09-22 UTC. Supersedes the 5ed67958 / 1,047-test checkpoint.
**Full API, all-feature behavior and performance parity are not yet certified.**

Latest [implementation and evidence report](uno-recycling-routing-validation-2026-09-22.md)
and [machine-readable CI checkpoint](uno-ci-checkpoint-1755f41b.json).
Earlier reports and the archived parity ledger remain historical evidence.

## Architecture and tested revision

PR #26 stays draft on `codex/uno-core-port`, based on master `3ca47316`.
The actual `TreeDataGrid.Core` assembly remains shared with Avalonia; source,
hierarchy, row and selection state is not copied into the Uno presentation.
No merge or public package release was performed.

Tested product: **1755f41b6c861ccba0d042e63f3aee0e47195393**.
Tested merge: **cd8e03f519fc9370caa93206bf92c50af2f0a0b6**.
Subsequent documentation-only changes do not alter the tested implementation.
Known pending geometry and recycling work is now pushed. Local shell/Python
execution was unavailable, so unrelated local files could not be enumerated;
this checkpoint does not claim inspection or publication of unknown local files.

## Completed functional evidence

[Run 35793101423](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35793101423),
job 106966176418, artifact 10722234140, validates unchanged committed sources:

| Gate | Result |
| --- | --- |
| Shared Core | 210 passed |
| Uno | 308 passed |
| Avalonia | 520 passed |
| Sample state | 36 passed |
| Total unit cases | **1,074 passed; zero failed/skipped** |
| Both native sample builds | Passed; zero warnings/errors |
| Sequential showcase | Passed |
| Isolated native suites | **35/35 passed** |
| Bare native measurement recovery | Passed |
| Activity Monitor | Five sections and lifetime checks passed |
| API dependencies | Zero unresolved types on both sides |
| Strict identical-assembly API self-comparison | Passed |

All twelve functional validation exit codes are zero. Independent platform
workflow 35793101453 has passed desktop builds/tests on Windows, Linux and macOS,
Linux native runtime/package consumers and Windows App SDK build/package consumers.
Browser build/pack has passed; final browser publication/aggregate status is in the
machine-readable checkpoint. Trimmed binding run 35793101431 and repository Build
35793101403 pass. Builds/publication are not browser or Windows physical-input tests.

## Implemented in this continuation

### Correct sparse geometry

Sparse inverse lookup now respects the exact representable Start coordinates used
by layout. Ordinary descent and adjacent-row correction are O(log Count); a bounded
O(log-squared Count) fallback handles larger discrepancies. The uniform O(1) path
is retained. Partial invalidation removes near-estimate measurements exactly rather
than applying measurement-update tolerance. Ten tests cover adjacent doubles,
mutations, both invalidation paths and warmed zero-allocation lookup.

### Faster synchronous row recycling

Rows reused during one synchronous layout pass avoid redundant native visibility
transitions. Surplus rows collapse in finally; direct/standalone retirement still
collapses immediately. DataContext/model/index/selection retirement stays synchronous.
The new layout-recycling fixture verifies identity, current bindings, multiple scroll
patterns, viewport shrink and cleanup. No timer or stale model retention is used.

A controlled same-runner before/after experiment reduced diagonal median time from
35.47765ms to 12.69305ms (about 64.2%) and vertical from 4.26950ms to 2.89630ms
(about 32.2%). Other workloads were mixed; some increased. That experiment did NOT
pass the overall performance budget. See the complete table in the detailed report.

### Targeted column events and lifetime safety

Ordinary Core property notifications route only to the owning view, not every cached
column. Definition-bound handlers correctly handle expander events forwarded with an
inner sender. Handler identity rejects already-captured events after remove/readd;
nested notification scopes restore correctly; retired views cannot publish afterward.
Twelve tests cover wide grids, forwarding, hidden columns, callback-time changes,
throwing observers, suspend/resume, collectability and zero-allocation warmed dispatch.
Global layout/synchronization is not claimed to be constant-time.

### Additional public contracts

TreeDataGridDiagnostics.EnableTracing controls the real presenter diagnostic state.
TreeDataGridRowModel and TreeDataGridRowModelEventArgs preserve derivability, borrowed
model identity and the actual shared Core IndexPath. They are snapshot contracts, not
replacements for Core source event types. Five unit cases verify them.

### Native image completion

The sample's Skia stream-loading path avoids Uno ForceLoad's redundant subscription/
invalidation decode, which can let a cancelled completion overwrite valid dimensions.
A temporary public non-visual ImageBrush holds one decode consumer until completion;
other heads retain SetSourceAsync. Streams and event/consumer subscriptions have
explicit lifetime handling and corrupt images report errors. Sixteen concurrent
unconsumed bitmaps, immediate post-task pixels, stable identities, invalid bytes and
owned native resource cleanup are checked without sleeps by image-completion.
The original delayed-recycling assertions remain unchanged.

An earlier post-assertion native exit 139 and later zero-dimension failure were both
recorded as failures, not successful aggregates. Final functional and independent
Linux native/package jobs pass; this does not certify all graphics shutdown behavior.

The continuation adds 27 unit cases and two registered native suites relative to the
preceding checkpoint. The inherited temporary recycling workflow was removed after
manual promotion of its tested source blobs.

## Current API acceptance

Compiled inventory: 1,748 baseline / 1,554 target shapes; 837 exact normalized
matches; 911 missing-or-different and 717 additional-or-different entries.
Categories: changed-same-identity 194; absent exported identities 60; missing member
on matching type 107; member of absent identity 422; overload/parameter difference 128.

These are declaration counts, not feature-completion percentages. Relocated Core,
inherited/native signatures, generated Avalonia artifacts and genuine missing contracts
still need explicit review and compatibility tests. Three absent exported identities
were implemented here. completeApiParityProven remains false.

## Current performance acceptance

[Run 35793101409](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35793101409),
job 106966064993, artifact 10722619485, tested product 1755f41b in two AB/BA pairs,
64 columns and 25 iterations. Both hosts and four measurement processes succeeded,
but the unchanged **1.10 median timing/allocation budget FAILED**.

| Workload | Avalonia median ms | Uno median ms | Uno / Avalonia |
| --- | ---: | ---: | ---: |
| Horizontal scroll | 0.31075 | 2.51330 | 8.09 |
| Vertical scroll | 1.19795 | 2.62505 | 2.19 |
| Distant diagonal scroll | 2.17180 | 10.77000 | 4.96 |
| Replace visible row | 1.75220 | 3.61445 | 2.06 |
| Resize visible column | 3.79860 | 4.57600 | 1.20 |
| Sort | 39.30620 | 55.96730 | 1.42 |

Sorting allocates less than Avalonia but remains slower. Only the dedicated
before/after experiment compares revisions on one runner; other hosted runs are
not controlled speedup comparisons. Scope is synchronous UI work and verified
settlement, not GPU completion, frame rate, physical input or all-feature performance.

## Remaining work and reproduction

Complete actual public contracts and tested Core/native equivalence decisions;
close horizontal scrolling/rebinding/layout/allocation gaps; execute browser runtime
and physical pointer/keyboard/Unicode/IME/drag-drop/screen-reader/DPI acceptance;
expand variable-height mixed-mutation performance and repeated multi-head reliability.
No test, trimming diagnostic or performance threshold was weakened.

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite layout-recycling --suite image-completion --suite wikipedia
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

Permanent workflows preserve unchanged inputs, source hashes, TRX, native logs,
screenshots and raw timing/allocation measurements, including failures. Later
completed artifacts supersede this checkpoint; pending jobs do not count as passes.
