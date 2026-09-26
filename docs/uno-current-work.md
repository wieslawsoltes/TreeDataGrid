# Current Uno completion checklist

Updated 2026-09-26 UTC. Tested implementation **23f033ee**.
**The six concrete preceding review findings are fixed. Full port/API/performance parity is not established.**

[Review fixes and contracts](uno-pr-review-fixes-2026-09-26.md) ·
[Exact execution checkpoint](uno-review-fixes-checkpoint-23f033ee.json) ·
[API preservation and evidence guide](uno-api-preservation.md) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-review-fixes.md)

## Scope and exact revisions

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
Starting head for these corrections was `5be0e5638cfd6be70ffe88839b695b790f013235`;
its preceding declarative-ownership/browser-validation work is retained, not counted
again as new work. Actual shared Core still owns sources, rows, hierarchy and selection.
This correction changes two Core ownership implementations and two native view files;
it does not duplicate source state or change the original Avalonia implementation.

Tested implementation: `23f033eef3d1a6243f130d14b62ccc9e82bf7359`.
Tested tree: `314ba8312579d897e239f2dcdb515cba0ffa3fc2`.
CI merge: `774ea26593d4f303326aa9fbd29b6f60c0321e23`, with that identical tree verified
through the Git commit API. The final documentation checkpoint follows completed
functional and all six platform jobs; it does not change the tested runtime/tools/tests.

## Six reviewed findings closed

| Finding | Retained correction | Verification |
| --- | --- | --- |
| Core row cleanup skipped after throwing model-event removal | Retire fields first; attempt model, expansion, children-reference and descendant cleanup independently; preserve original/ordered exceptions | Sixteen failure combinations and two reentrant observation cases |
| Materialized-row reset stops at first throwing sibling | Detach old rows/maps before callbacks, clean every sibling, preserve reentrant replacements, publish committed Reset even after cleanup failure | Four sorted/unsorted cleanup and nested-reset cases |
| Real factory captures obsolete comparison delegate | Match Avalonia's initially-present live callback and initially-absent snapshot behavior | Twelve paired public-factory cases across text, Boolean, nullable Boolean and template keys |
| Native style/column refresh resumes through stale ownership | Snapshot realized/pooled owners; validate presenter/source generations after callbacks; newer style request wins | Attached-grid source replacement, nested-style and same-presentation column-refresh callbacks plus edit/retirement recovery |
| API inventory consistency permits lost exports | Separate fail-closed full-record preservation gate, pinned reader source and real-inventory same-count negative control | Sixteen gate tests, one join test and rejected removed-export control in canonical CI |
| Historical evidence contains copied numbers/fingerprints | Recompute all four raw hosts; generate tables/manifest; verify committed numerical record and pinned artifact metadata | Eight renderer and eight committed-record checks; corrected checkpoint with original preserved |

The four runtime paths are `HierarchicalRow.cs`, `SortableRowsBase.cs`,
`TreeDataGridSourceExtensions.cs`, and `TreeDataGridRowsPresenter.cs`. Public
signatures, renderer, normalization rules and performance budget are unchanged.
Style/column configuration refresh now allocates bounded ownership snapshots. This
correctness tradeoff is not inserted into ordinary viewport scrolling and is not
presented as an optimization. Cleanup exceptions occur after structural retirement;
there is no rollback guarantee and old indexes must not be blindly retried.

## Executed regression proof

The exact same two new fixtures execute on original baseline and corrected runtime:

| Input | Core passed/failed | Factory passed/failed | Total passed/failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| Original `5be0e56` | 3 / 19 | 4 / 8 | **7 / 27** | 0 |
| Corrected `23f033ee` | 22 / 0 | 12 / 0 | **34 / 0** | 0 |

Only the new fixtures are copied into the baseline test projects; baseline runtime
source remains unchanged and tracked worktrees are checked clean. Failed initial
fixture compilation is preserved separately and is not used as negative-control
execution. Missing ModelIndexPath and older xUnit nullable-flow annotations were
fixed in the new fixture without changing its behavioral assertions.

Authored coverage: **34 .NET cases** (22 Core, twelve paired-framework, zero Uno),
**one composite loaded-native scenario** with three callback cases inside existing
appearance, and **33 Python test methods** (16 gate, one join, eight renderer, eight
committed-record). There are still **65 registered native suites**.

## Completed canonical functional evidence

[Run 36271915158](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36271915158),
job `108487367165`, passed all **nineteen** required stages on unchanged input:

| Test assembly | Passed |
| --- | ---: |
| Core | 250 |
| Uno | 1,046 |
| Original Avalonia | 536 |
| Sample state | 41 |
| Paired-framework | 305 |
| **Total** | **2,178; zero failed/skipped** |

**65/65 native suites**, sequential native execution and Activity Monitor's five
sections/lifetime checks pass. Both native sample builds have zero warnings/errors.
The final evidence join verifies actual TRX outcome records and counts, every
registered native suite's own pass marker, existing declarative/new appearance
review markers in isolated and sequential logs, compiled API preservation and a
rejected removed-export control. Merely generating internally consistent inventory
files is no longer enough to pass the review gate.

Report artifact: `10916381020`; source artifact: `10916116362`. Both are downloaded.
Full digests and provenance are retained in the checkpoint. Executed comparison
and fixture errors from earlier commits remain recorded rather than omitted.

## All platform jobs complete

[Run 36271915207](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36271915207)
completed **all six jobs successfully**: Ubuntu, Windows and macOS builds/tests;
Linux X11/native NuGet-consumer execution; Windows App SDK samples/package publication;
and browser build, pack, publish and actual published-consumer execution. Browser
execution is confirmed by the completed specialized job steps; its individual new
marker is not independently extracted from the archive in this continuation.

There is no pending macOS result at this checkpoint. Earlier incomplete statuses
remain historical rather than being used as present acceptance. Windows publication
is not Windows OS runtime execution. Automated browser/native scenarios do not
prove physical-device input, all-browser behavior, Unicode/IME, external screen-reader
or universal DPI acceptance.

## Declared preservation passes; full API gap remains

Both pinned source trees use the same production reader source
`d391854a1ef4c51f34b87837e286152b1430b3a6`. Full baseline/current declared inventories
preserve every previous raw target record, normalized shape and exact reference
match. There are **zero removed, rewritten, lost or added target declarations**.
The new same-count removed-export inventory control correctly exits nonzero.
Intentional reader/reference/floor changes require explicit review, not an automatic
waiver. The floor protects existing exports but does not certify all compatibility.

| Scope | Reference | Target | Exact | Missing/different | Additional/different |
| --- | ---: | ---: | ---: | ---: | ---: |
| Combined | 1,845 | 1,894 | 1,061 | **784** | 833 |
| Identical shared Core dependency | 590 | 590 | 590 | 0 | 0 |
| Independent UI assemblies | 1,255 | 1,304 | 471 | **784** | 833 |

Dependencies resolve, normalization collisions and strict-self-comparison differences
are zero. Supplemental metadata remains 13,465/15,916 entries, 3,887 exact, 9,578
missing-or-different and 12,029 additional-or-different. Raw differences remain
unwaived; dependency self-matches and declaration counts are not feature-completion
percentages or ABI/behavioral guarantees.

## Historical evidence repaired, not remeasured

[Repair run 36271912616](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36271912616)
passes all sixteen negative-control test methods, rederives all historical workloads
and verifies the committed numerical record. Artifact `10915383288` contains the
generated Markdown, JSON manifest, corrected checkpoint and artifact provenance.
All **51** original extracted files are counted and individually hashed in CI.

The corrected historical facts are **72/72 bytes** for both formatted-integer
workloads; live-integer candidate pass medians **54.736328125/53.9794921875 ns**;
the `d391854a` reader tree; 51 extracted files; and initial-failure artifact
**10913688104** with its verified metadata. The original erroneous checkpoint is
preserved under `docs/archive/uno-raw-text-checkpoint-before-evidence-repair.json`.
The corrected historical checkpoint keeps its original platform-status snapshot as
historical; this document records current execution separately. No new benchmark
or additional speedup is implied by correcting those records.

## Independent native performance remains failed

[Run 36271915168](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36271915168)
completed both framework builds and all four measured hosts but failed the unchanged
**1.10 median timing/allocation budget**. Artifact `10916219417` retains raw data.

| Workload | Uno/Avalonia time | Uno/Avalonia allocation |
| --- | ---: | ---: |
| Horizontal scroll | 2.366 | 0.695 |
| Vertical scroll | 2.179 | 1.955 |
| Distant diagonal scroll | 3.942 | 1.917 |
| Visible-row replacement | 1.607 | 2.668 |
| Visible-column resize | 1.524 | 1.516 |
| Sorting | 1.371 | 0.620 |

These are synchronous UI/layout and settlement measurements, not GPU completion,
frame rate or a controlled before/after performance claim for these fixes.

## Remaining acceptance and publication boundary

The six concrete preceding review findings are addressed; an exhaustive absence of
all other bugs is not proved. Remaining API/native/Core/inheritance contracts,
native layout/sorting costs, hierarchy/variable-height workloads, broader callbacks,
the earlier intermittent allocation observation, physical input/drag, Unicode/IME,
external accessibility and cross-head runtime acceptance remain open.

The final documentation-only checkpoint follows completed implementation validation.
It does not change tested source or imply new-head required checks are green. PR #26
remains draft; no merge or public release. No original assertion, normalization,
branch-protection rule or performance threshold is weakened.

Local execution tools returned ClientError; execution and extracted-file verification
ran in GitHub Actions. Logs, returned artifact metadata and Git tree identities were
inspected. No local compilation, archive extraction or independently recomputed local
archive hash is claimed.
