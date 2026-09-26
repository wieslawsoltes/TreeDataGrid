# Uno geometry, observable cells, recycling and API validation

Date: 2026-09-22. PR #26, `codex/uno-core-port`. No merge or public release.
Complete API, functional and performance parity remains unproven.

## Starting point and final product evidence

This continuation started from `161eda63f3245c54c353ee0d94c0d14b1db5a173`, not
from the older `540e3091` report. Intervening commits updated Uno.Sdk to 6.7.30,
fixed native measurement recovery/sequential integration, completed browser package
publishing and added explicit trimmed binding contracts. Starting-head Uno run
`35770833353`, Build `35770833216`, validation `35770833250` and trimmed-binding
`35770833362` were green. Performance `35770833226` failed the 1.10 budget. Those
are inherited fixes, not changes made by this continuation.

Final product: **`5ed67958522f0e254bd6a0d5557154d48f802eac`**.
Merge input **`e0f760d48bf6d222abfa0f21748dc36faa1ea293`** was validated in
[run 35783715258](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35783715258),
job `106935256126`, artifact `10718624091`. All twelve gate exit codes are zero
and the checkout is unchanged. Execution used GitHub Actions; no local execution
result is claimed for this continuation.

| Gate | Executed result |
| --- | --- |
| Core | 210 passed, no failures/skips |
| Uno | 281 passed, no failures/skips |
| Avalonia | 520 passed, no failures/skips |
| Sample state | 36 passed, no failures/skips |
| Total unit cases | **1,047 passed** |
| Native sample builds | Both passed, zero warnings/errors |
| Sequential showcase | Passed, including post-exception sizing/cache assertions |
| Isolated native suites | **33/33 passed**, including native-layout-recovery |
| Activity Monitor | All five sections and lifetime checks passed |
| Compiled API dependencies | Zero unresolved baseline/target types |
| Strict identical-assembly API self-comparison | Zero differences |

[Trimmed-binding run 35783715249](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35783715249)
publishes and executes the real trimmed consumer successfully.
In [Uno run 35783715226](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35783715226),
all three desktop build/unit jobs, Linux native/package checks and Windows App SDK
sample/package-consumer jobs completed successfully. Browser builds, packing and
package-consumer publication passed; artifact upload/post-job cleanup was still
running at the final observation recorded here. Neither publishing nor programmatic
native fixtures are complete browser-runtime/physical-input verification.

## Row geometry

`7e4bce1e` and `00cdf5a5` update RowGeometry without changing shared Core source,
selection or model identity. Uniform prefix/row queries use O(1) arithmetic with
boundary correction against exactly the multiplication used by Start. Uniform
insert/remove/move needs no row-count-sized storage; measured deviations retain
the sparse Fenwick path. Small invalidations visit indexes, large ranges visit
sparse entries, and full invalidation clears both stores. Last-deviation removal
clears accumulated residuals; overflowing inserts are rejected before mutation.

Ten new cases cover int.MaxValue counts; estimates 0.1, one-third, 28.1, 1e-200 and
1e200; exact/adjacent-double boundaries; sparse survivors; transactional overflow;
invalid ranges; and zero-byte warmed invalidation/uniform mutation. CI found that
captured LINQ display classes allocated before an early return. Explicit loops
after the fast-path check removed the allocation; the assertion was not relaxed.

## Observable cell writeback

`328d4b3f` fixes TextCell<T>/CheckBoxCell caching rejected proposals, which caused
identical-input retries to bypass the writer and falsely accept the edit. Rollback
now restores only an unsuperseded local proposal. A source normalization published
before a writer throws is retained, while equal retry still invokes validation.
Successful nested writes supersede older failed writers. Revision checks protect
application equality, notifications, source updates and writer callback boundaries.

Text buffers survive rejection, cancel displays the accepted value, and failure
cannot resurrect a disposed/cancelled edit. Value -> Text -> writer ordering is
retained. Writer and rollback-notification errors are preserved in AggregateException.
Fourteen new cases cover rejection/retry, normalization, nested writes, cancellation,
disposal, notification ordering, reentrancy and exception identity.

## Row-owned recycling

`65af4775c5638b14bcbe6f10a3d52a16c3c8b88b` avoids redundant local child collapse
inside a balanced whole-row deferred rebind before the parent hides. Unused cells
still finish EndRebind(false) and collapse. Horizontal-only recycling in a visible
row and standalone/generic presenters retain ordinary hiding.

The new row-recycling-visibility suite verifies exact retained row/cell identity,
zero child-collapse callbacks on replacement and ascending/descending sort, actual
collapse on horizontal recycling, bounded realization, and unbound/hidden cells
after source retirement.

Candidate run [35781596118](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35781596118),
job `106928158047`, artifact `10718900686`, identifies exact tested product patches
and blobs over `14aeac63`. It passed 1,041 unit cases, 33 native suites, sequential
integration, both builds and Activity Monitor before manual fast-forward promotion.
The six later classifier tests bring the final total to 1,047. An earlier candidate
failed a fixture GridLength ambiguity and was not promoted. Temporary candidate
scripts/write-capable workflows were removed. Final ordinary CI validates committed
sources, not an in-job patch.

## Specialized checkbox automation peer

`e0ebe7e9` adds TreeDataGridCheckBoxCellAutomationPeer and wires native checkbox
OnCreateAutomationPeer. `5ed67958` fixes the native ToggleState namespace after CI
caught its missing import. The public typed Owner, checkbox role, Toggle/ToggleState
share the existing generic peer's realization, current-source, disabled and read-only
guards, with no duplicate model/selection state or subscriptions.

Expanded automation checks verify specialized type/owner, toggle-pattern identity,
false/true/null cycling, read-only rejection, disabled actions and retired providers.
They pass individually and sequentially. Native exception/routed-event/type semantics
remain explicit review boundaries; this addition is not literal ABI-parity proof.

## API classification

`81ec9b5e` adds normalized documentation identity, declaring type and metadata name
to each compiled entry. Every raw baseline difference is classified; classified
count must equal raw missing count. Same-name relocation/overload candidates are
review suggestions, not approved equivalences. Raw signatures, strict mode and
completeApiParityProven=false remain. Six tests cover identical surfaces, missing
types/members, changed declarations/defaults/accessibility/returns, native parameter
candidates, relocation and deterministic ordering.

The report exposed the genuinely absent specialized checkbox peer, now ported.
After that fix:

| Measure | Count |
| --- | ---: |
| Baseline shapes | 1,748 |
| Target shapes | 1,544 |
| Exact namespace-normalized matches | 830 |
| Missing-or-different baseline entries | 918 |
| Additional-or-different target entries | 714 |
| Changed declaration at the same identity | 193 |
| Absent exported type identity | 63 |
| Member not declared on a matched type | 107 |
| Member of absent exported type | 428 |
| Overload/native-parameter identity difference | 127 |

Counts are not feature percentages. Relocated Core contracts, generated Avalonia
XAML/binding exports, inherited members and actual omissions need explicit review.
Remaining names include CellColumnBase<T>, CellColumnOptions, diagnostics and row-model
contracts alongside neutral contracts now owned by Core. Do not duplicate model or
selection state merely to reproduce old namespace identities. The absent-type count
changed from 64 to 63; raw baseline differences from 921 to 918. Dependencies resolve
fully. A successful inventory/self-check is not a strict cross-framework parity pass.

## Performance evidence

Final paired [run 35783715321](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35783715321),
job `106935256027`, artifact `10719391863`, successfully built/executed both hosts
with matched configuration, checked geometry/model/text identity, two AB/BA pairs,
64 columns and 25 iterations. The unchanged 1.10 median time/allocation budget fails.

| Workload | Avalonia ms | Uno ms | Uno/Avalonia time | Uno bytes |
| --- | ---: | ---: | ---: | ---: |
| Horizontal scroll | 0.63865 | 3.01625 | 4.72 | 29,816 |
| Vertical scroll | 1.59780 | 4.21040 | 2.64 | 212,064 |
| Distant diagonal scroll | 2.98110 | 36.21515 | 12.15 | 1,073,184 |
| Replace visible row | 2.57530 | 4.53425 | 1.76 | 109,696 |
| Resize visible column | 4.98270 | 6.76810 | 1.36 | 138,224 |
| Sort | 39.07605 | 66.75290 | 1.71 | 1,090,392 |

Sorting allocates 0.65 times the Avalonia median but remains slower. Other allocation
ratios exceed the budget. These are synchronous UI/verified-settlement measurements,
not GPU completion, frame rate or all-feature performance.

The earlier visibility experiment also preserved same-runner before/after data:

| Workload | Before Uno ms | After Uno ms | Before bytes | After bytes |
| --- | ---: | ---: | ---: | ---: |
| Distant diagonal scroll | 36.31660 | 37.45850 | 1,080,744 | 1,073,184 |
| Replace visible row | 4.65270 | 4.48800 | 110,200 | 109,696 |
| Resize visible column | 5.90200 | 6.59530 | 138,224 | 138,224 |
| Horizontal scroll | 2.65215 | 2.57465 | 29,816 | 29,816 |
| Vertical scroll | 4.09405 | 4.15610 | 213,576 | 212,064 |
| Sort | 64.74580 | 65.79770 | 1,097,952 | 1,090,392 |

This is a small allocation/visibility reduction, not broadly improved timing; some
medians increased. Both phases failed the budget. Candidate workflow success proved
correctness and complete metric collection, not performance acceptance. Ordinary
paired CI remains strict. Different hosted runner/revision timings are not controlled
speedups. Whole-process profile `35779214450` retains traces/tables/Speedscope data;
visibility/damage-region paths are investigation targets, but residence includes
waits/startup and is not CPU-only attribution. Profiled timings are not acceptance data.

## Remaining acceptance

Complete actual public contracts and tested native/Core equivalence decisions,
close measured timing/allocation gaps, execute browser runtime automation and real
pointer/keyboard/Unicode/IME/drag-drop/screen-reader/DPI coverage, and expand mixed
mutation/variable-height performance. Finite suite success does not certify every
feature/head. No assertion, trimming diagnostic or budget was weakened. The PR is
draft and unmerged; subsequent documentation-only changes do not alter tested code.

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite row-recycling-visibility --suite automation
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```
