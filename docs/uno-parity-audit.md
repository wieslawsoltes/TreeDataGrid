# Uno / Avalonia parity implementation ledger

The [current completion checklist](uno-current-work.md) supersedes historical
implementation TODOs below. The ledgers remain the detailed findings history;
all uncommitted implementation still requires validation.

## Baselines and workflow

Avalonia reference: master `3ca47316d724e5e040ab0281a880e8df999b25fc`, including
the v12 declarative API and the shared-Core presentation. Uno checkpoint before
this audit: local `46d867c1fa6f2f0ef5440efa4ebaab5219dfec71`; PR #26 still points
to `62f074b6edf87403b4b8b27a1d7ecfe14aa993c6`.

Remote state re-read on 2026-09-20: PR #26 remains OPEN/DRAFT on that same head;
remote master remains `3ca47316`. No newer base requires a rebase at this checkpoint.

Per the user's implementation-first instruction, finish the missing product code
before comprehensive validation. **The changes listed as “new code” below have
not been built or tested.** Earlier checkpoint results do not cover these changes.
Do not treat a successful desktop launch as completion of this ledger. Package
consumer checks, benchmarks and the final parity review follow implementation.

Functional parity does not mean bringing back Avalonia model adapters or copied
Core types. Framework-specific APIs use native Uno/WinUI contracts; neutral
sorting, filtering, row movement and selection remain in `TreeDataGrid.Core`.

The user's subsequent clarification requires maximum practical **code
compatibility**, not only equivalent behavior. The
[API compatibility contract](uno-api-compatibility.md) governs the remaining work
and corrections to the initially simplified Uno API. Differences need a real
Uno API/layout/rendering reason; missing functionality is not such a reason.

## Latest primitive compatibility correction (2026-09-20)

Public cell Model, template-cell DataContext and model notification sender now
preserve the original ICell, not the adapter; internal rendering/editing access
the separately named ViewModel. Added reference-named idempotent subscription
hooks and IsEffectivelySelected. Row lifecycle overrides now carry Avalonia's
indexed arguments, with public update/unrealize entry points. Extended native
cases are authored and UNRUN. Standalone realization/presenter configuration,
other outstanding implementation entries and all validation gates remain open.

The feature inventory below is the initial audit; subsequent checkpoints record
implementation progress, not validation or completion of the full port.

## Feature inventory

| Surface | Avalonia implementation reference | Uno implementation / remaining work |
| --- | --- | --- |
| Source and model identity | `TreeDataGrid.CoreModel.cs`, `Presentation/` | Direct Core Model plus new Source/SourceProperty compatibility entry, Source-based RowSelection/ColumnSelection, matching mutual Source/Model switching, generated publication and owner retirement. Actual Core objects retained; no copied source models. Source/cleanup regression gates authored, unrun; remaining compatibility/lifetime review applies. |
| Typed presentation configuration | `TreeDataGridPresentationOptions.cs`, `TreeDataGrid.CoreModel.cs` | New options interface, generic options/presentation and nullable assignable PresentationOptions DP; wrong model type/null policy result/wrong source identity rejected. Typed factories now return ICellColumn<TModel>. Added UI IColumn/ICell/layout contracts and text/checkbox/template facades. Activity Monitor uses the public text facade. Remaining custom-cell/base-class/layout parity and authored unit/native cases await review/validation. |
| Grid appearance | `Themes/Generic.axaml`, `Themes/Fluent.axaml` | New code: border/corner propagation, semantic brushes, header separators, selected/current/error visuals. Still needs row-level customization and complete theme review. |
| Header content and sorting | `Primitives/TreeDataGridColumnHeader.cs`, `TreeDataGrid.cs:OnClick` | New native header preserves object content and templates instead of stringifying it to append arrows. Separate glyph states, grid sort gate and Core column sort gate. |
| Header resize | `TreeDataGridColumnHeader.cs:ResizerDragDelta` | New native Thumb, pixel/auto/star-to-pixel drag, min/max clamp and double-tap Auto. Per-column resize override plus grid fallback. Gesture/cursor/RTL audit remains. |
| Header configuration | `ShowColumnHeaders`, header theme | New `ShowColumnHeaders`, `CanUserResizeColumns`, `CanUserSortColumns`, `ColumnHeaderStyle`, view header template/selector; horizontal scroll synchronization in both directions. |
| Column geometry | `TreeDataGridColumnarPresenterBase.cs`, column models | Fixed/auto/star sizing, min/max Auto/pixel constraints, visibility/order and horizontal viewport already implemented. Natural-size invalidation across runtime theme/font changes still needs review. |
| Row geometry | `TreeDataGridRowsPresenter.cs`, `TreeDataGridRow.cs` | New real TreeDataGridRow with a TreeDataGridCellsPresenter template, RowStyle and subclass lifecycle hooks. Sparse height/anchor geometry retained; rows and cells remain parented during normal reuse. Full public factory/configuration and native layout/retention validation remain. |
| Cell templates | text/checkbox/template/expander primitives | Native retained cells and display/editor hosts exist. New padding/border/corner propagation and chevron template. Cell-style configuration and custom-template lifecycle review remain. |
| Text options | `TextColumnOptions.cs`, `TreeDataGridTextCell.cs` | New format/culture/search metadata; wrapping/alignment/trimming exist. Editor uses unformatted raw values with matching parse culture. Search execution/input remains. |
| Checkbox | `TreeDataGridCheckBoxCell.cs` | Two/three-state, read-only and writeback exist. Error feedback and checkbox automation remain to audit/implement. |
| Expansion | `TreeDataGridExpanderCell.cs`, Core expansion observer | Lazy expansion, nested expansion/has-children updates and custom inner-column disposal exist. New native chevron states replace plus/minus content; no per-recycle template reapplication. |
| Editing | `TreeDataGridCell.cs`, text/template cell implementations | Buffered editing, transactions, validation retry, source-change cancellation and retained editors exist. New F2/tap/double-tap/WhenSelected gesture options. Focus traversal, template gesture exclusion and editing lifecycle events still need review. |
| Selection | row/cell selection interactions and Core selection | Row and rectangular cell selection, ranges, select-all, sorted/hidden-column mapping exist. New cancellable SelectionChanging, matching Row/Cell/Multiple flags, untouched-source selection preservation and typed SelectionChanged row/cell deltas. Current-cell state is set before synchronous Core callbacks. Full navigation/focus behavior and notification validation remain. |
| Incremental text search | `TreeDataGridRowSelectionInteraction.cs:HandleTextInput` | New column text/selector contracts only. Implement timeout, repeated-character cycling, wraparound and displayed-row search; wire actual text input, not just Latin virtual-key guesses. |
| Native text input | Uno keyboard implementation vs Avalonia text event | Local Uno source marks UIElement.CharacterReceived unsupported; KeyRoutedEventArgs.UnicodeKey is internal. Resolve an actual supported input path for Skia and browser; do not claim international-text parity from key-code mapping. |
| Drag/drop | `TreeDataGrid.cs` drag methods, row drag event args | New code: mouse/pen-secondary initiation, selected-index/model payload, cancel/allowed effects, native over/drop events, before/inside/after template indicator, sorted/self/descendant and stale-model safeguards, edge autoscroll and actual Core MoveRows. Awaited drag cleanup covers late starting cancellation; custom drops expose native args/deferrals. TargetRow now exposes an actual row; full interaction/lifetime validation is deferred. |
| Realization events and lookup | `CellPrepared/Clearing/ValueChanged`, `RowPrepared/Clearing`, `TryGet*` | New row/cell prepared/clearing events, CellValueChanged, row/cell/index/visual lookup and row-model lookup. No lifecycle argument allocation without subscribers. Prepared cells are registered and rebound before callbacks; clearing preserves old identity. Full factory/configuration compatibility and lifecycle exception/reentrancy validation remain. |
| Declarative API | `TreeDataGrid.V12.cs`, `TreeDataGridColumn.cs`, binding accessor | New ItemsSource/ColumnDefinitions and all declarative column kinds generate actual Core sources over the original list/models. Native probes observe nested paths; explicit row property/indexer writes preserve edit failures. Core observer hooks preserve nested expansion/child replacement without UI model copies. Source compatibility/owned-source promotion added. Advanced binding forms, unload/GC and complete reentrancy/failure handling remain; cases authored, unrun. |
| Fluent source construction | `TreeDataGridSourceExtensions.cs` | New same-named flat/hierarchical overloads, option types, inferred headers/setters, checkbox modes, direct/keyed templates and expander helpers. Weak-key view registrations retain actual Core sources/columns, explicit factories override them. Unit/native cases authored, unrun. |
| Row header | `TreeDataGridRowHeaderColumnInternal.cs` | New model-index-path-based numbering, read-only/non-sortable. Capture the index immediately because Core flat rows are flyweights. Sorting, sibling numbering and recycling cases authored, unrun. |
| Automation | all six `Automation/Peers` implementations | New header/cell peers with native HeaderItem/DataItem/CheckBox roles, text values, checkbox toggle and expand/collapse. Peers query current realization, suppress hidden pooled cells/headers and retain template/editor children. Grid/row providers, selection patterns/notifications, state-change notifications and native disabled-element error conventions still remain. |
| Retention/performance | latest Avalonia rebind, pools and presenters | Core binding/source fixes consumed directly; synchronous Begin/End rebind and bounded parented pools exist. Preserve these through the remaining implementation, then run native identity/lifetime and allocation/timing validation. |
| Showcase | Avalonia Demo and CoreDemo scenarios | Countries, People, Templates, variable-height Countries, Wikipedia, tree/flat Files and Find Country exist. New DragDrop scenario source-links DragDropItem, including Allow Drag/Allow Drop behavior, reset and clear-sort controls. New Declarative People uses XAML columns and the shared Person models with add/remove/edit controls. Compare every scenario's controls/actions, not just the scenario names; new code remains unvalidated. |
| Activity Monitor | original Uno PR #12 and shared-Core requirement | Ported in local 46d867c1 with all five sections, shared Core, native/demo providers and chart fixes. Theme integration and final sample interaction review remain. |
| Heads and package | Uno SDK projects and CI | Desktop Skia exists. Browser and Windows App SDK heads still need implementation and subsequent validation. Packaging must use `TreeDataGrid.Controls.Uno` and the actual Core package. |

Paths in the reference column are relative to `src/Avalonia.Controls.TreeDataGrid`
unless described otherwise. Presence in this inventory is not a claim that every
line in every reference has already been reviewed; continue the detailed audit
as each remaining implementation area is taken up.

## Theme decisions and findings

- The former plain Button header converted a model/control header to a string
  when sorted. It also had no resize grip or explicit header states. A dedicated
  `TreeDataGridColumnHeader` now keeps content, sort glyph and Thumb separate.
- The public Avalonia brush keys are retained as semantic Uno resources. Default
  (dark), Light and HighContrast dictionaries use native theme lookups. They
  provide selected background/foreground, hover/pressed header colors, grid
  lines, focus/current and validation borders.
- Sort/expansion paths use the baseline geometries. Expansion changes a state
  on a retained button template; it must not instantiate content or explicitly
  reapply templates on every recycled row.
- Retemplating headers detaches all old Thumb event handlers. Unrealization
  clears owner, column, templates and user content. The bounded horizontal pool
  remains parented during normal scrolling.
- Exact Avalonia type/style identity is not portable. Row-level customization
  and accessibility still need deliberate native counterparts, not a claim
  that styling each independent cell is equivalent to a row container.
- Avalonia declares `AllowTriStateSorting` in its declarative options, but the
  audited header click path alternates ascending/descending. Do not invent a
  three-state functional baseline solely from that unused option declaration.

## Deferred validation acceptance list

### Row-container implementation checkpoint (2026-09-06; unvalidated)

- Replaced the flat cell-child panel with actual row controls and per-row cells
  presenters. The outer row pool is capped at 32, and a shared budget caps all
  pooled cell controls at 256 rather than allocating 256 per row. Normal reuse
  does not move a cell between parents or call ApplyTemplate explicitly.
- Same-pass row reuse brackets cell unrealization/rebinding synchronously;
  unused template content clears at finalization. Source replacement retargets
  cells without forcing a native Measure/ApplyTemplate from the notification.
- Added RowPrepared/RowClearing, RowUnrealizeReason, subclass lifecycle hooks,
  realized-row access, index/visual lookup and model lookup. Flat Core models
  are captured rather than relying on persistent IRow wrapper identity.
- Drag/drop exposes TargetRow and the sample uses TargetRow.Model. The captured
  TargetModel remains available for stale-target checks across user callbacks.
- The native row template owns the full-row selection background. Row-selected
  cells retain selection state/foreground without a second translucent overlay.
- Source/column revision checks abort stale realization after user callbacks;
  cell recycling removes bookkeeping before invoking overridable cleanup.
  These safeguards still require the deferred failure/reentrancy regressions.
- Source review also corrected resize constraint precedence: MaximumWidth wins
  over a conflicting MinimumWidth, matching the existing sizing algorithm.

No build, test, benchmark, native launch, package validation or CI was run for
this checkpoint. Earlier evidence applies only to the earlier committed code.

### Selection and cell lifecycle checkpoint (2026-09-20; unvalidated)

- Added generic and non-generic TreeDataGridSelectionChangedEventArgs and the
  compatible typed control event. These derive from actual Core selection args;
  no neutral selection implementation was copied into the Uno assembly.
- Core row events provide selected/deselected model indexes and items. Item/index
  lists are captured before application callbacks can mutate lazy Core views.
  Cell events diff selected Core CellIndex snapshots, including hidden columns.
  As in Avalonia, cell deltas do not synthesize row-selection item payloads.
- Visual/anchor invalidation remains separate: index shifts, resume and model
  replacement do not masquerade as selected/deselected notifications. Cell
  snapshots are maintained only while detailed listeners exist. Resume and late
  subscription start from current Core state, without replaying hidden changes.
- Added CellPrepared/CellClearing/CellValueChanged and TreeDataGridCellEventArgs.
  Preparation follows registration/selection/rebind completion; clearing runs
  before old model/index identity is removed. Value notifications are suppressed
  during editing/rebind and emitted after a successful commit. The Cell.Model
  property exposes its view cell model, separately from RowModel.
- Cell realization generations prevent cleanup of a newer realization if an
  application callback recycles the same instance. A failed preparation handler
  returns the still-owned cell through the cleanup path. The native failure and
  reentrancy matrix still needs execution and further review.
- Added seven selection notification test cases, native control selection-delta
  assertions, and a native lifecycle suite covering bindings, edit commit/cancel,
  replacement, scrolling, source removal and CellPrepared source replacement.
  They are wired into the deferred suite but have not been executed.

No build, test, benchmark, native launch, package validation or CI was run for
this checkpoint. Product implementation remains incomplete in the rows above.

### Assignable typed options checkpoint (2026-09-20; unvalidated)

- Added ITreeDataGridPresentationOptions.Create, generic options with typed Core
  column inputs and a public generic presentation exposing its typed Core model.
  The initial non-generic options dictionary remains a supported explicit policy.
- PresentationOptions is now a nullable assignable dependency property. Changed
  options rebuild view state while retaining the supplied Core source. Factory
  failure before replacement restores the changed property, whether the failed
  assignment was Model or PresentationOptions, rather than restoring Model for
  every configuration error. Native DP argument Property support was checked in
  local Uno source.
- A custom policy returning null cannot silently fall back to defaults. A policy
  returning a presentation over another Core source is rejected and its view
  disposed; neither Core source is owned/disposed by that rejection.
- Activity Monitor's five generic sections now expose typed view options; the
  view assigns those options before assigning the section's shared Core source.
- Added seven unit cases plus a native suite for configured formatting, live
  replacement and wrong-model/missing-key/throwing/null-options rollback. These
  are authored code, not execution evidence.
- This does not close typed factory compatibility: the factory return remains
  CellColumn, not the complete ICellColumn<TModel>/UI-column facade contract.
  That explicit gap, typed text/checkbox/template options and fluent/declarative
  construction remain required, not accepted platform differences.

No build/test/native/package/benchmark/CI validation was run for these changes.

### Public column contracts checkpoint (2026-09-20; unvalidated)

- Typed factory return signatures now use ICellColumn<TModel>. Added public UI
  IColumn, IUpdateColumnLayout, ICell/ITextCell and cell-option interfaces. Native
  GridLength is converted at the UI boundary; neutral widths remain Core values.
- Added public text, boolean/nullable-boolean and template column constructors,
  plus corresponding generic options. They use the existing Core accessors,
  observer-backed bindings and pools. Direct Core-column constructors are also
  available; view constraints can override Core defaults without mutating them.
- Factory-created view bindings can differ from source bindings, but metadata and
  sorting stay attached to the actual supplied Core column. UI header changes
  notify the native presentation without changing the Core header. ActualWidth
  is committed from shared viewport geometry; custom layout implementations
  receive cell/header measurement indexes and natural-size discovery.
- Native factory results use the existing CellColumn fast path. An adapter accepts
  arbitrary ICellColumn implementations and forwards ownership/layout/notifications.
  Plain custom ICell values are readable; ITextCell supports textual writeback.
  Specialized custom cell kinds, transaction behavior and element-factory parity
  remain for the remaining primitive review—not claimed complete here.
- Text values expose ITextCell and retain binding retargeting. The public reuse
  hook rejects a cell created for another getter/column, without adding a second
  stored column reference to every binding. Expansion forwards templates/header
  metadata and committed widths from its inner view.
- Templates resolve direct DataTemplate instances or resource keys from the
  control/ancestor/application resources and provide optional editing templates.
  Native resource lookup availability was checked in local Uno source.
- Activity Monitor uses the new public text facade. Six unit cases and a native
  direct/resource-template suite were authored; the existing options native
  suite now uses the facade as well. No tests/builds/native runs were executed.

The implementation remains incomplete; this checkpoint does not establish parity
or performance. Mutable option behavior, remaining column/base types, custom cell
contracts and the existing ledger items still require implementation/review.

## Fluent construction checkpoint (2026-09-20)

The source extension overloads now target the actual shared Core sources. UI
configuration stays in a weak-key Uno registry and creates a fresh column/cache
per presentation; source Tag is untouched. Explicit presentation factories win
over fluent defaults and changed keys do not pick up stale registrations.
Templates have direct-Core constructors to avoid creating a parallel definition.
Source options are neutral snapshots; templates and native bindings remain UI-owned.

The matching option classes, text/boolean/nullable-boolean overloads, row headers,
direct/keyed template overloads and both hierarchical expander helper forms are
implemented. Inferred member setters use interpreted expression compilation;
read-only/computed getters stay read-only. Row headers snapshot model indexes,
not the reusable flat row reference or a sorted display index.

Two inspected Avalonia helper omissions are corrected and documented: forward
all configured common options to expander text columns, and enable template text
search when a search binding is provided. A lazy native binding probe preserves
converter/parameter semantics and clears DataContext after each search read.
General declarative bindings, input-driven search and tri-state sorting are not
claimed implemented by this checkpoint.

Eight new unit cases and a native fluent/template/search/resource-scope suite
are authored. They have not been run. No builds, tests, native launch, packages,
benchmarks, push or CI were performed for this checkpoint. Continue the remaining
declarative, primitive, interaction, automation, theme and platform-head code first.

## Declarative source and binding checkpoint (2026-09-20)

- Added ItemsSource DP, observable ColumnDefinitions/content metadata and a
  generated-source owner. Caller Model wins; generated sources/accessors are
  disposed after view replacement, not caller-owned models. Replacement revisions
  stop outer lifecycle callbacks from overwriting a newer nested presentation.
- Added a list type-erasure boundary that forwards original IList mutations and
  notifications. Actual Core flat/hierarchical sources retain model/index identity.
- Added native text/checkbox bindings and a Core expander subclass using native
  children/has-children/expansion bindings. Per-cell probes detach row context on
  suspension; native declared-source bindings are cleared when suspended.
- Added optional public neutral Core expansion and child-reference observation
  interfaces, retaining the existing expression observer implementation. Core
  rows own subscription disposal and child replacement. Collapsed lazy rows do
  not attach child-reference observers before they need the collection.
- Source inspection found Uno BindingPath.SetSourceValue catches/logs setter
  exceptions. Native BindingExpression.UpdateSource therefore cannot alone
  guarantee the grid's validation/retry contract. Added explicit ordinary row
  property/indexer writeback with converter/culture conversion and unwrapped
  setter errors; native binding remains responsible for observation/read values.
- Added Declarative People XAML over shared model code, four source/adapter unit
  cases, three Core observer cases and native nested editing/checkbox/expansion,
  child replacement, null recovery, ownership and subscription-cleanup gates.

All changes/cases are unvalidated. Advanced bindings (ElementName/RelativeSource,
attached/compiled paths and their error propagation), malformed/ambiguous indexers,
Source compatibility and full unload/reentrant-failure ownership still need code
review/implementation. No build/test/native launch/package/benchmark/push/CI was
performed; continue these and the remaining primitive/input/automation/theme/head
work before starting validation.

## Source API and replacement cleanup checkpoint (2026-09-20)

- Added native SourceProperty/Source using actual Core sources, plus Source-based
  row/cell selection accessors. Matched Avalonia's switching semantics: Source
  assignment clears Model; non-null Model clears explicit Source; generated
  sources reappear when the selected explicit entry is cleared.
- Published generated sources through SourceProperty without treating internal
  publication as an explicit user selection. Narrow property/value guards and
  configuration revisions preserve newer reentrant assignments and rollback.
- Retain a generated-source owner promoted into Source or Model while still
  selected/displayed. Retired owners are collected only after their views and
  configuration references move on; external Core sources are not disposed.
- Found a concrete reentrant-reset problem: clearing callbacks could install and
  lay out newer rows, after which the old reset's Children.Clear erased them.
  Reset now detaches old bookkeeping first and removes only its captured rows.
  Newer presenter generations reject stale SetPresentation completions.
- Cleanup now attempts all old rows, pooled cells and view columns even when one
  throws. Staged replacement columns are disposed when pool cleanup prevents
  their commit. Reentrant Dispose is idempotent. Normal recycled controls remain
  parented; these changes concern source/definition replacement and failure paths.

Four cleanup unit cases and a native source suite are authored for direct/native-
binding assignment, selection identity, generated ownership/publication, failed
factory rollback, nested source layout and throwing clearing callbacks. No build,
test, launch, benchmark, package, push or CI was run. Remaining advanced-binding,
unload/GC, retemplating/failure and full API/theme/input/platform work is not waived.

After the implementation ledger is closed, run:

### Accessibility implementation checkpoint (2026-09-20)

Implemented grid/row/header-presenter peers with the Avalonia names, roles and
realized-row selection contract. They use the actual current Core selection and
expander, preserve ordered realized children, and exclude retained hidden rows/
cells. Row selection rechecks source/selection/container/model-index identity
after user selection/edit callbacks. Disabled actions use the native automation
exception. Cell values honor DisplayText; existing lifecycle/value/selection
events deliver state notifications without separate model subscriptions.

Source inspection found Uno's FromElement can create a peer. Notifications now
use peer fields initialized only by OnCreateAutomationPeer, avoiding that work
on the ordinary recycling path. Only scalar previous-state snapshots are cached.

Authored AutomationRuntimeChecks for roles, multiple row selection, shared Core
expansion, text and nullable-checkbox writeback, disabled actions, child order,
cell-selection pattern exclusion, reentrant source replacement and stale-provider
rejection. Added to the smoke sequence; NOT executed. Native discovery/event
delivery, retained-pool behavior, assistive technology and allocation gates remain
unproven. No build, test, launch, package, benchmark, push or CI was run.

### Committed-text implementation checkpoint (2026-09-20)

Added view-owned incremental-search state and the control's committed-text path:
500 ms timeout, repeated-character cycling, last successful prefix, culture-aware
Unicode matching, Core row-index mapping, wraparound, opted-in column order,
virtualized matches, selection cancellation and callback-driven source/structure
invalidation. Selection-mode/source/unload changes reset input state. Nested grids
now form a boundary for keyboard and pointer cell routing.

Native CharacterReceived registration is capability-guarded. The local Uno Skia
source marks the public event unimplemented; UnicodeKey is internal. Skia OS
transport, IME/dead-key/layout handling and full keyboard focus/navigation remain
OPEN implementation items. An ASCII approximation is not accepted as completion.
Authored four engine unit cases and a native control suite using committed-text
injection, clearly distinct from actual OS-input validation. Nothing was run.

Follow-up source inspection confirmed that CoreWindow.KeyEventArgs.UnicodeKey is
also internal, so switching to CoreWindow events does not provide a supported
Unicode transport. The public InputKeyboardSource implementation only provides
key-state access; CharacterReceived is generated as an unsupported event. The
latest inspected Uno TextBox IME path is specific to a focused TextBox, not a
grid-wide committed-text event. Do not silently focus an invisible editor or use
reflection/private ABI access to claim compatible grid input.

Windows is a separate gate: Microsoft's [CharacterReceived documentation](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.uielement.characterreceived?view=windows-app-sdk-1.8)
explicitly excludes IME input. Uno's [input support matrix](https://platform.uno/docs/articles/features/pointers-keyboard-and-other-user-inputs.html)
documents Skia key events, not a composed-text event. Thus native Unicode/IME
delivery still needs an explicit supported integration decision and real input
validation, even if engine/injected-text cases pass. No framework checkout or
dependency version was changed during this investigation.

### Appearance implementation checkpoint (2026-09-20)

Corrected Source-only/declarative header sorting to use the current presentation's
Core model. Removed normal cell/header foreground overrides that prevented grid
brush inheritance; selected template/checkbox content now shares the semantic
selected foreground. Enabled native header focus visuals.

Own-control inherited font/text metrics, theme and FlowDirection callbacks now
invalidate native measurement/row geometry and reset natural column-width caches.
Hidden retained views and expander inner constraints are included. Row/header
style changes also invalidate caches. Normal scrolling stays monotonic and pooled
controls are not replaced/reparented by this code. Native Uno source already
mirrors RTL boundaries and transforms Thumb deltas into parent coordinates, so
no double mirroring or manual resize-sign inversion was added.

Authored three cache unit cases and AppearanceRuntimeChecks covering retained
native controls, font growth/shrink, foreground inheritance, Light/Dark resources,
Source header sorting and RTL alignment/virtualization. All are UNRUN. Native
high contrast, scaling, real pointer resizing and custom theme/layout contracts
remain unproven. No build, test, launch, package, benchmark, push or CI was run.

### Platform-head restoration checkpoint (2026-09-20)

Restored browser WebAssembly entry points and native Windows App SDK target/
manifest/unpackaged deployment for both samples. Defaults and explicit target
overrides are separated; the controls package includes the native Windows asset
when built on Windows. Both still reference the same shared Core implementation.

Activity Monitor keeps one chart drawing implementation: Skia targets use the
native canvas, while Windows presents a DPI-aware retained BGRA bitmap through
WinUI Image, with hover and unload/root-handler cleanup. This is a real platform
rendering difference and includes a pixel copy, not a zero-copy equivalence claim.
Windows/browser select demo telemetry without compiling the desktop macOS factory
path; browser demo labeling avoids unsupported host-machine-name queries.

Authored browser/Windows CI source-build and package-consumer publish lanes and
kept desktop runtime lanes explicitly desktop-only. No build/test/launch/publish/
workflow/push occurred. Browser file-watch/sandbox handling, runtime result
transport, trimming/AOT, and actual Windows/browser input/rendering/features remain
open. Head declarations do not close those gates. See `uno-platforms.md`.

### Browser runtime/sandbox implementation checkpoint (2026-09-20)

Added shared native/browser launch options and source-generated JSON smoke
reporting. Browser heads accept allowlisted boolean query flags and expose
complete/passed/error state without terminating the page; unhandled smoke failures
are included and cannot be overwritten by a later success. Desktop arguments and
exit behavior remain intact. Browser screenshots remain the runner's responsibility.

Extended the existing source-linked file model with an optional watcher-free mode
(default desktop behavior unchanged), inherited by lazy child nodes. Browser
uses a labeled sample-owned in-memory directory, not host folders; Open / refresh
rebuilds its snapshot. The file suite now asserts snapshot refresh on browser and
watcher updates on desktop, retaining shared-model identity/laziness/selection/
sorting checks. No separate browser file model was introduced.

Authored four launch/result and two snapshot unit cases; all unrun. Browser OS
input, actual screenshots, trimming/AOT, networking and package/native resource
validation remain open. No build/test/launch/package/push/workflow was run.

### Element factory compatibility checkpoint (2026-09-20)

Added the Avalonia-named ElementFactory property/default factory override and
public creation/recycle-key hooks. Cells receive their current view model before
factory selection; row/header factories receive shared Core rows/UI columns.
Presenter pools reject incompatible custom keys while retaining compatible native
parents and the synchronous rebind path. Direct factory consumers have same-parent
preference and cross-panel fallback, bounded pools and weak visual-root ownership.
The earlier cell-only delegate remains supported.

Factory replacement resets containers without disposing the caller's Core source.
Factory/key/template callbacks now have current-presentation guards; header reset
detaches bookkeeping before callbacks and cleans up only its old containers.
Wrong-type, foreign-parent and already-realized factory results are rejected.
Cell UpdateValue/OnModelPropertyChanged are protected virtual customization hooks.

Authored native cases for row/header/cell overrides, compatible replacement with
no unload/reparent, incompatible row and cell keys, factory replacement, legacy
delegates, factory-source reentrancy and direct-pool ownership/fallback. All are
UNRUN. Named specialized cell controls and full presenter/base-class compatibility
remain open. No build, test, launch, package, benchmark, push or CI was run.

### Specialized cell implementation checkpoint (2026-09-20)

Added named text/checkbox/template/expander controls with the corresponding scalar
dependency properties and default-style keys. Factory selection uses the view
column's kind, including newly realized/recycled/declarative values, rather than
guessing from bool/string runtime values. Text supports ITextCell formatting and
setter conversion; checkbox public/native/automation paths honor read-only and
three-state options. Template content/context follows the cell model, retains
the source-template override behavior and clears on final unrealization. Expander
properties track actual Core expansion/indent/show state; inner text formatting
metadata is forwarded from the existing inner cell value.

No extra model subscriptions or posted recycling callbacks were added. Specialized
controls reuse the existing synchronous rebind and retained child tree. Authored
native checks cover scalar writeback/external updates, rejected writes, appearance,
boolean text classification, template identity/no unload, cleanup and expander
properties; all UNRUN. Inner-expander custom control creation, public model/base/
presenter contracts and exact custom-template/automation parity remain open.
No builds, tests, launches, packages, benchmarks, pushes or CI were performed.

### Expander inner-factory implementation checkpoint (2026-09-20)

Expanders now create their inner cell through ElementFactory, honoring custom
recycling keys and retaining compatible controls in the same Border. The default
template has only the chevron/inner host plus outer state borders; unused native
text/check/template/editor hosts are not duplicated. Explicit retemplating and
incompatible keys release the old child; normal recycling stays synchronous.

Grid editing targets the factory-created inner control. Outer editing/validation
properties and public delegates follow the child; model value events resolve to
the registered outer cell without duplicate forwarding. Selection foreground,
index shifts, visual lookup and automation ownership were adapted. UI expander
contracts expose the actual shared Core row/inner cell. Factory/template/source
reentrancy guards prevent continuing a retired realization; edit-state callbacks
are attached once per retained child and detached on child release.

Native cases for text/check/template overrides, retained parenting, editing and
event counts, incompatible keys and source removal/reentrancy are authored and
UNRUN. Third-party cell-model adapters, remaining base/presenter APIs, full native
input/theme/automation parity and lifetime/performance gates remain open. No
build/test/launch/package/benchmark/push/CI occurred.

### Public cell models / custom adapter checkpoint (2026-09-20)

Added TextCell<T>, CheckBoxCell and TemplateCell UI contracts, with native typed
observable/observer binding constructors rather than Avalonia BindingValue imports.
Their observable subscriptions are disposed once and suppress inbound write echoes;
text transactions buffer raw culture-aware values and preserve conversion failures.

The custom column adapter no longer coerces checkbox/template models into text.
Per-cell kind/templates/culture/writeability and original model identity reach
rendering, factory keys and automation. Template transactions/editing content use
the actual template value, not an assumed row object. Checkbox toggling is separate
from CanEdit/gestures, including native/default and declarative paths. Native
template callback reentrancy checks run before column fallback callbacks.

Six model/adapter unit cases are authored and UNRUN. Third-party expander adapters,
custom suspension/reuse, remaining public primitive contracts and full native
rendering/interaction/lifetime/performance gates remain open. No builds, tests,
launches, packages, benchmarks, pushes or CI were performed.

### Third-party expander adapter checkpoint (2026-09-20)

Custom IExpanderCellPresentation models now adapt recursively without copying
their Core row/state or hiding original model identity from ElementFactory.
Per-cell inner kind/templates work even when the source column is an ordinary
custom column. Content replacement rebinds compatible native children, replaces
incompatible children and handles empty/restored content. An internal ownership
marker preserves one public Value event for replacement through nested wrappers.

Adapters detach only their own subscriptions; the original expander retains
ownership/disposal of its child models. Constructor failure and invalid cycles
release owned roots; replacing content detaches old adapter notifications before
the original owner's disposal. Ordinary custom text/check/template creation does
not allocate the cycle-detection set used for nested expander graphs.

Three unit cases and extended native checks are authored for identity, replacement,
single disposal, old notifications, kind/empty transitions, retained parenting,
value events and cycles; all UNRUN. Custom suspension/reuse, remaining public
primitive signatures and actual native correctness/lifetime/performance gates
remain open. No builds/tests/launches/packages/benchmarks/pushes/CI were run.

### Custom retained reuse / suspension checkpoint (2026-09-20)

Re-read Avalonia's pool and presenter paths: suspension opt-in is internal, while
public custom TryReuseCell is used during synchronous retained rebind. Uno now
stages custom models only for that same-pass handoff, supplies original ICell/Core
row objects to the column, and refreshes adapter metadata after retargeting.
Finalization/eviction/reset release unused models; regular native model pooling
keeps its safe suspension path. Custom-only staging dictionaries are lazy.

Pool checkout now respects the column's reuse policy. Failed/reentrant suspension
disposes a value that was not transferred; old pool stacks are drained to avoid
reusing entries already disposed during reentrant clearing. Retained callback
ownership is removed before calling user reuse/disposal code. No posted finalizer
or additional public pooling interface was introduced.

Four unit cases and a native original-model/options/subscription/reuse-failure/
scrolling/source-reentrancy suite are authored, UNRUN. Remaining public primitive
API, platform input/theme/features and lifetime/performance validation remain.
No builds/tests/launches/packages/benchmarks/pushes/CI were performed.

### Row viewport cache implementation checkpoint (2026-09-20)

Added the reference CacheLength DP contract (default zero, range 0–2 viewport
heights), edge buffer redistribution and protected native-Rect viewport hooks.
Small scrolls covered by the cache keep the realized row/cell set and avoid a
measure pass. Zero cache realizes only intersecting rows, without the prior
implicit one-row guard buffer. Row geometry/source/appearance/column changes
invalidate the cache; fixed-height column resizing is not skipped. Parent-retained
control/model pools are unchanged.
All three presenters now expose reference-named ordered GetRealizedElements;
header TryGetElement complements the existing row/cell lookup methods. These
queries exclude pooled containers and are covered by the authored native suite.

Authored geometry unit cases and a registered native suite for buffer boundaries,
invalid DP rollback, identity/no unload during small scrolls/cache shrink-grow,
recentered bounded realization, column width updates and empty/refilled/removed
sources. All are UNRUN. The broader primitive/presenter implementation and all
post-implementation validation gates remain open.

### Declarative write-path implementation checkpoint (2026-09-20)

The explicit native writeback adapter now validates an entire path before invoking
model accessors, resolves numeric/string indexer overloads deterministically and
rejects ambiguous fallbacks. It preserves string-key whitespace/quote semantics,
rejects inaccessible/read-only/copied-struct owners and retains original setter
exceptions. Authored focused cases cover these contracts, nested arrays/indexers,
typed culture/nullable/enum conversion and successful retry. All are UNRUN.
Generated metadata/AOT and native attached/relative/named/compiled binding forms
remain separate implementation requirements, not completed by this correction.

### Generated binding metadata and lifecycle checkpoint (2026-09-20)

Uno named-property writes now use its public generated metadata provider before
reflection, retaining declared types and read-only/DP semantics. Windows App SDK
does not compile against the Uno-specific API. Generated indexers, x:Bind,
attached/native relative/named binding forms and end-to-end trimming/AOT remain.

Nested binding mutations now have counter-based notification suppression and
revision-guarded error/model publication. User getters/converters cannot write a
retired endpoint; cleanup preserves the primary write failure and releases native
probe ownership before detachment. Reentrant snapshot cleanup does not suspend a
newer realization. Template search uses this reader to disconnect explicit Source
subscriptions after each snapshot, fixing a separate observer-retention path.
Five metadata cases and a registered native retirement/retry/subscription suite
are authored, UNRUN. All post-implementation validation gates remain outstanding.

### Header and custom-template compatibility checkpoint (2026-09-20)

Implemented the reference Header DP/default binding, live sort-icon theme
resources and a native resize-cursor Thumb. Cells recognize Avalonia's editor and
content-presenter part names in addition to the older Uno names. Editing visual
states enable custom native templates; template-bound Value changes stay in the
text edit buffer until commit, including initialization and cleanup. Cancellation
restores display values and rejected edits retain their buffer for retry.

Authored native cases check exact setter invocation counts, programmatic buffered
Value, custom editor/content identity, retained parents/no unload, header content
and resize-grip presence. All are UNRUN. Native property/XAML/visual-state and
cursor APIs are intentional framework adaptations, not claims of verbatim XAML
compatibility. Broader public primitive contracts and platform/binding gaps remain
open. No builds/tests/launches/packages/benchmarks/pushes/CI were performed.

### Row template/factory contract checkpoint (2026-09-20)

Added Rows, Columns and ElementFactory dependency-property counterparts on the
row. They reference the actual Core rows and native presentation columns. A row
override now controls its cells and expander children; clearing it restores the
grid factory. Explicit factory changes reset only that row's cells, retaining the
row and unrelated containers. Nested clearing-time assignments are coalesced and
retired row generations cannot be reattached. Final release drains stored DP
references even after callback failures. Normal compatible recycling stays on the
parent-retained path and gains no dispatcher operation or model subscription.

Authored native cases cover DP/source identity, release cleanup, localized factory
replacement, nested assignment with reentrant layout, fallback, and text/checkbox/
template expander propagation. These are UNRUN; standalone presenter/realization
contracts and other implementation gaps remain. No build/test/launch/package/
benchmark/push/CI was performed.

### Navigation and edit ownership checkpoint (2026-09-20)

Reference comparison found that flat row selection must not treat Left/Right as
horizontal cell navigation; hierarchy expansion/navigation remains available with
Shift. End now targets the last row when selection is initially empty. Hierarchy
parent/child navigation propagates a selection veto instead of reporting success.
Selection callbacks that replace the source or issue another selection cannot
apply the obsolete outer request to the newer state. Bring-into-view checks its
source, structure, presenter and scroller after callback-capable layout/scrolling.

Grid edit requests now retain transaction ownership across callbacks. A newer edit
started by CellValueChanged is not cleared by the outer commit; cancellation while
IEditableObject.BeginEdit is constructing an unregistered session cleans it up.
Recursive construction/commit is rejected rather than duplicating model writes or
EndEdit. A session-level guard covers direct cell/focus-loss commits as well as
grid calls. Authored native cases cover these transitions; two unit cases cover
recursive writes/EndEdit. All are UNRUN.

Source review also confirmed an outstanding focus difference: Avalonia cells are
focusable and navigation focuses the target cell, whereas the current Uno default
cell style sets IsTabStop=false and leaves focus on the grid. Native focus/tab
semantics must be implemented deliberately; selection movement alone is not full
keyboard parity. No builds/tests/launches/packages/benchmarks/pushes/CI were run.

### Native focus and virtualization checkpoint (2026-09-20)

Implemented the previously identified focus gap: native cells accept focus/system
visuals; keyboard movement focuses the target and keeps the focused column. F2
targets the focused cell. Pointer selection preserves interactive-child focus.
Editor Enter/Escape return to the cell unless a newer edit owns focus.

All presenters expose display-ordered realized children through native
GetChildrenInTabFocusOrder. Tab stays framework-driven; pooled native children
remain parented but excluded. Focused offscreen rows/cells/headers stay measured
and bound to their original model until focus leaves. Native LostFocus invalidates
retention without dispatcher posts or per-cell subscriptions. Removal/reset still
clears retained focus owners.

A registered native suite covers matching-column focus, forward/reverse and
post-recycling Tab order, two-axis model/parent identity/no unload, header retention,
release after focus leaves and source cleanup. All are UNRUN. Real OS keys,
Enter/Escape and platform focus visuals remain validation gates. Source inspection
also corrected two prior call sites treating TryGetCell's Control return type as
the specialized cell type. No build/test/launch/package/benchmark/push/CI was run.

### Generated indexer implementation checkpoint (2026-09-20)

The explicit writer now consults public generated indexer metadata before
reflection, preserving native string keys, read-only rules, setter errors and
retirement guards. Retained CLR string-indexer types supply conversions;
generated-only endpoints use object because native metadata lacks a declared
value type. Nine cases are authored, UNRUN; full trimming/AOT remains unproven.

Binding/declarative native fixtures now cast TryGetCell's Control result before
accessing cell-only members. Advanced binding review confirmed detached probes
in both frameworks: named/relative scope, failure propagation and late-name
cleanup still need specific work. No build/test/launch/package/benchmark/push/CI
was performed.

### Named-source and writeback ownership checkpoint (2026-09-20)

Local Uno name subjects now have explicit subscription ownership and retire the
old resolved probe on replacement. Pooling/disposal detaches the subject handler;
an unresolved subject never uses the row as its source. Native-resolved ordinary
relative paths use the error-aware writer on Uno. Explicit sources without INPC
refresh their expression after writes. Endpoint/owner checks reject a converter
replacing the target, including on Windows where DataItem need not be the leaf.
Additional nested traversal occurs only during edits, not realization/recycling.

Native regression cases and two nested-owner unit cases are authored, UNRUN.
Silent subject-null changes cannot force immediate refresh without a framework
notification; reads reject stale data and later lifetime operations detach it.
Outer namescopes, attached properties, x:Bind, trimming/AOT and Windows native
relative-source error propagation remain open. Public standalone presenter/base
contracts also remain implementation work. No builds/tests/launches/packages/
benchmarks/pushes/CI were run.

### Compatible cell realization checkpoint (2026-09-20)

Normal grid realization now invokes the public Avalonia-shaped cell Realize
override with factory, selection, original ICell and indexes. Existing Uno
overrides remain functional; specialized control setup is retained. A stack/value
context captures the actual row model before user code can retarget Core's
flyweight row. No per-recycle callback allocation or deferred work was added.

Direct standalone realization borrows the model and owns only its observation
adapter. Unrealization/failure releases that adapter without disposing the model.
Added EndEdit and the native selection-interaction query/event contract. Core
selection implements sorted row/cell mapping using its existing notifications.
Optional native input hook signatures do not by themselves provide complete
custom interaction or standalone input parity.

Registered standalone native checks plus grid override/recycling/flyweight checks
and one selection unit case are authored, UNRUN. Public row/presenter/base
standalone contracts remain open. No build/test/launch/package/benchmark/push/CI
was performed.

### Column contract prerequisite for row/presenter parity (2026-09-20)

Added the reference IColumns and ColumnListBase<TColumn> layout surfaces using
native framework geometry types and the existing Core notifying collection.
Presentation.Columns and row Columns now expose that contract over the same
native view entries. Renderer-only access remains a typed view of the same list.
Projection Reset notifications follow selection-index updates; disposal continues
through a throwing collection notification to release the view columns.

The existing native width solver is retained. Its completed viewport/constraint
state is adopted by the public collection, and public LayoutInvalidated drives
the native update path. Weak collection-owned column listeners replace the
reference's Avalonia weak-event helper without a global tracker or per-cell work.
Duplicate entries share one listener; rejected reentrant mutations do not alter
listener ownership.

Five unit cases and a native contract-to-Core-to-geometry width case are authored,
UNRUN. Standalone rows/cells presenters, the view-row contract and generic base
customization remain implementation work. No build/test/launch/package/benchmark/
push/CI was performed.

### View-row contract prerequisite (2026-09-20)

Added ITreeDataGridRows and a per-presentation facade returning actual Core IRow
objects, enumeration and index mappings. It adds public cell realization/release
and forwards the existing presentation notifications, including sort resets.
There is no copied row list, per-row wrapper or additional Core event observer.

The earlier exact public collection-identity assertion was Uno-specific and is
superseded: the common Avalonia API uses a view facade too. Tests now preserve the
stronger architectural distinction explicitly: Model.Rows is the original Core
collection, and facade indexing returns original IRow objects/models. Public
row/cells-presenter property types were updated to the compatible UI contract.

Public cells are caller-owned and disposed by UnrealizeCell; custom adapters
return the original UI model without a second observation adapter/registry.
Native grid cells keep their existing pool. New public creation is rejected when
suspended/disposed; a factory retiring its view before returning triggers cleanup.
GetRowAt follows the reference's known-origin/otherwise-unknown contract, leaving
precise measured geometry to the presenter.

Five unit cases plus a native public-row realization/writeback/borrowed-model
cleanup case are authored, UNRUN. Standalone row/presenter implementation remains
open. No build/test/launch/package/benchmark/push/CI was performed.

### Standalone row/cells-presenter realization (2026-09-20, implementation only)

Added the reference-shaped row Realize(factory, selection, columns, rows, index)
entry point and cells-presenter Items/Rows/ElementFactory dependency properties,
Realize, Unrealize and UpdateRowIndex. Independently hosted containers use the
existing IColumns/ITreeDataGridRows contracts, original Core row/model objects,
compatible cell realization hooks and element factory. No surrogate grid/source
or second collection of data rows is created. Native grid realization keeps its
existing pooling and geometry path; standalone viewport/focus handlers are
attached only when that path is first used.

The standalone path observes active collections, measures/commits public column
widths, follows the effective horizontal viewport and retains focused cells.
Unknown column anchors use the reference width estimator rather than realizing
all columns. Its bounded parent-local pool retains collapsed controls. Same-pass
layout recycling uses BeginRebind/EndRebind synchronously; explicit standalone
Unrealize releases models/content immediately, retaining empty containers only.
No dispatcher closure is introduced. Original row models are captured before
application callbacks can retarget Core's flat-row flyweight. Cell model cleanup
uses its original owning rows even after property replacement. Retirement during
factory/realization callbacks abandons publication and releases the borrowed model.

A native fixture is authored and registered for row/presenter API use, Core model
identity under flyweight retargeting, writeback, row selection, parent retention,
horizontal viewport, index changes, direct presenter configuration, and reentrant
retirement. It is UNRUN. No build/test/app/package/benchmark/push/CI was performed.

This supersedes the earlier missing standalone-realization entry-point finding,
not the entire presenter parity finding. Generic TreeDataGridPresenterBase and
TreeDataGridColumnarPresenterBase inheritance/customization contracts remain open.
The new presenter properties configure the standalone path; independently
overriding Items/Rows on a grid-owned presenter is not yet supported. Standalone
selection initialization is available, but grid-independent input/selection-event
routing and automatic parent-removal lifecycle still need reference comparison.

### Reference presenter bases (2026-09-20, implementation only)

Ported TreeDataGridPresenterBase<TItem>, TreeDataGridColumnarPresenterBase<TItem>
and RealizedStackElements from the current Avalonia reference rather than adding
another independent virtualization algorithm. The shared stack preserves the
reference's variable-size estimates, stable-range anchoring, insertion/removal/
movement index remapping and pooled scratch storage. The base carries the factory,
realize/unrealize/index, measurement/arrangement, viewport and two-pass layout hooks.

Native adaptations are explicit: Panel.Children replaces Avalonia's separately
managed visual/logical children; native dependency properties replace direct
properties; Loaded/Unloaded replace attachment callbacks; StartBringIntoView and
native anchor providers replace framework scrolling calls. Native Measure handles
valid-constraint caching; a presenter-owned constraint map supplies the two-pass
comparison because Avalonia's measure-validity API is not public in WinUI. Entries
are removed on recycling. Hidden children remain parented with a bounded budget,
and changing the element factory retires its old hidden containers. Tab traversal
uses realized display order. No dispatcher-per-recycle work is introduced.

Columnar custom presenters use IColumns for measurement and commits, initialize
committed widths before measuring, and subscribe to layout invalidation with
unload/reload cleanup. A native fixture derives vertical and horizontal presenters
and covers hook dispatch, variable-height viewport realization, collection index
changes, retained parents, bring-into-view, unload/reload and public width changes.
The fixture is registered but UNRUN; no compilation or runtime parity is claimed.

**Integration is still required:** the built-in rows, cells and headers presenters
currently retain their specialized Uno paths and do not yet derive from these
bases. Connect them without losing their Core-cell ownership, bounded pools or
retirement guards, then remove superseded standalone layout code. The reference
base's handling of application callbacks that replace collections during an
in-progress layout also needs integration review before it is used by the grid.
This checkpoint supplies the common implementation, not proof of full presenter
compatibility. No build/test/app/package/benchmark/push/CI was performed.

### Built-in header presenter adoption (2026-09-20, implementation only)

TreeDataGridColumnHeadersPresenter now derives from the common
TreeDataGridColumnarPresenterBase<IColumn>. Its former independent header layout
and pool loop was replaced by the reference realization engine. Realize/index/
unrealize/measure/arrange overrides are actual execution hooks, including for a
standalone Items/ElementFactory-configured header presenter. Grid-owned headers
continue to use the grid's committed geometry and constrained width solver, with
the same body/header viewport. Existing typed lookup and focused-header inclusion
remain available. The retained factory pool is bounded (64); ordinary reuse does
not detach its hidden children.

Headers expose public Realize(IColumns, index), UpdateColumnIndex and Unrealize.
They observe the original UI column's header/sort/resize/template metadata and
unsubscribe on release. Resize writes go through IColumns rather than assuming a
native CellColumn. Native owner sorting and header styles remain connected.
Standalone header-presenter accessibility no longer requires an owning grid.

The common engine now retires an in-progress layout when collection/factory
callbacks change its generation, deferring reset until the pass exits. Its
existing constraint map tracks in-flight controls too, so cleanup includes models
not yet published in a realized range. Cleanup continues across failures, and
measure/arrange failures retain cleanup exceptions. Focused-element reuse removes
the retained-focus listener; intra-container focus changes do not orphan it.

Extended native checks cover the built-in header subclass hooks, direct hosting,
live metadata, release/observation cleanup and source replacement during header
realization and generic measurement. They remain UNRUN. Rows and cells still need
common-base integration and removal of superseded standalone layout. No build,
test, app, package, benchmark, push or CI was performed.

### Built-in cells presenter adoption (2026-09-20, implementation only)

TreeDataGridCellsPresenter now derives from the shared columnar base. Its native
and independently hosted paths use the same reference realization/layout engine;
the superseded TreeDataGridCellsPresenter.Standalone.cs was removed. Public
Items/ElementFactory are inherited, Rows remains a native dependency property,
and realization/index/measurement/arrangement overrides execute for actual cells.
Lookup, ordered traversal and row-index updates remain connected to live cells.

The common base exposes narrow recycling hooks so cells retain their existing
column-keyed control pool and grid-wide 256 reservation budget (64 for independent
presenters). It does not put cells into a second factory pool. Ownership records
capture the original rows, UI cell, native presentation/value, factory and Core row
model before callbacks. Custom retained-model reuse, native suspended-model reuse,
legacy cell factories and specialized controls remain connected. Cleanup releases
through the original owner even after Rows or source replacement. Deferred
content is finalized synchronously, without per-recycle dispatcher closures.

Two native layout boundaries are explicit. Controls are parented before
realization so Uno resource/template lookup can find grid resources. Natural-width
cell measurement is not capped by the previous committed row extent, because the
existing native rows presenter constrains row measure to that extent; otherwise
Auto columns could not grow. Native Auto-valued column bounds likewise do not cap
their prerequisite natural measurement using the previous computed bound.
Committed geometry still controls grid-owned cell
arrangement. Core flyweight row models are captured separately from the row wrapper
and supplied through the normal specialized/compatible cell realization hooks.

Native fixture additions cover actual cells-presenter subclass hooks, original
Core models, parent-preserving row reuse and Rows replacement during realization.
Existing binding/template/lifetime/retained-custom-model and grid-wide pool checks
remain required. All checks are UNRUN. Rows-presenter adoption is still open,
as are final lifecycle/API review and all validation gates. No build/test/app/
package/benchmark/push/CI was performed.

### Built-in rows presenter adoption (superseding implementation checkpoint)

The rows presenter now inherits the common Core.IRow presenter base. Public
Items/ElementFactory/Columns configuration and real subclass lifecycle/layout
hooks are connected; the previous independent Uno realization/remapping loop is
removed. Items supplies the single collection-change subscription, replacing the
grid's additional forwarding path. Row initialization is shared between native
and standalone hosting, capturing the Core model before application callbacks.

The native sparse height/anchor geometry, explicit body viewport, committed
column widths, 32-row parented pool and 256-cell reservation budget are retained
through overrides. A first column-width batch collects natural measurements;
subsequent final measurement commits widths immediately. Standalone rows use the
public column/row contracts and receive horizontal viewport changes for their
cells. Cell budget ownership is separate from native input/presentation ownership,
so standalone row presenters share the budget without routing input to a foreign
grid view. Source replacement releases old cell/view references; ordinary
viewport recycling retains the existing parents and synchronous rebind path.

GenericPresenterRuntimeChecks now includes built-in row hooks, insert/move index
preservation, original Core row/cell models, sparse-height arrangement,
parent-preserving scroll recycling, pool bounds, bring-into-view and replacement
during realization. All UNRUN. Earlier checkpoints saying rows adoption remains
are superseded; remaining lifecycle/API/theme review and every validation gate
below are still required. No build/test/app/package/benchmark/push/CI was run.

### Presentation contract and selection-interaction dispatch (implementation checkpoint)

Source review found that the public selection-interaction interface's input hooks
were not used by the native grid, and several reference presentation members were
absent. Added the reference source/state/row-selection/sort/move/property-change/
sorted/interaction surface with direct Core forwarding. Native DataPackageOperation
replaces Avalonia drag effects, and indexes are the shared Core type. Existing
presentation source observation supplies notifications; no extra Core observer
or copied selection is introduced.

The grid now dispatches preview/down/up keys, pointer events and supported native
character events through the presentation's interaction. The default bridge uses
the existing native selection logic. Custom queries drive realized row/cell
highlights, including live interaction replacement. Editing gestures remain
available without a selection model. Interaction observers are disconnected on
replacement/unload and reconnected on load; nested-grid events remain isolated.

Four unit checks cover public Core identity/selection, sorting notification,
native move effects and interaction availability/lifetime. A registered native
fixture covers custom row/cell visuals, replacement, retired notifications and
unload/reload observer counts. All UNRUN; the latter is not OS-input evidence.
Native input execution, the separate Skia text-input capability decision, remaining
parity review and all validation gates are open. No build/test/app/package/
benchmark/push/CI was run for this checkpoint.

### Validation sequence (after remaining implementation)

1. Solution, package and all platform-head builds, then independent package
   consumers with a fresh validation package version and matching symbols.
2. Core/binding/geometry/selection/editing/declarative API tests and native
   lifetime/retention/failure regressions, including handlers that change sources.
3. All sample scenarios through real pointer/keyboard input, drag/drop, resize,
   editing, selection, source changes and theme switches; include Light/Dark,
   high-contrast where supported, scaling, RTL and custom header/cell/row themes.
4. Native automation inspection/actions and hidden-control exclusion.
5. Benchmarks/allocation measurements against the relevant Avalonia and prior
   Uno baselines, separately from correctness assertions and screenshot evidence.
6. Full diff review and local findings report; implement findings and rerun the
   affected validation before calling PR #26 ready. No release or merge is implied.
