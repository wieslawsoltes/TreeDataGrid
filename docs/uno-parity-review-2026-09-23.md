# Uno parity audit: declarations, behavior and platform evidence

Date: 2026-09-23 UTC. Repository `wieslawsoltes/TreeDataGrid`, PR #26.

**This is exhaustive accounting of the supplied compiled declaration inventory,
plus targeted source and differential behavior review. It is not certification
of every execution path, inherited framework API, platform or performance parity.**
The exact completed validation states and raw counts are recorded in
`uno-ci-checkpoint-90d5df7a.json`. The full generated review is preserved in its
CI artifact under `parity-review/review.json` and `parity-review/review.md`.

## Revision and architecture

This continuation starts at `6f12ba1df4291665013ce90dee708e2dde8298cd`.
Final product: `90d5df7a505af3dfadeb7883cbc346a6024cb597`.
Final product tree: `34cf203865564290d84472ff3bbca90f914b4c97`.
The original baseline remains master `3ca47316d724e5e040ab0281a880e8df999b25fc`.
The actual `TreeDataGrid.Core` assembly is shared by both views. No source, row,
hierarchy or selection implementation was copied into a second neutral model.
The PR remains draft; there is no merge, public release or dependency upgrade.

| Commit | Implementation |
| --- | --- |
| `3be25df7` | Exhaustive declaration accounting, source fingerprints and audit tests |
| `43f6218c` | Mutable native text-column options and culture-aware cell reuse |
| `791d0519` | Protected custom presentation selection hooks connected to the grid |
| `d3c4da90` | Real native and published-consumer contract fixtures |
| `1fdc1baa` | Direct comparison of both framework libraries; public fixture corrections |
| `2c531355` | Raw-value search parity, allocation tests and real committed-text search |
| `90d5df7a` | Correct the previous search oracle; restore non-menu row automation metadata |

Local shell and Python execution returned ClientError. All source changes were
made through GitHub and validated on committed, unchanged CI checkouts. Unknown
local working-tree files could not be enumerated; all changes authored here are
committed. No local-test or local-performance result is claimed.

## 1. Audit methodology and coverage

The existing Roslyn auditor inventories declared public/protected APIs in the
actual compiled `TreeDataGrid.Avalonia`, `TreeDataGrid.Controls.Uno` and shared Core
assemblies. The new `build/audit-uno-parity.py` consumes those inventories without
changing normalization, replacing their baseline or hiding any difference.

Every baseline declaration receives a record containing its raw declaration,
normalized declaration, declaring type, original classification/candidates, and
any same-name/same-arity Core owner candidates. Every additional target declaration
is retained too. Per-owner totals, assembly input hashes and the tested Git revision
make the accounting reproducible.

The review fails on lost/invented/duplicate differences, unresolved dependencies,
unknown candidates, malformed entries or totals that disagree with the inventories.
Twelve Python tests verify these accounting guarantees. Even a same-name Core
candidate remains `equivalenceAccepted=false`; a possible relocation is not an
accepted signature or behavioral equivalence.

A tracked-source inventory records SHA-256 and line counts for every C#, XAML,
AXAML and project file under Core, Avalonia and Uno. A complete textual scan lists
TODO/FIXME, NotImplementedException and PlatformNotSupportedException boundaries.
These markers are review inputs, not automatic defects. This scan is not a claim
that every source line has received semantic verification.

The generated categories distinguish exact declared shapes, changed declarations
at the same identity, undeclared members on matched types, overload/parameter
identity differences, missing exported owners, generated exports and shared-Core
candidates. The companion checkpoint records the final counts without converting
them into a feature-completion percentage.

### What is not proved by declaration matching

The supplied Roslyn inventory does not expand every inherited external framework
member or compare every custom attribute. A matching signature also does not prove
property defaults, lifecycle ordering, side effects, event payloads, native input,
rendering or performance. The new differential and runtime suites therefore run
as separate required gates; the original performance gate remains independent.

## 2. Behavior corrections found by the audit

### Mutable text-column options

Avalonia's text cell reads its mutable options object when queried. Uno previously
captured a permanent construction-time TextCellOptions snapshot. Changing format,
culture, alignment, wrapping, trimming or search policy after realization therefore
left existing cells using obsolete options.

The native TextColumn facade now retains the live options and shares one immutable
render snapshot per unchanged configuration. Its cells read current style, text,
edit gestures and parsing culture while preserving the actual Core column/binding
identity. Reuse across rows does not replace the options contract or accept cells
from unrelated definitions. Culture comparison uses object identity, not an
application-defined equality callback. No per-query or per-cell snapshot allocation
is introduced on the unchanged path.

The reference options object has no property-change event. This correction does
not invent automatic visual notifications: the current values are consumed on
query or the next normal model/render refresh. The native fixture changes a model
property, verifies the same cell instance renders the new French format, edits via
the actual native TextBox using the updated parsing culture, then checks teardown
and borrowed-source ownership.

### Search is distinct from display formatting

Avalonia TextColumn's internal search selector is
`ValueSelector(model)?.ToString()`. The Uno implementation previously applied
display StringFormat/Culture to this value, so adding a display prefix could change
which rows committed-text search found.

The native text facade now searches its raw selected value. An explicitly supplied
search selector still takes precedence. Display formatting remains unchanged,
including nullable/unformatted behavior and custom formatting callbacks. Default
search no longer evaluates a display formatter that may be expensive or throw.

The pre-existing Uno test `Text_Column_Uses_The_Presentation_Format_And_Culture`
encoded the divergent behavior and failed after the implementation correction.
It was not deleted or skipped. It now asserts the reference raw search value and,
separately, the exact previous Polish display string `Amount 12,50`. Four new
regressions cover formatter isolation, nulls, explicit selectors and string identity.
The direct differential tests execute Avalonia's public ValueSelector rather than
using inaccessible internal SelectValue members. The native consumer additionally
searches a `V={0:F2}` decorated column by the raw value `4` through committed-text
input. This is application text routing, not hardware keyboard/IME certification.

### Custom presentation selection hooks

The audited protected `RaiseNativeSelectionChanged` and
`RaiseNativeCellSelectionChanged` hooks were absent. They are now implemented and
connected to the real grid SelectionChanged event, not added as unused signatures.
The hooks preserve Core model/index identity, capture lazy deltas before publication,
and reject retired or superseded captures. Cell deltas do not fabricate selected
row-item payloads. Unobserved or inactive hooks do not enumerate their arguments.

These are explicit extension hooks, not another Core event subscription. Existing
built-in notifications still publish once. Grid forwarding rechecks presentation
ownership after refreshing selection, so a callback cannot deliver a retired delta
into its replacement. Tests cover observer removal, suspension/resume, disposal,
nested publication, exception recovery and actual custom presentation consumers.

### Row automation metadata

Restored public `TreeDataGridRowAutomationPeer.ShowsMenu`, matching the original
constant-false contract. Row expansion describes hierarchy, not a menu. The actual
native automation suite checks it on realized, expanded and retired rows while
retaining all existing role, provider, selection, disabled-state and stale-provider
assertions. This does not claim external screen-reader acceptance.

## 3. Direct framework contract execution

`tests/TreeDataGrid.Parity.Tests` references both actual view libraries and their
one shared Core assembly. It does not embed an alternate implementation of either
framework. Fourteen cases compare defaults, null/empty/Unicode text, formatting in
three cultures, mutable style options, shared-model property observation/writeback,
retained-cell retargeting, and Pixel/Auto/Star measurement plus exact column edges.

The raw selector comparison uses Avalonia's actual public ValueSelector and Uno's
public GetSearchText. The protected/internal Avalonia text-search implementation
was inspected separately; its private method is not called through reflection or
new friend-assembly access. This bounded suite is direct behavior evidence, not a
complete cross-framework UI comparison.

Seven mutable-options, nine selection-hook and four search-value Uno cases add
20 product-unit regressions. The fourteen differential cases make **34 new .NET
test cases**, plus twelve separate Python accounting tests and two new native
suites. The expanded automation assertions are part of the existing suite.

Focused warmed checks verify 4,096 unchanged option/text queries, raw string search
queries and unobserved cell-selection hooks allocate zero managed bytes on the
measuring thread. These do not establish an overall-grid timing improvement.

## 4. Implementation boundaries and remaining compatibility review

The source boundary scan confirmed several unsupported operations also exist in
the reference: inverse indent conversion, Star minimum/maximum constraints in the
custom column base, and unimplemented stable-key methods on the items-source view.
Their presence is documented rather than automatically reported as a new port bug.
A match to an unsupported baseline is not a promise of broader functionality.

Eight exported owner families currently lack even a same-name/same-arity Core
candidate in the accounting: ExpressionChainVisitor<T>, LightweightObservableBase<T>,
TypedBindingExpression<TModel,TValue>, TypedBinding<TModel>, TypedBinding<TModel,TValue>,
ColumnBase<TModel,TValue>, ITreeDataGridColumnSelectionModel and
TreeDataGridColumnSelectionModel. These include Avalonia-specific binding and
selection extension APIs; their native replacements or compatibility contracts
still require implementation/review. The accounting does not quietly waive them.

Other raw differences include Core model/index/row/selection relocations, native
WinUI property/event/input/automation signatures, inherited members and generated
Avalonia XAML exports. A same-name Core type is not sufficient proof of equivalent
semantics. A member absent from one declaring type may be inherited and callable;
adding a redundant forwarding member solely to improve a count is not completion.
The JSON keeps all candidates and raw declarations for explicit review.

Still required: remaining genuine public-contract implementations and tested
substitutions; inherited/attribute contract review; broader callback/lifetime and
mixed-mutation behavior; external keyboard/pointer and positive drag/drop;
Unicode/IME, screen readers, scaling/DPI and repeated multi-head reliability.
Current native/browser fixtures cannot certify those unrelated paths.

## 5. Validation and performance integrity

The Linux validator now requires fifteen successful stages, adding the direct
framework tests and the two accounting stages to the previous functional workflow.
It retains failure output, per-suite process results, input revisions and untouched
checkout verification. Native and browser samples run the new public consumer
fixtures through their ordinary sequential validation route.

CI exposed and retained initial fixture compile failures: native cell lookup is
Control-typed; WinUI and Core both name IndexPath; and interface SelectValue is
internal. The fixes use public APIs, an explicit Core alias and the public Avalonia
selector. No new internal-access permissions or reflection-preservation bypasses
were introduced. The earlier search expectation was corrected with the source and
executed oracle described above, not by accepting arbitrary output.

The independent paired benchmark keeps the **1.10 median time/allocation ratio
budget**. Its exact final measurements and status are in the companion checkpoint.
The workload measures synchronous UI-thread source/layout work and verified
settlement, not GPU completion, frame rate, every variable-height operation or
physical input. Different hosted runs are not controlled revision comparisons.
No whole-grid speedup is inferred from focused allocation tests. Performance
shortfalls remain failures, regardless of declaration or functional success.

## Reproduction

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/test-uno-parity-audit.py
python3 build/audit-uno-parity.py
python3 build/run-uno-native-suites.py --suite mutable-text-options --suite presentation-selection-hooks
dotnet test tests/TreeDataGrid.Parity.Tests/TreeDataGrid.Parity.Tests.csproj \
  -c Release -p:TreeDataGridUnoTargetFrameworks=net10.0
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

No test is skipped to obtain a parity result. Performance thresholds, trimming
checks, global rendering settings and Core ownership rules are unchanged. Consult
the exact checkpoint rather than promoting an earlier or incomplete run to a
later revision's acceptance.
