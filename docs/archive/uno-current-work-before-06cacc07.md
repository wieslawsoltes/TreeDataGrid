# Current Uno completion checklist

Updated 2026-09-25 UTC. Tested implementation **1449fd07**.
**Full API, behavioral and performance parity are not established.**

[Custom expander/content review](uno-custom-expander-review-2026-09-25.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-1449fd07.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-1449fd07.md) ·
[Custom value-column guide](uno-custom-value-columns.md)

## Architecture and revisions

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
The actual shared Core assembly owns sources, rows, hierarchy and selection.
No duplicated source model, public release or merge was introduced.

Starting head: `4d22b04279b58ff05ab8f9321210ffeaa7404e8a`.
Tested implementation: `1449fd079d6b2bde4913f17edc59a42244f81e1a`.
Product tree: `6c53c79f194826b924c1fc3b3163ca890c7fc1ba`.
Tested merge: `ae98a803304c4e5900c01ae07d4910ede1d74f42`.
The merge tree is identical to the product tree. Later documentation-only updates
do not change this product. Prior implementation is preserved, not counted again
as authored work in this continuation.

## Implemented and executed

- Custom expander replacement is transactional across application getters,
  metadata, event accessors and notifications. Newer nested changes win. Dynamic
  ancestor cycles are rejected, interrupted resets preserve current row/expansion
  state, and failed synchronization can recover without writing the old model.
- Failed replacements suspend custom descendant writes and release the old native
  editor. Live edit gestures, permission/visibility results, nested content and
  borrowed native values retain their ownership and retirement contracts.
- Leaf/column construction defers subscription removal until add accessors return.
  Factory failures and completed-wrapper retirement have separate ownership,
  preventing double disposal. Retired factories cannot return a live old cell.
  All owned cleanup is attempted and ordered application failures are preserved.
- The new native `custom-expander-lifetime` consumer exercises real editing,
  dynamic text/checkbox/empty/nested content, failure/cycle recovery, shared Core
  expansion and exact borrowed-model cleanup. It runs independently and in
  sequential native and published trimmed-browser consumers.

Authored coverage: **62 Uno unit cases and one native suite**. No new exported API
shapes or direct-framework cases are claimed. The first new native consumer failed
compilation because it referenced an internal event type; `1449fd07` corrects it
through the public property-change contract, without friend access or relaxed tests.

## Completed functional evidence

[Functional run 36115573480](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36115573480)
passes all fifteen stages on unchanged sources: **1,713 .NET cases**, zero failed
or skipped, **65/65 native suites**, sequential showcase/recovery, both native
sample builds with zero warnings/errors, and five Activity Monitor sections plus
lifetime checks. Core/Uno/Avalonia/sample/direct-framework totals are
228/757/536/41/151. The 12 Python audit tests, 39 metadata-semantic and 57
normalization checks pass. Artifact `10854358161` retains the evidence, including
the new native suite's successful execution and the complete raw API inventory.

## Platform execution

[Platform run 36115573436](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36115573436)
completed successfully across all six jobs: Windows/Linux/macOS builds and tests,
Windows App SDK package publication, self-contained native package execution, and
trimmed main/Activity Monitor browser publication and execution. All four browser
results passed: the main sequential consumer, pointer/keyboard input at scales 1
and 2, and the Activity Monitor consumer. The new custom-expander lifetime marker
passed in the published browser. The browser artifact is `10855063025`.
The repository build, Core/UI contract, published trimmed binding contract,
reference-pack and dependency-snapshot workflows also completed successfully.

Windows App SDK publication is not Windows OS runtime execution. Pinned Chromium
consumers and browser-dispatched input at scales 1 and 2 are not physical hardware,
universal browser, IME, external screen-reader or universal DPI acceptance.

## Performance remains failed

[Paired run 36115573488](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36115573488)
completes both builds and all four AB/BA hosts with valid frames but fails the
unchanged **1.10 median timing/allocation ratio budget**.

| Workload | Avalonia median ms | Uno median ms | Uno/Avalonia |
| --- | ---: | ---: | ---: |
| Horizontal scroll | 0.22935 | 1.24125 | 5.412 |
| Vertical scroll | 0.67070 | 1.58315 | 2.360 |
| Distant diagonal scroll | 1.34950 | 4.66450 | 3.456 |
| Replace visible row | 1.04850 | 1.57560 | 1.503 |
| Resize visible column | 1.35270 | 1.80880 | 1.337 |
| Sort | 20.08130 | 38.64165 | 1.924 |

Raw allocations, p95 and settlement data are in artifact `10855415774`. These are
synchronous UI/layout measurements, not GPU completion or frame rate. Different
runs are not a controlled before/after speedup experiment. The built-in flat text
workload does not isolate custom expander replacement. Focused zero-allocation
permission reads do not replace the failed whole-grid budget.

## Remaining gates

The compiled inventory remains 1,845 baseline / 1,833 target declarations, 1,010
exact normalized matches and 835 missing-or-different baseline entries. Both
reference sets resolve; self-comparison has no differences. Raw and supplemental
metadata differences are preserved. Counts are not feature-completion percentages
or automatically accepted Core/native equivalences.

Required: genuine member/signature/inheritance/attribute completion and explicit
tested Core/native mappings; broader mixed-mutation callback review; unchanged
native performance budgets with hierarchy/variable-height workloads; and physical
input/drag, Unicode/IME, external accessibility and cross-head scaling/lifecycle
acceptance. The custom-expander transactional content/construction/editor-lifetime
work described above is covered; these remaining gates are not marked complete.

All authored implementation is pushed. The retrieved source tree and local edits
were inspected, and exact Git trees were verified before publication. .NET/native
execution used GitHub Actions on the committed input; the local environment has
no .NET SDK. No assertion, Core ownership rule, trimming diagnostic, rendering
option or performance threshold was weakened.
