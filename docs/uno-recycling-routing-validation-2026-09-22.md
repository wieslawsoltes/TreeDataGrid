# Uno recycling, geometry, notification and image validation

Observed 2026-09-22 UTC. PR #26, `codex/uno-core-port`.
**This is a tested implementation checkpoint, not certification of full API or performance parity.**

Product revision: `1755f41b6c861ccba0d042e63f3aee0e47195393`.
CI merge input: `cd8e03f519fc9370caa93206bf92c50af2f0a0b6`.
Base branch: `master`, `3ca47316d724e5e040ab0281a880e8df999b25fc`.
Subsequent documentation changes do not alter product or test sources.
The [machine-readable checkpoint](uno-ci-checkpoint-1755f41b.json) records the
workflow observations; later completed artifacts take precedence over this report.

## Reconciliation and delivery

The previous visible report stopped at `5872719a`. The actual branch had advanced
to `103ad2b4`, containing a temporary row-recycling experiment. Its candidate source
was inspected and tested on GitHub before the exact resulting blobs were manually
promoted, preserving the newly committed geometry corrections. Both temporary
candidate files were removed. No force push, merge, automatic reference promotion
or public package release was performed.

Thirteen implementation/test commits were pushed after `103ad2b4`. The pending
geometry changes from the preceding source review are now real committed, tested
code rather than an uncompiled answer. The execution service in this session could
not enumerate local files or run a shell/Python process. Consequently, this report
does not claim that unrelated, unidentified files in a local working tree were
inspected or pushed. Validation described below executed on GitHub Actions, not
on a local machine. The downloaded evidence archives do not imply local execution.

## Sparse geometry correctness

`RowGeometry.RowAt` originally used Fenwick descent while `Start` accumulated its
prefix terms in a different floating-point order. For eight rows estimated at
25.6, with heights 64 and 128 at indexes zero and two, the inverse could select row
three for the representable value immediately below `Start(3)`.

The corrected implementation keeps the uniform O(1) path. Sparse descent remains
O(log Count), but its candidate interval is checked against the same `Start`
coordinates used by layout. The common one-row correction remains logarithmic.
An exceptional multi-boundary discrepancy uses bounded O(log-squared Count)
binary search instead of an unbounded scan. No per-query objects are allocated.
This is a correctness/performance tradeoff, not a claim that sparse inverse lookup
is faster than its previous, incorrect version.

Partial invalidation also called `SetHeight`, whose 1e-7 measurement-update
tolerance could retain a stale near-estimate entry. A measurement can reach that
state after first having a substantially larger height. Invalidation now removes
the entry exactly, independently of measurement tolerance, validates finite extent
before that mutation and clears residual Fenwick state after the final removal.

Ten new cases cover four fractional estimates and adjacent representable values,
sparse mutations, both partial-invalidation traversal choices, unaffected rows,
end/infinity handling and zero warmed allocation. The earlier uniform boundary,
large-count, rollback and allocation regressions remain passing. The regression
commit preceded the implementation, but a completed pre-fix CI run was not used
as evidence: the relevant intermediate runs were superseded.

## Synchronous retained-row recycling

The new row visibility deferral applies only within one synchronous layout pass.
It avoids collapsing rows immediately recycled into another visible slot, thereby
avoiding native inherited-state work across the entire retained cell subtree.
A presenter-owned reference-identity set records deferred rows. Its finally path
collapses any row not reused, including surplus rows after a viewport shrink, and
handles cleanup failures without silently dropping them.

`DataContext = null`, row index retirement and selection cleanup remain synchronous,
as in the original Avalonia implementation. Direct/standalone Unrealize still
collapses immediately. No timer, dispatcher delay, stale row model, private Uno
state access or visual-tree rebuilding is used to obtain the speedup.

`LayoutRecyclingRuntimeChecks` verifies retained parent/control identity, current
model bindings, disjoint/reverse/diagonal scrolls, eliminated collapse transitions
for synchronous reuse, surplus collapse and full retirement. It is permanently
registered as `layout-recycling`.

### Controlled before/after experiment

[Run 35789358543](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35789358543),
job `106953860519`, tested candidate `071f8fc5b7404795dc14901ab30e0f966cd1cf58`.
All 1,047 unit cases and 34 native suites passed there before promotion. Artifact
`10721148288` contains source changes, exact hashes and both measurement sets.

Both the unchanged source and candidate were measured on the same runner; each
measurement used two Avalonia/Uno AB/BA pairs, 64 columns and 25 iterations.

| Workload | Uno before ms | Uno after ms | Before bytes | After bytes |
| --- | ---: | ---: | ---: | ---: |
| Distant diagonal scroll | 35.47765 | 12.69305 | 1,073,184 | 1,023,504 |
| Vertical scroll | 4.26950 | 2.89630 | 212,064 | 202,128 |
| Horizontal scroll | 2.44975 | 2.68235 | 29,816 | 29,816 |
| Replace visible row | 4.37390 | 4.50075 | 109,696 | 109,696 |
| Resize visible column | 6.65100 | 7.57655 | 138,224 | 138,224 |
| Sort | 64.37290 | 63.98535 | 1,090,392 | 1,090,392 |

Diagonal and vertical medians fell approximately 64.2% and 32.2%, respectively.
Other results are mixed and some medians increased. Both 1.10 parity-budget checks
still failed. The temporary experiment's successful conclusion meant correctness
and complete measurement evidence, NOT successful performance acceptance. The
permanent paired workflow retains the failing budget result.

## Definition-specific column notifications

The old `TreeDataGridPresentation.OnColumnChanged` sent one Core column's ordinary
property event to every cached view column. The new observation table holds one
handler per Core definition and directs the event only to the corresponding view.
The definition is captured explicitly: hierarchical expanders forward their inner
column's event without changing its sender, so routing by event sender alone would
be incorrect. Related definitions each receive one notification; unrelated and
hidden definitions no longer receive a broadcast of another column's change.

This reduces the view-notification dispatch for an ordinary property event from
O(number of cached definitions) to O(1) per observing definition. It does not claim
that the subsequent global layout or every column synchronization is O(1).

The observer entry is retired before unsubscription. Each handler also verifies
that it is still the current handler for that definition. An old multicast-event
snapshot cannot notify a newly created view after an earlier observer removes and
re-adds the same definition. Nested notifications restore the previous notification
scope rather than incorrectly clearing the outer scope. Post-callback checks reject
disposal, definition removal or view replacement before publishing a global change.

Twelve tests cover 1/128-column routing, shared expander/inner definitions, hidden
views, callback-time removal/disposal, repeated suspend/resume, nested notifications,
throwing observers, obsolete multicast snapshots, collectability and 4,096 warmed
cached-event dispatches with zero managed allocation on the measuring thread.
Configuration creates a per-definition closure; it is not allocated per event.

## Public contracts added

`Uno.Controls.TreeDataGridDiagnostics.EnableTracing` forwards the actual existing
presenter diagnostic flag. DEBUG trace call sites remain in use and Release call
sites remain omitted; this is not an unrelated compatibility flag.

`TreeDataGridRowModel` and `TreeDataGridRowModelEventArgs` now expose the original
snapshot/argument pattern, including derivability, null model values and borrowed
object identity. The snapshot uses the actual shared `TreeDataGridCore.IndexPath`;
it does not copy a Core model or retain a mutable anonymous row wrapper. These are
public data contracts, not a replacement of Core source event signatures.
Five unit cases verify these additions. The compiled inventory now reports three
fewer absent exported identities than the preceding 5ed67958 checkpoint.

## Native image completion and failure handling

The existing delayed-thumbnail regression failed intermittently after its original
Image control was recycled. Adding explicit ImageOpened/ImageFailed completion and
retaining the input stream did not fully solve it: the new concurrent completion
fixture reproduced a bitmap whose task completed and was subsequently reset to
zero dimensions by another native decode completion.

The inspected Uno source explains this race: BitmapSource.ForceLoad subscribes
(which can start decoding), then immediately invalidates the source (starting a
second decode and cancelling the first). The inspected BitmapImage stream path
can apply the cancelled operation's error dimensions without first validating that
it is still current. Stream cloning copies the data; stream lifetime alone is not
the complete explanation. The inspected upstream source is not treated as a byte-
for-byte audit of the installed package; runtime assertions are the executable
verification of the replacement path.

On `__SKIA__`, the sample now calls `SetSource` once and temporarily retains a
public, non-visual `ImageBrush` consumer until native decode completion. Existing
or recycled-away template consumers share that operation instead of forcing a
second decode. Other heads retain `SetSourceAsync`. Image identity stays model-
owned, event handlers and the temporary consumer are detached in finally, the input
stream wrappers remain owned until completion, and actual native errors are recorded.
A watchdog fails a stalled decode rather than converting a timeout into success.

The new `image-completion` suite verifies sixteen concurrent unconsumed bitmaps,
positive pixel dimensions immediately after each task (no timing sleeps), lazy
identified HTTP requests, distinct stable identities and explicit corrupt-payload
failure. The fixture releases its own native bitmap resources before host shutdown
where the platform supports IDisposable. Existing delayed recycling assertions
were not relaxed.

Intermediate failures remain evidence: run 35791436701 had a native process exit
139 after reporting image assertions passed; run 35792193634 exposed the later
zero-dimension race. Neither run is counted as a successful aggregate. The final
functional run and independent Linux native/package job pass with both corrections.
This does not establish that all native graphics shutdown races on all hosts are
eliminated.

Inspected primary source paths in unoplatform/uno:
- `src/Uno.UI/UI/Xaml/Media/Imaging/BitmapSource.cs`
- `src/Uno.UI/UI/Xaml/Media/Imaging/BitmapImage.cs`
- `src/Uno.UI/UI/Xaml/Media/ImageSource.crossruntime.cs`
- `src/Uno.UI/UI/Xaml/Media/ImageBrush.crossruntime.cs`
- `src/Uno.WinRT/Storage/Streams/RandomAccessStreamOverStream.cs`

## Final completed functional evidence

[Run 35793101423](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35793101423),
job `106966176418`, artifact `10722234140`, executed the unchanged merge input
`cd8e03f5` containing product `1755f41b`. All twelve validation exit codes are zero.

| Gate | Result |
| --- | --- |
| Core tests | 210 passed |
| Uno tests | 308 passed |
| Avalonia tests | 520 passed |
| Sample-state tests | 36 passed |
| Total | 1,074 passed; zero failures/skips |
| Both native builds | Zero warnings/errors |
| Sequential showcase | Passed |
| Independent native suites | 35/35 passed |
| Bare framework measurement recovery | Passed |
| Activity Monitor | All five sections and lifetime checks passed |
| Metadata audit dependencies | Zero unresolved types on either side |
| Strict identical-assembly API self-check | Passed |

The independent Uno workflow 35793101453 also completed the three-OS desktop
build/test matrix, Linux native and package-consumer checks, and Windows App SDK
build/package-consumer checks successfully. Its browser build and pack steps had
completed when this report was prepared; final workflow status is recorded in the
machine-readable checkpoint rather than inferred from earlier successful steps.
The trimmed binding consumer run 35793101431 and repository Build run 35793101403
completed successfully.

## Current performance boundary

[Run 35793101409](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35793101409),
job `106966064993`, artifact `10722619485`, measured the final product in two AB/BA
pairs on one runner. Both hosts built and all four measurement processes exited
zero. The unchanged 1.10 median synchronous time/allocation budget FAILS.

| Workload | Avalonia ms | Uno ms | Uno / Avalonia | Uno bytes |
| --- | ---: | ---: | ---: | ---: |
| Horizontal scroll | 0.31075 | 2.51330 | 8.09 | 29,816 |
| Vertical scroll | 1.19795 | 2.62505 | 2.19 | 202,128 |
| Distant diagonal scroll | 2.17180 | 10.77000 | 4.96 | 1,023,504 |
| Replace visible row | 1.75220 | 3.61445 | 2.06 | 109,696 |
| Resize visible column | 3.79860 | 4.57600 | 1.20 | 138,224 |
| Sort | 39.30620 | 55.96730 | 1.42 | 1,090,392 |

These final values are not a controlled chronological comparison with a different
hosted runner. Only the dedicated before/after experiment above uses a single
runner for both revisions. Sorting allocates less than Avalonia but is slower.
Raw medians, p95 and settlement data are in the artifact. This benchmark covers
synchronous UI-thread work and verified layout settlement, not GPU completion,
frame rate, physical input or every feature.

## Current API and remaining implementation boundary

Baseline shapes 1,748; target 1,554; exact namespace-normalized matches 837;
missing-or-different baseline entries 911; additional-or-different entries 717.
The audit classifies 194 changed declarations at matching identities, 60 absent
exported identities, 107 absent declarations on matching types, 422 members of
absent identities and 128 overload/parameter-identity differences.

These are NOT feature-completion percentages. Core relocations, inherited members,
native framework types and generated Avalonia exports need explicit tested mapping;
genuine public-contract omissions still need implementation. The added snapshot
contracts do not establish full source or binary compatibility. `completeApiParityProven`
and `completePerformanceParityProven` remain false.

Remaining work includes actual public contracts and verified Core/native mappings;
horizontal scrolling, rebinding, layout and allocation cost; browser runtime tests;
real pointer/keyboard, Unicode/IME, drag/drop, screen-reader and DPI acceptance;
variable-height/mixed-mutation performance; and repeated multi-head reliability.
No source duplication, skipped assertion, disabled trimming diagnostic or relaxed
performance threshold was used to mark these gaps complete.

## Reproduction

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite layout-recycling --suite image-completion --suite wikipedia
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```
