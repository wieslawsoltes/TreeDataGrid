# Column observer ownership and exception safety

Review date: 2026-09-25 UTC.
Product commit: `c92c78ca08bcfccd3f2c0c46bfb1e8b7e7f36787`.
Consumer checkpoint: `6b8d43b26e25038f82cac2b60b9306873e9ad907`.
[Current checklist](uno-current-work.md) ·
[Execution checkpoint](uno-ci-checkpoint-6b8d43b2.json) ·
[Typed column contracts](uno-typed-column-contract.md)

## Reproduced defects

The old `ColumnListBase<TColumn>` invoked custom `PropertyChanged` remove accessors
before updating the collection and its aligned constraint storage. If an accessor
threw, the old entry remained in the collection after its observer ownership had
already been removed. Replacing a column also retired the original observer before
trying to attach the replacement; a failed replacement therefore left the original
column present but unobserved. Passing a null replacement had the same failure mode.

Clear stopped at the first failed accessor, leaving other observers attached and
collection storage uncommitted. RemoveRange could stop halfway without issuing its
complete range notification. A throwing add accessor followed by a throwing rollback
accessor replaced the original error rather than preserving both. Publishers that
retained removed handlers could continue invalidating current geometry indefinitely.

Subscription callbacks also reentered collection or layout operations. The outer
insert could overwrite newer structure, double-register a column, or publish into
geometry that a nested layout query had incorrectly marked clean before insertion.

The test-first commit `72383243ec91b9e31256691c6cf55121f79fd268` adds 20 cases without
changing product code. Linux job `108128927575` in
[run 36152484026](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36152484026)
compiled and executed them: 18 failed and two passed. All 810 preceding Uno cases
passed, giving 812 passed / 18 failed / 830 total. This is an intentionally failing
baseline, not final acceptance. Artifact `10873085067` contains the baseline TRX.

## Ownership protocol

The collection retains one weak-owner subscription per distinct column identity,
with a count for duplicate entries. It never disposes caller-owned columns.

A new subscription is attached in an inactive state. Until attachment returns,
application callbacks observe the old collection; the staged observer accepts no
notifications. A separate collection revision distinguishes structural changes
from layout-only callbacks. A newer structural mutation is preserved and causes
the stale outer add/replace to throw `InvalidOperationException` after cleaning up
its staged observer. Layout-only reentry is permitted. The exception is intentional:
silently skipping an insertion would let inherited range insertion count an entry
that was not inserted.

Replacement acquires the new observer before retiring the old one. Same-object
replacement and duplicate removal reuse the existing observer. Validation and
attachment failures do not disable an otherwise unchanged original column.

Retirement removes subscription ownership and disables event delivery before the
base collection publishes its structural change. Constraint storage is aligned
before base notifications. The remove accessor runs after that publication, so it
may query geometry or repopulate the collection without an outer stale write
removing its replacement. The subscription uses one cached delegate and marks its
removal attempt complete before calling application code. A retained old delegate
is inert, and recursive cleanup cannot detach a newer observer of the same column.

The implementation is UI-thread/reentrant code, not a concurrent collection.
No lock, global subscription table, copied Core state or renderer change was added.
The existing width solver and geometry-query implementation are unchanged; geometry
is invalidated at the structural commit rather than before an attachment callback.

## Error and batch semantics

| Failure phase | Observable result |
| --- | --- |
| Argument validation or failed attachment | The original structure/observer remains, unless the application itself performed a newer nested mutation. |
| Structural mutation during attachment | The newer mutation remains; the staged observer is retired; the stale outer operation throws. |
| Notification or retired-accessor failure | The structural mutation has committed; it is not rolled back over newer callback work. |
| Multiple failures | The original operation/notification exception is first, followed by cleanup errors in attempted order. |

Each retired observer is attempted independently. A single exception is rethrown
with its original identity; multiple exceptions are preserved in an aggregate,
without flattening nested application aggregates or replacing their identities.
Collection-notification delivery itself retains the shared base's behavior: a
throwing subscriber can stop subsequent subscribers. This change does not replay
failed notifications or claim all subscribers ran.

The InsertRange, RemoveRange and Reset overrides preserve the actual shared base's
batching rules and defer retired-accessor execution until the outer batch unwinds.
Consequently, an unsubscribe failure cannot interrupt the structural work of an
otherwise successful RemoveRange or suppress its aggregate notification. Deferred
cleanup storage is detached before calling application code, allowing a nested
batch during cleanup to own a separate queue.

This does not make arbitrary user batch actions atomic. A throwing action or an
illegal mixed batch operation retains the shared Core base's existing semantics;
earlier successful mutations may remain. Retired observers are still cleaned on
unwind. Applications must not blindly retry removal at an old index after a
post-commit exception: the requested removal may already have succeeded.

A publisher that deliberately refuses to remove a handler can retain the inert
subscription object until the publisher releases it. The collection cannot repair
an arbitrary publisher's event storage. The subscription holds only a weak reference
to the collection and cannot deliver retired notifications to it.

## Coverage and consumer boundary

The 20 new .NET cases cover attachment rollback, original/cleanup exception identity,
clear and range cleanup, reset notification, failed null replacement, duplicate
entries, same-object reuse, reentrant removal and insertion, layout-only reentry,
constraint alignment, retained-handler isolation and subsequent reuse. None of
the baseline assertions is removed or relaxed by the product correction.

`TypedColumnContractRuntimeChecks.Observers.cs` adds one composite consumer scenario
inside the existing `builtin-column-comparison` suite. It uses actual shared Core
rows and real native cell factories through both typed and legacy contracts, and
checks failed attachment recovery, independent cell ownership, duplicate observers,
throwing Clear with a reentrant replacement, inert retained handlers, same-factory
readdition and complete range notification before cleanup exceptions.

The scenario is wired into isolated and sequential native checks, the NuGet package
consumer and the published trimmed-browser showcase. The enclosing suite retains
its rendering/editing/sorting/virtualization assertions. The new fault injection
itself targets column collections and native cell values, not every attached-grid
source replacement or external OS event path. No additional registered suite or
new differential-framework case is claimed. Consult the execution checkpoint for
completed versus pending runs and exact source-tree identity.

## Remaining acceptance

This is a correctness correction, not a whole-grid speedup or proof of API parity.
It adds three overrides of already inherited batch methods to the declared surface;
raw audit inventories must retain those declarations rather than normalize them
away. The independent 1.10 performance budget, original tests, Core ownership,
trimming diagnostics and native rendering settings are unchanged.

Remaining genuine API contracts and tested Core/native mappings, broader custom
callback behavior, native timing/allocation budgets, hierarchy/variable-height
performance, physical input/drag, Unicode/IME, external accessibility and
cross-head lifecycle/scaling remain acceptance gates. PR #26 stays draft; no merge
or public release is authorized by this checkpoint.
