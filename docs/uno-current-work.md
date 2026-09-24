# Current Uno completion checklist

Updated 2026-09-24 UTC. Tested implementation **89525188**.
**Full API, behavioral and performance parity are not established.**

[Typed-root and observer review](uno-root-observer-review-2026-09-24.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-89525188.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-89525188.md) ·
[Custom value-column guide](uno-custom-value-columns.md)

## Shared architecture and revisions

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
The actual shared Core assembly still owns sources, rows, hierarchy and selection.
No Core source, dependency, native rendering, trimming or release policy changes
are part of this continuation. No merge or public release was performed.

Starting head: `37a7f2ca0156174c7eba1d0c0bde9df838150845`.
Product: `89525188ae3e12357e700e790e12df3660dc11b6`.
Product tree: `631b18993d8d9970990b850fcce1ce2eb44d4945`.
Tested merge: `d53006cd365327b86137f5e36759cd29d1fcceb6`.
Documentation does not change the tested implementation.

The intervening built-in Binding descriptor and last-value fixes in `2c181a0d`
and `37a7f2ca` were already present, preserved and revalidated. They are not counted
as authored here. The archived checklist's missing-Binding paragraph is superseded:
built-ins now expose a descriptor and protected factory actually consumed by cells.
Existing retained/pooled cells preserve construction snapshots; Core selector and
sorting ownership remain independent. Legacy combined inheritance is still a
separate equivalence question.

## Implemented and tested in this continuation

`b7ad1eb2` scopes each observable-root callback to its activation's exact CellBinding.
A previous subscription cannot replace or terminate a newer activation. Rejected
subject writes cannot refresh replacement activations/roots. Error publication and
cleanup preserve original exceptions in order. Current root completion still leaves
its final model observed. Nine unit cases cover these boundaries and zero managed
allocation across 4,096 warmed old/current callback pairs.

`f8a86915` adds the `typed-root-lifetime` consumer for actual text and nullable-checkbox
controls. The same control survives reactivation over caller-owned typed cell models;
old value/null/error callbacks are rejected; scalar writes reach only the current
model; fallback/recovery and final-root observation work; all subscriptions retire
without disposing borrowed root observables. It runs independently and in sequential
native/published browser consumers.

`89525188` reuses immutable observer snapshots for stable multi-subscriber publication.
Membership and terminal transitions invalidate the cache under the original gate.
Nested publications retain original outer-snapshot behavior; duplicate subscriptions,
callback ordering and exception semantics match the actual Avalonia implementation.
Twelve added cases cover six differential traces, three allocation comparisons at
2/8/128 subscribers and three removed/terminal observer collectability checks.
Each 4,096-publication warm native allocation loop passes with zero managed bytes.

The offline materializer reproduces this explicit adaptation exactly from unchanged
pinned/reference inputs. Source reproduction and all existing differential tests are
still enforced. This continuation adds **21 .NET cases and one native suite**.

## Completed functional and platform execution

[Functional run 36004598398](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36004598398),
job `107649396717`, passes all fifteen required stages on an unchanged checkout.
Artifact `10810336124` contains the full inventories, fingerprints, TRX and native logs.

| Gate | Result |
| --- | --- |
| Core / Uno / Avalonia / sample-state tests | 228 / 612 / 536 / 41 passed |
| Actual-framework comparison assembly | 122 passed |
| **Total .NET cases** | **1,539; zero failed/skipped** |
| Registered native suites | **60/60 passed** |
| Sequential showcase and native recovery | Passed |
| Both native sample builds | Zero warnings/errors |
| Activity Monitor | All five sections and lifetime checks passed |
| Python audit / metadata semantic / normalization checks | 12 / 39 / 57 passed |

Platform matrix [36004598542](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36004598542)
is **fully successful**: Windows/Linux/macOS desktop jobs, Linux native regression
and package-consumer execution, Windows App SDK sample/package build and publication,
browser builds, trimmed publication and **actual execution of both published browser
consumers**. Browser job `107649970087` reports four passed routes and zero failures:
showcase, monitor, and browser-dispatched pointer/keyboard input at device scales
1 and 2. The workflow's success was verified after its 13:33:23 UTC update, before
pushing the final documentation commit. These are pinned-Chromium routes, not
physical hardware, universal browser, IME, external screen-reader or universal DPI
acceptance. Windows App SDK build/publication is not claimed as OS runtime execution.

Independent contract reproducibility `36004598917` verifies all 23 generated outputs
offline and runs all 122 framework cases. Repository Build `36004598339` and the
independently published/executed trimmed-binding contract `36004598362` pass too.

## Whole-grid performance still fails

[Paired run 36004598940](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36004598940),
artifact `10810002332`, completes both builds and all four AB/BA hosts with valid
frames. The unchanged **1.10 median timing/allocation budget remains failed**.

| Workload | Avalonia median ms | Uno median ms | Uno/Avalonia |
| --- | ---: | ---: | ---: |
| Horizontal scroll | 0.19765 | 1.57820 | 7.98 |
| Vertical scroll | 0.62235 | 1.56045 | 2.51 |
| Distant diagonal scroll | 1.15785 | 5.24995 | 4.53 |
| Replace visible row | 1.15900 | 2.22995 | 1.92 |
| Resize visible column | 1.91550 | 2.39800 | 1.25 |
| Sort | 18.96220 | 43.59355 | 2.30 |

Sorting allocates less but remains slower. Scope is synchronous UI work and verified
layout settlement, not GPU completion or frame rate. The flat workload does not
isolate multi-observer typed-expression fan-out. Matched focused allocation tests
do not replace this failed gate, and no controlled whole-grid before/after speedup
is established by comparisons against older jobs on other hosted machines.

## Remaining API and platform acceptance

The unchanged auditor records **1,845 baseline / 1,833 target declarations, 1,010
exact normalized matches and 835 missing-or-different baseline entries**. Dependencies
fully resolve and strict self-comparison has zero differences. Supplemental metadata
and every raw difference remain available; no Core equivalence is automatically
accepted and no declaration count is a feature-completion percentage.

Required before ready: finish actual member/signature/inheritance/attribute
contracts and explicitly tested Core mappings; complete remaining callback and
mixed-mutation review; meet unchanged native performance budgets with broader
hierarchy/variable-height workloads; extend physical input/drag, Unicode/IME,
external screen-reader and cross-head scaling/lifecycle verification.

All authored implementation is pushed. Local shell/Python execution returned
ClientError, so unrelated unknown local files could not be inspected. No assertion,
trimming diagnostic, ownership rule, rendering setting or budget was weakened.
