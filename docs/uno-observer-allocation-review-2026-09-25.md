# Native container observer allocation review

2026-09-25 UTC. Product change `16b7aad1`; tested checkpoint `5bbd677a`.
[Current checklist](uno-current-work.md) · [Execution checkpoint](uno-ci-checkpoint-5bbd677a.json) ·
[Comparison artifacts](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36127563554/artifacts/10860148409)

## Implementation and ownership

`TreeDataGridCell` and `TreeDataGridColumnHeader` now retain one lazily initialized
`PropertyChangedEventHandler` per native container. Both attachment and detachment
use that same delegate instead of converting an instance method group on each
recycling transition. The cache refers to the container, not a captured model; it
is not global. Retaining a delegate for the container's lifetime trades a small
per-container retained object/reference for less repeated allocation. Existing
sender/realization checks, add/remove accessor ordering, deferred header cleanup,
model ownership, native dependency-property assignments and rendering behavior
remain intact. The header's equivalent split reindex guard has no semantic change.

The shared Core assembly is unchanged. No source, row, hierarchy or selection
copies, public API additions, skipped application callbacks or renderer shortcuts
were introduced. Only two product files changed. The native consumer and workflow
are test/measurement additions. Commit `8f4b1960` first published the preceding
continuation's pending column-transaction checkpoint; that work is not newly
counted here.

## New executed native checks

`NativeObserverAllocationRuntimeChecks` executes inside the existing `cell-lifecycle`
suite, including sequential native and published-browser consumers. Its cell check
performs 1,024 warm-up and 4,096 measured unsubscribe/subscribe cycles with **zero
managed bytes on the measuring thread**. Construction, native property setters and
application notifications are outside that measurement. It separately verifies
single notification delivery, idempotent observation/removal, retained-container
reuse on a different row, former-source isolation and borrowed-model cleanup.

The header check alternates two custom columns across 256 realizations and verifies
exact add/remove delegate identity, 128 attachments/removals per model, current
header updates and no updates after retirement. Existing throwing-accessor and
reentrant lifetime tests remain unchanged. These are **two new native checks**, not
new xUnit cases or additional registered suites: totals remain 1,740 .NET cases and
65 suites. A zero-allocation observation micro-path is not a zero-allocation grid.

## Controlled before/after measurement

[Comparison run 36127563554](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36127563554)
ran exact unmodified baseline `06cacc07` and candidate `16b7aad1` on one runner in
baseline/candidate/candidate/baseline order. Every revision pass executes alternating
Avalonia/Uno and Uno/Avalonia pairs: **16 successful framework hosts total**. Each
Uno workload has 100 measurements per revision. Runtime, OS, architecture, GC mode,
font, viewport and workload configuration match. Offline inspection also confirmed
that all eight Uno hosts have identical ordered frame geometry and realized-row/
cell counts for every operation/iteration. The comparison did not use smaller
viewports or fewer realized cells in the candidate.

| Workload | Baseline bytes | Candidate bytes | Allocation change | Baseline ms | Candidate ms | Timing change |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Horizontal scroll | 24,448 | 22,528 | -7.85% | 2.49625 | 2.23530 | -10.45% |
| Vertical scroll | 194,592 | 191,904 | -1.38% | 2.47625 | 2.49935 | +0.93% |
| Distant diagonal scroll | 985,112 | 971,672 | -1.36% | 10.03245 | 9.51120 | -5.20% |
| Replace visible row | 102,144 | 101,248 | -0.88% | 3.82410 | 3.56155 | -6.87% |
| Resize visible column | 132,528 | 132,528 | +0.00% | 7.67350 | 8.14050 | +6.09% |
| Sort | 1,052,056 | 1,038,616 | -1.28% | 66.94455 | 66.19130 | -1.13% |

Values are pooled medians. Negative changes are reductions; positive changes are
increases. Allocation reductions repeat in both candidate passes and both baseline
passes; resize allocations are unchanged. Timing results are mixed, including
**+0.93% vertical-scroll** and **+6.09% resize** medians. No universal speedup or
statistical significance is claimed. Full per-pass medians, p95, settlement metrics,
raw frames and original budget outcomes remain in artifact `10860148409`.

The fixed-text benchmark has 10,000 rows, 64 columns, an 800x480 viewport, 32-pixel
rows and **headers disabled**. It does not establish a whole-grid header timing
improvement; header observer identity is tested separately. The workload is not
hierarchy/variable-height, GPU completion, frame rate, input latency or physical
hardware acceptance. The comparison workflow reports collection success only
when all exact revision builds/hosts succeed and validates every original 1.10 gate
outcome. It does not replace or relax that gate.

## Independent parity gate remains failed

[Paired run 36127567515](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36127567515)
completed both builds and all four hosts on tested merge `9a7f7f15`, with valid
frames. It still fails the unchanged 1.10 median timing/allocation budget:

| Workload | Uno/Avalonia median time | Uno/Avalonia median allocation |
| --- | ---: | ---: |
| Horizontal scroll | 7.272 | 1.063 |
| Vertical scroll | 2.424 | 1.957 |
| Distant diagonal scroll | 3.940 | 1.928 |
| Replace visible row | 1.639 | 2.668 |
| Resize visible column | 1.776 | 1.516 |
| Sort | 2.280 | 0.620 |

This independent run has different timings from the controlled comparison; they
must not be combined into a before/after claim. Artifact `10860617646` preserves
its raw evidence. Lower allocation in one path does not certify port performance.

## Functional, platform and remaining acceptance

[Functional run 36127567558](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36127567558)
passes all fifteen stages, 1,740 .NET cases with zero failures/skips, 65/65 native
suites and sequential regression execution. Both native sample builds have zero
warnings/errors; all five Activity Monitor sections and lifetime checks pass.
Python audit/metadata semantic/normalization checks remain 12/39/57. The new
observer marker appears in isolated and sequential native logs.

Exact platform results and publication/runtime distinctions are recorded in the
execution checkpoint and current checklist.

The compiled API inventory remains 1,845 baseline / 1,833 target declarations,
1,010 exact normalized matches, 835 missing-or-different baseline and 823 additional-
or-different target entries, with both dependency sets resolved. Raw supplemental
metadata remains preserved; no difference was automatically accepted. Genuine API/
inheritance/Core mappings, broader custom-source callback review, the unchanged
performance budget with hierarchy/variable-height coverage, physical input/drag,
Unicode/IME and external accessibility acceptance remain open. PR #26 stays draft;
no merge or release is claimed.
