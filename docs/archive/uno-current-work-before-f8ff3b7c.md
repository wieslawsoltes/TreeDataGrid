# Current Uno completion checklist

Updated 2026-09-25 UTC. Tested checkpoint **5bbd677a**; product change **16b7aad1**.
**Full API, behavioral and performance parity are not established.**

[Observer allocation review](uno-observer-allocation-review-2026-09-25.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-5bbd677a.json) ·
[Comparison artifacts](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36127563554/artifacts/10860148409) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-5bbd677a.md)

## Revisions and implementation

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
Sources, rows, hierarchy and selection remain owned by the actual shared Core
assembly. No duplicate Core state, merge or public release was introduced.

Starting head: `06cacc070c5cb596717934eee95ecca4cf7971b1`.
Recovered prior documentation: `8f4b19606e41b7a5425d79341ab8905bdacb624e`.
Product change: `16b7aad1a8772ae2abea728ced47bfe8e9405ff0`.
Tested checkpoint: `5bbd677a61c29b503b3bce75a9e0745c64a0ac6e`.
Tested tree: `3e959d60aa10176c85f461873b6dbacd702a54cf`.
CI merge: `9a7f7f150ef750626892c8e8e4722d016ef78afe`, with the identical tree.
The checkpoint differs from the product commit only by its comparison workflow.
The initial product run was superseded; complete acceptance evidence below is
from the tested checkpoint, not incomplete earlier runs.

Native cells and headers now cache one instance PropertyChanged delegate per
container and reuse it for subscription/removal throughout recycling. No model is
captured by the cache. Existing sender/realization checks, custom accessor order,
deferred header cleanup, native property assignments and rendering are retained.
No exported API shapes or Core source files changed.

The existing `cell-lifecycle` suite now includes two native checks: 4,096 warmed
cell observation cycles allocate zero managed bytes on the measuring thread,
followed by delivery/reuse/borrowed-model lifetime checks; 256 header realizations
verify exact delegate identity, balanced accessor calls and current/retired updates.
This is **two new native checks, zero new .NET cases and zero new registered suites**.
The earlier 27 column-transaction cases are preserved, not counted again.

## Completed validation

[Functional run 36127567558](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36127567558)
passes all fifteen stages: **1,740 .NET cases**, zero failed/skipped, **65/65 native
suites**, sequential regression checks, both native sample builds with zero
warnings/errors, and five Activity Monitor sections plus lifetime checks.
Core/Uno/Avalonia/sample/direct-framework totals remain 228/784/536/41/151;
Python audit/metadata-semantic/normalization checks remain 12/39/57.
Artifact `10860058378` retains TRX, logs and complete API inventories. The new
observer marker is present in isolated and sequential native logs.

[Platform run 36127567483](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36127567483)
completed all six jobs successfully: Windows/Linux/macOS builds/tests, Linux
native package consumers, Windows App SDK build/package publication, and actual
published trimmed-browser execution. All four browser routes passed: showcase,
Activity Monitor and browser-dispatched pointer/keyboard routes at scales 1 and 2.
The new observer-allocation marker is present in the published showcase console.
Browser artifact `10860474221` retains the results and published consumers.

Windows App SDK publication is not Windows OS runtime execution. Pinned Chromium
routes are not physical hardware, universal browser, IME, external screen-reader
or universal DPI acceptance. This documentation was finalized after platform
execution completed.

The repository Build, contract reproducibility, published trimmed-binding,
reference-pack and dependency-snapshot workflows also passed. The downloaded CI
source reconstructs the exact tested Git tree. .NET/native execution used GitHub
Actions; no local .NET SDK was available.

## Controlled allocation improvement; mixed timing

[Comparison run 36127563554](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36127563554)
executes exact baseline `06cacc07` and candidate `16b7aad1` on one runner in ABBA
revision order, with alternating framework order per pass. All 16 framework hosts
succeeded. Each Uno operation has 100 samples per revision. All eight Uno hosts
have identical ordered realized frame geometry and row/cell counts.

Median allocation fell 7.85% for horizontal scrolling, 1.38% vertical, 1.36%
diagonal, 0.88% visible-row replacement and 1.28% sorting; resize was unchanged.
Pooled timing medians were mixed: horizontal -10.45%, diagonal -5.20%, replacement
-6.87%, sort -1.13%, **vertical +0.93% and resize +6.09%**. These are observations
from one controlled diagnostic, not statistical confidence or universal speedup.
The review and artifact `10860148409` preserve full timing/allocation/p95/settlement
and per-pass data. Headers are disabled in this benchmark; header observer identity
is verified by the separate native check, not by these timings.

## Independent performance and remaining gates

[Paired run 36127567515](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36127567515)
completed both builds and all four hosts, but the unchanged **1.10 median timing/
allocation ratio budget still fails**. Uno/Avalonia timing ratios are 7.272
horizontal, 2.424 vertical, 3.940 diagonal, 1.639 replacement, 1.776 resize and 2.280
sort. Artifact `10860617646` preserves raw data. Do not combine this independent
run with the controlled comparison into a before/after claim. A zero-allocation
observation path and lower whole-grid allocation do not certify performance parity.

Compiled inventory: 1,845 baseline / 1,833 target declarations, 1,010 exact normalized
matches, 835 missing-or-different baseline and 823 additional-or-different target
entries. Dependencies resolve; self-comparison has no differences. Every raw
metadata difference is preserved, not automatically accepted as a Core equivalence
or represented as a feature-completion percentage.

Remaining: genuine API/member/inheritance/attribute contracts and explicit tested
Core/native mappings; broader custom-source and mixed-mutation review; unchanged
performance budgets with hierarchy/variable-height workloads; physical input/drag,
Unicode/IME, external accessibility and cross-head scaling/lifecycle acceptance.
No assertion, ownership rule, trimming diagnostic, rendering setting or performance
threshold was weakened. PR #26 remains draft; the port is not marked complete.
