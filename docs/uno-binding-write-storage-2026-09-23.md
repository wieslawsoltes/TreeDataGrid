# Uno binding failures, conversion ownership and storage

Date: 2026-09-23 UTC. PR #26, `codex/uno-core-port`.
This is an implementation report, not certification of complete API or performance parity.
The accompanying `uno-ci-checkpoint-5339693a.json` records execution outcomes separately.

## Exact change scope

The continuation starts at `0f1cec1725162e3927bbe4473fbb7a6a841bdccb` and
implements four code/test commits:

| Commit | Change |
| --- | --- |
| `7a82ff9aa0f24b4220c70f74ef546856d91bc185` | Binding diagnostic identity and dual-failure propagation |
| `26fd5351d438ba2b501c3803bf4c40d30752fcc9` | Conversion/write lifetime ownership |
| `6e4b58d08f4c460ae385b52d645179f3e195c844` | Public native/browser consumer regression |
| `5339693abbfd2452b8ab6c34c3a7d48dbd9240f2` | Inline root-owner storage and shared stateless accessors |

Product tree: `505036d6388cc304ecfb30f24d0f2779abfcbdcf`.
CI merge input: `5b163f7a160e0bb03952b69778bcfd88a16e1ca6`.
Later documentation commits do not change this implementation.

All authored changes were pushed directly through the GitHub connection. Local
shell and Python execution returned ClientError, so unknown local working-tree
files could not be inspected or certified as pushed. Execution evidence comes
from unchanged committed CI checkouts, not a local run. No Core model/source layer,
framework dependency, acceptance threshold, rendering option or existing assertion
was replaced. No merge, public release or ready-for-review transition was performed.

## Diagnostic replacement and failure preservation

CellBinding previously compared exception types to detect an Error change. A new
exception of the same type could replace Error without notifying the public
BoundCell, leaving observers with stale diagnostic state. Refresh now compares
exception reference identity. A new instance publishes the existing Value then
Error notifications; reobserving the exact same instance stays quiet. Clearing the
error still notifies even when the scalar value remains null. Comparison does not
invoke an exception's Message, Equals or GetHashCode overrides.

Write still refreshes after every attempted setter, including a failed setter
that partially changes its model. If both the setter and refresh/notification
fail, the result is an AggregateException containing the original setter failure
then the refresh failure. A single failure is propagated unwrapped with its
existing dispatch information. Successful writes allocate no failure collection.

The nine CellBindingFailureContractTests cover public notification order and
published identity, duplicate suppression, recovery, all four setter/observer
failure combinations, uninspectable exception overrides, suspend/dispose during
getters and 4,096 warmed writes with zero measuring-thread managed allocation.
The first commit's complete functional run `35904651490` passed 1,241 unit cases,
all 48 then-registered native suites, sequential integration and Activity Monitor.
It is not substituted for validation of the later commits.

## Conversion cannot transfer a write to a recycled row

BoundCell previously performed object conversion before entering CellBinding.Write.
IConvertible, enum-input ToString and custom formatting callbacks can execute
application code. If that code returned a cell to its real presentation pool and
reused it for a different row, the older conversion result could be written to the
new model. Reusing the same model also requires a new lifetime, not an identity-only
check. A nested assignment without recycling could likewise be overwritten by the
returning older conversion.

Conversion now executes inside CellBinding's write boundary. It captures both
the binding lifetime and a write revision, verifies them after conversion, and
commits only while both remain current. Retarget, Suspend or Dispose rejects the
older result. A newer typed or converted nested assignment supersedes it. Original
conversion failures still propagate, without refreshing or overwriting a newer
binding's state. Invalid/inactive/read-only operations are rejected before invoking
application conversion. Ordinary already-typed and null writes retain a direct path.

The existing conversion policy remains: nullable underlying types, enum parsing,
Convert.ChangeType, and the configured/current culture. No converter registry or
model cache was added. BoundCell was extracted into its own file without changing
its namespace or public CellValue contract. No generated or reflection metadata
was added merely to make the new tests work.

Fourteen BoundCellConversionTests cover different/same-row reuse, suspension,
disposal, newer typed/converted assignments, enum callbacks, exception recovery,
read-only/inactive conversion avoidance, nullable decimal/enum inputs and a warmed
4,096-write allocation invariant. These test names and counts describe authored
coverage; completed execution belongs to the machine-readable checkpoint.

## Public package-consumer integration

BindingWriteContractRuntimeChecks adds seven scenarios using the actual shared
Core source, TreeDataGridPresentation.RealizeCell and RecycleCell APIs. It verifies
that the fixture really reuses the same CellValue while changing its row. The
cases cover different/same-model pooling, suspension/disposal, nested conversion,
public error notifications, dual failures, subsequent recovery and exact subscription
cleanup. The original source remains caller-owned and usable.

The registered `binding-write-contract` native suite is also invoked by the
sequential sample route used by published native and trimmed browser consumers.
The same contract runs in those consumers rather than substituting compile-only
checks. Browser execution, native execution and xUnit cases remain separately
reported; seven runtime scenarios are not counted as seven extra xUnit cases.

## Binding allocation work

Flat delegate bindings previously allocated a one-element accessor array and a
one-element owner array per cell. The root accessor is now shared stateless
storage, and each binding stores its own root owner inline. A simple expression
binding also avoids its singleton owner array. Only additional nested owners
require an array. Expression-specific compiled accessors retain the existing
ConditionalWeakTable lifetime and JIT/AOT policy; no row enters static storage.

Root/child alias deduplication remains reference-based. Moving a nested owner from
the root to another object and back keeps exactly one subscription per distinct
owner. Suspend/Dispose clears the root as well as additional slots. Four storage
cases cover delegate/expression construction budgets, alias transitions and model
collectability while the suspended binding is retained.

The construction budget measures 1,024 warmed root-only bindings, including their
two event delegates, at no more than 256 managed bytes per binding. The output
array, model, column and caller callback are allocated before the measurement.
This is a focused allocation bound, not an exact portable object-size promise or
a claim of zero allocation when constructing a binding.

Combined new coverage: 27 xUnit cases and one registered native suite containing
seven public integration scenarios. Existing allocation and lifetime tests remain.

## Whole-grid acceptance remains separate

Paired run `35906105859`, job `107334088486`, artifact `10770294728` ran both
native hosts with the unchanged 1.10 median timing/allocation budget. Both builds
and all four AB/BA host processes exited zero with valid measured frames. The
budget failed. Artifact SHA-256:
`7082177df149bb8370dcb0792a9ef6c976e729d4291b4c4dd9a30a6249cdffd6`.

| Workload | Avalonia median ms | Uno median ms | Uno/Avalonia | Uno allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| Horizontal scroll | 0.31815 | 2.53460 | 7.967 | 25,768 |
| Vertical scroll | 1.24255 | 2.60940 | 2.100 | 196,608 |
| Distant diagonal scroll | 2.43895 | 10.30420 | 4.225 | 994,480 |
| Replace visible row | 2.45435 | 3.61570 | 1.473 | 102,816 |
| Resize visible column | 4.54060 | 8.08430 | 1.780 | 132,528 |
| Sort | 44.45850 | 66.07985 | 1.486 | 1,062,136 |

These are paired framework comparisons on one machine, not controlled before/after
comparisons with earlier hosted runners. No overall grid speedup is established.
The allocation changes are independently testable but do not replace timing or
allocation-ratio acceptance. Sorting allocates less than Avalonia in this workload
but remains slower. Raw p95 and settlement data are retained in the artifact.
The benchmark measures synchronous UI-thread work and layout settlement, not GPU
completion, frame rate, input latency, variable-height or all-feature parity.

## Remaining acceptance and reproduction

This work corrects binding behavior and internal storage, not exported public API
shapes. Core relocations, native types, inherited contracts, generated exports and
actual omissions still require explicit, tested compatibility decisions. Declaration
difference counts are not feature-completion percentages. Full API equivalence,
remaining native timing/allocation gaps, broader variable-height and mixed-mutation
benchmarks, physical input/drag, Unicode/IME, screen-reader and DPI acceptance are
not certified by these changes.

```sh
dotnet test tests/TreeDataGrid.Uno.Tests/TreeDataGrid.Uno.Tests.csproj \
  -c Release -p:TreeDataGridUnoTargetFrameworks=net10.0
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite binding-write-contract
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

The exact completed outcomes and artifact references are recorded in the companion
checkpoint. Pending/superseded runs are not passes; no test or performance budget
was weakened to change an outcome.
