# Native text rendering: geometry repair and measured optimization rejection

2026-09-28 UTC. Starting head `183584c6`; geometry repair `fc4cd6bc`;
measured candidate `c7ae9344`; retained runtime `0cc1ca9b`.
[Current checklist](uno-current-work.md) · [Exact checkpoint](uno-ci-checkpoint-0cc1ca9b.json)

## Retained result

The invalid flattened text template is withdrawn. The default template again uses
the exact native Border/Grid layout from the reviewed active-state reference,
while retaining the root visual-state groups and isolated private data context.
The subsequent same-string publication guard was tested and benchmarked, then also
withdrawn after an adverse resize result. The original renderer source is restored
byte-for-byte. No whole-grid performance improvement is claimed in this continuation.

The retained additions are a native text-ownership regression scenario, its wiring
into appearance acceptance, and an exact-source comparison with negative-control
tests. There is no new model cache, native measurement shortcut, source-copy layer,
public API change, renderer-backend switch or performance-budget relaxation.

## Recovering the actual starting state

The preceding reported checkpoint was `dcd4dc34`, but six later commits were already
on the branch. They included native profiling, a flattened default text template,
a paired performance collector and an exact-geometry/native-raster fixture. Those
changes are prior work, not recounted as authored here.

Canonical run 36385154686 at `183584c6` failed appearance. On the Light theme with
asymmetric thick borders and the normal text state, the reference text bounds were
`[10,37,267,27]` but the flattened candidate returned `[10,36,267,27]`. Both reported
the same desired size, font size and padding. This is an observed one-pixel geometry
difference, not an inferred performance issue. The corresponding sequential process
also returned 139 after its assertion failure; it is not accepted as validation.

`fc4cd6bc` restores Generic.xaml to Git blob
`ac49036259c2a8acb5a889ba00a8938e7c89c7db`, the exact active-state reference from
`f1fb840ac12aca8851fd5d55589b80ece72bf6fc`. It does not move visual-state groups back
to an ineffective child location. Cell padding/border reservation, independent
translucent overlays, text trimming, themes and data-context isolation remain.

The existing fixture still compares sixteen Light/Dark, thin/thick, font/wrapping,
selection/current/validation configurations. Native text bounds must be exactly
equal. On non-WASM heads, RenderTargetBitmap dimensions and every pixel byte must
also match. WASM retains geometry/state tests but does not pretend to run the native
raster API. Only the withdrawn experiment's Grid-root and one-fewer-visual demands
were changed to the original Border-root and equal-topology requirements. No
geometry tolerance or special-case one-pixel offset was introduced.

Canonical run 36404502592 on the restored source passed all nineteen stages,
2,257 .NET cases and 65 native suites. This establishes the corrected reference
before the next performance candidate is compared against it.

## Profile interpretation and scope

The previously collected profile at run 36309146783 used the pinned tracing tool,
64 columns and 600 checked operations per framework. Its sampled-thread residence
includes startup, waits, rendering/finalizer threads and UI work; it is not CPU-only
attribution. Damage-region accumulation, drawing and retirement/visibility chains
appear among the useful investigation targets. Profiled durations are not combined
with the uninstrumented acceptance gate to claim a speedup.

Source inspection confirmed that same-pass horizontal and vertical recycling already
defers visibility changes and finalizes unused controls. Reimplementing that existing
mechanism was not counted as new optimization. A lazy-editor-host direction was
considered but not implemented; no deferred-loading placeholders or editing changes
were added in this continuation.

## Candidate: omit a redundant native text assignment

The single runtime candidate file was `Primitives/TreeDataGridCell.Render.cs`.
After evaluating the existing DisplayText getter, the candidate omitted assignment
only when ReadLocalValue returned the exact incoming string instance, no native
binding expression was attached, and the effective Text value had the same identity.
The render/realization-generation checks remained before and after the native work.

This intentionally did not use string equality, cache a display result, suppress a
custom getter, or treat an equal styled/bound value as locally owned. Distinct string
instances and values originating from other dependency-property priorities still
reached the original setter. Binding ownership and reentrancy needed native tests;
those semantics were not inferred solely from equal displayed strings.

The candidate was published in `0e2b1f74`, with the exact-source benchmark added in
`c7ae9344`. It is NOT in retained runtime `0cc1ca9b`: the original renderer Git blob
`09939e28cce4968c5407634af91c8ce5108fda1b` is restored exactly. The full source diff
between `fc4cd6bc` and `0cc1ca9b` contains no runtime-library file.

## Native regression coverage retained

`NativeTextAssignmentRuntimeChecks` is loaded in the existing appearance suite.
A derived native text cell is compared against direct assignment to a real native
TextBlock, rather than against a hand-written simulation of dependency properties.
Nine scenarios cover acquiring local priority over an equal default; 4,096 repeated
live display-getter evaluations; equal-content but distinct string objects;
OneWay and OneTime binding replacement behavior; original getter-exception identity;
nested getter refresh taking precedence over an obsolete outer value; nested native
property callbacks; and null/empty publication. Source observations are cleaned up.

These are nine scenarios in one composite native check, not nine new xUnit cases or
an extra registered suite. Existing exact geometry/raster assertions, editing,
source retirement, sorting and virtualization acceptance are preserved. There is
no executed failing-baseline claim for these new ownership assertions: they are
intended to pass the original native setter as well as a compatible candidate.

Four new Python test methods exercise the comparison collector, including rejection
of unrelated/template/Core/runner changes, duplicate paths, wrong revisions,
missing samples, failed frames and nonfinite/negative measurements. They execute
explicitly, avoiding hyphenated-module discovery ambiguity.

## Controlled native measurement

[Run 36406417272](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36406417272),
job `108876126971`, compares exact baseline `fc4cd6bc` against `c7ae9344` in
baseline/candidate/candidate/baseline process order. Each pass runs the unchanged
native parity collector with two alternating framework pairs, 64 columns and 25
iterations per operation. Both frameworks completed all sixteen processes, yielding
100 samples per operation, per framework, per revision. Ordered frame evidence was
identical across the eight hosts for each framework. Those frame records are
layout/realization assertions, not a pixel comparison of the benchmark workload.

The collector verifies that the only runtime source difference is the reviewed
render method, native templates have identical Git blobs, and benchmark/runner
sources are unchanged. Separate worktrees retain exact revision metadata. Binary
fingerprints are stable between passes of the same revision; different source
revisions can carry different build metadata even for unchanged Core/Avalonia
sources. No claim of byte-identical cross-revision assemblies is made.

Environment: .NET 10.0.12, x64 Ubuntu 24.04.5 LTS, workstation GC, inherited runtime
defaults with no tiered-compilation or ReadyToRun override. Workload: 10,000 rows,
64 fixed 128-pixel columns, 800x480 viewport, 32-pixel rows, DejaVu Sans 14, hidden
headers/scrollbars and zero cache length. Both exact revisions use active visual
states. This is synchronous native UI/layout plus separately recorded settlement,
not startup, physical input latency, GPU completion or frame rate.

### Complete Uno pooled medians

| Operation | Baseline UI ms | Candidate UI ms | Candidate/baseline | Baseline bytes | Candidate bytes |
| --- | ---: | ---: | ---: | ---: | ---: |
| Horizontal scroll | 0.75310 | 0.57575 | 0.764507 | 14,728 | 14,728 |
| Vertical scroll | 1.60805 | 1.51705 | 0.943410 | 191,904 | 191,904 |
| Distant diagonal scroll | 5.66675 | 4.64720 | 0.820082 | 966,096 | 966,096 |
| Replace visible row | 1.69550 | 1.58015 | 0.931967 | 101,248 | 101,248 |
| Resize visible column | 1.74825 | 2.45110 | **1.402031** | 130,272 | **132,672** |
| Sort | 38.76875 | 40.54200 | **1.045739** | 1,038,760 | 1,038,760 |

The resize median increases 40.20%, its UI p95 increases from 2.7714 to 5.1618 ms,
and its settled median increases from 2.05615 to 2.8478 ms. Sorting UI median rises
4.57%. Resize pooled allocation rises by 2,400 bytes, while its per-host medians
show that both byte totals already occur in the experiment. This is a measured
observation, not attribution of a particular native allocation site.

Some pooled scroll medians are lower, but the unchanged Avalonia controls also
vary materially. For example, Avalonia horizontal median changes 0.20560 to
0.14530 ms, and vertical changes 0.62310 to 0.46070 ms. All six Avalonia pooled UI
medians are lower in the candidate-labelled passes. The baseline Uno horizontal
host medians span 0.4719 to 1.0905 ms. These controls and per-host values prevent
claiming that every favorable difference is caused by the render guard.

No statistical significance, universal speedup or complete causal attribution is
claimed. No samples or workloads were discarded and no comparison rerun was used
to select a favorable result. Given the resize regression, unchanged allocations
for five operations, and varying unchanged controls, the candidate does not justify
retention. `0cc1ca9b` restores the original renderer rather than retaining only the
favorable pooled numbers.

All four independently computed 1.10 native gates in this experiment fail. The
comparison workflow's success means collection and input/correctness validation
completed, not that native performance parity passed.

## Reproduction and retained evidence

```bash
python3 build/test-native-text-assignment-comparison.py
python3 build/compare-native-text-assignment.py \
  --baseline fc4cd6bc3625ec98347056f2e0786d967e88e305 \
  --candidate c7ae9344e6afc77432ab4f449306d29da22c3efa \
  --output artifacts/text-assignment-independent-reproduction
```

Use a fresh output directory. The manual diagnostic workflow defaults to the exact
historical candidate, not the current reverted or documentation head. Its collector
rejects other runtime/template/workload changes. The normal functional, platform,
API-preservation and independent performance workflows remain unchanged.

Artifact `10962223411` has 61 files according to the upload log and size 85,131 bytes.
GitHub reports SHA-256
`644001fb90749183f971a7319a6c9caaf83a50e0f298de37f871dc3778ac4a72`.
It preserves all raw measurements, original failed gate outcomes, p95, per-host
medians, configuration, source blobs, compiled fingerprints and build logs.
The archive was downloaded but not extracted or rehashed locally in this turn.

The withdrawn flattened-template experiment and its original failing geometry log
remain in history. Its comparison collector must be invoked from its own original
candidate checkout; it intentionally does not accept the restored Border topology
as though it were the old flat candidate. Its older inactive-state comparison is
not mixed into this active-state experiment.

## Acceptance boundary

The retained runtime has no API declaration change. The compiled audit and API
preservation results are recorded in the checkpoint; 781 raw declared differences
and supplemental/native/Core adaptations are not waived. The ordinary native gate,
Firefox's previously failing routes, physical OS composition, external accessibility,
variable-height performance and the earlier intermittent allocation observation
remain separate work. Successful geometry or managed/native unit execution does not
establish those acceptances.

All execution in this continuation uses GitHub Actions because local container and
Python execution returned ClientError. GitHub job outcomes, complete returned logs,
artifact metadata and commit/tree/blob identities were inspected. No new local
compilation, independent local archive hash or reconstructed source-tree verification
is claimed. PR #26 remains draft; no merge or public release.
