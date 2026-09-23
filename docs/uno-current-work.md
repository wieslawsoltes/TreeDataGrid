# Current Uno completion checklist

Updated 2026-09-23 UTC. Tested product **9a57ca17**.
**Complete API, all-feature and performance parity remain unproven.**

[Hierarchy and typed-column implementation](uno-hierarchy-columnlist-2026-09-23.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-9a57ca17.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-9a57ca17.md)

## Architecture and revision

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
The actual TreeDataGrid.Core assembly remains shared with Avalonia. Sources,
rows, hierarchy and selection are not copied into a second model layer.
No dependency upgrade, global rendering change, public release or merge occurred.

Starting commit: `0ccd3c9e4192a9febc109d3f8ba6adb633cdf69c`.
Tested product: `9a57ca17a34d10f86234625709b6dba9065fc2b6`.
Tested tree: `dc6d77269bd7644db5d16aaf679ec3a0a83df8dc`.
Tested merge: `dbc30daef6d53914d1ddbf8152480dc319bda4ab`.
Later documentation-only changes do not modify this tested implementation.

## Completed functional execution

[Functional run 35911127297](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35911127297),
job `107351123303`, artifact `10773337333`, completed successfully against an
unchanged checkout. All results below belong to this final product, not a union
of earlier runs:

| Gate | Result |
| --- | --- |
| Core / Uno / Avalonia / sample-state units | 210 / 507 / 536 / 36 passed |
| Total units | **1,289 passed; zero failed/skipped** |
| Registered native suites | **50/50 passed** |
| Sequential showcase / measurement recovery | Passed |
| Both native sample builds | Zero warnings/errors |
| Activity Monitor | Five sections and lifetime checks passed |
| API dependency resolution / strict self-diff | Fully resolved / no self-differences |

[Platform run 35911127119](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35911127119)
separately runs Windows/Linux/macOS desktop jobs, Linux native package consumers,
Windows App SDK consumers and published trimmed browser consumers. Its exact
completion state is recorded in the machine-readable checkpoint; pending browser
publication/execution is not counted as a pass. Pinned Chromium coverage is not
universal browser, physical-input, screen-reader or DPI acceptance.

## New implementation

Expander subscriptions now retain the original observed model and retire state
before callbacks. Every owned cleanup stage is attempted independently, including
inner-cell disposal after failed event removal. Initialization and child-source
transactions finish application event accessors before physically unsubscribing;
late adds cannot silently reattach disposed values. Revision checks reject older
child sources after nested updates or disposal, while explicit HasChildren stays
lazy. Permission and visibility getters recheck retirement after application code.
Factory/configuration failures preserve both primary and cleanup errors and release
the inner exactly once. Arbitrary external accessors that never actually remove
a handler still report a failure; they are not falsely guaranteed to unsubscribe.

HasChildren descriptors are now shared per hierarchical column through weak keys,
removing repeated per-cell descriptor creation and Core getter compilation. Each
row still owns its own binding and observers. Cached notification arguments avoid
per-event allocation. Focused warm descriptor, notification and permission-query
loops pass zero-allocation tests; whole-grid timing is a separate failed gate.

Public `Uno.Controls.Models.TreeDataGrid.ColumnList<TModel>` reuses native
`ColumnListBase<ICellColumn<TModel>>`. Built-in/custom columns, precise collection
notifications, actual Core row identity, Pixel/Auto/Star sizing and caller ownership
are covered. The change from Avalonia's combined IColumn<T> to ICellColumn<T> is
an explicit shared-Core interface mapping, not a literal ABI-equivalence claim.

This continuation adds **30 unit cases and one native suite with seven public
consumer scenarios**. The latter also runs in the sequential consumer path.
Two intermediate compile mistakes were corrected; no assertions or diagnostics
were relaxed to turn those failures into passes. See the implementation report.

## Still-open API and performance acceptance

The compiled inventory is 1,748 baseline / 1,601 target declarations, 866 exact
normalized matches and 882 missing-or-different baseline entries; no dependencies
are unresolved. These are not feature-completion percentages. Core relocations,
native signatures, inherited members, generated exports and remaining actual
omissions still require explicit compatibility decisions and compilation tests.

[Paired run 35911127341](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35911127341),
artifact `10772732869`, built both hosts and completed all AB/BA processes with
valid frames, but failed the unchanged **1.10 median timing/allocation budget**.

| Workload | Avalonia median ms | Uno median ms | Uno/Avalonia |
| --- | ---: | ---: | ---: |
| Horizontal scroll | 0.47460 | 2.58465 | 5.446 |
| Vertical scroll | 1.03645 | 2.68325 | 2.589 |
| Distant diagonal scroll | 2.52325 | 10.13375 | 4.016 |
| Replace visible row | 1.99035 | 3.39730 | 1.707 |
| Resize visible column | 3.68910 | 4.53460 | 1.229 |
| Sort | 40.89315 | 56.89695 | 1.391 |

Raw p95/allocations/settlement data remain in the artifact. These are same-runner
framework measurements, not controlled revision comparisons against earlier jobs.
They measure synchronous UI work and settlement, not GPU completion or frame rate.
The standard flat-grid workload does not directly measure HasChildren descriptor
construction. No overall-grid speedup or performance parity is claimed.

Required before ready: genuine API completion, unchanged native timing/allocation
budgets, broader hierarchy/variable-height/mixed-mutation benchmarks, repeated
multi-head runtime validation, positive physical keyboard/pointer/drag, Unicode/IME,
screen-reader and DPI coverage. The current passing fixtures do not close these.

All authored implementation changes are pushed. Local execution returned
ClientError, so unknown unrelated local files could not be inspected or certified
as pushed. No test assertion, Core ownership rule or performance threshold changed.

```sh
dotnet test tests/TreeDataGrid.Uno.Tests/TreeDataGrid.Uno.Tests.csproj \
  -c Release -p:TreeDataGridUnoTargetFrameworks=net10.0
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite hierarchy-ownership
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```
