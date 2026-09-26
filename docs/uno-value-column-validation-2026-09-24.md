# Native value-column and typed-cell parity checkpoint

Date: 2026-09-24 UTC. PR #26, `codex/uno-core-port`.
**Complete API, all-feature behavior and performance parity are not established.**

[Custom value-column migration guide](uno-custom-value-columns.md) ·
[Exact machine-readable checkpoint](uno-ci-checkpoint-db48f25d.json)

## Revision and ownership

This continuation starts at `a342fcf21f7ec09e1ff75aa5a9309b425bbdf843`, not the
older documented `89202b5a` head. Typed bindings, column selection, richer API
metadata analysis, additional Core corrections and browser-dispatched input at
two device scales were already present. They were preserved and revalidated,
not counted as newly authored work here.

| Commit | Authored implementation |
| --- | --- |
| `1fb1c36ba3e590d029da20ffac6b14f00b01e7ab` | Custom value-column extension bases and 15 reference comparisons |
| `3be58adc4ad60eda15d70621416400408f664cb3` | Typed binding cell constructors and eight unit cases |
| `5fefa03c96566f0989d9543db413d9ef9773f4e6` | Native/sequential custom-column consumer and seven reference comparisons |
| `db48f25d463e0fa5cfe15aea8f4f6ae313374f1b` | Allocation-free public-cell notifications and seven regression cases |

Final product tree: `749e23f1ebbcb52b1e49ebb235e6fa976169b73e`.
Tested merge: `1ebdc024c879db0584f7b9ec01a6e56025a37cf1`.
The subsequent documentation commit does not change this product. The PR remains
draft; no merge, public release, dependency upgrade or global renderer change
was performed. The actual shared TreeDataGrid.Core assembly remains authoritative
for sources, rows, hierarchy and selection.

Local shell/Python execution returned ClientError. Changes were pushed directly
through GitHub and executed on unchanged CI checkouts. Unknown unrelated local
working-tree files could not be inspected or certified as pushed. All code and
documentation authored in this continuation are on the PR branch.

## Custom value-column extension contract

`Models.TreeDataGrid.ColumnBase<TModel>` now derives from the existing native
`Presentation.CellColumnBase<TModel>`. Its typed Options and base options view
forward reads and writes to the same caller-owned sizing policy. No second Core
definition, layout solver, timer or change-notification mechanism is introduced.

`ColumnBase<TModel,TValue>` exposes the reference constructor pattern, public
ValueSelector and Binding, cached ascending/descending comparisons, and protected
CreateBindingExpression. The explicit selector and binding may intentionally
read different values and remain distinct. Sort policy is captured at construction,
while sizing options stay live. Invalid directions return null and null-model
ordering follows the reference comparer. Each cell binding owns independent
observations through the already implemented typed-binding engine.

Fifteen actual Avalonia-versus-Uno cases verify sorting, null inputs, custom
comparers, construction-time policy, live sizing, Pixel/Auto/Star measurement,
same-model observation/writeback, separate selectors/bindings and zero-allocation
warmed policy queries. This is a working derivation contract, not a disconnected
class added merely to change an inventory count.

Existing native built-ins retain their ValueCellColumn hierarchy and their existing
Core/recycling path. The compatibility base is an ICellColumn view over real Core
rows. This does not certify identical built-in inheritance, framework ABI, or every
inherited signature. Actual grid sorting remains the Core definition's policy;
a custom view comparator does not silently overwrite it.

## Typed TextCell and CheckBoxCell constructors

The public cells now accept IObservable<Uno.Data.BindingValue<T>>, with an optional
separate IObserver<BindingValue<T>> writer. A typed expression can be supplied
directly. This adapts the reference subject-based construction without adding
an Rx dependency, replacing raw observable constructors, or duplicating cell state.

Cells own only their subscriptions, not the caller's subject, descriptor, expression,
model or source. Read-only construction requires no writer; writable construction
rejects an observable without writeback before subscribing. Unset/DoNothing keep
the last scalar. Error-only results preserve it; a value-bearing error publishes
the fallback and its diagnostic; normal values recover. Native diagnostic retention
is an explicit extension over the reference cell's value-only observation.

Typed receive and scalar revisions prevent old fallback errors from overwriting
nested publications, local assignments or newer source errors. Retirement checks
reject post-disposal updates. The existing public expression OnNext rejection
policy is preserved; an arbitrary observer is not falsely described as transactional
validation. The explicit throwing DP write path remains a separate contract.

Eight unit cases verify text/checkbox observation, nullable states, ignored values,
diagnostics/recovery, nested publications, separate writers and source ownership.
Seven additional direct two-framework cases cover null/empty/Unicode text,
three-state checkbox values, read-only policy and deterministic unsubscription.

## Real native and published-consumer integration

The new `value-column-base` suite derives custom text and nullable-checkbox
columns using only the public base and typed-cell constructors. Factories attach
them to two actual shared Core definitions with 200 observable rows. Assertions
cover real editor writeback, nested owner replacement, retained native control
identity, three-state checkbox DP updates, whole-row replacement, header and
width propagation, distant navigation, Core sorting, bounded realization and
complete subscription cleanup. Caller-owned Core sources survive view retirement.

The same fixture is called by the sequential showcase used by native and trimmed
browser package consumers, rather than being restricted to an isolated test host.
No test-only friend access or reflection into private framework state was added.

## Public-cell allocation and reentrancy correction

Text and checkbox scalar cells reuse immutable PropertyChangedEventArgs while
preserving Value/Text/Error publication order and existing write/lifetime guards.
The text identity format uses the existing conservative formatting helper. Only
ordinary-culture string identity formatting returns the existing string; derived
CultureInfo formatters, alignment, escaping and format errors retain normal runtime
behavior. No model/value/culture result cache was introduced.

Seven unit cases verify custom formatting, stale fallback diagnostics after local
assignments or nested OnError, and the exact warmed write path:

| Warmed workload | Iterations | Notifications | Managed bytes on measuring thread |
| --- | ---: | ---: | ---: |
| Typed text writes plus identity-format queries | 4,096 | 12,288 | **0** |
| Typed nullable-checkbox writes | 4,096 | 8,192 | **0** |

These are focused allocation contracts. The standard native benchmark primarily
uses built-in value-cell presentations and does not isolate these custom typed-cell
constructors. No overall-grid speedup is inferred from the focused tests.

## Completed functional evidence

[Run 35976852444](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35976852444),
job `107559341254`, completed successfully against the exact product merge above,
with all fifteen validation stages passing and the checkout unchanged.

| Gate | Result |
| --- | ---: |
| Shared Core tests | 228 passed |
| Uno tests | 567 passed |
| Avalonia control tests | 536 passed |
| Sample-state tests | 41 passed |
| Direct framework comparisons | 68 passed |
| **Total .NET cases** | **1,440 passed; zero failed/skipped** |
| Registered native suites | **56/56 passed** |
| Sequential showcase / native measurement recovery | Passed |
| Both native sample builds | Zero warnings/errors |
| Activity Monitor | Five sections and lifetime checks passed |

This continuation adds **15 Uno unit cases, 22 differential cases and one native
suite** over the starting branch. The twelve Python audit tests, 39 compiled-metadata
semantic checks and 57 normalization checks also pass; these audit-tool checks
are reported separately, not added to the .NET test total.

Artifact `10798418106` has SHA-256
`96957ce14c1a8e15a8c232d4387d3fdf6b623431f59852ac79c20504bb5c3910`.
It preserves the detailed audit, TRX, native logs and source inventory. The earlier
`5fefa03c` aggregate was superseded after its validation steps succeeded; that
cancelled aggregate is not used as final acceptance evidence.

Platform [run 35976852724](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35976852724)
separately executes desktop, native/package, Windows App SDK and browser consumers.
Its exact completed state and browser input scope are recorded in the companion
checkpoint, rather than inferring execution from publication. Repository Build,
contract reproducibility and the published/executed trimmed-binding contract also
have separate final-product runs recorded there.

## Compiled audit: missing owner family closed, parity still unproven

The enhanced audit already present on the starting branch includes explicit
interface contracts, generic constraints, attributes and supplemental inherited
metadata. Its 1,845 baseline shapes are a wider scope than the older 1,748-shape
reports; raw totals across those audit versions are not feature-completion deltas.

| Final review category | Baseline shapes |
| --- | ---: |
| Exact normalized declared shape | 1,009 |
| Shared-Core candidate review | 354 |
| Changed declaration at same identity | 213 |
| Member not declared on matched type | 119 |
| Overload/parameter identity difference | 142 |
| Framework-generated export review | 8 |
| Missing exported-owner review without candidate | **0** |
| **Total accounted** | **1,845** |

The target has 1,828 declared shapes, **836 raw missing-or-different baseline
entries** and 819 additional-or-different target entries. No metadata dependency
is unresolved. All raw differences are preserved; the strict self-diff matches
both declared and supplemental inventories exactly.

`UNO_PARITY_MISSING_OWNERS=[]` closes the previous unmatched-owner category after
porting the value-column base. It does NOT prove that every remaining relocated
Core type is compatible or that every inherited/native signature is equivalent.
The 44 raw absent exported identities remain explicit Core/generator candidates.
The supplemental metadata comparison records 13,465 baseline / 15,715 target
entries, 3,824 exact, 9,641 missing-or-different and 11,891 additional-or-different.
Those metadata counts are also not feature percentages or automatically accepted
framework substitutions. `completeApiParityProven` remains false.

## Paired native performance remains failed

[Run 35976852509](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35976852509),
job `107559263201`, built both hosts and completed all four AB/BA processes with
valid measured frames. Sources remained unchanged. The existing **1.10 median
timing/allocation ratio budget fails**.

| Workload | Avalonia median ms | Uno median ms | Uno/Avalonia | Uno median allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| Horizontal scroll | 0.47205 | 2.83610 | 6.008 | 25,768 |
| Vertical scroll | 1.02310 | 3.01850 | 2.950 | 196,608 |
| Distant diagonal scroll | 2.68005 | 11.57715 | 4.320 | 994,480 |
| Replace visible row | 2.50410 | 3.99710 | 1.596 | 102,816 |
| Resize visible column | 4.68030 | 8.48655 | 1.813 | 132,528 |
| Sort | 41.34930 | 66.12735 | 1.599 | 1,062,136 |

Artifact `10798516362` has SHA-256
`72628367b70b70d053c4f188082cf3890b9e20516a789d77cc86346140a8a70a`.
Raw p95, allocation and settlement data are retained. Sorting allocates less
than Avalonia in this workload but remains slower. These are same-runner framework
comparisons, not controlled revision comparisons against earlier hosted machines.
They measure synchronous UI work and verified layout settlement, not GPU completion,
frame rate, all-feature behavior or hardware-input latency. No controlled overall
speedup or complete performance parity is established by this continuation.

## Remaining acceptance

Finish genuine member/signature/inheritance and attribute equivalence work;
validate shared-Core mappings without cloning model state; meet the unchanged
native budget; expand hierarchy/variable-height/mixed-mutation measurements;
and complete broader positive physical keyboard/pointer/drag, Unicode/IME,
external screen-reader, scaling and repeated multi-head lifecycle validation.
The empty missing-owner category does not waive any of those requirements.

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite value-column-base
dotnet test tests/TreeDataGrid.Parity.Tests/TreeDataGrid.Parity.Tests.csproj \
  -c Release -p:TreeDataGridUnoTargetFrameworks=net10.0
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

No test assertion, trimming diagnostic, Core ownership rule, rendering setting
or performance threshold was relaxed. Later completed artifacts supersede this
revision-specific checkpoint; pending and superseded jobs are never passes.
