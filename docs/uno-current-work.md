# Current Uno completion checklist

Updated 2026-09-24 UTC. Tested product **db48f25d**.
**Full API, all-feature behavior and performance parity remain unproven.**

[Latest implementation and audit](uno-value-column-validation-2026-09-24.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-db48f25d.json) ·
[Custom value-column guide](uno-custom-value-columns.md) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-db48f25d.md)

## Architecture and revision

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
Actual TreeDataGrid.Core sources, rows, hierarchy and selection remain shared with
Avalonia. There is no duplicate model layer, dependency upgrade, merge or public
release in this continuation.

Starting head: `a342fcf21f7ec09e1ff75aa5a9309b425bbdf843`.
Tested product: `db48f25d463e0fa5cfe15aea8f4f6ae313374f1b`.
Product tree: `749e23f1ebbcb52b1e49ebb235e6fa976169b73e`.
Tested merge: `1ebdc024c879db0584f7b9ec01a6e56025a37cf1`.
Subsequent documentation-only commits do not modify the tested implementation.

The starting branch already contained typed bindings, column selection, richer
metadata auditing, Core fixes and browser input tests at two scales. Those were
preserved/revalidated, not counted as authored in this continuation.

## Completed functional validation

[Run 35976852444](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35976852444),
job `107559341254`, artifact `10798418106`, passes all fifteen stages on unchanged
sources:

| Gate | Passed |
| --- | ---: |
| Core / Uno / Avalonia / sample-state tests | 228 / 567 / 536 / 41 |
| Direct framework comparisons | 68 |
| Total .NET cases | **1,440; zero failed/skipped** |
| Registered native suites | **56/56** |
| Python audit tests | 12 |
| Compiled metadata semantic / normalization checks | 39 / 57 |

Sequential showcase/recovery, both native sample builds, and all five Activity
Monitor sections/lifetime checks pass. Native sample builds have zero warnings/errors.
[Platform run 35976852724](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35976852724)
is tracked separately in the exact checkpoint, including published browser execution;
build/publication is never substituted for runtime acceptance.

## New implementation

- `ColumnBase<TModel>` and `ColumnBase<TModel,TValue>` reuse the native layout and
  typed-binding engines. The live typed/base options views share one owner;
  comparers are cached with reference construction-time policy; explicit selectors
  and binding descriptors remain distinct; cells receive actual Core rows.
- Text/checkbox cells accept typed BindingValue observables and separate writers
  without a new Rx dependency. Existing raw-value constructors remain. Cells own
  subscriptions, not caller subjects/models/sources. Unset, DoNothing, null,
  fallback/diagnostic recovery and nested updates have explicit tested behavior.
- The new native custom-column fixture exercises real editor/checkbox writeback,
  nested owner changes, row replacement, header/width propagation, distant/sorted
  rendering, bounded realization and cleanup. It also runs sequentially in native
  and trimmed browser package consumers.
- Public-cell notifications reuse immutable event arguments. Identity formatting
  avoids a string copy without bypassing custom culture formatters. Typed fallback
  diagnostics reject stale results after local writes, nested publication or OnError.
  Warmed 4,096-write text/checkbox tests allocate zero managed bytes, with exactly
  12,288 / 8,192 notifications respectively.

Authored coverage: **15 Uno unit cases, 22 direct framework cases and one native
suite**. Existing native facades retain their ValueCellColumn inheritance and
recycling paths; full built-in inheritance/ABI equality is not claimed.

## Audit result and remaining contracts

All **1,845 baseline shapes** are accounted for. There are 1,828 target shapes,
1,009 exact normalized matches, **836 raw missing-or-different** baseline entries
and 819 extra/different target entries, with zero unresolved metadata. The enhanced
baseline differs in scope from the old 1,748-shape audit; do not compare those raw
counts as a feature-completion percentage.

The review's unmatched exported-owner family category is now empty after the
value-column port. Raw absent identities remain Core/generator candidates, not
automatically accepted equivalences. The remaining categories are 354 Core
candidates, 213 changed declarations, 119 members not declared on matched types,
142 overload/parameter differences and eight generated exports. Supplemental
inherited/attribute metadata differences are retained in the full report.
`completeApiParityProven` remains false.

## Performance still fails

[Paired run 35976852509](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35976852509),
artifact `10798516362`, completes both builds and all four AB/BA processes with
valid frames but fails the unchanged **1.10 median timing/allocation budget**.
Time ratios: **6.01x horizontal, 2.95x vertical, 4.32x diagonal, 1.60x row replacement,
1.81x resize, 1.60x sort**. Sorting allocates less but remains slower. Raw medians,
p95 and settlement data remain in the artifact and implementation report.

These are synchronous UI/settlement results, not GPU completion or frame rate.
Focused public-cell allocation results do not prove a full-grid speedup; the standard
benchmark does not directly isolate the newly ported custom typed-cell path.
No controlled overall-grid improvement or performance parity is claimed here.

## Required before ready

Complete genuine signature/member/inheritance/attribute equivalence work and
explicitly tested Core mappings; meet unchanged performance budgets with broader
hierarchy/variable-height/mixed workloads; extend physical keyboard/pointer/drag,
Unicode/IME, screen-reader, scaling and repeated multi-head acceptance. Empty owner
review and passing fixtures do not close those remaining requirements.

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite value-column-base
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

All authored changes are pushed. Local execution returned ClientError, so unrelated
unknown local working-tree changes could not be enumerated. No test assertion,
trimming diagnostic, Core ownership rule or performance threshold was weakened.
