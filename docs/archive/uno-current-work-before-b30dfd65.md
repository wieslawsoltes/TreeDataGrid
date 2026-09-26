# Current Uno completion checklist

2026-09-26. Tested implementation **aa112e51** on `codex/uno-core-port`, PR #26.
**Draft; full API and performance parity remain open. No merge or release.**

[Public expander lifetime review](uno-public-expander-lifetime-2026-09-26.md) ·
[Canonical CI checkpoint](uno-public-expander-ci-aa112e51.json) ·
[Previous checklist preserved](archive/uno-current-work-before-aa112e51.md) ·
[Previous binding optimization](uno-binding-reassignment-review-2026-09-26.md)

## Implemented

The public `ExpanderCell<TModel>` now handles disposal inside row event accessors,
observable subscription calls and initial-value callbacks. Retirement immediately
rejects delivery; cleanup waits until constructor callbacks return and returned
leases are captured. Construction stops before subsequent subscription stages.
Independent cleanup preserves original/ordered exceptions and cannot run twice.

Disposed expansion writes throw `ObjectDisposedException` instead of mutating a
still-live shared Core row. Edit/visibility reads return false after reentrant
retirement. Borrowed Core rows and observable sources remain caller-owned.
The same native control recovers with a new public model over the exact same Core
row, including native text editing, expansion and isolation from old sources.

Nineteen shared unit/runtime cases were added. The unchanged runtime fails twelve;
the candidate passes all nineteen. Earlier assertions, public signatures, Core,
renderer and performance budgets are unchanged. Three private lifetime flags were
added; no performance improvement or cross-thread synchronization is claimed.

## Completed canonical and local validation

[GitHub functional run 36255768340](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36255768340)
passed all fifteen stages. Downloaded artifact `10910253933` was independently
hash-verified and its raw TRX/native logs inspected:

**2,009 .NET cases passed, zero failed/skipped:** 228 Core, 970 Uno, 536 Avalonia,
234 paired-framework and 41 sample-state. **65/65 registered native suites** and
sequential smoke passed. Both new constructor/recovery markers occur in isolated
and sequential native logs. The same local suite totals independently pass.
No native suite registration or paired-framework test was added.

The production API reader still reports 1,845 reference / 1,888 target declarations,
1,055 exact, **790 missing/different** and 833 additional/different. Dependencies
resolve, normalization collisions are zero, and strict self-comparison is clean.
Core's 590 identical dependency records are not independent UI-port coverage.

Local validation used an offline recovered SDK/cache and an SDK-only selection of
available 8.0.31 reference/apphost packs instead of unavailable 8.0.25. Repository
pins remained unchanged. Local SourceLink warnings, early restore failures, source
hashes and baseline failures are retained. Canonical CI passed independently without
those local SDK adjustments. The review's earlier pending-CI snapshot is historical;
the linked canonical checkpoint supersedes it.

## Platform and performance boundaries

Implementation `aa112e51a7fdb06c3090f2b6fe64b9ebc7b4c5a0`, tree
`1c494e35d67f91837cb3609e780b37af813bbf79`.

Build, trimming, contract reproducibility, reference packs and dependency snapshot
passed. Current [platform run 36255768332](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36255768332)
has four completed successful jobs: Windows/Ubuntu tests, Windows App SDK
build/package publication and Linux native/NuGet consumers. Browser execution is
in progress and macOS is queued at this checkpoint. Neither is counted as passed.
The previous implementation's run `36242946789` finished all six jobs, including
macOS; it is not substituted for current-head execution.

The fresh [native performance run 36255768337](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36255768337)
completed both builds and all four hosts but failed the unchanged **1.10** budget.
Artifact `10910721852` retains raw results. This correctness fix does not establish
a controlled before/after performance improvement or full framework parity.

Only documentation follow-ups use `[skip ci]` to avoid superseding active product
checks under existing concurrency rules. The implementation was not skipped; this
does not make documentation-head checks green or satisfy merge requirements.

Remaining: API/native-type/Core/inheritance contracts, native text/layout and sorting
costs, hierarchy/variable-height workloads, physical input, IME, external accessibility
and cross-platform runtime behavior.
