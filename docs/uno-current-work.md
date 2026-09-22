# Current Uno completion checklist

Updated 2026-09-22. Supersedes the older 989-test / 30-of-31-suite checkpoint.
**Complete API, functional and performance parity is not yet certified.**

The [implementation report](uno-geometry-observable-validation-2026-09-22.md)
describes this continuation. The subsequent
[final CI checkpoint](uno-ci-checkpoint-5ed67958.json) records completed workflow
outcomes and supersedes the report's earlier browser-upload-in-progress observation.

## Branch, architecture and revision

PR #26 remains draft on `codex/uno-core-port`, based on master
`3ca47316d724e5e040ab0281a880e8df999b25fc`. Changes are committed directly to the
PR branch. Nothing was merged or released publicly.

`TreeDataGrid.Controls.Uno` uses the actual `TreeDataGrid.Core` assembly shared with
Avalonia. Source, hierarchy, rows and selection remain Core objects; native binding,
presentation, recycling, input and accessibility remain in Uno.

Tested product: **`5ed67958522f0e254bd6a0d5557154d48f802eac`**.
CI merge input: **`e0f760d48bf6d222abfa0f21748dc36faa1ea293`**.
The later documentation-only commit does not change product sources.

## Completed CI

| Workflow | Run | Result |
| --- | --- | --- |
| Uno multi-platform builds and package consumers | 35783715226 | Success |
| Repository Build | 35783715283 | Success |
| Committed-source functional/API inventory validation | 35783715258 | Success |
| Real trimmed binding consumer publication and execution | 35783715249 | Success |
| Dependency snapshot | 35783715284 | Success |
| Paired native performance | 35783715321 | **Failure: ratio budget exceeded** |

The Uno workflow completed all three desktop build/test jobs, Linux native runtime
and package-consumer checks, Windows App SDK sample builds/packing/package-consumer
publication, and both browser sample builds/packing/package-consumer publication.
Those build/publication successes do not prove browser runtime or physical-input
behavior.

[Functional run 35783715258](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35783715258),
job `106935256126`, artifact `10718624091`, reports all twelve exit codes zero,
with no checkout modification:

| Gate | Result |
| --- | --- |
| Shared Core | 210 passed; zero failures/skips |
| Uno | 281 passed; zero failures/skips |
| Avalonia | 520 passed; zero failures/skips |
| Sample state | 36 passed; zero failures/skips |
| Total unit cases | **1,047 passed** |
| Both native sample builds | Passed; zero warnings/errors |
| Sequential showcase | Passed, including post-exception sizing/cache tests |
| Independently hosted native suites | **33/33 passed** |
| Bare native measurement-recovery probe | Passed |
| Activity Monitor | All five sections and lifetime checks passed |
| Metadata dependencies | Zero unresolved baseline/target types |
| Strict identical-assembly API self-comparison | Zero differences |

## Implemented in this continuation

Uniform RowGeometry queries now use O(1) arithmetic, and uniform structural mutations
allocate no row storage. Sparse measured rows retain the Fenwick path. Small-range
invalidation avoids whole-measurement scans and key snapshots. Exact floating
boundaries, int.MaxValue counts, overflow rollback and zero warmed allocations are
tested. The initial captured-LINQ allocation was corrected without relaxing its test.

Observable TextCell<T>/CheckBoxCell rejected writes now roll back only unsuperseded
proposals. Same-input retry invokes validation again. Source normalization, successful
nested writes and newer notification values survive reentrancy. Failed EndEdit cannot
resurrect a disposed/cancelled edit; the existing Value/Text/writer order is retained.

Whole-row recycling avoids redundant local child visibility changes inside balanced
deferred rebinds. Horizontal-only/standalone recycling still hides normally. Native
checks cover exact retained identity, zero child collapse on replacement/sort,
normal horizontal collapse, bounded realization and full source cleanup.

The metadata audit now classifies every raw difference using documentation identities,
declaring types and overload names. Candidates are suggestions, not approved
compatibility mappings. Six deterministic classifier tests pass. A concrete omission
identified by that report, TreeDataGridCheckBoxCellAutomationPeer, is now ported and
created by native checkbox controls. Its typed Owner, toggle cycle, read-only/disabled
rejection and retired-provider contracts pass native assertions.

The new suites add 30 unit cases relative to starting head `161eda63`: ten geometry,
fourteen observable-write and six API-classification cases. There is also a new native
row-recycling suite and expanded automation assertions.

## Previously reported blockers

This continuation started from `161eda63`, newer than the previous visible report.
Intervening changes had already updated Uno.Sdk to 6.7.30, fixed native measurement
recovery/sequential integration, and completed browser package publishing plus
explicit trimmed binding contracts. These are inherited fixes, not newly implemented
here. Current runs reconfirm their exercised behavior; old reports remain historical.

Existing retained row/cell/template identity, cross-column reuse, standalone/grid
variable bring-into-view, declarative null recovery, themes, cache resizing, custom
factories, editing, selection/lifetime and wide-grid tests continue to pass.

## API acceptance remains open

Current compiled surface: baseline 1,748; target 1,544; exact namespace-normalized
matches 830; missing-or-different baseline entries 918; additional-or-different
entries 714. These are NOT feature-completion percentages.

| Raw-difference category | Count |
| --- | ---: |
| Changed declaration at the same documentation identity | 193 |
| Exported type not found at that identity | 63 |
| Member not declared on a matched type | 107 |
| Member of an absent exported type | 428 |
| Overload/native-parameter identity difference | 127 |

Relocated Core contracts, generated/binding Avalonia exports, inherited members,
native type differences and actual omissions require explicit review. The checkbox
peer reduces absent identities from 64 to 63 and raw differences from 921 to 918.
Inventory execution/self-comparison is not strict cross-framework API acceptance;
completeApiParityProven remains false.

## Performance remains below acceptance

[Paired run 35783715321](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35783715321),
job `106935256027`, artifact `10719391863`, built and executed both hosts on one
machine with two AB/BA pairs, 64 columns and 25 iterations. The unchanged **1.10
median time/allocation ratio budget fails**.

| Workload | Avalonia median ms | Uno median ms | Uno / Avalonia |
| --- | ---: | ---: | ---: |
| Horizontal scroll | 0.63865 | 3.01625 | 4.72 |
| Vertical scroll | 1.59780 | 4.21040 | 2.64 |
| Distant diagonal scroll | 2.98110 | 36.21515 | 12.15 |
| Replace visible row | 2.57530 | 4.53425 | 1.76 |
| Resize visible column | 4.98270 | 6.76810 | 1.36 |
| Sort | 39.07605 | 66.75290 | 1.71 |

Sorting allocates less than Avalonia but remains slower. The same-runner visibility
experiment reduced some allocation/visibility operations, not overall timing; some
medians increased. Its complete before/after table is in the report. Different
hosted runner/revision timings are not controlled speedups. Sampled profiles include
waits/startup. Acceptance measures synchronous UI work/verified settlement, not
GPU completion, frame rate or all-feature performance.

## Remaining acceptance

Complete actual public contracts and tested Core/native equivalence decisions;
close native timing/allocation gaps; execute browser runtime automation and physical
pointer/keyboard/Unicode/IME/drag-drop/screen-reader/DPI coverage; expand mixed-mutation
and variable-height performance. Finite suite success does not certify every feature.

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite row-recycling-visibility --suite automation
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

Permanent workflows preserve committed inputs, failures, hashes, TRX, native logs
and metrics. Temporary candidate mutation workflows were removed after manual
promotion. No assertion, trimming diagnostic or performance threshold was weakened.
Later completed artifacts supersede this checkpoint; running/cancelled work never
counts as successful validation.
