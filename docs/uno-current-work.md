# Current Uno completion checklist

2026-09-26. Implementation **aa112e51** on `codex/uno-core-port`, PR #26.
**Draft; full API and performance parity remain open. No merge or release.**

[Public expander lifetime review](uno-public-expander-lifetime-2026-09-26.md) ·
[Previous checkpoint preserved](archive/uno-current-work-before-aa112e51.md) ·
[Previous binding optimization](uno-binding-reassignment-review-2026-09-26.md)

## Implemented in this continuation

The public `ExpanderCell<TModel>` now handles disposal inside row event accessors,
observable subscription calls and initial value callbacks. Retirement immediately
rejects further delivery, while cleanup waits until constructor callbacks return
and all returned leases are captured. Construction stops before subsequent stages.
Independent cleanup preserves original/ordered errors and cannot run twice.

Disposed expansion writes throw `ObjectDisposedException` instead of mutating a
still-live shared Core row. Edit/visibility reads return false after reentrant
retirement. Borrowed Core rows and observable sources are never disposed by the cell.
The same native control can recover with a new public model over the same Core row,
including native text editing, expansion and isolation from old sources.

Nineteen shared unit/runtime cases were added. The unchanged runtime fails twelve;
the candidate passes all nineteen. Earlier assertions, public signatures, Core,
renderer and performance budgets are unchanged. Three private lifetime flags were
added; no performance improvement or cross-thread safety is claimed.

## Verified local results

**2,009 .NET cases passed, zero failed/skipped:** 228 Core, 970 Uno, 536 Avalonia,
234 paired-framework and 41 sample-state. **65/65 registered native suites** pass,
as do focused public-expander execution and the sequential native smoke run.
No native suite registration or paired-framework test was added.

The production API reader still reports 1,845 reference / 1,888 target declarations,
1,055 exact, **790 missing/different** and 833 additional/different. All dependencies
resolve, normalization collisions are zero, and strict self-comparison has no
differences. Core dependency matches are not independent UI-port coverage.

Local tests use an offline recovered SDK/cache; SDK metadata selects available
8.0.31 reference/apphost packs instead of unavailable 8.0.25. Repository dependency
pins remain unchanged. SourceLink warnings and initial restore failures are retained.
The review details local input verification and the native-runner path adjustment.
These local results are not substituted for canonical CI.

## GitHub validation snapshot

Implementation: `aa112e51a7fdb06c3090f2b6fe64b9ebc7b4c5a0`.
Tree: `1c494e35d67f91837cb3609e780b37af813bbf79`.

Current [functional run](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36255768340)
and [platform run](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36255768332)
have not completed at this documentation snapshot. Trimming, contract reproducibility,
reference packs and dependency snapshot passed. The previous implementation's
platform run `36242946789` now has all six jobs passing, including macOS; it is not
represented as current-head execution.

The new [native performance run](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36255768337)
completed both builds and four hosts but failed the unchanged **1.10** budget.
Artifact `10910721852` retains the raw measurements. No performance budget was
relaxed, retry requested or before/after speedup inferred from unrelated runs.

Only the documentation-only follow-up uses `[skip ci]` to avoid replacing active
product checks under existing concurrency rules. The implementation was not skipped;
this does not make documentation-head checks green or satisfy merge requirements.

Remaining acceptance includes API/native-type/Core/inheritance contracts, native
text/layout and sorting costs, hierarchy/variable-height workloads, physical input,
IME, external accessibility and cross-platform runtime behavior.
