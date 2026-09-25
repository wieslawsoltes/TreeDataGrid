# Uno column projection transactions and observer lifetime

Review date: 2026-09-25 UTC. Product: `06cacc07`. Starting head: `6ce462a8`.
[Execution checkpoint](uno-ci-checkpoint-06cacc07.json) ·
[Current checklist](uno-current-work.md)

## Corrected failures

A custom column factory could change its Core definition's PresentationKey while
constructing an old-key view. The previous path recorded the key after the factory
returned, making that old view appear current. A factory could also remove/reorder
columns, suspend/resume, or dispose the presentation while an older synchronization
was still holding a live dictionary enumerator or preparing a visible projection.
The returning operation could recurse, publish an obsolete projection or retain a
view after its presentation retired.

The previous dictionary of delegates did not make an unfinished add accessor an
explicit owner. Removal during a callback could run before the accessor actually
attached the handler, allowing it to return with a retired registration still
attached. Suspend could stop at the first failing cleanup. Resume could miss a
structural mutation made by a custom definition accessor before the Core column
collection itself was observed.

## Transaction and ownership design

`TreeDataGridPresentation.Columns.cs` captures each ordered Core definition and
its key/visibility before invoking a factory. `_cellOperationVersion` invalidates
returning work; `_synchronizingColumns` and `_columnsAgain` coalesce nested changes.
A staged dictionary owns every successfully returned view immediately, including
views returned after reentrant retirement. Each staged entry keeps the pre-call
key. The operation checks its version after factories and pool cleanup, and also
checks the actual ordered inputs so Resume-time detached notifications cannot
silently evade invalidation.

The commit transfers view ownership without application callbacks. Selection
column maps precede visible-list publication. Application notification callbacks
are followed by validity checks; an obsolete snapshot is not announced as a new
layout. A simple throwing list observer still receives the pre-existing independent
layout-notification behavior. Staged and retired values are released in `finally`,
with every independent cleanup attempted. Single failures use their original
exception; aggregates preserve primary-before-cleanup order. No source, row,
hierarchy or selection state is duplicated into the view.

`ColumnObservation` binds a cached handler to an exact definition/registration,
not to the event's sender. Its desired subscription is derived from current
presentation activity, retirement and registration identity. The `_changing` guard
lets application add/remove accessors finish before a new attachment decision.
Add-then-throw is rolled back and its original error is preserved. Captured old
multicast notifications are ignored after registration replacement. An arbitrary
accessor that refuses removal cannot be forcibly detached: its failure stays
observable while other independent owned cleanup is attempted.

Suspend invalidates activity/version before callbacks, snapshots cleanup owners,
and attempts every independent release. Resume requested inside removal is queued
until old cleanup finishes. Resume attaches Core structural observation before
custom definition accessors and verifies its lifecycle generation at the reviewed
boundaries. Failed active resume rolls back while retaining the original failure.
These tests do not certify all conceivable custom ITreeDataGridSource accessors.

## Executed regression matrix

`ColumnFactoryTransactionTests` contributes 16 cases: key supersession; remove,
replace, move, add and hide mutations; disposal; suspension and suspend/resume;
unobserved Resume mutations; pool-cleanup reentry; collection-publication reentry;
retired-view cleanup mutations; changed width metadata; staged factory/cleanup
failure order; and throwing publication with independent layout/retirement cleanup.

`ColumnObserverLifetimeTests` contributes 11 cases: retirement/suspension before
and after attachment; add failure with optional removal failure; removal inside
Resume attachment; every-definition cleanup on Suspend/Dispose; delayed Resume
inside removal; and recursive Suspend idempotence.

`ColumnFactoryRuntimeChecks` runs within the existing `presentation-pool` suite.
Its 160-row native grid changes the presentation key, Core order, visibility and
column collection from a factory. It checks depth-one factories, obsolete-view
disposal, actual native text/row identity, Core selection mapping, editor commit,
147-pixel width propagation, factory-failure recovery, distant row 120 rendering,
realization below the existing 2,048-cell bound, and retirement while another
factory is returning. It verifies all tracked columns dispose once, item binding
subscriptions reach zero, and the caller-owned Core source retains all 160 rows.
The original presentation-pool tests remain unchanged after this added scenario.

The marker `UNO_RUNTIME_COLUMN_FACTORY_TRANSACTIONS_PASSED` is present in isolated
`presentation-pool/runtime.log`, sequential `native-smoke.log` and the published
browser's `showcase/console.json`. Registered suites remain **65**, not 66. This
continuation adds **27 unit cases and one native scenario**, no new differential
cases or exported API shapes.

## Evidence and acceptance boundary

Functional run **36123533101** passes all fifteen stages: **1,740 .NET cases**,
**65 native suites**, sequential native execution, both native builds with no
warnings/errors and Activity Monitor checks. Platform run **36123533150** passes
all six jobs and all four actually executed published-browser routes. The two
intermediate compile failures and their fixes remain in history; neither is used
as completed acceptance evidence.

The unchanged API auditor reports 1,845 / 1,833 declarations, 1,010 exact normalized
matches, 835 missing-or-different and 823 additional-or-different entries. No raw
mismatch was removed or automatically accepted as a Core/native equivalence.
Paired run **36123533134** produces valid measurements but still fails the **1.10**
timing/allocation gate. This is correctness/lifetime work, not a demonstrated
whole-grid speedup. Hierarchy/variable-height performance, remaining API/inheritance
contracts, broader mixed custom-source callbacks and physical input/IME/external
accessibility acceptance remain open. PR #26 stays draft; no merge or release.
