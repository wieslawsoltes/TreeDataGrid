# Uno API compatibility contract

The user clarified that the port should be code-compatible with Avalonia as far
as possible, except where Uno APIs or layout/rendering semantics require a
difference. This is an acceptance requirement, not an optional migration guide.

## Rules for the remaining implementation

- Preserve public type/member names, generic arity, flags, event payloads,
  defaults and behavioral contracts. Reuse the Avalonia organization after the
  framework namespace substitution (`Avalonia.Controls` → `Uno.Controls`).
- Do not label a reduced API or an independently redesigned interface an
  unavoidable platform difference. Revisit the initial slim Uno API accordingly.
- Keep the actual shared `TreeDataGrid.Core` as the source of neutral models and
  algorithms. Where compatibility facades are needed, they belong in the Uno
  presentation assembly and must delegate to Core rather than copy it.
- Preserve existing native recycling/virtualization improvements when adapting
  control/presenter types. Layout implementation differences are justified by
  Uno measurement/parenting semantics, not by convenience.
- Differences must have a source-backed reason and a migration example. Verify
  compatibility by compiling representative Avalonia/Core sample code with only
  the documented framework substitutions, after product implementation is done.

## Current correction list

| Surface | Compatibility action | State |
| --- | --- | --- |
| Selection enum | `Uno.Controls.TreeDataGridSelectionMode`; `Row=1`, `Cell=2`, `Multiple=4`; support flags such as `Row \| Multiple` | New code, unvalidated. Initial descriptive names remain aliases. `Source` and `None` are explicitly extra conveniences. |
| Selection defaults | Default property value `Row`; do not overwrite a supplied Core selection when the property was untouched | New code, unvalidated. Audit WinUI styling/binding precedence in validation. |
| Edit gestures | `Uno.Controls.Models.TreeDataGrid.BeginEditGestures` with matching values; `BeginEditGestures` option name | New code, unvalidated. Initial `EditGestures` name remains an alias. |
| Text options | Preserve `TextAlignment`, `TextWrapping`, `TextTrimming`, `StringFormat`, `Culture`, `IsTextSearchEnabled` | New TextColumnOptions<TModel>, TextColumn<TModel,TValue>, ITextCell/ITextCellOptions and existing immutable native text options. Culture/format/writeback use the Core binding path. Mutable-option timing still needs comparison/validation. |
| Drag/drop | Preserve `AutoDragDropRows`, `RowDragStarted`, `RowDragOver`, `RowDrop`, `Models`, `AllowedEffects`, `Inner`, `Position`, `TreeDataGridRowDropPosition` | Native implementation added, unvalidated. `TargetRow` now exposes the actual row control; `TargetModel` remains an additional identity snapshot. |
| Grid/row/cell lookup and lifecycle | Preserve `TryGetRow`, `TryGetCell`, `TryGetRowModel`, prepared/clearing/value events | New real row/cells presenters, lookup overloads, row/cell event args, RowPrepared/RowClearing, CellPrepared/CellClearing/CellValueChanged and subclass lifecycle hooks. Native regression cases authored; full lifecycle/failure validation remains. |
| Selection event data | Preserve `TreeDataGridSelectionChangedEventArgs` and selected/deselected item/index/cell-index payloads | New typed control event and generic/non-generic args, deriving from actual Core selection args. Row payloads are captured from Core; cell deltas use source CellIndex values, including hidden columns, matching Avalonia. Separate visual invalidation avoids fabricated selection deltas. Tests authored, unrun. |
| Presentation configuration | Preserve typed options/factories and assignable presentation-options property | New options/presentation contracts and assignable PresentationOptions DP. Factories now return ICellColumn<TModel>, with public UI IColumn/ICell/IUpdateColumnLayout and text/checkbox/template column facades. Native columns keep the fast path; arbitrary interface columns use a view adapter. Custom-cell kinds/editing, abstract base types and remaining layout semantics still need review. Unit/native cases are unrun. |
| Declarative/fluent API | Preserve ItemsSource/ColumnDefinitions, column classes, bindings, row headers and source extensions | New ItemsSource DP, ColumnDefinitions collection/content metadata, all five declarative column kinds and actual generated Core sources. Native row bindings observe nested paths; ordinary property/indexer writeback propagates failures. Added Source/SourceProperty and Source-based RowSelection/ColumnSelection with Model switching and generated ownership. Advanced binding forms, remaining unload/reentrancy/error behavior and compatibility compilation remain open; new cases are unrun. |
| Primitive types and themes | Preserve row/cells presenter, cell kinds and customization points | New TreeDataGridRow/TreeDataGridCellsPresenter, RowStyle/native default template, ElementFactory and overridable creation/recycling keys wired to cells/rows/headers. Named cell controls expose scalar properties/styles; expanders now create and retain factory-selected inner controls with delegated editing. Full public cell-model/row/presenter/base-cell contracts remain open. All new factory/lifecycle/cell code is unvalidated. |

## Framework differences that need explicit mapping

- Avalonia property registration and XAML theme selectors become WinUI dependency
  properties, control templates and visual states. Theme brush names and public
  appearance properties should remain recognizable/compatible.
- Native drag effects use `DataPackageOperation`; the native args expose
  `AcceptedOperation`/`AllowedOperations`, not Avalonia's `DragEffects`. The
  default remains Move only for unsorted automatic row movement; custom handlers
  opt into other effects. Native data packages/deferrals replace DataTransfer.
- Uno/WinUI do not expose Avalonia's public custom routed-event registration
  mechanism. Current row drag notifications are CLR events with RoutedEventArgs
  payloads; routing compatibility needs explicit review/documentation, not a
  silent claim that event bubbling is identical.
- Native ScrollViewer, automation providers and binding objects replace the
  corresponding Avalonia interfaces. Preserve control-level names and behavior
  while using those framework types.
- Visual lookup accepts native `DependencyObject`, covering WinUI text and
  template elements that are not `Control`. Row customization uses a native
  `Style`/`ControlTemplate`. These substitutions do not remove the row container
  or its Model, RowIndex, CellsPresenter and realization lifecycle.
- Selection arguments use the actual shared Core IndexPath/CellIndex and base
  selection event types; they do not introduce another neutral selection model.
  Cell-selection deltas populate the cell-index collections, while row deltas
  populate row indexes/items, as in the Avalonia Core presentation.

This document records requirements and work in progress. It does not certify
source or binary compatibility, nor validate the new working-tree code.

## Generated binding metadata and callback lifetime checkpoint (2026-09-20)

Named property writes now consult Uno's public `BindableMetadata.Provider`
before reflection. Generated getters/setters and dependency-property descriptors
preserve declared conversion types and read-only rules; no provider, endpoint or
model is retained in a new global cache. The Windows App SDK target excludes this
Uno-specific API and keeps its existing native read/reflection-write path. This
closes a generated named-property gap, not full trimming/AOT, generated indexer,
x:Bind or attached-property parity. Five metadata unit cases are authored, UNRUN.

Native binding suppression now counts nested operations rather than using a
single boolean. Write resolution checks realization revisions after user
getters/converters/conversion and before invoking a setter. A retired operation
cannot overwrite newer error state or restore its old model. The primary write
exception survives refresh/notification cleanup, and active edits can retry.
Disposal releases probe ownership before clearing DataContext and its expression;
both detach operations are attempted, including disposal during installation.
Snapshot cleanup does not suspend a newer reentrant realization.

Template text-search bindings share the native snapshot reader. This also clears
explicit-source expressions after a search: clearing DataContext alone did not
disconnect a Binding.Source observer. A native regression suite covers converter/
setter retirement, original error identity, retry and subsequent notification,
source cleanup and repeated explicit-source search snapshots. It is authored and
UNRUN. No current-state build, runtime, leak or performance claim is made.

### Generated indexer follow-up (2026-09-20)

Write resolution now uses Uno's public generated indexer getter/setter before
CLR fallback, after the existing array path. Native string-key semantics include
quoted/whitespace/numeric keys; a generated string endpoint is not redirected to
an unrelated integer overload. Generated read-only metadata stays authoritative,
and original setter errors/retirement checks apply to indexed intermediate owners.

The metadata API supplies no indexer value type. A retained public string-indexer
declaration supplies conversions when available; generated-only endpoints use the
native object contract without guessing from a current value. They may require a
converter for typed setters. Nine cases cover keys, nesting/retirement, failure/
retry, read-only metadata and conversion with competing CLR overloads, all UNRUN.
This does not establish full trimming/AOT compatibility.

Source review confirms that both the Avalonia accessor's StyledElement probe and
Uno's FrameworkElement probe are detached. They do not automatically inherit the
cell's namescope/templated parent. Uno local ElementNameSubject references can
supply a source explicitly, but named/relative writeback still falls back to native
UpdateSource. Error propagation, late-name cleanup and actual scope behavior
remain specific review requirements, not covered by ordinary row-path support.

### Named-source ownership and native writeback follow-up (2026-09-20)

Local Uno `ElementNameSubject` bindings now use an owned, removable subscription
and a resolved-source probe. Suspend/dispose detaches the subscription; replacing
the resolved element retires its previous probe and observers. Unresolved names
do not fall back to the row. This avoids the native late-name handler whose
ownership is not exposed through the binding contract. No tracker or dispatcher
callback was added to ordinary row bindings.

Uno's public resolved binding root now feeds the error-aware property/indexer
writer for native-resolved relative sources. Attached-property paths still use
native UpdateSource. Explicit sources without INPC refresh by reinstalling their
expression, since toggling DataContext does not refresh Binding.Source. Writes
also reject a changed native endpoint; nested paths independently recheck their
owner after conversion for Windows App SDK, whose DataItem contract does not
guarantee leaf-owner identity. The extra path traversal is edit-only.

Authored native cases cover late-name resolution/replacement, setter errors/retry,
subject removal during conversion, cleanup, nested-owner replacement, relative
Self and explicit-source refresh without INPC. Two unit cases cover nested-owner
replacement/removal during conversion. All are UNRUN. Uno does not emit a subject
event when its element becomes null: reads reject stale values and writes/retarget/
suspend detach old observers, but immediate visual refresh on that silent change
is not promised. Outer/load-time namescopes, attached-property error propagation,
x:Bind and full trimming/AOT remain separate compatibility questions. Native
relative-source error propagation on Windows App SDK is also not established.

## Declarative write-path resolution checkpoint (2026-09-20)

The explicit writeback path now validates the whole property/indexer path before
running getters or setters. It no longer silently skips repeated/trailing dots,
accepts unbalanced indexers or removes arbitrary quote characters from keys.
Numeric index tokens prefer an int indexer, then string; quoted numeric tokens
select string. A remaining ambiguous overload is rejected before mutation instead
of choosing the first reflection result. String keys preserve whitespace and
single quotes, following the inspected Uno binding key coercion rather than the
previous unconditional trim. Nested arrays/indexers retain the actual reference
owner, and setter exceptions preserve their original identity for edit retry.

Private intermediate getters, read-only endpoints and copied intermediate structs
cannot silently report a successful write. Keys containing a dot or nested bracket
are rejected because the inspected Uno BindingPath parser cannot observe that
endpoint consistently. Native reads/observation are unchanged. ElementName,
RelativeSource, attached-property resolution and generated binding metadata/AOT
still need their own implementation review; this is not complete binding parity.
Focused path/conversion/failure cases are authored and UNRUN.

## Row viewport cache checkpoint (2026-09-20)

`TreeDataGridRowsPresenter.CacheLength`/`CacheLengthProperty` now match the
reference's default zero, accepted range 0–2, and unit of additional viewport
heights before/after the visible region. Native invalid dependency-property
assignments restore the old value before throwing. The buffer transfers missing
space at an extent edge to the other side, matching the Avalonia algorithm.
Reference-named protected `GetMeasureViewport` and
`NeedsMeasureForViewportChange` hooks use native `Windows.Foundation.Rect`.
Rows, cells and headers also expose reference-named `GetRealizedElements()` in
display-index order; headers now provide `TryGetElement(int)` alongside the row/
cell implementations. Queries omit pooled containers and do not add work to the
normal layout path unless explicitly enumerated.

Small scrolls contained in a positive cache window retain its realized rows and
skip row/cell remeasurement. Zero cache no longer silently adds fixed guard rows;
partial boundary rows are included and exact end boundaries are exclusive.
Horizontal viewport changes still remeasure cells. Source/height/appearance and
column geometry changes invalidate the cached window, including fixed-height
column resizing. Uno uses its sparse row-geometry extent rather than Avalonia's
Border bounds to clip the buffered window. Existing same-parent pools and
synchronous rebind remain in use; no delayed work or reparenting was introduced.

Geometry unit cases and a native cache suite are authored for edge compensation,
exact/partial boundaries, variable heights, invalid values, small-scroll identity,
shrink/grow without unload/reparent, bounded recentering, fixed-height column
resize, source empty/refill and removal. They are UNRUN. Remaining presenter/base
contracts and the full validation pass are still outstanding.

## Public cell identity and row hooks checkpoint (2026-09-20)

Two avoidable API differences were found in the primitive review. `TreeDataGridCell.Model`
now returns the original UI `ICell`, rather than its native presentation adapter.
Custom controls can inspect/cast it just as in Avalonia; template-cell DataContext
and `OnModelPropertyChanged` sender also expose that original object. The adapter
is separately available as `ViewModel` (the initial base `Value` accessor remains).
Native rendering, editing and automation use the adapter internally without
changing public model identity. `IsEffectivelySelected` includes the owning row
and outer expander cell.

Protected `SubscribeToModelChanges`/`UnsubscribeFromModelChanges` follow the
reference's extension names. They share the existing notification path: repeated
subscription calls are idempotent, and recycling removes the previous subscription.
There is no extra model subscription per call or deferred dispatcher callback.

Row overrides now receive `OnRealized(int rowIndex)` and
`OnUnrealizing(int rowIndex, TreeDataGridRowUnrealizeReason reason)`. `UpdateIndex`,
`Unrealize()` and `UnrealizeOnItemRemoved()` have public reference-compatible
entry points. The earlier Uno hook overloads remain available through the default
indexed implementations. Full standalone realization/presenter configuration and
base-class contract compatibility still require implementation review.

Native regression cases have been extended for original custom-model identity,
notification sender/count after rebind and indexed row lifecycle callbacks. They
are UNRUN. No build/test/sample/package/benchmark/CI/push is claimed for this pass.

## View-row contract and ownership (2026-09-20)

Added ITreeDataGridRows over the actual Core IRows contract, including the
reference-named GetRowAt, RealizeCell and UnrealizeCell members. Presentation.Rows,
row Rows/RowsProperty and cells-presenter Rows now expose that view contract.
There is one facade per presentation, not a second neutral row collection or
per-row wrapper: indexing, enumeration and index mappings delegate to the Core
source, preserving the original IRow and model objects.

This deliberately supersedes the initial Uno-only assertion that the public
presentation.Rows collection object itself must equal Model.Rows. Avalonia's
shared-Core implementation already has this UI row facade. Model.Rows still is
the original Core collection, and regression assertions now check that identity
plus original row objects through the facade. The shared-Core requirement has
not been replaced with copied models or algorithms.

Public realization creates a caller-owned UI cell. Custom column adapters expose
their original UI model directly on this path, so there is no extra adapter or
ownership registry. UnrealizeCell disposes it; an active cell remains the caller's
responsibility even after presentation disposal. Normal grid realization still
uses the existing native CellValue pool. Suspension/disposal reject new public
cells, suppress forwarded notifications, and retire a cell created by a factory
that changes the view before returning. No additional Core collection subscription
is installed: the facade forwards the presentation's existing notifications.

GetRowAt reports the known origin and otherwise an unknown position, matching the
Avalonia view-row implementation; precise measured heights remain presenter-owned.
Five unit cases and an additional native public-row-to-cell writeback/cleanup case
are authored, UNRUN. Standalone row/presenter realization and generic presenter
customization remain unfinished; these contracts are their prerequisites.

## Public column layout contract (2026-09-20)

The row/presenter review found a prerequisite missing from the initial Uno API:
Avalonia's IColumns measurement/position/commit contract. Added IColumns and
ColumnListBase<TColumn> with native Size/Rect/GridLength, keeping reference method
names, geometry caching, width estimation, viewport updates and nested width
batches. The notifying collection base is reused from shared Core.

Presentation.Columns and TreeDataGridRow.Columns now expose IColumns. The actual
entries are the existing view columns over the same Core definitions; the native
renderer accesses that same list through an internal typed view. Projection
changes publish a batched Reset after selection indexes are updated. The native
grid still uses its existing width solver and adopts its completed viewport/
constraint commit into the public layout state. Public layout invalidation feeds
the grid's existing geometry/update path. Sample accesses to Uno-only column
members now cast to CellColumn explicitly, as distinct from the common IColumn
contract.

Column observation uses a collection-owned weak listener per distinct column,
with duplicate-entry counts and deterministic removal. There is no global tracker
or per-cell subscription. Reentrancy is checked before changing that ownership.
Five unit cases and a native public-width/Core/native-geometry check are authored,
UNRUN. The Core Collection<T> base already supplies IReadOnlyList<T>; no second
neutral collection implementation is introduced.

This is a prerequisite for row/presenter compatibility, not its completion.
Standalone row/cells-presenter realization, view-row contracts and generic
presenter customization still require implementation. Arbitrary presentation
implementations must still supply native CellColumn views; the existing typed
column factory adapter remains the route for custom ICellColumn implementations.

## Compatible cell realization entry point (2026-09-20)

`TreeDataGridCell.Realize(factory, selection, ICell, columnIndex, rowIndex)` is
now a public virtual customization entry point used by normal native grid
realization, not an isolated convenience overload. Existing Uno realization
overrides and specialized text/checkbox/template setup remain in the path.
The handoff uses a value-type context cleared after the call, not a per-recycle
allocation or dispatcher post. It captures the row model before calling the
override because another access can retarget Core's flyweight row.

The same overload supports independently hosted cell models. Those models are
borrowed: unrealization disposes only an added observation adapter, never the
caller-owned model. Template-resolution/selection-query failure runs cleanup.
Added the reference-named protected/internal EndEdit entry point over the existing
transactional commit. Existing public BeginEdit/CommitEdit boolean results remain
available for native validation handling.

`Uno.Controls.Selection.ITreeDataGridSelectionInteraction` supplies the matching
selection queries/event and optional input hooks with native routed args. The
Core-backed selection view supplies sorted row/cell queries and forwards its
existing visual-change event without another source subscription. Input routing
still belongs to the native grid; the interface alone does not establish full
custom interaction/presentation parity or standalone keyboard/pointer behavior.

Authored tests cover real-grid override dispatch/recycling/flyweight identity,
standalone model identity, observation/writeback, selection, specialized values,
synchronous reuse, templates and failure cleanup; a selection unit case covers
sorted/hidden-column and empty-column mappings. All are UNRUN. Public row and
presenter/base standalone contracts remain implementation work.

## Element factory implementation checkpoint (2026-09-20)

The port now exposes `ElementFactory`/`ElementFactoryProperty`, protected
`CreateDefaultElementFactory`, and `TreeDataGridElementFactory` with the matching
`GetOrCreateElement`, `RecycleElement`, `CanReuseElement`, protected `CreateElement`,
`GetDataRecycleKey`, and `GetElementRecycleKey` names. Overrides receive actual UI
cell models, UI columns or shared Core rows. `GetOrCreateElement` takes a native
`FrameworkElement` parent because Uno's `Panel` does not derive from `Control`.
Returned containers use native `Control`, as expected after framework substitution.

The direct factory pool tries the same parent first, then unparented elements,
then Avalonia-style cross-panel fallback. Weak parent keys and weak fallback
entries avoid keeping obsolete visual roots alive; entries are reused and pools
are bounded. The grid's existing presenter pools remain the sole owners of their
pooled controls, consult the same recycling-key overrides and retain compatible
native parents. They do not register containers in a second factory pool.

Changing the factory resets native containers, not the caller-owned Core source.
The earlier Uno-only `CellFactory` delegate remains supported as a cell-specific
override; restoring its original delegate restores ElementFactory-based creation.
Cells also expose protected virtual `UpdateValue` and `OnModelPropertyChanged`.

This does **not** complete primitive compatibility. Native factory regression
code is authored and included in the deferred smoke sequence; it has not been
compiled or run. The subsequent specialized-cell checkpoint is described below.

## Specialized cell implementation checkpoint (2026-09-20)

The default factory now selects `TreeDataGridTextCell`, `TreeDataGridCheckBoxCell`,
`TreeDataGridTemplateCell` and `TreeDataGridExpanderCell`. Selection uses view-cell
presentation kind, never the runtime value type: a boolean text column stays text.
The legacy combined base cell remains available for existing Uno customization.

- Text: scalar `Value`, `TextAlignment`, `TextWrapping`, `TextTrimming` and matching
  dependency-property identifiers; uses ITextCell formatting/writeback where supplied.
- Checkbox: nullable scalar `Value`, `IsReadOnly`, `IsThreeState`; native checkbox
  changes, public setters and automation honor read-only/three-state behavior.
- Template: `Content`, native `ContentTemplate`, `EditingTemplate`, cell-model
  DataContext; source-template caching preserves a consumer's display override
  while the source template is unchanged.
- Expander: `Indent`, `IsExpanded`, `ShowExpander`; values follow actual Core row
  notifications and public expansion writes return to that row.

All use the existing model notification subscription and retained native template
implementation. Dependency properties are synchronized without reparenting,
posting dispatcher work or adding model subscriptions; unchanged formatting enum
values are compared before native DP writes. Final unrealization clears scalar/
content references, while synchronous rebind keeps template content alive until
the new realization. Default styles are keyed per specialized type and share the
native cell template. This does not assert native performance or style parity.

Remaining public cell-model/base/presenter signatures and exact custom-template/
specialized automation-peer compatibility are implementation requirements, not
waived platform differences. Native scalar/retention/writeback regression code
and additional expander checks are authored; all remain unrun. The subsequent
inner-control step follows.

## Expander inner-control checkpoint (2026-09-20)

Expanders now use the public element factory for the actual inner cell model,
including its custom recycling keys. A compatible child stays attached to the
same native Border through synchronous BeginRebind/Unrealize/Realize/EndRebind.
Incompatible children and explicit retemplating release the old child. Inner
controls do not own/dispose the outer value's inner model or its column.

The native default expander template has a chevron and `PART_Content` Border,
not a second collection of unused text/checkbox/template/editor hosts. Uno Border
is the counterpart to Avalonia's Decorator here. The existing optional native
`PART_InnerCellHost` spelling remains recognized for custom templates.

Editing targets the inner control; the outer control delegates Begin/Commit/Cancel,
EditingText and error state, and mirrors editing/validation dependency properties.
Value notifications identify the registered outer cell once, after the inner
scalar state has updated. Inner controls receive the row's current indexes and
selected foreground while the outer cell owns current/selection borders. Visual
lookup resolves an inner element to its public outer cell. Automation recognizes
this ownership and applies inner checkbox read-only/three-state overrides.

Added UI `IExpander` and `IExpanderCellPresentation` contracts referencing the actual
Core row and inner cell; no neutral row/model is copied. Arbitrary third-party
cell-model adapters and other remaining public signatures still require review.
Native regression cases cover custom text/check/template children, retained parent,
edit commit/cancel/value events, incompatible keys and source removal/reentrancy.
They are authored only; no builds, native execution or performance evidence yet.

## Public cell-model checkpoint (2026-09-20)

Added UI `TextCell<T>`, `CheckBoxCell` and `TemplateCell` with value constructors,
matching scalar/options/editing members and disposable observable subscriptions.
They use Core's NotifyingBase but are view models, not copies of Core rows/sources.
Native observable constructors accept `IObservable<T>` and an `IObserver<T>` writer
(or an observable that also implements the observer interface). Avalonia's
`ISubject<BindingValue<T>>` is framework-specific and is not imported into Uno.
There is no new Reactive package dependency. Editable observables without a writer
are rejected at construction, and inbound values are not echoed to the writer.

Custom-column adapters preserve checkbox/template kind, three-state/read-only
metadata, per-cell template callbacks, display text/culture, original cell identity
for ElementFactory callbacks and owned-cell disposal. A template editor uses its
actual content and IEditableObject transaction, even when content differs from the
row model. Model template callbacks receive the actual inner control for expanders.
Checkbox `CanEdit=false`/None gestures mean no text editor; separate internal-view
`CanWrite` keeps toggling enabled when the binding is writable. Native/default and
declarative checkbox paths follow the same distinction.

Text edits buffer raw culture-aware values, cancel cleanly, and keep conversion
failures available for retry instead of silently discarding them. Nullable/enum
conversion is handled explicitly. These semantics preserve the port's error/
validation requirements rather than copying the reference's swallowed exceptions.

Six unit cases are authored for observable ownership/no echo, text transactions/
conversion/culture, checkbox semantics, template transaction forwarding and custom
adapter model/kind/writeback identity. They are UNRUN. Custom-cell suspension/reuse
contracts, remaining public base/presenter signatures and native custom-model
rendering are still open. This checkpoint does not certify compatibility or
performance. The subsequent third-party expander step follows.

## Third-party expander adapter checkpoint (2026-09-20)

`ICellColumn<TModel>` can now return an application implementation of
`IExpanderCellPresentation`, not only the built-in ExpanderCellValue. The adapter
preserves the original model for factory dispatch and returns its actual Core Row
and original Content. Inner models are recursively adapted by their own kind.
No separate Core row, source or expansion state is constructed.

Content notifications can replace text/check/template/expander models, clear the
inner content, and restore it later. Compatible controls use synchronous retained
rebind; incompatible kinds release the old child. A custom expander from a regular
column uses per-cell inner metadata instead of requiring a built-in expander-column
class. Cyclic custom content is rejected. Native inner templates are resolved on
the actual factory-created child. Replacement value notifications retain the public
Value name while identifying the owning expander internally, avoiding a lost or
duplicated outer-cell event through nested wrappers.

Ownership follows the reference expander contract: the custom expander owns and
disposes its Content. The presentation owns/disposes the returned root cell;
adapters detach their own subscriptions but never separately dispose borrowed
native child models. A custom content setter must publish replacement before
disposing its previous child, so its control can cancel editing and detach first.

Three unit cases and extended native cases are authored for identity, replacement,
empty/restored content, kind switching, value events, retained parents, single
disposal, retired notifications and cycle rejection. All are UNRUN. Remaining
custom reuse/public primitive API work and complete validation are still required.

## Custom reuse and suspension checkpoint (2026-09-20)

The reference's IRecyclableCell suspension opt-in is internal, not an extension
interface for application cells. Its public `ICellColumn<TModel>.TryReuseCell`
also serves synchronous retained-cell rebinding. Uno now follows this distinction:

- A custom column receives its original ICell and the actual new Core IRow<TModel>
  during same-pass reuse. Adapter options/gestures and silent expander content
  changes are refreshed after successful retargeting.
- Old custom models are held only through the synchronous rebind attempt. An
  unused model is recycled/disposed at end-of-pass finalization, eviction, column
  removal or reset. No dispatcher work or new public unsafe pooling opt-in is used.
- Long-lived model pooling still requires a view value that can safely suspend.
  Taking such a value calls its owning column's reuse policy rather than bypassing
  a custom column through the native value's retarget method.
- False/throwing reuse releases the old model; source changes invalidate the
  in-flight handoff. Throwing or reentrant suspension cannot leave an unowned or
  stale value in the pool. Clearing drains old bucket stacks before reuse resumes.

Retained-model bookkeeping is allocated lazily only for custom-column rows. Native
default columns keep their existing suspended model-pool path. Native controls
still use their bounded parented pools and factory recycling keys.

Four unit cases and a native custom-reuse suite are authored for raw model/Core
row identity, safe pooled reuse, failed/reentrant suspension, metadata refresh,
rejected/throwing reuse, old subscriptions, scrolling finalization and source
changes. All are UNRUN. Remaining public primitive contracts and full validation
are still required; this is not benchmark evidence.

## Configuring the current typed presentation surface

The view now accepts an options object, matching Avalonia's assignment pattern:

```csharp
var options = new Uno.Controls.Presentation.TreeDataGridPresentationOptions<Person>();
options.Columns["Name"] = column =>
    new Uno.Controls.Models.TreeDataGrid.TextColumn<Person, string>(
        (TreeDataGridCore.Models.ValueColumn<Person, string>)column,
        new Uno.Controls.Models.TreeDataGrid.TextColumnOptions<Person> { StringFormat = "[{0}]" });
grid.PresentationOptions = options;
grid.Model = source;
```

Factories now return ICellColumn<TModel>; ValueCellColumn remains usable as an
implementation of that interface. Public text/checkbox/template facades use actual
Core accessors and rows. Template columns accept native DataTemplate objects or
resource keys, including an optional editing template. This is a working-tree API
illustration, not verified source-compatibility evidence. Configure a dictionary before assigning it;
mutating the same options object is not an observable configuration change.

The initial Uno-only getter dictionary can still be supplied as an explicit
non-generic TreeDataGridPresentationOptions instance. PresentationOptions now
defaults to null; callers must not assume a mutable default object exists.

UI column widths/options use Microsoft.UI.Xaml.GridLength; source definitions
continue to use TreeDataGridCore.GridLength. The facade converts these values,
retains the original Core source column for metadata/sorting, and keeps a custom
view header and view binding independent. Both length types support Auto/pixel/star.

## Fluent sources on shared Core (implementation checkpoint)

```csharp
using TreeDataGridCore;
using Uno.Controls;

var source = new FlatTreeDataGridSource<Person>(people)
    .WithRowHeaderColumn("#")
    .WithTextColumn(x => x.Name)
    .WithCheckBoxColumn(x => x.Enabled)
    .WithTemplateColumnFromResourceKeys("Details", "PersonTemplate", "PersonEditor");
grid.Model = source;
```

The same overload names, option names, header inference, inferred setters and
read-only switches are retained for flat and hierarchical sources. DataTemplate,
Binding and GridLength use their native WinUI types; sources/indexes stay Core.
The hierarchy helpers create actual Core expander definitions and pass through
HasChildren/IsExpanded expressions to Core's existing observation machinery.

UI metadata is associated with Core columns through a weak-key registry in the
Uno assembly, not source Tag values or a second source implementation. Explicit
PresentationOptions factories take precedence. Changing a PresentationKey stops
using the old registration. Each view gets a separate column/template cache;
Core options are neutral snapshots and templates are not stored in Core options.

Row headers capture the last model-index-path component plus one, matching the
Avalonia numbering convention rather than numbering sorted/expanded visible
rows. Capture is important because flat Core sources reuse a single row object.

Two source-inspection findings are intentionally corrected: the Avalonia fluent
expander-text helper copied only Width from the configured common options; Uno
forwards all common policy. The Avalonia fluent template helpers assigned the
search selector without enabling IsTextSearchEnabled; Uno enables it when a
TextSearchBinding is supplied. AllowTriStateSorting remains an exposed option
without implemented header behavior, as in the inspected Avalonia helper; do not
infer tri-state interaction support from the property alone.

The native search selector uses a lazy binding probe (including converters and
parameters), clears its DataContext after each read, and is evaluated on the UI
thread. Full declarative binding/writeback is still a separate open task. Eight
unit cases and a native fluent suite have been authored, not executed. This
checkpoint is not a source-compatibility or runtime validation claim.

## Declarative sources and native bindings (implementation checkpoint)

The control now accepts ItemsSource and a TreeDataGridColumns collection containing
TreeDataGridTextColumn, TreeDataGridCheckBoxColumn, TreeDataGridTemplateColumn,
TreeDataGridHierarchicalExpanderColumn and TreeDataGridRowHeaderColumn. Column
definitions can be XAML content. A Model explicitly supplied by the caller takes
precedence; clearing it restores the generated source. Generated sources use the
actual Core flat/hierarchical classes over the original row objects. A list
adapter preserves collection notifications/indexes and mutable-list operations.
Plain enumerables are materialized; observable non-IList collections retain
Core's explicit unsupported-collection error instead of silently losing updates.

Native Binding defaults to OneWay, unlike Avalonia's normalized default in the
inspected accessor. Use Mode=TwoWay for declarative editing/expansion writeback;
this is a native binding API difference. Binding objects, converters and paths
stay in the Uno assembly. Native probes handle nested observation and owner
replacement. Ordinary row property/indexer writes explicitly invoke the public
endpoint because Uno's BindingPath setter logs and swallows model exceptions.
The explicit writer applies ConvertBack, nullable/enum/type conversion and column
culture, allowing the existing edit session to report setter/conversion failures.

Core now exposes optional, framework-neutral expansion/child-reference observer
interfaces. The existing expression column still uses its original property-path
observer. Declarative Uno columns supply native subscriptions, while Core owns
the rows, expansion and collection replacement. Child-reference observation is
deferred until expanded/empty-row recovery, preserving explicit lazy children.

The new Declarative People scenario uses XAML definitions with the existing
shared Person/People model code. Four adapter/source unit cases, three Core
observer cases and one native declarative suite are authored, not executed.
Outstanding implementation/review: advanced ElementName/RelativeSource/attached
property and compiled binding forms (including failure propagation), public
Source compatibility, malformed/ambiguous indexers, unload/GC and all source-
replacement failure/reentrancy paths. None is waived as an unavoidable platform
difference. Individual definition properties are not observable; collection
changes rebuild the generated source, matching the inspected Avalonia path.

## Source compatibility and ownership (implementation checkpoint)

Source/SourceProperty now provide the compatibility entry point alongside Model.
Both accept the actual Core ITreeDataGridSource: this is not a recreation of the
legacy Avalonia source/model assembly or a claim of binary compatibility with it.
Assigning Source clears Model; assigning a non-null Model clears the explicit
Source choice and publishes Source=null. Clearing the selected entry point
reveals the current declarative source, if one exists. Source publishes that
generated source through the native dependency property. RowSelection and
ColumnSelection expose its actual Core selection instances, matching the
Source-based access pattern rather than manufacturing another selection model.

Native Source binding updates are supported. Internal publication is distinguished
from application assignments; only the exact restoration/publication value is
suppressed, so nested assignments of a different value are still handled. A CLR
Source assignment of the current generated instance is explicitly honored even
when native DP equality would suppress a change notification.

The control never disposes caller-supplied sources. If its own generated source
is explicitly selected through Source or Model, the owning bundle is retained
while that source remains selected/displayed, even if ItemsSource changes. The
bundle retires after all those references move on. This is per-control source-
replacement bookkeeping, not a per-cell or global subscription registry.

Source resets snapshot and detach old row bookkeeping before invoking clearing
callbacks. Cleanup removes only those old row controls, not a whole child
collection that may now contain rows from a newer reentrant source. Custom cell/
column disposal failures no longer stop cleanup of the remaining owned objects.
Normal scrolling/rebinding still uses parented pools; no dispatcher deferral or
extra style application was introduced by this change.

Four cleanup unit cases and a native Source compatibility/ownership/reentrancy
suite are authored, unrun. All remaining advanced-binding, unload/GC, template-
callback and full parity gates still apply; source-compatibility compilation is
not yet proven.

## Automation compatibility (implementation checkpoint)

The native grid, row and column-headers presenter now have same-named public,
derivable peers alongside the cell/header peers. Grid selection and row selection
items use the current Core row selection, including model-index mapping after
sorting/expansion. Roles match Avalonia: DataGrid, TreeItem, Header and HeaderItem.
Rows expose read-only values and expandable rows expose toggle/expand patterns.
Native provider interfaces replace Avalonia provider interfaces; rectangular cell
selection is not misrepresented as arbitrary row selection. This follows the
inspected Avalonia baseline rather than inventing an incompatible selection model.

Accessible children are ordered by current column index and omit retained hidden
containers. Providers reject retired/disabled containers and recheck realization,
source and selection identity after selection-cancellation/edit callbacks. Cell
values respect the rendered DisplayText override. State notifications use existing
view events, not new Core subscriptions. Peers retain only scalar notification
snapshots, never an old source, row wrapper or cell-value model.

One concrete Uno implementation difference matters for overhead: the inspected
FrameworkElementAutomationPeer.FromElement calls GetAutomationPeer, which creates
a peer. Notification paths therefore use a field populated by OnCreateAutomationPeer
instead of FromElement; ordinary recycling does not request peers or format extra
automation values. Native OS accessibility may request peers normally.

A native provider suite is authored and connected to smoke validation, but unrun.
Actual assistive-technology discovery, native event delivery, focus, platform
support and recycling/GC/performance still require the later validation pass.

## Committed-text search (partial implementation checkpoint)

The input layer now implements Avalonia's opted-in row-column prefix search,
500 ms typing window, repeated-character cycling, last-successful-prefix behavior,
wraparound and ordered multiple-column processing. It reads actual Core models,
including unrealized rows, and brings the selected match into view. Cell selection
and active editors do not participate. Source/row/column/selection changes during
user selectors or cancellation callbacks invalidate the operation. Culture-aware
matching and complete Unicode text elements avoid splitting surrogate pairs;
committed multi-code-unit input is accepted by the protected OnTextInput(string)
hook. Native event-argument types necessarily differ from Avalonia's TextInputEventArgs.

On platforms that implement UIElement.CharacterReceived, the event feeds this
hook. ApiInformation guards registration. IMPORTANT: the inspected Uno Skia
implementation marks this event unimplemented, and its KeyRoutedEventArgs.UnicodeKey
is internal. Skia OS text delivery therefore remains UNIMPLEMENTED; the protected
hook and engine are not substitutes for that requirement. No private reflection
or ASCII virtual-key approximation has been installed. IME/composition, dead keys,
keyboard layout and platform-head input delivery remain explicit implementation/
validation work. Four engine cases and a native committed-text suite are authored,
unrun; the latter explicitly does not prove OS input delivery.

Keyboard and pointer cell lookup now stop at a nested TreeDataGrid boundary so an
outer grid cannot process a nested grid's input as its own selection/edit action.

## Appearance and native layout (implementation checkpoint)

Removed default cell/header Foreground setters that shadowed the owning grid's
inherited brush. Selected template content and checkbox labels now receive the
same semantic selected-foreground resource as text and expanders. Header keyboard
focus uses native system focus visuals. Source-only/declarative headers now sort
Presentation.Model, the actual current Core source, rather than the mutually
exclusive Model property.

The control observes its own inherited font family/size/style/weight/stretch,
character spacing, text-scaling setting, FlowDirection and ActualTheme changes.
These invalidate variable-row geometry and native measurements and reset natural
width caches (including hidden retained views and expander inner columns). Row/
header style changes do the same. Measurements remain monotonic during normal
scrolling; a deliberate appearance change starts a new measurement epoch so a
smaller font can shrink Auto widths. Controls are retained, not retemplated or
reparented by this invalidation, and no per-cell subscription or dispatcher post
is added. Custom column implementations with their own private measurement caches
still require review against the public layout contract.

Uno's inspected layout source mirrors a FlowDirection boundary and corrects text
orientation; Thumb transforms pointer positions into its parent's coordinates.
The port keeps logical column geometry and resize deltas, without a second manual
RTL flip. This is source-based reasoning, not native RTL validation. Three cache
tests and AppearanceRuntimeChecks are authored for font growth/shrink, control/
parent retention, inherited foreground, live Light/Dark, Source-only header sort,
RTL column order/header alignment and horizontal bring-into-view. They are unrun;
high contrast, scaling, custom themes and real RTL pointer resizing remain gates.

## Header and custom cell template contracts (implementation checkpoint)

Headers expose the reference-named `Header` dependency property; the default
presenter binds it without stringifying the model. `Content` remains populated for
existing Uno templates. Sort geometry uses live theme resources. A small native
`Thumb` subclass supplies the horizontal resize cursor because Uno/WinUI expose
cursor assignment through the protected `ProtectedCursor` API; cursor ownership
follows load/unload rather than cell recycling.

Cells recognize `PART_Edit`, `PART_ContentPresenter` and
`PART_EditingContentPresenter`, retaining earlier Uno part names as fallbacks.
`Editing`/`NotEditing` visual states replace Avalonia's editing pseudo-class;
framework XAML syntax, property registration and visual states still require a
native template rather than verbatim Avalonia XAML. Two-way text editors use the
existing edit buffer: initialization, cancellation and editor cleanup must not
write to the row, while commit/retry writes the buffered value. Compatible
recycling retains the custom editor and content subtrees and their parents.

Native checks are authored for these names, scalar buffering, exact setter counts,
validation retry, header content and retained subtree/parent identity. They are
UNRUN; this checkpoint does not establish runtime, cursor or visual parity.

## Row template properties and element factories (implementation checkpoint)

`TreeDataGridRow` now exposes native `RowsProperty`, `ColumnsProperty` and
`ElementFactoryProperty` counterparts. Rows/columns retain the actual Core rows
and current Uno presentation-column list, not copied collections. As in Avalonia,
realization supplies the grid's factory; a row may then override it. Clearing the
override falls back to the grid factory. The override applies to ordinary cells
and recursively to expander children. The legacy grid CellFactory delegate still
applies when the row uses the grid's factory, not when it overrides that factory.

An explicit factory change invalidates that row's cell containers and cancels an
edit in that row only. It retains the row itself and leaves other rows' cells
alone. Nested changes during clearing coalesce into the same reset; row-generation
checks prevent old cleanup from reattaching a retired source. Normal compatible
scroll/rebind paths retain parents and do not use this configuration-reset path.
Final release clears dependency-property references even if a cleanup callback
throws. Native cases for identity, local replacement, fallback, reentrancy and
expander propagation are authored, UNRUN. Standalone presenter/realization
configuration remains a separate compatibility gap.

## Navigation and transaction ownership (implementation checkpoint)

Row-selection Left/Right now act on the hierarchy rather than moving the current
column; cell-selection arrows keep their two-dimensional behavior. Empty-selection
End targets the final row. Vetoed parent/child movement returns failure. Selection
and bring-into-view abandon retired source/layout requests after callbacks.

Commit/cancel/begin ownership is protected across application callbacks, including
starting a new edit from CellValueChanged. Recursive transaction construction and
recursive commit are rejected; cancellation during BeginEdit does not leave an
unregistered active editor. Session guards also cover direct cell/focus-loss paths.
Authored native/unit cases remain UNRUN. Actual cell focus transfer, Tab traversal
and OS keyboard delivery remain separate unfinished parity work.

## Native focus and ordered Tab traversal (implementation checkpoint)

Default cells now accept native keyboard focus and system focus visuals. Arrow
navigation focuses the target cell; row navigation preserves the focused column,
with a fallback for custom non-focusable cells. F2 uses the focused cell. Pointer
selection preserves interactive-child focus. Enter/Escape return to the cell,
unless a callback has started another editor or retired that cell.

Presenters override GetChildrenInTabFocusOrder to expose current realized
containers in display order, excluding pooled children without detaching them.
The framework still handles Tab/Shift+Tab and interactive template descendants;
no replacement Tab algorithm or dispatcher loop is introduced. This uses the
[documented WinUI child-order hook](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.getchildrenintabfocusorder?view=windows-app-sdk-1.8).

As in Avalonia's focused-container path, viewport recycling retains a focused
row/cell/header and its current model, measured and parented. Native focus loss
invalidates layout so offscreen containers can then recycle; source/item removal
still clears them. Retention includes nested UI without routing a nested grid's
selection input to its outer grid. Native focus/Tab/two-axis/header/cleanup cases
are authored, UNRUN. OS keyboard delivery, editor key handling and platform focus
rendering remain validation gates, not established parity.

## Standalone row and cells-presenter entry points (implementation checkpoint)

TreeDataGridRow now accepts Realize(elementFactory, selection, columns, rows,
rowIndex), including realization before its template is applied. Its cells
presenter exposes Items, Rows and ElementFactory dependency properties and public
Realize/Unrealize/UpdateRowIndex. A directly configured presenter does not require
a row or a grid parent. Both paths use the original Core models and the common
UI row/column contracts. Compatible cell overrides are called once and retain the
row model captured before user code can retarget a Core flyweight row.

Standalone horizontal layout uses Uno EffectiveViewportChanged and native
Measure/Arrange; model realization and width commits use the common contracts.
Recycled controls remain parented in a bounded pool. Same-pass layout replacement
uses synchronous rebind; explicit Unrealize releases content and model ownership
immediately. The native grid fast path is preserved. A native fixture is authored
and registered, UNRUN; compilation and runtime parity are not established.

Remaining differences: generic presenter base-class extension hooks, overriding
Items/Rows independently on grid-owned presenters, and grid-independent input,
selection-event and parent-removal behavior still require implementation/review.
This checkpoint does not close those findings or claim complete API parity.

## Generic presenter base port (implementation checkpoint)

The reference TreeDataGridPresenterBase<TItem> and
TreeDataGridColumnarPresenterBase<TItem> now exist with the Avalonia realization,
index, factory, measure/arrange, viewport and final-measure extension signatures.
RealizedStackElements is ported from the same reference, preserving the collection
remapping and variable-size anchoring algorithms. Native Panel parenting,
dependency properties, Loaded/Unloaded, StartBringIntoView and native measurement
replace only their framework counterparts. Factory-parent arguments accept a
FrameworkElement because a WinUI Panel is not a Control.

Native custom-subclass regression checks are authored and registered, UNRUN.
The built-in row/cell/header presenters have not yet adopted these bases: their
inheritance compatibility and execution of these hooks still require integration.
This is not a facade claiming that unconnected overrides customize the grid.
The existing grid fast paths remain in place until their ownership, recycling and
callback-retirement behavior has been carried into that integration.

## Header presenter adoption (superseding checkpoint)

The built-in TreeDataGridColumnHeadersPresenter now derives from the shared
TreeDataGridColumnarPresenterBase<IColumn>, and the reference lifecycle/layout
overrides execute for real header realization. Its Items/ElementFactory properties
also support independently hosted headers. The grid's committed geometry and
width solver remain authoritative when there is an owning grid.

TreeDataGridColumnHeader now exposes Realize(IColumns, columnIndex),
UpdateColumnIndex and Unrealize. It subscribes to its original UI column only while
realized and performs resize writes through the common collection. Native sorting,
styles, templates and focused-header lookup are retained. The generic engine has
generation-based retirement for source/factory changes inside layout callbacks,
including cleanup of controls not yet published in the realized range.

Extended native checks are authored, UNRUN. Rows/cells have not yet adopted the
common bases; that remaining inheritance/hook integration is not implied by this
header-only checkpoint. Runtime/layout/performance parity remains unverified.

## Cells presenter adoption (superseding checkpoint)

TreeDataGridCellsPresenter now derives from
TreeDataGridColumnarPresenterBase<IColumn>, with actual realization, index,
measure/arrange and recycle override dispatch. Grid-owned and standalone cells
use this common layout engine; the separate standalone layout implementation was
removed. Public Items/ElementFactory configuration is inherited and Rows remains
assignable. Original model ownership is tracked through creation and retirement,
while the native grid-wide control budget and Core binding/model pools remain in
use through dedicated recycling hooks.

Native parenting precedes realization for Uno resource lookup. Native Auto cell
measurement uses an unconstrained horizontal input because the surrounding Uno
rows presenter measures rows against the last committed extent. Neither change
alters the captured Core model or the common public cell realization hook.
Standalone/built-in subclass and retirement cases are authored, UNRUN. The rows
presenter still requires common-base adoption and full behavior/performance/API
parity remains unverified.

## Rows presenter adoption (superseding checkpoint)

TreeDataGridRowsPresenter now derives from TreeDataGridPresenterBase<Core.IRow>.
It inherits Items/ElementFactory, exposes the reference Columns property, and
dispatches actual row realization, index, measure/arrange and recycling through
the common overridable hooks. The independent Uno row realization/remapping loop
was removed. The grid no longer forwards row events a second time: the common
base observes Items. All three built-in presenter kinds now use the common bases.

The remaining native layout adaptations are explicit: sparse measured-height
geometry supplies row positions/anchors, the grid supplies its body viewport and
committed column geometry, and native controls retain their parents while pooled.
Column natural-width gathering uses one deferred batch followed by committed-width
measurement, as in Avalonia. The existing 32-row/256-cell bounds remain; standalone
rows use the same engine with public IColumns and ITreeDataGridRows. Row realization
now has one initialization path for native and standalone hosting, preserving the
captured original Core model and native prepared/clearing callbacks.

Extended built-in row subclass, collection, variable-height, parent-retention,
pool-bound and callback-retirement fixtures are authored, UNRUN. Common-base
adoption is an implementation checkpoint, not proof of API or behavior parity.
Final lifecycle/API/theme review and all validation gates remain open.

## Presentation surface and selection dispatch (implementation checkpoint)

The reference presentation surface now includes SourceIdentity, hierarchical/
sorted state, row selection queries/mutations and selected lists, SortBy, MoveRows,
INotifyPropertyChanged, Sorted and SelectionInteraction. These forward to the
original Core source and selection rather than copied state. MoveRows takes native
DataPackageOperation effects; indexes remain the shared Core IndexPath type.
The existing presentation observation publishes the new notifications without
adding another Core subscription layer.

The grid now routes preview/down/up keyboard, pointer and supported committed-
character events through SelectionInteraction. Default interaction methods call
the existing native selection implementation. Editing gestures remain independent
of selection, and nested-grid input stays isolated. Row/cell highlighting queries
the actual interaction, including custom presentation overrides. Replacement and
unload remove the prior interaction observer; reload reconnects once. Source
replacement inside editing/gesture preparation does not route the old event to
the replacement presentation.

Four presentation contract unit checks and a native custom-interaction visual/
replacement/unload fixture are authored and registered, UNRUN. The native fixture
does not synthesize or certify OS input. Skia committed-text transport remains a
separate framework limitation; these hooks do not resolve it. Remaining parity
review and compilation/runtime/performance validation are still open.

## Theme layout and template properties (implementation checkpoint)

Grid Columns, Rows, Presentation and Scroll dependency properties now publish the
original view collections and native scroll part, with retirement guards when a
property callback installs a newer source. Default presenter templates bind the
compatible collection/factory properties. Native RowsPresenter initialization
also handles template-bound Items arriving before its grid owner/configuration.

Default row/cell borders now wrap content and reserve layout space, as in the
reference, rather than being drawn as overlays. Row measurement includes that
chrome in its desired extent. Retained content/editor hosts and parented recycling
are unchanged. String headers use a native template selector for ellipsis without
converting control/model headers to strings; explicit column templates and theme
selectors remain usable. Header sorting calls the presentation's SortBy hook.
The public IndentConverter supplies the same 20-pixel hierarchy spacing using
native Thickness/IValueConverter signatures.

Appearance/source-publication fixtures and converter checks are extended/authored,
UNRUN. The [current checklist](uno-current-work.md) records the remaining specific
implementation questions; historical blanket “primitive/theme APIs remain” notes
are not a new backlog. Runtime/custom-theme parity remains unverified.

Template-part getters (RowsPresenter, ColumnHeadersPresenter and native Scroll)
now return null before template application or when absent, matching Avalonia.
The samples' existing loaded-template assumptions are explicit nullable assertions,
and the native source fixture checks the unapplied state. This is unrun as well.
