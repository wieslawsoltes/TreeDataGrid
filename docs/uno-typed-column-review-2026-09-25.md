# Typed column dispatch, projection and initial geometry review

Review date: 2026-09-25 UTC. Tested implementation: `f8ff3b7c`.
Starting head for this continuation: `01ecd1f5`.
[Typed column guide](uno-typed-column-contract.md) ·
[Execution checkpoint](uno-ci-checkpoint-f8ff3b7c.json) ·
[Current checklist](uno-current-work.md)

## Defects corrected

The pending typed-column change made `ICellColumn<TModel>` inherit the new native
`IColumn<TModel>`. A custom subclass reimplementing the legacy interface thereby
reimplemented the typed base interface as well. Its public factory could replace
the intended explicit legacy factory. The original regression fixture deliberately
throws from that public override; its assertion remains intact and now passes.

The interfaces are now independent. The library's native and compatibility bases
implement both and route typed creation through the actual `ICellColumn` factory
slot. Existing untyped virtual overrides remain honored. Comparison forwarding is
separate: built-in/custom virtual policies remain in force, and legacy-only
factories without a comparison return null. Core sorting is not replaced by a
native view's comparison delegate.

The independent interfaces require an explicit read-only typed projection for
`ColumnList<TModel>`. Typed columns retain their object identity. Legacy-only
factories receive one lazy, non-owning facade per list/cache entry, reused for
duplicate entries. The mutable factory list and native `IColumns` path retain the
original object. Facades neither subscribe on their own nor dispose columns.
Caller-added handlers are forwarded unchanged, including the original sender.
A weak-key/ephemeron cache prevents a removed column/facade pair from rooting itself.

The first implementation exposed a further variant-interface mapping defect:
`((IColumns)list)[0]` could select the typed read-only slot and return a facade,
losing the original layout object. An explicit untyped interface map at the same
inheritance level fixes indexer/enumerator dispatch. The failing identity test was
not relaxed. Native, typed, factory and non-generic collection routes are now
independently exercised, together with mutation detection.

Native built-in pixel columns also returned NaN for `ActualWidth` before first
layout, producing a zero-width estimate for an 80+50-pixel list. `ValueCellColumn`
now exposes its configured pixel width until a width is committed. Committed
constraints, including zero, remain authoritative. Auto/star remain unmeasured;
no fake natural measurement is recorded and no Core definition is mutated.
The original 130-pixel estimate assertion now passes unchanged.

## Reference comparison correction

The nine pending comparison-policy cases had compared Auto's raw numeric payload:
Avalonia uses zero and WinUI uses one although Auto ignores that payload. The
comparison now asserts every unit flag and the unit enum, and still requires exact
numeric pixel/star values. Nine new cross-framework cases cover all three built-in
column kinds with Auto, pixel and star widths, including initial ActualWidth.
This is an explicit native-unit mapping, not a global normalization exception or
blanket waiver of raw API differences. No native renderer setting, Core ownership
rule, trimming diagnostic or performance budget was changed.

## Authored coverage and preserved work

This continuation adds **14 Uno unit cases** (12 typed-projection cases and two
interface-identity cases), **nine direct-framework cases**, and one native legacy
factory/projection scenario inside the existing `builtin-column-comparison` suite.
No additional registered native suite is claimed.

Coverage includes raw and typed identities, legacy explicit factories on native
and compatibility subclasses, comparison identity, forwarded sender/handler
identity, duplicate subscriptions, caller-owned disposal, factory exceptions,
mutation detection, weak-cache collection, warmed zero-allocation indexing, and
initial/committed geometry. The new native scenario verifies two independently
created cell values over the exact same Core row, explicit factory counts, duplicate
facade identity and complete subscription cleanup. Its enclosing scenario checks
real rendering, editor commit, sorting and distant virtualization.

The previous continuation's typed API, twelve Uno cases, eleven direct-framework
cases and the incoming `01ecd1f5` automation callback work are preserved and
revalidated, not counted again as authored work here. The offline-development
workflow was also preserved; no local compilation is claimed because the local
execution tools returned ClientError. Compilation and runtime evidence come from
GitHub Actions on the unchanged committed tree.

## Completed functional evidence

[Run 36143814796](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36143814796),
job `108099839407`, passes all fifteen stages:
**228 Core + 810 Uno + 536 Avalonia + 41 sample-state + 171 differential = 1,786
.NET cases**, with zero failed or skipped; **65/65 native suites**; sequential native
execution; both native sample builds with zero warnings/errors; and all five
Activity Monitor sections plus lifetime checks. Python audit/metadata-semantic/
normalization checks remain 12/39/57. Artifact `10868642526` retains full evidence.

The implementation tree is `b0941c6d337049b09bee2532fc43ad8ac5f0880a`.
CI merge `9c5e85e1da07e76f6cf7253e3b55739c27c90675` has the same tree.
The final execution checkpoint records platform completion and distinguishes
build/package publication from actual runtime execution.

## Independent performance evidence

[Run 36143814751](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36143814751)
completed both builds and all four alternating AB/BA hosts. Its unchanged **1.10**
median timing/allocation budget remains failed. These measurements are not a
controlled before/after test of the typed-column changes.

| Workload | Avalonia median ms | Uno median ms | Time ratio | Allocation ratio |
| --- | ---: | ---: | ---: | ---: |
| Horizontal scroll | 0.53155 | 2.70585 | 5.090 | 1.063 |
| Vertical scroll | 1.28570 | 2.72845 | 2.122 | 1.957 |
| Distant diagonal scroll | 2.64200 | 9.40840 | 3.561 | 1.928 |
| Replace visible row | 2.59405 | 3.92030 | 1.511 | 2.668 |
| Resize visible column | 4.84590 | 8.28055 | 1.709 | 1.516 |
| Sort | 43.68275 | 71.76715 | 1.643 | 0.620 |

Artifact `10868586955` preserves raw allocations, p95 and settlement measurements.
The scope is synchronous UI-thread source/layout work and verified settlement,
not GPU completion, frame rate, physical input, variable-height or full-feature
parity. A focused allocation-free typed lookup does not replace this failed gate.

## Remaining acceptance

The unchanged auditor reports **1,845 baseline / 1,839 target declarations,
1,011 exact normalized matches, 834 missing-or-different baseline and 828
additional-or-different target entries**. Dependencies resolve; self-comparison is
clean. Supplemental metadata remains fully preserved: 13,465 / 15,787 entries,
3,838 exact, 9,627 missing-or-different and 11,949 additional-or-different. Counts
are not completion percentages or automatically accepted Core/native equivalences.

Genuine remaining member/signature/inheritance/attribute differences, broader
custom-source/mixed-callback review, the unchanged performance budget with
hierarchy/variable-height workloads, and physical input/drag, Unicode/IME,
external accessibility and cross-head lifecycle/scaling acceptance remain open.
PR #26 remains draft. This checkpoint does not mark the entire port complete.
