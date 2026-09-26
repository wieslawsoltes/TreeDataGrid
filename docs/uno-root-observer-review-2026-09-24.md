# Typed-root activation and observable snapshot review

Date: 2026-09-24 UTC. PR #26, `codex/uno-core-port`.
**This work does not certify complete API, behavioral or performance parity.**

See [the exact execution checkpoint](uno-ci-checkpoint-89525188.json) and
[current checklist](uno-current-work.md) for completed CI results and the still-open
acceptance gates. The previous checklist is [preserved unchanged](archive/uno-current-work-before-89525188.md).

## Scope and ownership

This continuation starts at `37a7f2ca0156174c7eba1d0c0bde9df838150845`, not the
older conversational checkpoint `15357e71`. The intervening `2c181a0d` and
`37a7f2ca` already implemented consumed mutable built-in Binding descriptors,
protected factories and last-value preservation on getter errors. Those changes
are preserved and revalidated, not counted as authored here.

| Commit | Change |
| --- | --- |
| `b7ad1eb2d049ef8129b2b58c4e76ac4ac35eb8a3` | Apply the pending typed-root lifetime correction and nine unit cases |
| `f8a86915de960e9aad2159f563ea619ab95c63ed` | Native text/nullable-checkbox activation consumer, also in sequential browser execution |
| `89525188ae3e12357e700e790e12df3660dc11b6` | Stable observer snapshots, synchronized offline materializer and twelve differential/allocation/lifetime cases |

Product tree: `631b18993d8d9970990b850fcce1ce2eb44d4945`.
CI merge input: `d53006cd365327b86137f5e36759cd29d1fcceb6`.
Actual shared Core sources, rows, hierarchy and selection are unchanged. Root
observables and model objects remain caller-owned. No dependency, native rendering
setting, trimming diagnostic or acceptance budget changes are included. Local shell
and Python execution returned ClientError; execution evidence comes from unchanged
committed CI inputs, not a local test claim. Unknown local files could not be inspected.

## Root callbacks belong to an activation

The expression formerly gave each RootObserver only its owning expression. After
the last target observer unsubscribed and another target subscribed, the expression
was active again but owned a new CellBinding. A callback captured by the old source
subscription could then replace the new activation's root. An old OnError could
terminate a new activation, or permanently poison an inactive expression before
its next subscriber arrived.

RootObserver now also captures its activation's CellBinding. Both OnNext and
OnError require the expression to be active and its current binding to be that
exact object. This is identity validation, not value equality and not a model-name
or row-index approximation. Replaying a retired null, value or error performs no
model lookup or publication. A current root completion remains a no-op: as in the
existing contract, its final model continues to be observed after the root stream
completes.

Use the expression on its source's owning thread. These guards handle synchronous
reentrancy and callbacks already captured before unsubscription; they do not add
cross-thread dispatch or certify arbitrary simultaneous UI mutation as safe.

## Failed writes cannot refresh a newer activation

Public subject OnNext keeps its existing write-rejection policy. It now captures
the current binding and root revision before invoking the setter. A rejected old
write cannot refresh whichever replacement binding happens to be current when
control returns. The existing native throwing write path is not replaced with the
subject's nonthrowing policy.

When terminal error publication throws and cleanup also fails, RootFailed preserves
both exceptions in execution order. Single failures propagate without unnecessary
wrapping. Cleanup still releases the owned binding and subscription token, never
the caller's source observable or model.

`TypedBindingRootLifetimeTests` adds nine cases: obsolete non-null/null values,
obsolete errors during active and inactive intervals, reactivation during token
disposal, failed writes that replace activation, final-model observation after root
completion, observer/cleanup exception identity and order, and warmed allocation.
The allocation test performs 4,096 old/current callback pairs and requires zero
managed bytes on the executing thread. Its retired model is neither evaluated nor
subscribed.

## Native and trimmed consumer coverage

`TypedRootLifetimeRuntimeChecks` is registered as `typed-root-lifetime` and also
runs through the sequential native/published browser consumer. It uses actual
TreeDataGridTextCell and TreeDataGridCheckBoxCell controls, borrowed public typed
TextCell/CheckBoxCell models and an observable-root TypedBindingExpression.

Each path retires the first cell model, creates a second subscription to the same
expression and reuses the exact native control. Explicit replay of the previous
source observer's value/null/error cannot change the current control's scalar,
indexes, model or subscription ownership. Native Value writes reach only the
current source model, and external model notifications do not echo into a second
write. Current null-root fallback/recovery and observation after stream completion
remain functional. Native Unrealize does not dispose borrowed cell models; their
owner's Dispose releases the final root subscription. All observed model handlers
and source tokens are gone at final cleanup, while root-observable DisposeCalls
remains zero.

This is native dependency-property/control execution, not simulated OS input or
proof of a particular third-party root producer. The repository's separate browser
input driver and physical-input acceptance gates retain their own scope.

## Stable multi-observer snapshots

The imported LightweightObservableBase allocated an observer-array snapshot for
every publication with multiple subscribers. The native adaptation now caches that
immutable array for the current membership. Adding or removing a subscription
invalidates it under the same gate. Error/completion clear the cached field before
publishing the terminal notification. The one-observer fast path is unchanged.

Arrays are never modified in place. A nested publication after membership changes
uses a new snapshot, while the outer publication keeps its original captured list.
This deliberately preserves the reference behavior, including duplicate observer
subscriptions, outer callbacks already captured before removal or termination,
callback order and exception propagation. It does not silently introduce a different
observable protocol or claim to fix every exceptional edge of the reference base.

The tradeoff is one extra cached-array reference per observable and one snapshot
allocation after membership changes, rather than one array for every stable fan-out.
Removed and terminal observers are not retained by the cache. The retained-object
checks keep the observable alive while collecting a payload formerly owned by an
observer.

`ObservableSnapshotParityTests` adds twelve cases:

- Six callback traces compare actual Avalonia and Uno implementations under removal,
  addition, remove/re-add of the same observer, completion, error and a throwing
  observer, including nested and subsequent publications and lifetime hook counts.
- Three matched same-process allocation cases use 2, 8 and 128 subscriptions, each
  with 4,096 warmed publications. Native publication must allocate zero managed
  bytes; reference array allocation is checked independently. Every duplicate
  subscription still receives exactly the same number of callbacks.
- Three retained-observable checks verify removed, completed and failed observer
  payloads are collectible after the immutable snapshot has been used.

These are bounded behavioral and managed-allocation measurements, not wall-clock
speedups or the whole-grid acceptance benchmark. Application observer allocations,
cold snapshot creation and subscription churn are outside the zero-allocation claim.

## Deterministic materialization remains enforced

The snapshot transformation is also implemented by
`build/materialize-uno-parity-contracts.py`. It verifies the exact expected add,
remove, snapshot and terminal sites before applying the transformation. The upstream
C# and hash-pinned input blobs are unchanged, as are the original differential tests
and the declared generated-output set.

`python3 build/materialize-uno-parity-contracts.py --check` still performs an exact
offline generated-source comparison. No networking or generated-source mutation is
introduced into ordinary builds, and no mismatch is accepted merely because the
runtime tests pass. The independent reproducibility run executes both this check
and the actual framework comparison assembly.

## Current built-in binding boundary

The already-present ValueCellColumn Binding descriptor is lazy and consumed by new
cells through an immutable instruction snapshot. Existing retained/pooled cells
keep their construction snapshot. Editing Binding does not rewrite Core's getter,
sorting policy or a live cell's subscriptions. Protected CreateBindingExpression
creates an independent typed expression for its row. This is no longer a missing,
disconnected property, despite the older archived checklist.

Legacy combined built-in inheritance and other member/signature/interface/attribute
mappings remain separate review items. Inherited contracts are not duplicated merely
to reduce declared-member counts; for example the native items-source facade already
inherits protected OnItemsSourceChanged from its actual Core base. The raw auditor
continues to preserve these differences rather than auto-certifying equivalence.

## Remaining acceptance and reproduction

Full API equivalence, broader callback/mixed-mutation behavior, unchanged native
performance budgets, hierarchy/variable-height workloads and physical input,
Unicode/IME, external screen readers and cross-head scaling remain open. The flat
grid performance workload does not isolate multi-observer typed-expression fan-out.
A passing focused allocation test must not replace a failed whole-grid budget.

```sh
dotnet test tests/TreeDataGrid.Uno.Tests/TreeDataGrid.Uno.Tests.csproj -c Release \
  -p:TreeDataGridUnoTargetFrameworks=net10.0 \
  --filter FullyQualifiedName~TypedBindingRootLifetimeTests
dotnet test tests/TreeDataGrid.Parity.Tests/TreeDataGrid.Parity.Tests.csproj -c Release \
  -p:TreeDataGridUnoTargetFrameworks=net10.0 \
  --filter FullyQualifiedName~ObservableSnapshotParityTests
python3 build/materialize-uno-parity-contracts.py --check
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite typed-root-lifetime
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

The exact completed checkpoint distinguishes implementation commits, native execution,
platform build/publication/runtime scope and focused versus whole-grid performance.
No assertion, source ownership rule or performance threshold was weakened.
