# Current Uno completion checklist

Updated 2026-09-25 UTC. Tested implementation **06cacc07**.
**Full API, behavioral and performance parity are not established.**

[Column transaction review](uno-column-transactions-review-2026-09-25.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-06cacc07.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-06cacc07.md) ·
[Previous custom-expander review](uno-custom-expander-review-2026-09-25.md)

## Architecture and revisions

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
The actual shared Core assembly owns sources, rows, hierarchy and selection.
No duplicated source model, merge or public release was introduced.

Starting head: `6ce462a80823188f802e00d1c89e34f8643a1e63`.
Tested implementation: `06cacc070c5cb596717934eee95ecca4cf7971b1`.
Product tree: `b35bd05665cb5628477c1cfc44594b2cb6fcb608`.
Tested merge: `5ab562998327fe4af1341a661fce927a4303f69c`.
The CI merge tree matches the product tree. The final documentation checkpoint
was prepared after the implementation's complete platform workflow passed.
Previous implementation is preserved, not counted again as authored work.

## Implemented and executed

Column replacement now captures ordered Core identities, presentation keys and
visibility before application factories execute. Nested synchronization is queued
rather than recursive. Returning obsolete factory values are released, never
labelled with a newer key or installed into a retired presentation. Operation-local
snapshots are checked again after factories and pool cleanup, including during
Resume while definition observation is detached.

Selection maps and visible columns are published for the surviving snapshot.
Staged and retired view ownership remains exact across reentrant publication and
throwing cleanup. A collection observer's exception does not skip independent
layout notification unless that snapshot has actually been superseded. Primary
failures retain their identity; multiple failures retain execution order.

Identity-scoped column observations defer removal until custom add accessors
return. Captured old callbacks cannot target a replacement registration. Suspend
attempts every owned cleanup, and Resume requested during removal waits for old
cleanup. Resume observes Core structural changes before invoking custom definition
accessors, preventing missed callback-time removals and reorders. These are tested
paths, not certification of every arbitrary custom source or event accessor.

Authored coverage: **27 Uno unit cases** (16 transaction and 11 observer cases),
plus **one native scenario inside the existing `presentation-pool` suite**.
The registered suite count remains 65. No new differential cases or exported
public/protected API shapes are claimed.

The native scenario uses 160 shared Core rows and checks latest key/order/visibility,
serial factories, actual text rendering, native editing, Core selection mapping,
resize, failed-factory recovery, distant virtualization and source-retirement
cleanup. It runs independently through `presentation-pool`, sequentially in the
native consumer, and in the actual published trimmed-browser consumer.

Intermediate `8b3260b2` failed compilation until the Core column cast was corrected.
`9439bd9f` passed all .NET cases but its new sample had an ambiguous IndexPath;
`06cacc07` qualifies the Core type without changing the selection assertion.
Failed intermediate builds are not counted as completed acceptance.

## Completed functional and platform evidence

[Functional run 36123533101](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36123533101)
passes all fifteen stages on unchanged committed sources: **1,740 .NET cases**,
zero failed or skipped; **65/65 native suites**; sequential showcase/recovery;
both native sample builds with zero warnings/errors; and all five Activity Monitor
sections plus lifetime checks. Core/Uno/Avalonia/sample/direct-framework totals are
228/784/536/41/151. Python audit/metadata-semantic/normalization checks are 12/39/57.
Artifact `10859940130` retains TRX, native logs and the complete raw inventories.

[Platform run 36123533150](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36123533150)
completed successfully across all six jobs: Windows/Linux/macOS builds and tests,
Linux native package execution, Windows App SDK build/package publication, and
trimmed browser publication and execution. All four browser routes passed: main
sequential consumer, Activity Monitor, and browser-dispatched pointer/keyboard input
at scales 1 and 2. The new column-factory marker appears in the published browser
console. Artifact `10859481819` retains browser results and published consumers.
Repository Build, reproducibility, published trimmed binding, reference packs and
dependency snapshot checks also passed. Exact IDs and checksums are in the checkpoint.

Windows App SDK publication is not Windows OS runtime execution. Pinned Chromium
and browser-dispatched input are not physical hardware, universal browser, IME,
external screen-reader or universal DPI acceptance.

## Performance remains failed

[Paired run 36123533134](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36123533134)
completes both builds and all four AB/BA hosts with valid frames but fails the
unchanged **1.10 median timing/allocation ratio budget**.

| Workload | Avalonia median ms | Uno median ms | Uno/Avalonia |
| --- | ---: | ---: | ---: |
| Horizontal scroll | 0.32235 | 1.76945 | 5.489 |
| Vertical scroll | 0.91695 | 1.97165 | 2.150 |
| Distant diagonal scroll | 1.74325 | 7.98095 | 4.578 |
| Replace visible row | 1.69590 | 2.59245 | 1.529 |
| Resize visible column | 2.81385 | 3.18350 | 1.131 |
| Sort | 33.86655 | 53.66795 | 1.585 |

Artifact `10859480309` retains raw allocations, p95 and settlement data. These are
synchronous UI/layout measurements, not GPU completion or frame rate. The workload
does not isolate mixed custom-column factory callbacks. No controlled before/after
whole-grid speedup is claimed, and earlier profiling of another revision is not
current performance evidence.

## Remaining gates

The compiled inventory remains 1,845 baseline / 1,833 target declarations, 1,010
exact normalized matches, 835 missing-or-different baseline and 823 additional-or-
different target entries. Both dependency sets resolve; self-comparison has no
differences. Raw and supplemental metadata differences remain preserved. Counts
are not feature-completion percentages or automatically accepted Core mappings.

Required: genuine member/signature/inheritance/attribute completion and explicit
tested Core/native mappings; broader custom-source and mixed-mutation callback
review; unchanged native performance budgets with hierarchy/variable-height
workloads; and physical input/drag, Unicode/IME, external accessibility and
cross-head scaling/lifecycle acceptance. Those gates are not marked complete.

All six authored source files match the downloaded CI source byte-for-byte.
.NET/native execution used GitHub Actions on committed input; the local environment
has no .NET SDK. Existing assertions, Core ownership, trimming diagnostics,
rendering options and performance thresholds were not weakened.
