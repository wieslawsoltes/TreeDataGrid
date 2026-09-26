# Uno custom expander content and adapter lifetime review

## Scope and ownership

This continuation starts at `4d22b04279b58ff05ab8f9321210ffeaa7404e8a` on
PR #26 (`codex/uno-core-port`). The actual `TreeDataGrid.Core` source, row,
hierarchy and selection instances remain shared with Avalonia. No Core model,
row wrapper, source copy or alternative selection state was introduced.

The implementation checkpoint is `1449fd079d6b2bde4913f17edc59a42244f81e1a`,
tree `6c53c79f194826b924c1fc3b3163ca890c7fc1ba`. Later documentation-only
commits do not change that implementation. See the [execution checkpoint](uno-ci-checkpoint-1449fd07.json)
and [current acceptance checklist](uno-current-work.md) for completed run IDs,
artifacts and remaining gates. Full API, behavioral and performance parity are
not established; the PR remains draft.

| Commit | Authored change |
| --- | --- |
| `fa5006c1` | Transactional custom expander replacement and 41 regression cases |
| `1629e557` | Constructor/factory ownership, failed-content editor retirement, 21 cases and a native consumer |
| `1449fd07` | Native consumer observes the public `Value` property-change contract |

The last correction removes the consumer's accidental reference to an internal
event-args type. It does not expose internals, add friend access, remove the
single-notification assertion, or change the product implementation. The preceding
commit passed all 1,713 .NET cases, but failed native compilation; it is not counted
as a completed native acceptance run. Its dependent metadata audit was also unable
to resolve native dependencies. Final evidence must use the corrected input.

## Transactional content synchronization

`CellColumnAdapter.Expander.cs` now owns the custom-expander implementation as a
separate partial. Each adapter retains its ancestor path and an operation revision.
Content lookup, creation of a child adapter, event-accessor callbacks and public
notifications cannot publish an older result over a newer nested replacement.
Reentrant requests are serialized. Returning unused candidates lose their adapter
subscriptions without transferring ownership of the application model.

The new identity is installed before old-content unsubscription invokes custom
code. A nested replacement or retirement during that cleanup prevents obsolete
value notifications. Interrupted reset notifications carry their expansion,
visibility and row state forward to the newest stable content. A replacement that
committed its identity but encountered a cleanup exception retains its pending
value notification for a later retry.

Dynamic self/ancestor cycles are rejected during replacement, not only initial
construction. A later valid notification can recover the same expander. Ordinary
same-content notifications retain the nested adapter identity and subscriptions.
Edit gestures are read live rather than copied only at construction. Permission,
visibility and gesture reads reject results that became obsolete during the
application getter. Retired writes and expansion setters reject before invoking
application code.

## Failed replacements and native editors

While content is unsynchronized, custom descendant wrappers reject writes and the
expander reports no realizable content to native inner controls. A failed candidate
attachment therefore cannot leave an editor writing to the previous row/model.
The failure is surfaced, the old owned subscriptions remain recoverable, and a
successful later notification restores native content and editing. This applies
recursively to custom expander descendants.

This does not mutate or take ownership of a borrowed native `CellValue`. A caller
holding its raw model directly still controls that model's own lifetime and write
protocol. The expander's public write entry and native editor stop using it when
the parent is retired or unsynchronized.

## Construction and deterministic cleanup

Custom leaf and column adapters now record subscription intent before event
addition, but defer removal until that accessor returns. An accessor can retire an
adapter and then attach its handler; cleanup still runs after attachment. Failed
construction leaves raw model ownership with the factory. A successfully constructed
but already retired wrapper completes owned-model release outside the factory's
construction-failure catch, so a throwing `Dispose` cannot trigger a second disposal.

Column realization rejects disposed entry before calling the custom factory. A cell
returned after the column retires is released once. Retirement during cell adaptation
also releases the completed wrapper. A retained-reuse refresh cannot return success
if its callback retired the column.

Parent notification removal, child-wrapper cleanup and owned-model disposal are
attempted independently. One failure preserves its exception identity and dispatch;
multiple failures preserve execution order. Construction/notification failures are
not hidden by subsequent cleanup failures. Captured notifications from retired
wrappers are ignored. Application event accessors that refuse to remove a handler
cannot be forced to detach; their failure is observable while other cleanup is
still attempted.

## Added executable coverage

`CustomExpanderLifetimeTests` adds **41 cases**: nested getter/metadata/add/remove
callbacks, before/after-attachment retirement, notification reentry, reset recovery,
failed replacements, borrowed native values, live gestures, cleanup failure
combinations, queued callbacks, ancestor cycles and stable nested identity.

`CustomAdapterConstructionTests` adds **21 cases**: owned/borrowed leaf retirement,
completed-wrapper disposal failure, combined add/remove/dispose failures, failed
column construction, real presentation-factory cleanup, retired factory results,
retirement during adaptation and retained-reuse refresh.

The warmed permission/visibility/gesture test performs 4,096 reads per property with
zero managed bytes on the measuring thread after warmup. This excludes setup and
application allocations, and is not whole-grid timing or allocation acceptance.

The **`custom-expander-lifetime` native suite** uses actual native controls and a
shared Core hierarchy. It checks geometry/model identity, metadata-triggered content
replacement, exactly one public value notification, real editor commit, failed
attachment disabling the old editor, recovery, nested cycle rejection, checkbox and
empty-content transitions, Core expansion/collapse and borrowed-model cleanup during
native unrealization. It runs both independently and in sequential native and
published trimmed-browser consumers; it is not a unit-only simulation.

Total authored coverage is **62 Uno unit cases and one registered native suite**.
No new direct-framework comparison cases or exported API shapes are claimed. Existing
assertions, source-ownership rules, trimming diagnostics, native rendering settings,
raw API inventories and the 1.10 paired performance budget are unchanged.

## Remaining acceptance boundary

This completes the reviewed custom-expander replacement, constructor/factory and
failed-editor lifetime work described above. It does not establish arbitrary
third-party callback correctness, certify all legacy combined inheritance contracts,
or complete cross-platform physical-input and external-accessibility acceptance.

The compiled audit still needs explicit reviewed Core/native equivalences and genuine
signature/inheritance/attribute completion. The paired native timing/allocation
budget remains failed. Broader hierarchy/variable-height performance, physical
keyboard/pointer/drag, Unicode/IME, external screen-reader and cross-head scaling and
lifecycle acceptance remain separate gates. The current checklist records these
without turning raw declaration counts into a feature-completion percentage.
