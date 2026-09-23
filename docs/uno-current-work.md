# Current Uno completion checklist

Updated 2026-09-23 UTC. Tested checkpoint **3664bf02**.
**Full API, all-feature and performance parity are not yet certified.**

[Latest implementation, measurements and evidence](uno-extent-drag-validation-2026-09-23.md) ·
[Machine-readable checkpoint](uno-ci-checkpoint-3664bf02.json) ·
[Previous checklist, preserved unchanged](archive/uno-current-work-before-3664bf02.md)

## Architecture and ownership

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
The actual `TreeDataGrid.Core` assembly is shared with Avalonia. Sources, rows,
hierarchy, selection and ownership are not copied into a second model layer.
No merge, ready transition or public package release was performed.

The tested source is `3664bf02eef78355f662161bb2b0683ac6b3260c`, tree
`7c81a692a19b2624dedd0b34e689ca5407bb7ec1`, validated as merge
`c0222f3d08a253247c0b06f6fab56842975cc249`. Documentation-only commits do
not change this implementation. This continuation started at `49b02656` and
preserved its already-completed width, expander, binding and browser fixes.

All changes authored in this continuation were pushed directly through GitHub.
Local shell/Python execution was unavailable; unknown local working-tree files
could not be enumerated or certified. CI ran the unchanged committed sources.

## Completed functional checkpoint

[Functional run 35863339084](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35863339084),
job `107188668656`, artifact `10751253656`:

| Gate | Result |
| --- | --- |
| Core / Uno / Avalonia / sample-state tests | 210 / 408 / 536 / 36 |
| Total unit cases | **1,190 passed, zero failed/skipped** |
| Registered native suites | **43/43 passed** |
| Sequential native showcase and native recovery | Passed |
| Both desktop samples | Zero warnings/errors |
| Activity Monitor | Five sections and lifetime checks passed |
| API metadata resolution / strict self-comparison | Fully resolved / zero self-differences |

[Platform run 35863339064](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35863339064)
has passed the three desktop jobs, Linux native/package consumers and Windows
App SDK sample/package consumers. Browser builds and package creation pass; the
final browser publication/runtime job was still in progress when recorded.
Do not infer its completion from an earlier revision's successful browser run.
Repository Build and the independent published trimmed-binding contract pass.

## New implementation in this continuation

- Hosted cell extents reuse the parent's already committed width, instead of
  scanning every column for an estimate that would immediately be discarded.
  Column identity/cardinality guards preserve the public standalone/custom
  estimator fallback. A native 2/1,024-column regression verifies no estimator
  calls on the hosted path, geometry updates, mismatch fallback and zero warmed
  query allocation through public package-consumer APIs.
- Public `Models.TreeDataGrid.DragInfo` exposes borrowed shared Core source/path
  data and native DataPackageView lookup through the existing weak token registry.
  Native data-transfer types are deliberately adapted, not declared ABI-identical
  to Avalonia. A public snapshot never registers a live drag automatically.
- Native drag validation now captures ownership before source lookups and rejects
  retirement after each application-controlled indexer/child selector. Cancellation
  cannot read cleared model lists or shorten a live-list loop into false success.
  Nine unit cases cover reentrancy, identity, exceptions, disposal, collectability,
  lazy snapshots and allocation-free warm validation.
- The `drag-info` native suite verifies real native package handling for unrelated,
  malformed, empty, unknown and copied tokens without inventing a live operation.
  Physical drag/input and positive OS operation interaction remain separate gates.
- A read-only same-runner investigation records before/after measurements and
  separate managed traces. It establishes diagnostic evidence, not a parity pass.

This adds **nine unit cases and two registered native suites**. The initial
extent fixture's access-to-internals compile failure was corrected using public
APIs; no friend access or relaxed checks were introduced.

## Performance remains failed

The same-runner extent experiment is mixed: vertical and row-replacement medians
improved modestly, while horizontal, diagonal, resize and sort medians increased.
It does **not establish an overall grid speedup**. The redundant estimator work
is demonstrably removed, but that is not substituted for timing acceptance.

[Paired run 35863339075](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35863339075),
artifact `10751073587`, completes both builds and all AB/BA host measurements but
fails the unchanged **1.10 median time/allocation budget**. Latest time ratios:
6.41x horizontal, 2.70x vertical, 3.94x diagonal, 1.20x row replacement, 1.13x resize
and 2.18x sort. See raw medians/p95, revision-order limitations and separate
profiling scope in the detailed report. Profiled timings are not acceptance data.

Profiles identify composition damage/drawing, resource finalization, row-reset
visibility, measurement and text shaping as further investigation paths. They
include waits and non-UI threads, so their percentages are not exclusive CPU cost.
No global native rendering switches or horizontal visibility assertions changed.

## Remaining API and cross-platform acceptance

The compiled inventory has 1,748 baseline and 1,599 target declarations, 865 exact
normalized matches and 883 missing-or-different baseline shapes, with zero
unresolved types. These are not feature-completion percentages. Core relocations,
native types, inherited contracts, generated exports and true omissions require
explicit, tested equivalence decisions. `completeApiParityProven` remains false.

Required before ready: complete genuine public-contract work; meet the unchanged
native budget with broader variable-height/mixed-mutation workloads; extend real
browser and physical keyboard/pointer, Unicode/IME, drag/drop, screen-reader and
DPI coverage; and establish repeated multi-head lifecycle/render reliability.

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite committed-extent --suite drag-info
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

No test assertion, trimming diagnostic or performance threshold was weakened.
Later completed artifacts supersede this dated checkpoint; pending jobs are not passes.
