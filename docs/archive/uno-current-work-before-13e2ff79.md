# Current Uno completion checklist

Updated 2026-09-25 UTC. Tested implementation **266883a0**;
measured runtime optimization **14be7480**.
**The audit is more rigorously verified and horizontal recycling is faster;
full API, behavioral and performance parity are not established.**

[API audit correctness and interpretation](uno-api-audit-review-2026-09-25.md) ·
[Avalonia comparison and performance evidence](uno-performance-audit-review-2026-09-25.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-266883a0.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-266883a0.md)

## Revisions and architecture

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
The actual shared Core assembly owns sources, rows, hierarchy and selection.
This review introduces no copied Core state, renderer change, merge or public release.

Starting head: `889bfde669e430e507d8aeb16826e131cbf42226`.
Audit reader/accounting: `d42c3e3f9d53167948c180444569787cfca4aaa2`.
Runtime optimization: `14be7480c26962973ec921fabd3e3f5a168344a6`.
Audit review integrity: `da989f8df53c79e04cdde942f375b437e9c85afe`.
Tested implementation: `266883a04a7d6e11cd6c41eef16a69e0f1e8cadf`.
Tested tree: `0d03c332b872c94f943763cf0c2d1fa7925664e7`.
CI merge: `56eaa26c064dbabc6ac37c459e74a39490e672b0`, with the identical tree
verified through the Git commit API. The runtime library is unchanged between the
measured `14be7480` and tested `266883a0`; intervening changes are audit tooling,
comparison/regression workflows and a new fixture compilation correction.
Final documentation follows completed platform execution and changes only docs.

## What the 834 audit differences actually mean

The number is reproducible **declared metadata**, not 834 missing features. It
includes changed native signatures, moved Core owners, inherited rather than
redeclared members, changed bases/modifiers and generated exports. No raw difference
was removed by this review and no candidate mapping was accepted automatically.

| Scope | Baseline | Target | Exact | Missing/different | Additional/different |
| --- | ---: | ---: | ---: | ---: | ---: |
| Historical all-input audit | 1,845 | 1,847 | 1,011 | 834 | 836 |
| Shared Core on both sides | 590 | 590 | 590 | 0 | 0 |
| UI assemblies only | 1,255 | 1,257 | 421 | 834 | 836 |

The same Core bytes contribute 590 self-matches, not independent Uno UI coverage.
All scopes reconcile and normalized collisions are zero. Exact declarations are
not a feature-completion percentage. The 834 split into 214 changed declarations
at the same identity, 43 absent exported owners, 316 members of absent owners,
118 undeclared members on matched types and 143 overload/parameter differences.
The absent-owner group includes 351 Core-relocation candidates and eight generated
exports. Separately, 148 raw differences have same-name inherited candidates;
these overlap the categories and are not subtracted or treated as equivalences.

The audit now emits `scope-accounting.json`/`.md` beside complete raw and supplemental
inventories. Supplemental counts remain 13,465 / 15,813, with 3,838 exact matches,
9,627 missing/different and 11,975 additional/different. Dependency resolution and
strict self-comparison pass. The guide explains source-compatibility, declaration,
inheritance, metadata and behavioral boundaries instead of hiding mismatches.

## Actual audit defects corrected and reproduced

The full declared reader used JSON's default numeric serialization for constants,
which threw on NaN/infinity even though a supplemental helper handled them safely.
The full path now uses the existing exact scalar representation, preserving
nonfinite values and signed-zero bits. Twenty-seven emitted-PE fixtures exercise
actual `Surface.Read`, not only helpers or a self-comparison of product binaries.

The Python reviewer previously validated only a candidate's normalized string,
allowing fabricated remaining metadata to pass as target evidence. It now requires
the entire verified target record. Both producer/reviewer detect ambiguous normalized
identities before sets/dictionaries can hide declarations. Malformed records,
duplicate candidates and boolean/floating-point counts are explicitly rejected.
Twelve original review tests remain unchanged; eleven integrity tests were added.

[Original/corrected regression run 36182461885](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36182461885)
executes exact original `889bfde6` and corrected `da989f8d` tooling on identical
compiled constant-bearing inputs. Original audit exits 2; corrected strict audit
exits 0 with no differences. Original Python review passes its 12 existing tests
but seven new test methods fail assertions and two raise errors; the corrected
review passes all 23. The original output's 12 assertion failures include subtests,
not twelve independent failing methods. Both old/new logs are retained in artifact
`10884832987`. No original tooling source was patched for this proof.

## Avalonia source review and runtime optimization

The performance guide compares actual Avalonia presenter code against Uno's bounded
realization, native container/model recycling, natural-width measurement, viewport
lookup, delegate caches and Core data ownership. Many reference techniques are
already implemented and are not recounted as new work. Avalonia's `IsMeasureValid`
plus constraints cannot safely be replaced by a size-only Uno cache: native text,
font, theme, wrapping, template and descendant changes can invalidate measurement
without changing those keys. Dirty native measurement is not skipped.

Baseline profile [36179566554](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36179566554)
records both original frameworks, 600 valid operations each, with exact pinned
sources and tracing tools. Visibility and retirement paths motivated the change.
Whole-process sampled residence includes startup, waits and other threads; it is
not per-operation CPU or allocation attribution. Artifact: `10883881462`.

`TreeDataGridCellsPresenter.RecyclingVisibility.cs` preserves the visibility of a
parented cell reused during its own synchronous horizontal measure. Previously it
could collapse the native subtree just before making it visible again. Model
retirement, binding, editing, native measurement and lifecycle callbacks still run.
A finally path finishes every unused visual decision before returning, including
exceptions and reentrant source retirement. It never collapses a newer realized
identity. Ordinary removals are not deferred across dispatcher turns. The retained
pending set has a per-presenter memory cost; this is not a zero-allocation grid.

One new loaded-native scenario inside `cell-lifecycle` verifies stable controls over
five horizontal windows, actual text/Core identity, zero same-pass rebind collapse,
bounded realization, surplus-cell hiding, callback retirement and recovery. Marker:
`UNO_RUNTIME_HORIZONTAL_RECYCLING_VISIBILITY_PASSED`. No new xUnit case or registered
native suite is claimed. The initial new fixture used the wrong CacheLength owner;
`f3dc28a5` corrects it to `RowsPresenter` without changing library code or assertions.

## Controlled performance evidence, including regressions

[ABBA comparison 36181683208](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36181683208)
executes baseline `889bfde6` and runtime candidate `14be7480` on one runner in
baseline/candidate/candidate/baseline order, alternating frameworks within each pass.
All 16 framework hosts complete. Each Uno operation has 100 samples per revision;
all eight ordered Uno frame sequences have identical geometry and row/cell counts.
Artifact `10884114103` preserves every raw sample and original 1.10 gate result.

| Operation | Baseline ms | Candidate ms | Time change | Allocation change |
| --- | ---: | ---: | ---: | ---: |
| Horizontal scroll | 1.76345 | 0.87880 | -50.17% | 22,528 to 15,688 bytes (-30.36%) |
| Vertical scroll | 1.88985 | 1.97305 | +4.40% | 191,904 to 196,608 bytes (+2.45%) |
| Distant diagonal scroll | 7.03115 | 6.50295 | -7.51% | Unchanged |
| Visible-row replacement | 2.37080 | 2.26040 | -4.66% | Unchanged |
| Visible-column resize | 3.46815 | 3.29395 | -5.02% | Unchanged |
| Sort | 45.36360 | 42.34140 | -6.66% | Unchanged |

These are pooled medians, not statistical confidence intervals. Horizontal p95
improves 3.0243 to 1.1984 ms and settlement median 1.98690 to 1.11330 ms. Diagonal
p95 worsens 9.787 to 11.238 ms despite its lower median. Vertical timing/allocation
regressions and per-pass variance are retained, not explained away by a favorable
separate run. The complete allocation-stack cause is not established. Headers,
hierarchy and variable row heights are not covered by this fixed-text benchmark.

## Completed current implementation validation

[Functional run 36182466684](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36182466684)
passes all fifteen stages on unchanged committed sources: **1,858 .NET cases**
(228 Core, 882 Uno, 536 Avalonia, 41 sample-state, 171 direct-framework), zero
failures/skips; **65/65 native suites**; sequential native smoke; both native builds
with zero warnings/errors; and five Activity Monitor sections plus lifetime checks.
Tooling passes 27 full-reader, 39 semantic, 57 normalization and 23 Python checks.
Report artifact `10885255434`; source artifact `10884194747`.

[Platform run 36182466644](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36182466644)
passes all six jobs: Windows/macOS/Linux builds/tests, Linux native/NuGet execution,
Windows App SDK build/publication and published trimmed-browser consumers. The new
native visibility marker appears in both sequential and NuGet-consumer logs. Native
artifact: `10885152456`.

Browser job `108228075921` passes showcase, Activity Monitor and all fifteen external
input stages at both device scales 1 and 2, using Chromium 143.0.7499.4/Playwright
1.57.0. The new scenario is wired into the passed showcase; its individual browser
marker was not separately extracted. Browser artifact `10885643712`. Existing
browser UnoSplashScreen warnings remain. Native Invoke/browser-driver success is
not physical-device, all-browser, IME, external accessibility or universal DPI
acceptance; Windows publication is not Windows OS runtime execution.

Build, dependency snapshot, reference packs, trimmed binding and reproducibility
also pass. No retry was requested for this final implementation. The previously
reported intermittent allocation observation remains undiagnosed.

## Independent budget and remaining work

[Current paired run 36182466685](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36182466685)
completes both builds/all four hosts but **fails the unchanged 1.10 budget**.
Time ratios Uno/Avalonia are horizontal 3.069, vertical 2.035, diagonal 3.497,
replacement 1.618, resize 1.565 and sort 1.850. Horizontal allocation is 0.741x the
reference; other allocations and timing still exceed budget. Artifact `10884334633`.
Do not combine different-runner numbers into additional before/after claims.

Required next acceptance: genuine API contracts and explicitly tested Core/native
mappings; vertical/diagonal lifecycle/allocation and source-sort work; unchanged
budgets with hierarchy/variable-height workloads; the intermittent allocation
investigation; physical input/drag, Unicode/IME, accessibility and cross-head
lifecycle/scaling. All optimizations and full Avalonia-equivalent performance are
not marked complete. No existing assertion, Core implementation, renderer setting,
trimming diagnostic, workload or performance threshold was weakened.

Local execution tools returned ClientError. .NET/Python/native/browser execution
was performed in GitHub Actions; logs, artifact metadata and Git tree identities
were inspected. Archive digests below are runner/API observations, not locally
recomputed hashes or independently reconstructed source archives.
