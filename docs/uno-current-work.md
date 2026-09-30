# Current Uno completion checklist

Updated 2026-09-30. **Latest round: [demo parity, visual comparison and performance](uno-demo-parity-2026-09-30.md).**
The Uno sample now mirrors the Avalonia demo tab for tab; the round fixed a star-width
layout hang, missing row/cell visual states, Core filtering, native columns in Core
sources, and trimmed-browser bindings, and reduced sort/resize/replacement cost to about
Avalonia's level on Linux CI. A continuation draws cell text directly with Skia
([design](uno-direct-text-rendering.md)), pixel-identical to the text block it replaces;
scrolling went from 3.4-4.8x to 1.3-2.6x Avalonia's time, with the remainder in Uno's
scroll presenter, composition invalidation and (macOS) accessibility. Full API and
performance parity remain open.

The previous checkpoint follows unchanged.

Updated 2026-09-28 UTC. Retained tested implementation **0cc1ca9b**.
**Full API and native-performance parity remain open. No new whole-grid speedup is claimed.**

[Native rendering and performance review](uno-native-render-performance-review-2026-09-28.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-0cc1ca9b.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-0cc1ca9b.md)

## Exact state and retained work

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
Shared Core continues to own sources, rows, hierarchy and selection. No merge,
public release, copied Core state, native renderer switch or acceptance waiver.

This continuation starts at `183584c6f9b8d809baaea921e01977becf5e1a3f`, six commits
beyond the previous documented `dcd4dc34` checkpoint. Those earlier profiling,
flattening and appearance-test changes were inspected, not recounted as new work.

| Commit | Current continuation |
| --- | --- |
| `fc4cd6bc` | Restore exact active-state Border/Grid geometry after the flattened template fails |
| `0e2b1f74` | Test a guarded same-string native assignment and add ownership/reentrancy coverage |
| `c7ae9344` | Compare exact baseline/candidate native sources in ABBA order |
| `0cc1ca9b` | Withdraw assignment guard after adverse resize/sort results; retain tests and evidence |

Retained tree: `b61a6c7a0b319a7d79b266fcbd6f2b4a159059be`.
CI merge: `e9eef4e63023704ab8df61fe96ab792a6df39066`, with that exact tree verified
through the Git commit API. No local source-tree reconstruction is claimed.

Generic.xaml is restored to exact blob `ac49036259c2a8acb5a889ba00a8938e7c89c7db`
from the reviewed active-state reference `f1fb840a`. Root visual states and isolated
data context remain. The flattened template moved thick-border text from
`[10,37,267,27]` to `[10,36,267,27]` in the original failing run; no tolerance or
special-case offset was substituted for correct native layout.

The measured render guard is not retained. `TreeDataGridCell.Render.cs` again has
exact original blob `09939e28cce4968c5407634af91c8ce5108fda1b`. The complete diff
between `fc4cd6bc` and `0cc1ca9b` has no runtime-library source file. Relative to
this continuation's starting head, Generic.xaml is the only runtime source change.
Original Avalonia, shared Core, bindings, editing and API declarations are unchanged.

## Retained native and collector coverage

One new composite native fixture compares the actual cell renderer with direct
native TextBlock assignment. Nine scenarios cover local-value priority, string
identity, 4,096 repeated live getter evaluations, OneWay/OneTime binding ownership,
original exceptions, reentrant getter/property callbacks and null/empty text. It
runs in the existing appearance suite; no new registered suite or xUnit case is
claimed. Four Python test methods validate the exact-source comparison collector
and its negative controls.

The existing appearance fixture preserves all exact geometry/state and raster
assertions across sixteen configurations: Light/Dark, thin/asymmetric thick borders,
font size, wrapping/trimming and independent selection/current/validation overlays.
Only the withdrawn optimization's one-fewer-visual/Grid-root requirements become
the original equal-topology/Border-root requirements. No semantic geometry/pixel
assertion was weakened. Native rendering logs show sixteen exact byte comparisons
and eight visuals on both sides. WASM does not claim native raster-API execution.

## Completed canonical and native validation

[Canonical run 36407211011](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36407211011),
job `108878716071`, passes **all nineteen stages** on unchanged committed input:

| Assembly | Passed |
| --- | ---: |
| Core | 250 |
| Uno | 1,111 |
| Original Avalonia | 536 |
| Sample state | 41 |
| Paired framework | 319 |
| **Total .NET cases** | **2,257; zero failed/skipped** |

**65/65 native suites**, sequential execution, both native sample builds with zero
warnings/errors, Activity Monitor's five sections/lifetime, production API reader,
strict self-comparison and compiled declaration preservation pass. Existing earlier
review regression fixtures and the removed-export negative control remain enforced;
they are not newly authored cases here.

Report artifact `10962893012` retains raw inventories, TRX and full logs; source
artifact `10962737790` preserves input. Returned metadata hashes are in the checkpoint.
The report was downloaded; no local extraction or independently recomputed hash is
claimed.

Linux native job `108878921434` explicitly logs the new
`UNO_RUNTIME_NATIVE_TEXT_ASSIGNMENT_PASSED` marker with nine cases in both sequential
and NuGet-consumer execution. Both logs also report
`UNO_RUNTIME_TEXT_TEMPLATE_PARITY_PASSED: states=16; exactNativePixelStates=16; referenceVisuals=8; candidateVisuals=8`.
Native artifact `10963375323` retains the associated results and rendered samples.

## Platform snapshot: desktop passed; browser execution still pending

[Ordinary platform run 36407211069](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36407211069)
has **five successful completed jobs**: Windows, macOS and Ubuntu builds/tests,
Linux X11/native NuGet consumers, and Windows App SDK build/package publication.
Its browser build, pack and publish stages pass, but actual published-consumer
execution is still running at this documentation snapshot. It is not counted as
passed merely because publication succeeds.

[Three-engine run 36407210841](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36407210841)
has passed its driver tests and packing; identical-bundle publication is in progress.
No completed Chromium/Firefox/WebKit result for this retained revision is inferred
from prior runs. The earlier Firefox failures are not fixed or waived by this work.
Build, dependency snapshot, reference packs, reproducibility and trimmed binding
workflows have separately completed successfully for `0cc1ca9b`.

The final four-path documentation-only commit uses `[skip ci]` to avoid superseding
active implementation/browser runs under existing PR concurrency rules. No product
commit, assertion, protection, API gate or native-performance threshold is skipped
or weakened. New-head required checks are not represented as green; no merge is
requested. Windows publication is not Windows OS runtime acceptance, and browser
automation is not physical IME or external screen-reader acceptance.

## Controlled performance decision: candidate withdrawn

[Comparison run 36406417272](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36406417272)
completed all sixteen native processes on one runner, comparing `fc4cd6bc` with
`c7ae9344` in baseline/candidate/candidate/baseline order. The unchanged AB/BA native
runner supplies 100 samples per operation/framework/revision. Template and benchmark
inputs are identical, runtime defaults are inherited, and ordered native frame
records match within each framework. These are layout/realization records, not
benchmark pixel comparisons.

| Operation | Baseline UI median ms | Candidate UI median ms | Candidate/baseline |
| --- | ---: | ---: | ---: |
| Horizontal scrolling | 0.75310 | 0.57575 | 0.764507 |
| Vertical scrolling | 1.60805 | 1.51705 | 0.943410 |
| Distant diagonal scrolling | 5.66675 | 4.64720 | 0.820082 |
| Visible-row replacement | 1.69550 | 1.58015 | 0.931967 |
| Visible-column resizing | 1.74825 | 2.45110 | **1.402031** |
| Sorting | 38.76875 | 40.54200 | **1.045739** |

**Resize median worsens 40.20%, resize p95 2.7714 to 5.1618 ms, and sorting median
4.57%.** Five operations have unchanged allocation medians; resize's pooled median
rises 130,272 to 132,672 bytes, with both amounts appearing among the hosts. This is
not attribution of a specific allocation site.

The unchanged Avalonia controls also vary: horizontal 0.20560 to 0.14530 ms and
vertical 0.62310 to 0.46070 ms; all six Avalonia pooled UI medians are lower in
candidate-labelled passes. Baseline Uno horizontal host medians span 0.4719 to
1.0905 ms. Favorable pooled scroll results therefore do not prove a causal speedup.
No sample/workload was discarded, no comparison retry selected a favorable outcome,
and no confidence interval or universal improvement is claimed.

The guard was withdrawn rather than retained with only its favorable results.
Artifact `10962223411` preserves all samples, per-host medians, p95, settlement,
source and compiled fingerprints, and every failed independent gate. The manual
comparison workflow defaults to the exact historical candidate, not the reverted
runtime or a documentation head. Collection success is not performance acceptance.

## Independent retained-runtime gate and API boundary

[Native gate 36407210902](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36407210902)
completes both builds and all four processes, but still fails the unchanged **1.10**
timing/allocation threshold:

| Operation | Uno/Avalonia median time | Uno/Avalonia allocation |
| --- | ---: | ---: |
| Horizontal scrolling | 2.610 | 0.695 |
| Vertical scrolling | 2.737 | 1.930 |
| Distant diagonal scrolling | 4.226 | 1.917 |
| Visible-row replacement | 1.624 | 2.668 |
| Visible-column resizing | 1.780 | 1.518 |
| Sorting | 1.510 | 0.620 |

This independent run is not a controlled before/after comparison with the experiment.
Absolute timings from different runners are not combined into a product speedup.
The scope remains synchronous UI/layout and settlement, not GPU completion/frame rate.

The unchanged API inventory remains **1,845 reference / 1,903 target declarations,
1,064 exact, 781 missing-or-different, 839 additional-or-different**. Shared Core's
590 identical records are dependency self-matches, not independent UI parity. There
are no unresolved dependencies or normalization collisions. Strict target
self-comparison matches all 1,903 declared and 15,944 supplemental records. The
preservation gate has no lost export, rewritten old raw record or lost reference
match. Supplemental cross-framework differences remain 9,575/12,054; no waiver.

Remaining work: actual native/Core/inheritance API adaptations, native layout/text/
retirement/source-sort costs, broader hierarchy and variable-height performance,
Firefox's unclosed routes, the earlier intermittent allocation observation, physical
input/drag, OS composition and external accessibility. The present result is a
correctness restoration and stronger performance evidence, not completion of parity.

Local container/Python execution returned ClientError. Tests and benchmarks ran in
GitHub Actions. Returned complete logs, artifact metadata and Git object identities
were inspected; no local compilation, archive extraction, locally recomputed digest
or independently reconstructed source tree is claimed in this continuation.
