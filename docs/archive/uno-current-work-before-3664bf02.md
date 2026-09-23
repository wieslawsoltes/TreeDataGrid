# Current Uno completion checklist

Updated 2026-09-23 UTC. Product checkpoint **9e03ce28**.
**Full API, all-feature and performance parity are not yet certified.**

[Detailed implementation and evidence](uno-width-expander-validation-2026-09-23.md) ·
[Machine-readable checkpoint](uno-ci-checkpoint-9e03ce28.json) ·
[Previous checklist, preserved unchanged](archive/uno-current-work-before-9e03ce28.md)

## Architecture and source ownership

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
The actual `TreeDataGrid.Core` assembly is shared with Avalonia. Sources, rows,
hierarchy, selection and ownership are not copied to another model layer.
No merge or public package release was performed.

Tested product: `9e03ce28f082580bab71df633046622844d0ce58`.
Tested merge: `ca0c590f1756c3e022d992d180e00f3857cd36b0`.
Product tree: `ed5d18b28986fe2a1ef4ad5a57dad42e4ebbac72`.
The local staged source tree was verified equal to that GitHub tree. All identified
workspace code changes are pushed; SDK/package caches and diagnostic artifacts are
not repository source. Subsequent documentation-only commits do not change the tests.

## Completed acceptance at this revision

[Functional run 35854319565](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35854319565),
artifact `10747056977`, records zero failed/skipped unit cases:

| Gate | Result |
| --- | --- |
| Core / Uno / Avalonia / sample-state tests | 210 / 399 / 536 / 36 |
| Total unit cases | **1,181 passed** |
| Registered native suites | **41/41 passed** |
| Sequential native showcase and measurement recovery | Passed |
| Both desktop samples | Zero warnings/errors |
| Activity Monitor | Five sections and lifetime checks passed |
| Compiled API dependencies / strict self-comparison | Fully resolved / passed |

[Platform run 35854319634](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35854319634)
is fully successful: three-OS desktop builds/tests; Linux native package consumers;
Windows App SDK samples/package consumers; browser sample builds, trimmed publication,
and **execution of both published consumers in Chromium**. This is more than a publish
check, but it is not all-browser/physical-input/accessibility parity. The independent
published trimmed-binding contract and repository Build also pass.

## Latest implementation

- Width calculation captures each column/constraint once, solves on numeric-only
  stack or pooled storage and commits caller output only after success. Sixteen
  regressions include reentrancy and failure atomicity; eleven failed before the fix.
- Public `ExpanderCell<TModel>` uses a borrowed real Core row, observable expansion
  and visibility, owned content/subscriptions, error propagation and complete cleanup.
  Eight unit tests and a native/browser runtime suite cover controller writeback,
  editing, retained identity and disposal. Framework binding types are explicitly
  adapted to the established observable contract, not declared signature-identical.
- Cached nested binding owner expressions compile on JIT-capable hosts; interpreter
  fallback is retained for browser/AOT. Two tests reduce warmed nested property/indexer
  retarget allocations from 152/312 bytes per call to zero on the validated JIT host.
- The private wrapping-test `Text` endpoint is precisely preserved for trimming.
  The browser now passes the unchanged variable-height and sequential assertions.
- A read-only reference-pack snapshot supports reproducing the actual net8 Core
  offline. No source feeds, SDK targets, trimming policies or thresholds were changed.

This continuation adds **26 unit cases and one native suite** to the starting branch.
The previously proposed `ColumnGeometry.Commit` snapshot fix was already present at
that starting head; it was preserved rather than overwritten or counted as new work.

## Performance and API boundaries

The isolated same-process width-solver experiment shows constrained layouts at about
**5x faster**, with zero warmed allocations. Nested-binding retarget allocation also
improves. These are focused measurements, not overall native-grid acceptance.

[Paired native run 35854319556](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35854319556),
artifact `10746788587`, completed both hosts and four AB/BA processes but **failed the
unchanged 1.10 median time/allocation budget**. Latest time ratios are 4.95x horizontal,
2.39x vertical, 3.97x distant diagonal, 1.39x row replacement, 1.79x column resizing,
and 1.56x sorting. See raw metrics and qualifications in the detailed report.

The compiled inventory has 1,748 baseline and 1,592 target declarations, 864 exact
normalized matches and 884 missing-or-different baseline shapes. These are not
feature-completion percentages. Core relocations, native types, inherited contracts,
generated Avalonia exports and real omissions still require explicit classification.

## Required before ready

- Complete genuine public-contract omissions and tested Core/native equivalence.
- Meet the unchanged native timing/allocation budget; expand variable-height and
  mixed-mutation workloads. Do not substitute profiled timings for acceptance.
- Extend runtime coverage beyond the current Chromium consumers, including physical
  keyboard/pointer, Unicode/IME, drag/drop, screen readers and DPI across heads.
- Establish repeated multi-head lifecycle/render reliability before marking ready.

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite public-expander --suite row-sizing
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

No assertion, trimming diagnostic or performance threshold was weakened. Later
completed artifacts supersede this dated checkpoint; pending jobs are not passes.
