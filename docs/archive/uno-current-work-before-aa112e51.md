# Current Uno completion checklist

Updated 2026-09-26 UTC. Tested implementation **081f62ac**; product **18323cb8**.
**Full API and performance parity are not established. macOS validation remains queued.**

[Binding reassignment review](uno-binding-reassignment-review-2026-09-26.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-081f62ac.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-081f62ac.md)

## Actual branch history and ownership

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
The actual shared Core assembly owns sources, rows, hierarchy and selection.
No duplicate Core state, merge or public release was introduced.

This continuation starts at `c392b11d531b9255beddfa7d1faa907fe1dd943f`, not the stale
`93af6508` checkpoint. Earlier committed work is preserved and revalidated:

| Earlier commit | Previously implemented work | Raw declared gap |
| --- | --- | ---: |
| `361dd48b` | Recover nineteen portable declarations and JIT accessor continuation | 806 |
| `920e2f9e` | Nine generic presenter declarations; safe member-reader/root-link reuse | 797 |
| `c392b11d` | Seven specialized-cell declarations; independent-expression instruction caching | 790 |

These are catch-up records, not newly authored declarations in this continuation.
The recovery distinguishes original local evidence from remote execution. The new
runtime change does not change the exported declaration inventory.

New product: `18323cb8adeafc4f931efdd030d7ec50282fab52`.
Tested implementation plus diagnostic: `081f62ac02065d4db7ee697a0ad69b2060cdacf1`.
Tested tree: `a217acd1c3e33b62ec778db993308f60bb3cdcd0`.
CI merge: `0e08e0a4d568a4e822ec09a3b95a029f319b3e2b`, with the same tree.
Tree identity was verified through the Git commit API, not local reconstruction.

## Implemented instruction reuse

TypedBinding Read and Write assignments preserve an existing immutable instruction
only when the assigned delegate is reference-identical. Equal-but-distinct delegates
still invalidate it. The Links setter preserves a same-array snapshot, but GetPlan
still checks every element against the owned snapshot. In-place mutation and null
entries cannot bypass validation; different arrays retain immediate invalidation.

Every assignment still advances CellRevision, including same/null assignments with
no cached plan. Public modes, required reader/writer/link rules, fallback semantics,
root ownership, observation and cleanup are unchanged. Expressions retain independent
mutable state and disposal. No global cache, new runtime field, native measurement
shortcut or renderer change was added. Concurrent descriptor/array mutation is not
made safe by this UI-thread/reentrant ownership policy.

Authored coverage: **eighteen Uno unit cases and one composite native-consumer
scenario**, zero new paired-framework cases and zero registered native suites.
The focused scenario uses actual native TextCell models and independent roots,
writes, validation, repair and cleanup. Its enclosing value-column-base suite
retains loaded rendering, editing, sorting and virtualization assertions. No
preexisting test assertion changed. No executed failing-baseline claim is made.

## Completed functional validation

[Functional run 36242946920](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36242946920),
job `108406964357`, passed all fifteen stages on unchanged committed sources:
**228 Core + 951 Uno + 536 Avalonia + 41 sample-state + 234 paired-framework =
1,990 .NET cases**, zero failures/skips; **65/65 native suites**; sequential native
execution; both native sample builds with zero warnings/errors; and five Activity
Monitor sections plus lifetime checks. Existing audit-reader and Python-integrity
checks also pass. Inventory-generation success is not cross-framework compatibility.

Report artifact `10906287213` retains full TRX, logs and raw API inventories.
Source artifact `10906881911` preserves the exact validation input.

## Platform results and documentation CI boundary

[Platform run 36242946789](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36242946789)
has **five completed successful jobs out of six**. Windows and Ubuntu builds/tests,
Linux native/NuGet consumers, Windows App SDK builds/package publication, and the
published-browser build/pack/publish/execution job all passed.

The new `UNO_RUNTIME_BINDING_REASSIGNMENT_PASSED` marker was inspected in sequential
native and native NuGet-consumer logs. Browser execution is established by the
completed execution step; its individual new marker was not independently extracted
from the browser artifact. Native artifact: `10906213502`; browser: `10906124211`.
The zero-warning statement above concerns the two functional native sample builds,
not a blanket claim about all platform steps.

**macOS job `108406988193` is still queued and has no executed steps.** It is not
reported as passed, failed, skipped or replaced by earlier-revision evidence. No
retry or cancellation was requested. Therefore the full platform workflow has not
completed successfully at this checkpoint.

The existing Uno pull-request workflow cancels an older run when a newer run for
the same PR starts. The final four-path documentation-only commit uses `[skip ci]`
to avoid replacing this queued product-validation run. Product and test commits
were not skipped. No workflow definition, branch protection, acceptance assertion
or performance threshold is changed. This does not make new-head checks green;
GitHub may leave skipped required checks pending. No merge is requested. See
[GitHub's workflow-skip behavior](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/skip-workflow-runs).

Repository Build, trimmed binding, contract reproducibility, dependency snapshot
and reference-pack workflows passed on the tested implementation. Native Windows
publication is not Windows OS runtime acceptance; browser automation is not physical
input, universal-browser, IME or external screen-reader acceptance. The earlier
intermittent allocation observation remains unexplained.

## Controlled allocation gain and adverse timing controls

[Comparison run 36242946975](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36242946975)
uses exact baseline/candidate revisions and one identical external public harness
in ABBA process order. Four hosts completed twelve workloads, with thirty measured
batches per workload/revision. Tiered compilation and ReadyToRun were disabled
identically for both sides. This is not default-runtime or native-frame acceptance.

Reassigning all three identical inputs reduces expression-creation allocation
**528 to 280 bytes (-46.97%)** and the pooled median **129.69 to 89.48 ns (-31.01%)**.
The observed-expression and native TextCell-model paths save the same 248 bytes,
with medians decreasing 10.99% and 10.81%. Repeated single reader/writer/link
assignments also allocate less. Stable, genuinely changed, transient and direct
controls show unchanged allocation medians.

**Adverse observations are retained:** stable-control median increases **13.12%**,
mutated links **4.09%**, transient descriptors **1.91%**, and direct construction
**3.26%**. Replaced-link p95 increases from 181.57 to 219.78 ns. These results are
not dismissed as proven noise or described as confidence intervals. The narrow
allocation benefit is retained with explicit timing tradeoffs, not a universal
speedup or complete causal claim. No samples/workloads were discarded and no
comparison retry was requested.

Artifact `10905764930` preserves all raw samples, fingerprints, p95 and per-pass
medians. The review includes all twelve workloads. The managed diagnostic is not
native-control layout, full-grid sorting, startup, GPU completion or frame rate.

## Current API audit: unchanged by this patch

| Scope | Reference | Target | Exact | Missing/different | Additional/different |
| --- | ---: | ---: | ---: | ---: | ---: |
| Combined inputs | 1,845 | 1,888 | 1,055 | 790 | 833 |
| Identical Core dependency | 590 | 590 | 590 | 0 | 0 |
| Independent UI assemblies | 1,255 | 1,298 | 465 | 790 | 833 |

The 790 differences comprise 208 changed declarations at the same identity,
43 absent exported type identities, 316 members of absent types, eighty members
not declared on matched types and 143 overload/parameter-identity differences.
Shared Core matches are not independently ported UI contracts. Dependencies resolve,
normalization collisions are zero and no raw difference is waived. Supplemental
metadata: 13,465/15,880 entries, 3,887 exact, 9,578 missing-or-different and 11,993
additional-or-different. Strict self-comparison matches all 1,888 declared and
15,880 supplemental records with zero differences. Counts are not feature-completion
percentages, ABI guarantees or automatically accepted native/Core mappings.

## Independent native performance remains failed

[Run 36242946887](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36242946887)
completed both builds and all four measured hosts but failed the unchanged **1.10**
timing/allocation gate. Artifact `10906717819` preserves its raw measurements.

| Workload | Uno/Avalonia time | Uno/Avalonia allocation |
| --- | ---: | ---: |
| Horizontal scroll | 2.340 | 0.695 |
| Vertical scroll | 1.925 | 1.957 |
| Distant diagonal scroll | 3.697 | 1.917 |
| Visible-row replacement | 1.451 | 2.668 |
| Visible-column resize | 1.638 | 1.516 |
| Sorting | 1.458 | 0.620 |

These are synchronous UI/layout and settlement measurements, not GPU completion,
frame rate or a controlled before/after native gain from this patch. Do not combine
the independent gate with the managed diagnostic into an extra speedup claim.

Remaining: actual API/signature/inheritance/native-type contracts; measured native
text/layout, lifecycle and sorting costs; hierarchy/variable-height workloads;
broader callback and intermittent-allocation investigation; physical input/drag,
Unicode/IME, external accessibility and cross-head lifecycle/scaling. Full parity
is not established. No original assertion, Core rule, normalization policy, trimming
diagnostic, rendering option or performance budget was weakened.

Local execution tools returned ClientError. Tests and benchmarks ran in GitHub
Actions. Completed logs, artifact metadata and repository tree identities were
inspected; no local compilation, archive extraction, independently reconstructed
source tree or locally recomputed archive digest is claimed in this continuation.
