# Uno committed extents, drag ownership and measured performance

Date: 2026-09-23 UTC. PR #26, `codex/uno-core-port`.
**This checkpoint does not certify complete API or performance parity.**

## Exact scope and revisions

This continuation started at `49b026561fdc6beb89f44d628256e5564e653de4`.
Its preceding width-solver, public-expander, nested-binding and browser-trimming
fixes were already present and were preserved, not counted as new work.

The five implementation/test/investigation commits are:

| Commit | Change |
| --- | --- |
| `7ec79aad2f5a9972f19c6c07369e6c032c736b82` | Hosted extent calculation uses committed geometry |
| `8fd1e5dc726a995a12b3a80964c6a7a5a92bb344` | Compile the extent regression using public package-consumer contracts |
| `e4f6718e015b0518f088a7761b20a1289bedad9f` | Read-only same-runner comparison and separate managed profiles |
| `7f4ea0f0488470613c266bc98221ecf097fc0bc9` | Public native DragInfo and cancellation-safe drag validation |
| `3664bf02eef78355f662161bb2b0683ac6b3260c` | Real native data-package boundary regression |

The tested final source is `3664bf02`, tree
`7c81a692a19b2624dedd0b34e689ca5407bb7ec1`, as merge input
`c0222f3d08a253247c0b06f6fab56842975cc249`. Later documentation commits do
not alter this implementation. No merge, ready-for-review transition or public
package release was performed. The actual shared Core assembly remains the
owner of sources, rows, hierarchy and selection.

Local shell and Python execution were unavailable in this continuation. Changes
were committed directly using the GitHub connection and executed on unmodified
CI checkouts. All changes authored here are pushed; unknown local working-tree
files could not be enumerated or certified. Previous local execution claims in
archived reports belong to those previous environments, not this continuation.

## Hosted extent calculation

`TreeDataGridColumnarPresenterBase.CalculateSizeU` formerly called the public
column estimator for every realized row. `TreeDataGridCellsPresenter` then
replaced that result with its parent's already committed total width. On a wide
grid this repeated an O(total columns) scan for each realized row without using
its result.

The built-in cell presenter now supplies its committed extent through an internal
hook only when its column-collection identity and cardinality match its parent.
Standalone/custom presenters and mismatched collections retain the existing
constraint-dependent estimator. Native child measurement, Auto-width discovery,
width commits and public presenter override contracts are unchanged.

The native `committed-extent` regression uses a compiled sample row template,
a public subclass exposing the protected extent query and a counting public
IColumns implementation. It covers 2 and 1,024 columns, no estimator calls on the
hosted path, changed committed widths, identity/cardinality mismatch fallback,
null configuration and cleanup. The warmed 4,096-query loop allocates zero
managed bytes on the measuring thread. This is a work-bound/allocation invariant,
not evidence of an end-to-end timing improvement.

The first fixture attempted to access library internals from the sample and
failed compilation in run `35860317827`. The next commit rewrote it using public
package-consumer APIs; no friend-assembly access or relaxed diagnostics were
added. The corrected fixture passes in the completed runs below.

## Public DragInfo and reentrant validation

`Uno.Controls.Models.TreeDataGrid.DragInfo` exposes a borrowed Core `Source`,
its `Model` alias and the supplied `IEnumerable<IndexPath>` without copying or
eagerly enumerating the paths. The class remains derivable. `DataFormat` is the
existing native string property key, `TreeDataGrid.Controls.Uno.RowDrag`.
`TryGet(DataPackageView, out DragInfo)` resolves only a live, current process-local
operation through the existing weak token registry. A constructed snapshot does
not register a drag, serialize a source or grant drop permission.

This deliberately adapts Avalonia's data-format/data-transfer types to native
WinUI/Uno types and preserves actual shared Core identity. It is not claimed to
be a literal framework-independent ABI match. A caller deliberately retaining a
public snapshot retains its borrowed source; the global operation registry does
not gain a strong source reference. Snapshots are allocated only for explicit
public lookup, not on every automatic DragOver.

The original native session validator read its live Indexes and Models lists
around source lookups. A user collection indexer or child selector could cancel
the operation during lookup. Cancellation emptied the lists, allowing a stale
index read or an incorrectly shortened validation loop.

The new internal RowDragState captures the source and read-only list identities,
then validates those identities after every application-controlled lookup before
reading or comparing the result. Cancellation rejects the operation immediately.
Model comparison remains reference-based; application lookup failures propagate;
Release is idempotent and never disposes the borrowed source. There is no deferred
callback or per-validation transaction allocation.

Nine new unit cases verify first/second-lookup cancellation, identity versus value
equality, null results, source ownership, exception preservation, released-model
collectability, lazy public snapshots, constructor validation, and zero allocation
across 4,096 warmed validations. The additional `drag-info` native suite uses real
DataPackage/DataPackageView instances to verify ordinary text, non-string, empty,
unknown and copied-token rejection, repeated lookup, no implicit registration and
preservation of unrelated package data. A successful live physical drag, native
pointer delivery and cross-window OS interaction remain separate acceptance gates.

## Completed functional execution

[Functional run 35863339084](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35863339084),
job `107188668656`, artifact `10751253656`, validates the exact merge input above.
The checkout remained unchanged.

| Gate | Result |
| --- | --- |
| Shared Core | 210 passed, zero failed/skipped |
| Uno presentation | 408 passed, zero failed/skipped |
| Avalonia control | 536 passed, zero failed/skipped |
| Sample state | 36 passed, zero failed/skipped |
| Total units | **1,190 passed** |
| Registered native suites | **43/43 passed** |
| Sequential native showcase / measurement recovery | Passed |
| Both desktop sample builds | Zero warnings/errors |
| Activity Monitor | Five sections and lifetime checks passed |
| Compiled API resolution / self-comparison | No unresolved types / zero self-differences |

This continuation adds nine unit cases and two native suites over the starting
branch's 1,181 units and 41 suites. It does not reclassify earlier failures as passes.
The preceding product `7f4ea0f0` also passed its 42 registered suites and all 1,190
units in run `35862077438`; the 43rd suite is a newly executed native boundary test.

[Platform run 35863339064](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35863339064)
records success for all three desktop build/unit jobs, Linux native regressions
and both native package consumers, and Windows App SDK sample builds, package
creation and both consumers. The browser builds and package creation have passed;
its final publication/runtime stage was still in progress when this report was
prepared. Do not count the pending browser job as completed. Repository Build
`35863339003` and the published trimmed-binding contract `35863338976` are successful.

## Same-runner extent experiment: no overall speedup established

[Investigation run 35861303772](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35861303772),
job `107181911518`, artifact `10751045015`, compares unchanged baseline `49b02656`
with extent candidate `8fd1e5dc` on one machine. Each revision runs matched
Avalonia/Uno hosts in two AB/BA pairs, with 64 columns and 25 iterations/workload.
The baseline revision is measured before the candidate revision, so revision-order
and machine-state effects remain possible. The raw results are retained.

| Uno workload | Baseline median ms | Candidate median ms | After/before |
| --- | ---: | ---: | ---: |
| Horizontal scroll | 2.25385 | 2.62840 | 1.166 |
| Vertical scroll | 2.85975 | 2.72760 | 0.954 |
| Distant diagonal scroll | 11.04580 | 11.42785 | 1.035 |
| Replace visible row | 4.35110 | 4.07450 | 0.936 |
| Resize visible column | 5.27835 | 8.15255 | 1.545 |
| Sort | 53.87720 | 68.65820 | 1.274 |

Most allocation medians are unchanged; column resizing increased from 130,128 to
132,528 bytes in this run. These mixed and sometimes worse timings do not establish
an overall native-grid improvement. The removal of redundant estimator calls is
independently tested, but is not used to claim a measured speedup or equal performance.
The independent 1.10 hard gate was not changed. The investigation workflow's success
means both hosts, comparison and profile collection completed, not that parity passed.

The same artifact contains separate dotnet-trace 10.0.745401 recordings using
`dotnet-sampled-thread-time,dotnet-common`, inclusive/exclusive TopN reports and
Speedscope exports for each framework. Instrumented workloads use 200 iterations
and verify all 1,200 samples. They are not substituted for uninstrumented timings.
Profiles include initialization, render and asynchronous threads, waits and native
resource cleanup. Their percentages are not exclusive CPU or UI-thread percentages.

Within those limitations, recorded paths worth deeper investigation include Uno
composition damage-region union/drawing, native resource finalization, row-reset
visibility propagation, measurement and text shaping. These are diagnostic leads,
not a demonstrated sole cause. The existing horizontal visibility assertions remain
unchanged; no native framework global rendering switches were disabled.

Official profiler semantics and commands:
[dotnet-trace documentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace).

## Latest paired acceptance still fails

[Paired run 35863339075](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35863339075),
job `107188668391`, artifact `10751073587`, ran the final tested source on one
runner. Both builds and all four host processes exited zero and validated their
geometry/model frames. The unchanged **1.10 median time/allocation budget fails**.

| Workload | Avalonia median ms | Uno median ms | Uno/Avalonia | Uno allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| Horizontal scroll | 0.40980 | 2.62660 | 6.409 | 26,040 |
| Vertical scroll | 1.00500 | 2.71005 | 2.697 | 196,608 |
| Distant diagonal scroll | 2.70095 | 10.62840 | 3.935 | 995,904 |
| Replace visible row | 2.45510 | 2.94440 | 1.199 | 102,816 |
| Resize visible column | 4.75395 | 5.38035 | 1.132 | 132,528 |
| Sort | 35.51855 | 77.35170 | 2.178 | 1,062,136 |

Sorting allocates less than Avalonia in this workload but is slower. Different
CI revisions run on different machines; do not interpret this table against an
earlier job as a controlled before/after improvement. Raw p95 and settlement
metrics remain in the artifacts. The scope is synchronous UI-thread work and
verified layout-settlement latency, not GPU completion, frame rate, physical input,
variable-height, all-feature or complete performance parity.

## Remaining API and runtime acceptance

The compiled inventory records 1,748 baseline and 1,599 target shapes, 865 exact
namespace-normalized matches, 883 missing-or-different baseline entries and 734
additional-or-different target entries. Both dependency sets fully resolve. The
self-comparison has zero differences. `completeApiParityProven` remains false.

These are declarations, not feature-completion percentages. Core relocations,
native signatures, inherited members, generated Avalonia exports and actual
omissions require explicit, tested equivalence decisions. Adding the public
DragInfo does not certify all those unrelated contracts.

Remaining acceptance includes genuine public-contract work, measured native
scroll/rebind/layout improvements, broader variable-height/mixed-mutation paired
workloads, browser/runtime reliability beyond one Chromium configuration, and
physical keyboard/pointer, Unicode/IME, drag/drop, screen-reader and DPI coverage.
No test, trimming diagnostic, source ownership rule or performance threshold was
weakened to obtain a passing result.

## Reproduction

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite committed-extent --suite drag-info
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

The read-only investigation workflow preserves its exact baseline/candidate
revisions and profile recipe. Later completed CI artifacts supersede this dated
checkpoint; an in-progress job is never treated as a pass.
