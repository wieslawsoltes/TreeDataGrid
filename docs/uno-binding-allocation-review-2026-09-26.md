# Binding allocation review and retained performance tradeoffs

Review date: 2026-09-26 UTC. Baseline `73d4c0f3`; first candidate `7de9f507`;
retained narrower candidate `0fefe457`; validated checkpoint `1f81fb34`.
[Current checklist](uno-current-work.md) · [Execution checkpoint](uno-ci-checkpoint-1f81fb34.json) ·
[Presenter APIs and audit correction](uno-presenter-api-review-2026-09-26.md)

## Source comparison and retained implementation

`CellBinding<TModel,TValue>` previously constructed both an instance property-change
delegate and an instance collection-change delegate for every binding, even when
its current owner never exposed one of those notification interfaces. The binding
already caches owner accessors, stores the root inline, deduplicates nested owners
by identity and serializes attachment, removal, retargeting and disposal. Those
mechanisms remain unchanged. No model is placed in a static cache.

The first candidate made both handlers lazy. Exact-revision measurement found
creation allocation savings, but all six native timing medians worsened. Its lazy
property-handler path is not retained. The final implementation restores the
original readonly eagerly constructed property handler and property-attachment
path. Only the collection handler is initialized on its first actual
`INotifyCollectionChanged` attachment:

```csharp
if (owner is INotifyPropertyChanged property)
{
    propertyAttempted = true;
    property.PropertyChanged += _propertyChanged;
}
if (owner is INotifyCollectionChanged collection)
{
    var handler = _collectionChanged ??= OnCollectionChanged;
    collectionAttempted = true;
    collection.CollectionChanged += handler;
}
```

The collection delegate is stable for that binding once created. Initialization
precedes the attempted-attachment flag; add-then-throw rollback still removes the
exact handler. Reentrant callbacks finish their serialized transaction before
queued retirement/retarget processing. A later row with different notification
capabilities creates the handler when required. Owner identity, deduplication,
callback ordering, original exception handling and cleanup are unchanged. There
is no new retained cache object or global observer registry.

The actual Avalonia/Uno presenter and binding implementations were reviewed for
reuse, row identity and native measurement. Dedicated Uno text templates already
omit unrelated checkbox/expander trees and isolate their private DataContext;
these were not rewritten or recounted as new optimizations. Native measurement
invalidation, typography, wrapping, selection and rendering remain unchanged.
This is a binding allocation correction, not implementation of every remaining
Avalonia optimization.

## Lifetime coverage and public creation diagnostic

`LazyBindingDelegateTests` adds fourteen cases: four owner-capability policies,
three stable-identity retarget/suspend cases, changing notification capabilities,
binding-owned rather than globally shared handlers, first-attachment failure and
recovery for both notification interfaces, retirement during first attachment,
and 4,096 warmed retarget/suspend cycles with zero managed allocation after warmup.
Reflection only inspects private handler fields outside the measured loop.

Narrowing the design changes the expectations of the four newly authored
allocation-policy cases. The other ten new behavioral cases and all tests that
predate this continuation retain their assertions. No preexisting allocation
threshold was relaxed. The retained Uno test total is 896.

`benchmarks/TreeDataGrid.BindingCreation` measures public value-column cell
creation, current-row value access and disposal over an actual shared Core row.
Plain, property-only, collection-only and dual-notification models use the same
selector. Subscriber counts and value checksums must match after every batch.
Each host warms 64 batches of 1,024 cells, then records fifteen batches of 8,192
cells per model kind. Four ABBA hosts provide thirty samples per kind/revision.
The harness is copied identically outside each exact-revision source worktree;
its hashes, actual library hash, runtime and GC mode are recorded.

The diagnostic excludes native controls and does not measure initial grid display,
input latency, native text rendering or whole-frame performance. Its allocation
result is not the entire application cost per cell.

## First candidate: preserved unfavorable timing result

[Run 36221096429](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36221096429)
compares baseline `73d4c0f3` with both-lazy candidate `7de9f507`. All sixteen native
framework hosts succeed, with 100 Uno samples per operation/revision and identical
ordered realized frame sequences. Artifact `10899485954` retains raw evidence.

| Operation | Baseline ms | Both-lazy ms | Timing change |
| --- | ---: | ---: | ---: |
| Horizontal scroll | 0.91250 | 0.93605 | +2.58% |
| Vertical scroll | 2.46910 | 2.58065 | +4.52% |
| Distant diagonal | 8.90950 | 9.38185 | +5.30% |
| Visible-row replacement | 3.61110 | 3.67900 | +1.88% |
| Visible-column resize | 7.20840 | 7.84160 | +8.78% |
| Sort | 65.74995 | 71.39660 | +8.59% |

The creation diagnostic saved 128 bytes for plain models and 64 for each
single-interface model. Those savings do not establish a whole-grid improvement
when every native timing median worsens. In particular, the plain-model 248-byte
result belongs to this superseded candidate, not the retained version. The first
collector and all results remain reproducible; it was not rerun until favorable.
A genuinely narrower implementation received a separate comparison.

## Retained narrower change: allocation gain with mixed timing

[Run 36221807680](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36221807680)
compares exact baseline `73d4c0f3` and retained candidate `0fefe457` on one runner in
baseline/candidate/candidate/baseline order. The previous collector is pinned by
commit; only its candidate revision changes. Framework order within each pass
alternates Avalonia/Uno then Uno/Avalonia. All sixteen native hosts pass frame
validation, with 100 Uno samples per operation/revision and eight identical ordered
frame sequences. These compare layout/realization records, not pixel-identical
screenshots.

The environment is .NET 10.0.12, x64 Ubuntu 24.04.5, workstation GC. The unchanged
native workload has 10,000 rows, 64 columns, 800x480 viewport, fixed 32-pixel rows,
128-pixel columns, DejaVu Sans 14, hidden headers/scrollbars, cache length zero and
25 measured iterations after five warmup iterations. It does not establish
hierarchy, variable-height, header, GPU-completion or universal-browser performance.

### Managed creation

| Model notifications | Baseline bytes/cell | Retained bytes/cell | Median creation time change |
| --- | ---: | ---: | ---: |
| None | 376 | 312 | -3.30% |
| Property only | 376 | 312 | -4.36% |
| Collection only | 376 | 376 | -1.20% |
| Property and collection | 376 | 376 | -0.97% |

Plain and property-only models save one 64-byte delegate in this measured x64
runtime: **17.02% lower factory allocation**. Collection-observing models retain
the same allocation. Object size is runtime/architecture-dependent; 64 bytes is
a measurement here, not a portable CLR guarantee.

### Native workloads

| Operation | Baseline ms | Retained ms | Timing change | Baseline bytes | Retained bytes |
| --- | ---: | ---: | ---: | ---: | ---: |
| Horizontal scroll | 1.16680 | 1.16100 | -0.50% | 15,688 | 14,728 |
| Vertical scroll | 2.44180 | 2.58985 | **+6.06%** | 191,904 | 191,904 |
| Distant diagonal | 8.48515 | 8.09055 | -4.65% | 971,672 | 966,096 |
| Visible-row replacement | 3.26455 | 3.20220 | -1.91% | 101,248 | 101,248 |
| Visible-column resize | 6.61495 | 5.57815 | -15.67% | 132,528 | 132,528 |
| Sort | 59.46240 | 57.79870 | -2.80% | 1,038,616 | 1,038,616 |

Horizontal allocation falls **6.12%**, diagonal allocation **0.57%**; the other
four allocation medians do not change. Timing is not uniformly improved. Vertical
median increases 6.06%, and settlement medians increase 1.37% horizontal, 6.47%
vertical and 0.32% replacement. Pooled p95 values decrease for all six operations,
but one candidate pass contains a vertical p95 of 11.6992 ms; the pooled vertical
p95 is 3.4307 ms. Pooling must not hide that per-pass outlier.

The small allocation-reducing implementation is retained with this explicit
latency tradeoff. No statistical significance, complete causal attribution or
universal speedup is claimed. Results from the first and second runners must not
be mixed into another before/after claim. Artifact `10899284529` retains all 63
files, individual samples, pass summaries, p95, settlement, provenance and the
unchanged failed 1.10 gate results. A green collector means collection succeeded,
not that framework parity passed.

## Independent acceptance still fails

[Final independent run 36221810071](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36221810071)
completes both builds and all four hosts on the validated source tree, but still
fails the unchanged **1.10 timing/allocation budget**:

| Operation | Uno/Avalonia median time | Uno/Avalonia allocation |
| --- | ---: | ---: |
| Horizontal scroll | 3.143 | 0.695 |
| Vertical scroll | 2.409 | 1.957 |
| Distant diagonal | 3.899 | 1.917 |
| Visible-row replacement | 1.569 | 2.668 |
| Visible-column resize | 0.897 | 1.489 |
| Sort | 1.489 | 0.620 |

Resize timing is below the limit in this run but its allocation is not. That does
not make the operation or overall gate pass. Artifact `10899795200` preserves raw
results. This independent run is not part of the ABBA before/after result.

The unchanged invalidation/measurement and Core ownership rules remain mandatory.
Remaining work includes native text/measurement and lifecycle costs, source sorting,
hierarchy/variable heights, vertical latency, the earlier intermittent allocation
observation, API adaptations and physical input/IME/accessibility acceptance.
Full Avalonia-equivalent performance and API parity are not established. Exact
execution and source-verification boundaries are recorded in the checkpoint.
