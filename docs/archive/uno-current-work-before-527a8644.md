# Current Uno completion checklist

Updated 2026-09-24 UTC. Tested product **f7a1cc33**.
**Full API, all-feature behavior and performance parity are not established.**

[Ownership/pool audit and measured comparison](uno-cell-pool-review-2026-09-24.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-f7a1cc33.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-f7a1cc33.md) ·
[Custom value-column guide](uno-custom-value-columns.md)

## Shared architecture and exact source

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
Actual TreeDataGrid.Core sources, rows, hierarchy and selection remain shared with
Avalonia. This continuation changes native presentation ownership/pooling only;
no Core/Avalonia product source, dependency, renderer-global configuration, merge,
ready transition or public release changed.

Starting head: `3eb3470aa96337fa31af45bb8d85fa35ca9a0366`.
Tested product: `f7a1cc3361acd9b1420bc22e48e05f32613053a5`.
Product tree: `a2f0a824c7d194b76ad4b041b8af65f682d2ec9e`.
Tested merge input: `7496ee233da2deaf52c2fa19244297617bbb02f0`.
Documentation-only commits do not change this implementation.

## Completed functional and platform validation

[Run 35988541535](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35988541535),
job `107596847415`, completed all fifteen required stages with an unchanged checkout.
Artifact `10803601014` contains complete API inventories, source fingerprints,
TRX and native runtime logs. The linked audit also records the earlier failed
fixture build and its correction; that failed run is not acceptance evidence.

| Gate | Result |
| --- | --- |
| Core / Uno / Avalonia / sample-state cases | 228 / 594 / 536 / 41 passed |
| Direct framework comparisons | 68 passed |
| **Total .NET cases** | **1,467; zero failed/skipped** |
| Registered native suites | **57/57 passed** |
| Sequential showcase and measurement recovery | Passed |
| Both native sample builds | Zero warnings/errors |
| Activity Monitor | Five sections and lifetime checks passed |
| Python audit tests | 12 passed |
| Compiled metadata semantic / normalization checks | 39 / 57 passed |

[Platform run 35988541603](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35988541603)
is **fully successful**, verified after its final `2026-09-24T10:51:10Z` update.
Windows/Linux/macOS desktop jobs, native Linux package-consumer execution, Windows
App SDK sample/package-consumer builds and publication, browser builds and trimmed
publication all pass. Browser job `107596847247` completed actual execution of
both published consumers and browser-dispatched input at device scales **1 and 2**.
These are pinned Chromium tests, not physical hardware, all browsers, IME,
external screen-reader or universal DPI acceptance. Documentation was pushed only
after the complete product platform run finished, avoiding cancellation of its
browser acceptance.

Repository Build `35988541667`, reproducibility `35988541631` and the published,
executed trimmed-binding contract `35988541572` also passed.

## Implemented in this continuation

- Internal visible-column ownership uses reference-keyed cached membership, not
  application Equals/GetHashCode or a per-cell whole-column scan. Mutation-time
  queries use current storage; all mutation and exception paths invalidate the
  cache. Public collection semantics and existing event reentrancy stay intact.
- Realization checks presentation version and indexed column identity across
  factory, retarget, configure and suspension callbacks. Retired owned results
  are released exactly once, with primary and cleanup failures preserved. Borrowed
  retained cells still leave disposal with their caller. Rejected reuse reacquires
  Core's mutable anonymous row wrapper before fallback creation.
- Empty model-pool buckets are removed immediately instead of consuming the
  32-column budget forever. Existing empty-stack storage is reused. Limits remain
  256 values, 32 nonempty column buckets and 16 spare empty stacks; model/source
  cleanup is not deferred and cache capacity was not enlarged.
- Public native/sequential `presentation-pool` checks verify four factory-retirement
  modes, repeated reuse/writeback across 128 columns, exact model subscriptions,
  wide-grid navigation/rendered text and final source cleanup. The same suite
  executes in the published consumer route.

New coverage: **27 Uno unit cases and one native suite**. Warmed 4,096-iteration
membership and recycle/realize tests allocate zero managed bytes on their measuring
thread. Initial fixture compilation failed because TryGetCell returns Control;
the corrected checked native-cell cast retains every assertion. Failed run
`35988003981` is not counted as acceptance.

## Performance remains a failed acceptance gate

The read-only [same-runner comparison](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35987997845)
compares exact baseline `3eb3470a` then candidate `c0ab40c1`, each with two AB/BA
framework pairs. Horizontal, vertical and diagonal medians improve in that run;
row replacement, resizing and sorting regress. All allocation medians are unchanged.
Revision-order/machine-state effects remain possible. **No overall speedup or
performance equivalence is established.** Artifact `10803381115` retains all data.

The independent [paired acceptance run](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35988541552),
artifact `10802734576`, completes both builds and all four host processes with valid
frames, but still fails the unchanged **1.10 median timing/allocation budget**:

| Workload | Avalonia median ms | Uno median ms | Uno/Avalonia |
| --- | ---: | ---: | ---: |
| Horizontal scroll | 0.50090 | 2.49935 | 4.99 |
| Vertical scroll | 1.22260 | 2.38050 | 1.95 |
| Distant diagonal scroll | 2.36940 | 9.04295 | 3.82 |
| Replace visible row | 1.83730 | 3.79350 | 2.06 |
| Resize visible column | 3.63930 | 6.26390 | 1.72 |
| Sort | 40.83475 | 58.94830 | 1.44 |

Scope is synchronous UI work and verified layout settlement, not GPU completion
or frame rate. Sorting allocates less than Avalonia but remains slower. A timing
comparison across different hosted-runner jobs is not a controlled revision test.

## API and remaining acceptance

No exported declaration count changed: 1,845 baseline / 1,828 target declarations,
1,009 exact normalized matches, 836 raw missing-or-different baseline entries,
819 additional-or-different entries, zero unresolved metadata and complete strict
self-comparison. Supplemental inherited/interface/attribute differences remain
explicit. The empty missing-owner review category is not proof that Core relocation
candidates or native signatures are equivalent. Counts are not feature percentages.

Required before ready: complete genuine public member/signature/inheritance and
attribute equivalence, explicit tested Core mappings, broader callback failure and
mixed-mutation review, unchanged paired native timing/allocation budgets with
hierarchy/variable-height workloads, and physical input/drag, IME, external
screen-reader and scaling coverage across heads. Existing automated passes do not
waive those requirements.

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite presentation-pool
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

All authored implementation is pushed. Local shell/Python execution returned
ClientError, so unknown unrelated working-tree changes could not be inspected.
No assertion, trimming diagnostic, Core ownership rule, rendering quality setting
or performance threshold was weakened.
