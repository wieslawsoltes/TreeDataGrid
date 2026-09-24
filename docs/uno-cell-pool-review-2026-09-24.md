# Uno presentation-cell ownership and pool audit

Date: 2026-09-24 UTC. Repository `wieslawsoltes/TreeDataGrid`, PR #26,
`codex/uno-core-port`. **Complete API, behavioral and performance parity remain open.**

This continuation starts at `3eb3470aa96337fa31af45bb8d85fa35ca9a0366`.
The final product source is `f7a1cc3361acd9b1420bc22e48e05f32613053a5`, tree
`a2f0a824c7d194b76ad4b041b8af65f682d2ec9e`. It contains the product changes
measured at `c0ab40c1` plus their public consumer fixture, diagnostic workflow and
fixture type correction. The [execution checkpoint](uno-ci-checkpoint-f7a1cc33.json)
records exact completed runs, artifacts and unresolved acceptance. Documentation
commits do not alter this product.

## Scope and commits

| Commit | Change |
| --- | --- |
| `e9be20a45879bdbc252d41327b6f8de835719bb7` | Reference-identity visible-column ownership index |
| `c0ab40c188cc00a8b596e635211a7225ef0e78f4` | Cell publication lifetime checks, exact cleanup and pool turnover |
| `b8968ccc1f53bf56888812992c65afc8d3c88cda` | Public native/sequential regression and read-only paired comparison |
| `f7a1cc3361acd9b1420bc22e48e05f32613053a5` | Correct the fixture's public Control return-type cast |

No Core/Avalonia product source, dependency version, renderer-global option,
public release setting or performance threshold was changed. Native view state
is not copied into another Core source, row collection or selection implementation.
Local shell and Python execution failed with ClientError; authored sources were
pushed using GitHub and executed on unchanged CI checkouts. Unknown unrelated
local working-tree files could not be enumerated or certified as pushed.

## Findings and implemented ownership rules

### Reference identity, not application equality

`TreeDataGridPresentation<TModel>` previously used inherited `Collection<T>.Contains`
when deciding whether a cell column still belonged to its visible projection.
That uses value equality and a linear scan. A custom column can override Equals,
so an unrelated column could be treated as an owner. The scan also repeated for
every realized/recycled cell on a wide grid.

The internal sealed `VisibleColumnList` now supplies its own ownership lookup:
a lazy, reference-keyed HashSet in the stable state. It never calls a column's
Equals or GetHashCode. All insert/remove/replace/move/clear paths invalidate the
index before and after mutation, including throwing callbacks and partial failed
batches. Queries during mutation scan the actual current storage by reference,
without caching intermediate contents. Duplicate references remain members until
their last occurrence disappears.

The index adds no collection listener: Core's existing one-listener reentrancy
contract stays unchanged. It is confined to the internal owning projection; the
public Collection<T>.Contains behavior of arbitrary caller-owned lists is not
redefined. Removed columns are not rooted by an obsolete cache.

Stable queries are expected O(1) with O(column count) index storage and rebuild
work after structural mutation. Callback-time scans remain O(column count).
This does not make native row layout, column sizing or all scroll work O(1).

### Callback-safe cell publication

The previous fresh-cell path did not release a factory value if ConfigureCell
threw. The pooled path could publish a cell after its reuse/configuration callback
retired the presentation. Checking only the returned column identity was not
sufficient for suspend/resume or hide/show cycles that restored that identity.

The generic presentation now tracks a cell-operation version across suspension,
column synchronization, changed row collections, row mutations and sorting.
Factory, retarget, configuration and suspension results must return to the same
active version and indexed column identity before publication/pooling.

`RealizeCell` exclusively owns a newly created or popped cell until successful
return. Configuration failure or stale ownership releases that cell exactly
once. A single failure preserves its original exception; a failing Dispose is
aggregated after the primary failure. Null factory results are rejected explicitly.
By contrast, retained `TryReuseCell` borrows its input: false leaves disposal with
the caller. It does not silently dispose a cell still owned by the presenter.

No external application callback is assumed to be pure. In particular, Core's
flat rows intentionally reuse an anonymous row wrapper. A rejected reuse or
cleanup callback can read another row without changing the source collection.
The fallback path reacquires the requested Core row rather than reusing that
now-retargeted wrapper. Actual Core row/model identities remain shared.

### Exhausted column buckets no longer consume the pool budget

The previous model pool retained empty dictionary entries after their last cell
was taken. Each empty entry still consumed one of the 32 column-pool slots.
After enough distinct columns had been visited, later columns lost pooling even
though no earlier value remained in those slots.

A popped bucket is now removed as soon as it becomes empty, before application
callbacks execute. Its empty stack storage goes into the existing bounded
`EmptyStackPool<CellValue>` helper. New buckets rent that storage without retaining
old column identity or model references. Cleanup clears the spare-storage cache.

The limits remain **256 pooled cell values, 32 nonempty column buckets, and 16
cached empty stack objects**. This is budget accounting and storage reuse, not a
larger cache or delayed source-lifetime cleanup. Revision checks also prevent a
suspend/resume callback from depositing its old value into a newer pool.

## Regression coverage

`VisibleColumnIdentityTests` adds 11 cases: 1/1,024-column equality traps, precise
membership during property and collection callbacks, duplicate identities,
throwing observers, partial batches, original single-listener reentrancy,
zero-allocation warmed positive/negative lookups, foreign-equal-column rejection,
and collectability.

`PresentationCellOwnershipTests` adds 16 cases: new/popped configuration cleanup,
primary-plus-cleanup error identity/order, retirement inside factory/reuse/configure,
suspend/resume, row replacement, sort and visibility cycles, borrowed retained-cell
cleanup ownership, mutable anonymous-row recovery, suspension failure and retirement,
128-column budget turnover, and allocation-free warmed realize/recycle cycles.

These 27 new .NET cases passed before the native fixture correction. Existing
Core/Avalonia/sample/differential tests were not removed or skipped. The full
final-product results are recorded separately in the execution checkpoint.

The new public `presentation-pool` consumer verifies four factory-retirement modes,
128-column repeated binding reuse and writeback, exact model-subscription release,
and real native grid navigation across columns 0/32/64/96/127 and back. It checks
the requested row's model and rendered text, bounded realization, and final source
removal. The same fixture is included in sequential native and published trimmed
browser execution; it is not merely a library-internal unit test.

The first sample version failed because public `TreeDataGrid.TryGetCell` returns
Control, not TreeDataGridCell. Commit `f7a1cc33` adds the checked concrete cast.
It changes no assertion. Run `35988003981` remains a failure, with native execution
not run; its unresolved audit dependencies came from that incomplete sample build
and are not accepted as a successful inventory. Final acceptance uses a separate
unchanged checkout, not an edited CI workspace.

Focused warmed allocation checks run 4,096 iterations. They measure only managed
bytes on the executing thread, after startup/index/cache warmup. They are not
substituted for the independent full-grid performance gate.

## Same-runner experiment: mixed results, not overall parity

[Comparison run 35987997845](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35987997845),
job `107595089021`, completed successfully. Artifact `10803381115`, SHA-256
`91f70240e1a06215081c2d00b7ed2558579ba9c0d3391278c7a3df5a21e91abe`, retains
both full reports, raw measurements, outcomes and derived comparison.

It tests exact unmodified baseline `3eb3470a` then candidate `c0ab40c1` on one
runner, with two AB/BA framework pairs per revision, 64 columns and 25 iterations
per workload. Revision order itself is not counterbalanced; machine-state/order
influences remain possible. Neither profiling nor source rewriting is enabled.

| Uno workload | Before median ms | After median ms | After / before |
| --- | ---: | ---: | ---: |
| Horizontal scroll | 2.76440 | 2.67300 | 0.967 |
| Vertical scroll | 2.80585 | 2.57245 | 0.917 |
| Distant diagonal scroll | 11.62980 | 10.27320 | 0.883 |
| Replace visible row | 3.98415 | 4.14015 | 1.039 |
| Resize visible column | 7.61165 | 8.06040 | 1.059 |
| Sort | 66.02100 | 76.81600 | 1.164 |

All six median allocation counts were unchanged in this workload. The measured
scroll medians improved, while row replacement, resize and sort worsened.
**An overall grid improvement or performance equivalence is not established.**
Successful diagnostic collection does not mean the 1.10 acceptance budget passed.
Both framework builds and all four measurement processes were required to exit zero;
budget failure remained explicitly recorded. The independent hard-gated performance
workflow is unchanged and its latest result is in the execution checkpoint.

Measurements cover synchronous UI work and verified layout settlement, not GPU
completion, frame rate, full input latency or all-feature performance. The 128-column
reuse fixture proves corrected pool behavior, not a timing budget at that width.

## Declared API audit and remaining review

No exported product API shape was intentionally added in this continuation.
Existing raw declared/inherited/interface/attribute differences remain preserved;
Core mappings and native signatures are not automatically declared equivalent.
The current review of apparent missing members confirms why adding wrappers simply
to change counts is unsafe: native `TreeDataGridColumn` inherits scalar policy from
ColumnCreateOptions, the native items-source view inherits the actual Core view,
and IExpanderCell inherits its presentation contract. That does not by itself
prove every caller-facing contract, attribute or event-order equivalence.

The full existing audit, normalization checks and direct framework comparisons
are rerun with the product. The checkpoint records their exact counts and dependency
resolution; partial metadata output from a failed build is never accepted.

Remaining acceptance includes genuine member/inheritance/signature compatibility,
explicit shared-Core substitutions, broader callback/collection failure review,
meeting the unchanged paired native timing/allocation budget, variable-height and
hierarchy/mixed-mutation workloads, and physical keyboard/pointer/drag, Unicode/IME,
external screen-reader and scaling checks across heads. Browser-dispatched input at
device scales 1 and 2 remains bounded automated coverage, not all those external gates.

## Reproduction

```sh
dotnet test tests/TreeDataGrid.Uno.Tests/TreeDataGrid.Uno.Tests.csproj \
  -c Release -p:TreeDataGridUnoTargetFrameworks=net10.0 \
  --filter 'FullyQualifiedName~VisibleColumnIdentityTests|FullyQualifiedName~PresentationCellOwnershipTests'
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite presentation-pool
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

The diagnostic `.github/workflows/uno-cell-pool-comparison.yml` preserves its exact
baseline/candidate recipe. No test, trimming rule, source-ownership policy, render
quality setting or performance threshold was weakened to produce a green check.
