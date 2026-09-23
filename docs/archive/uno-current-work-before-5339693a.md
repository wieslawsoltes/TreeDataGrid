# Current Uno completion checklist

Updated 2026-09-23 UTC. Tested product **c6f3936c**.
**Full API, all-feature and performance parity remain unproven.**

[Implementation and executed evidence](uno-row-lifetime-formatting-2026-09-23.md) ·
[Completed machine-readable checkpoint](uno-ci-checkpoint-c6f3936c.json) ·
[Previous checklist, preserved unchanged](archive/uno-current-work-before-c6f3936c.md)

## Architecture and exact revision

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
The actual `TreeDataGrid.Core` assembly is shared with Avalonia. Sources, rows,
hierarchy and selection are not copied into a new view-owned model layer.
No merge, public release, dependency change or relaxed acceptance gate occurred.

Tested product: `c6f3936c33f3d840f850f7908ee05844cc1e8f61`.
Tested tree: `193ca45f48dae266ed43edc7c99037be54865cc0`.
Tested merge: `79bb0a2685b3de87fad14b48508c7d32c0402192`.
Documentation-only commits do not change the tested implementation. This work
starts at `0116268d`; its preexisting column-layout correction is preserved.

All changes authored here are pushed directly through GitHub. Local shell/Python
returned ClientError, so unknown local working-tree files could not be enumerated
or certified. The executed CI checkouts remained unchanged.

## Completed functional and platform validation

[Run 35886616210](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35886616210),
job `107268673539`, artifact `10762967481`, is successful:

| Gate | Result |
| --- | --- |
| Core / Uno / Avalonia / sample-state units | 210 / 450 / 536 / 36 |
| Total unit cases | **1,232 passed, zero failed/skipped** |
| Registered native suites | **45/45 passed** |
| Sequential showcase | Passed, including 17 new row-lifecycle scenarios |
| Native measurement recovery | Passed |
| Both desktop sample builds | Zero warnings/errors |
| Activity Monitor | Five sections and lifetime checks passed |
| Compiled metadata / strict self-comparison | Fully resolved / zero self-differences |

[Platform run 35886616151](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35886616151)
is fully successful. All three desktop jobs, Linux native/package consumers,
Windows App SDK samples/package consumers, browser builds, package creation,
trimmed publication and **execution of both published browser consumers** pass.
Browser job `107268830807` completed its actual runtime step; the overall success
was verified after the run's 16:21:40 UTC update. The sequential browser showcase
includes both new row suites, rather than omitting their callback assertions.
Repository Build and the independent published/executed trimmed-binding contract
also pass. This is pinned Chromium coverage, not every browser or OS input stack.

## Implemented in this continuation

- The pending row-lifetime correction is now applied and tested: invalid index
  rejection, recursive teardown idempotence, guards against reindex/realization
  during retirement and independent cleanup attempts after throwing callbacks.
  A single error preserves identity/dispatch; multiple errors preserve order.
- Realization is reserved before invoking a custom Count getter. Reentrant Count
  cannot publish a row that the outer call overwrites. Validation/lookup failures
  and cancellation release guards, and obsolete reindex work cannot overwrite a
  callback's newer realization. Public native fixtures exercise all boundaries.
- Both row suites run in isolated native processes and the sequential showcase
  used by native and published browser consumers. They contain 11+6 scenarios.
- Exact identity formatting for strings/null avoids unnecessary copies with
  ordinary cultures. Derived CultureInfo custom formatters, non-string formatting,
  escaping, alignment and exception contracts retain the runtime path.
- Bound cells reuse immutable Value/Error notification arguments while preserving
  notification order. Warmed configured formatting and observed retarget loops
  allocate zero managed bytes in the new tests; no model/value/culture cache is added.

This continuation adds **20 unit cases and two native suites** over its 1,212-unit,
43-suite starting branch. The starting commit's separate 22-case column fix is
not counted again. CI caught and then verified the correction of an initial test
factory namespace error; no diagnostics were suppressed.

## Still-failing performance and API acceptance

[Paired run 35886616240](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35886616240),
artifact `10763500194`, completed both hosts and all AB/BA processes but failed
the unchanged **1.10 median time/allocation budget**. Latest synchronous time
ratios are 9.62x horizontal, 3.11x vertical, 5.41x diagonal, 1.67x row replacement,
1.23x column resize and 2.00x sorting. Sorting allocates less but remains slower.
These measurements are not GPU completion/frame-rate data or controlled revision
speedups across different hosted machines. No whole-grid improvement is claimed.

The focused allocation tests and full-grid timing gate measure different paths.
The current fixed-text benchmark does not explicitly configure composite text
options; do not treat its timings as the measured effect of the formatting helper.

The compiled inventory remains 1,748 baseline/1,599 target declarations, 865 exact
normalized matches and 883 missing-or-different baseline entries, with zero
unresolved types. These are not feature-completion percentages. Core relocations,
native types, inheritance, generated exports and true omissions require explicit,
tested compatibility decisions; completeApiParityProven remains false.

Remaining work: genuine API completion; measured native scrolling/rebinding/layout
performance; broader variable-height/mixed-mutation benchmarks; positive physical
input/drag, Unicode/IME, screen-reader and DPI verification; and repeated multi-head
runtime reliability beyond the currently executed browser configuration.

```sh
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite row-lifetime --suite row-construction
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```

No assertion, trimming diagnostic or performance threshold was weakened. Later
completed artifacts supersede this checkpoint; pending runs are not passes.
