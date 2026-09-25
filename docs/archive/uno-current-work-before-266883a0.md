# Current Uno completion checklist

Updated 2026-09-25 UTC. Tested implementation **ab5f30a6**.
**Full API, behavioral and performance parity are not established.**

[Tri-state sorting guide](uno-tristate-sorting.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-ab5f30a6.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-ab5f30a6.md)

## Revisions and architecture

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
Starting head: `bf703838bcaa16cbc059f3a7a339b528777a050b`.
Implementation: `ab5f30a6d00bfa33332d750b6e7e1ea2c6f1af4f`.
Tested tree: `df3410e3bf492d312abe82d02b9e5dce274cfcb6`.
CI merge checkout: `2897938891625862b6b8f3af1829d1c9aae5d171`.
The reviewed local tree matches the GitHub tree and archived CI tree; all fourteen
changed files were also byte-compared against the downloaded CI source archive.
Final documentation is a separate follow-up; it changes no implementation,
test, build or workflow source and is not itself the tested implementation above.

Sources, rows, hierarchy and selection remain owned by the actual shared Core
assembly. No merge, public release, renderer change or Core copy is introduced.
The previous viewport/observer work is retained, not counted as new work here.

## Implemented opt-in sorting cycle

The previously inert `AllowTriStateSorting` creation option now reaches typed,
fluent, declarative, template, custom-adapter and expander view columns. The
opt-in cycle is ascending → descending → source order; another click restarts
ascending. Default two-state behavior is unchanged.

The third transition delegates through `TreeDataGridPresentation.ClearSort()`
to the existing shared Core implementation. It restores current collection order,
not a captured initial order, and preserves source/row/selection identity and
hierarchical expansion. Multiple views observe the source's shared sort state.
The typed custom adapter now forwards the live per-column sorting permission.

Header activation checks realization/request generations, current direction,
owner/presentation/column identity, indexed membership, enabled/resize state and
permission before dispatch. Application callback retirement or a newer sort must
not allow an obsolete transition to clear either source. No glyph is manually
published after the source callback. This is an opt-in functional extension,
not proof of an already-implemented Avalonia third-state interaction.

## Authored and executed coverage

Added **21 Uno unit cases**, **ten native scenarios** inside the existing
`builtin-column-comparison` suite, and **three browser pointer stages**. There
are no new registered native suites or direct-framework cases. New native checks
use loaded header controls and their public Invoke provider, observe actual Click
delivery, and assert source order, visible glyph and realized Core-model identity.
Browser checks remain separate external pointer/keyboard delivery, not scripted
.NET sort calls. The browser protocol now contains fifteen stages per scale.

Functional run **36169575190**, job **108185550222**, passed all fifteen stages:

| Gate | Result |
| --- | --- |
| Core / Uno / Avalonia cases | 228 / 882 / 536 passed |
| Sample-state / direct-framework cases | 41 / 171 passed |
| Total .NET cases | **1,858 passed, zero failed or skipped** |
| Registered native suites | **65/65 passed** |
| New native scenarios | **10/10 passed in isolated and sequential logs** |
| Strict API self-comparison | **1,847/1,847 exact, zero differences** |

Platform run **36169575203** validates Windows/macOS/Ubuntu builds and tests,
Linux X11 runtime and native NuGet consumers, native Windows App SDK build/package
publication, and published trimmed Chromium consumers. Browser status is recorded
in the exact execution checkpoint. Native Windows publication is not Windows OS
runtime execution; automated Chromium input is not physical-device/all-browser,
IME or external screen-reader acceptance.

Downloaded source, validation, Ubuntu-test and paired-performance archive digests
were independently recomputed. The source.tar digest also matched its manifest.
Python driver syntax, the fifteen-stage fake-page dispatch, invalid target bounds
and out-of-order readiness rejection were checked locally. No local .NET compiler
was available; .NET execution evidence comes from the unmodified CI checkout.

The old intermittent allocation failure is not diagnosed. All four unchanged
warm-layout allocation cases passed in the inspected Ubuntu run. No retries,
preexisting unit assertion changes or performance-threshold changes were used for
this implementation's initial functional validation.

## Independent gates still outstanding

Paired native performance run **36169575234** remains **failed** at the unchanged
**1.10** budget. Latest median time/allocation ratios (Uno / Avalonia):

| Workload | Time | Allocation |
| --- | ---: | ---: |
| Horizontal scroll | 5.232 | 1.063 |
| Vertical scroll | 2.382 | 1.957 |
| Distant diagonal scroll | 3.369 | 1.928 |
| Visible-row replacement | 1.539 | 2.668 |
| Visible-column resize | 1.349 | 1.516 |
| Sort | 1.456 | 0.620 |

This is not a controlled before/after experiment for this feature. The raw
artifact retains p95, allocations and settlement data. These measurements are
synchronous UI/source/layout work, not GPU completion or frame rate.

API audit: **1,845 baseline / 1,847 target declarations; 1,011 exact matches;
834 missing-or-different baseline / 836 additional-or-different target entries**.
Dependencies resolve. This feature adds five public declarations; it does not
reduce the original 834 baseline differences or silently waive native/Core mappings.

- [ ] Complete genuine API/signature/inheritance/attribute contracts and explicit Core/native mappings.
- [ ] Explain the intermittent allocation observation and continue custom-source callback review.
- [ ] Meet unchanged performance budgets; extend hierarchy/variable-height workloads.
- [ ] Complete physical input/drag, Unicode/IME, external accessibility and cross-head acceptance.
