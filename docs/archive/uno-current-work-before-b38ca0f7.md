# Current Uno completion checklist

2026-09-26 UTC. Tested implementation **b30dfd65** on `codex/uno-core-port`, PR #26.
**Draft: full API and performance parity remain open. No merge or release.**

[Scalar construction and allocation review](uno-scalar-subscription-review-2026-09-26.md) ·
[Nullable-format regression correction](uno-nullable-formatting-review-2026-09-26.md) ·
[Exact CI checkpoint](uno-ci-checkpoint-b30dfd65.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-b30dfd65.md)

## Actual starting point and commits

The actual starting head was `31f9857801a890e8a760aaafc318d383daacc1ff`.
It already contained the previous interrupted numeric-formatting optimization and
its tests, benchmark and workflow. Those additions are preserved and measured,
not counted as newly authored here. Earlier shared-Core, API, binding, expander
and native recycling work remains intact.

| Commit | New work in this continuation |
| --- | --- |
| `43bb25ad` | Raw/typed scalar constructor retirement, direct-owner observers, 32 regressions and native consumer |
| `9b2a4512` | Same-test baseline proof and exact-source ten-workload scalar comparison |
| `926cb2ea` | Nullable double-boxing correction, seven tests and unchanged numeric controls |
| `b30dfd65` | Concrete observer-attachment overloads; repeat the complete comparison on the new source |

Tested tree: `3f02a9a1343dc4330e8f2869cb55baca4fa61169`.
CI merge: `dc80e45663bb6c4d6d15020bb3eee633686e91fa`.
Its tree is identical according to the Git commit API. Final documentation changes
no implementation, test or workflow input from this tested tree.

## Scalar constructor correctness and allocation

Raw TextCell/CheckBoxCell construction now releases subscription leases returned
after synchronous retirement instead of retaining them in already disposed cells.
Both raw and typed constructors retire an escaped cell when Subscribe or its initial
application callback throws. The original exception remains observable, and late
value/error delivery is inert. A publisher that throws without returning its lease
remains responsible for its inaccessible registration. Caller-owned observable
sources and shared Core rows are not disposed by these constructors.

Raw observers now store one cell owner instead of allocating two instance delegates
plus a wrapper. Concrete raw/typed attachment overloads preserve the same late-lease
and failure semantics. Typed value/error generations, completion behavior, initial
read versus live writeback, and the existing notification order remain unchanged.
No public signature, per-cell field, global cache, renderer or Core storage changes.
This is reentrant lifetime handling, not cross-thread synchronization.

The same 32 new tests execute against both products: original runtime **20 pass /
12 fail**, retained runtime **32 pass / zero fail**, with none skipped. Only the new
test fixture is overlaid on the baseline; its product sources remain unmodified.
The new public consumer uses raw/typed text and nullable-checkbox models, independent
owners, writeback, stale callbacks, cleanup and throwing initial equality. It uses
no private reflection. Its existing value-column-base suite retains loaded-grid
rendering/editing/sorting/virtualization assertions. Focused new checks use cell
models, not an additional independently attached visual tree.

## Corrected inherited numeric regression

The previous numeric candidate allocated **88 bytes rather than 64** per nullable
integer display. Runtime string/null tests could box a nullable value before the
composite fallback boxed it again. The corrected identity-format route checks the
generic type first. Reference-string identity is statically guarded; unknown value
types and Nullable<T> reach composite formatting once. Existing exact numeric and
exact-CultureInfo checks remain, preserving custom providers and live culture data.

Six new nullable value/allocation cases and one string-identity case cover the change.
The unchanged public comparison measures nullable display at **64 bytes per query**
in every corrected pass, equal to the original pre-numeric baseline. Existing numeric
argument-box reductions remain, but formatted result strings are still allocated.

Authored coverage totals **39 Uno cases**, **one composite native scenario**,
**zero new paired-framework cases** and **zero new registered native suites**.
Prior numeric tests are revalidated rather than recounted. No existing assertion
was removed or relaxed.

## Completed canonical functional validation

[Run 36263498845](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36263498845)
passed all fifteen stages on unchanged committed input:

| Suite | Passed |
| --- | ---: |
| Core | 228 |
| Uno | 1,016 |
| Original Avalonia | 536 |
| Sample state | 41 |
| Paired framework | 255 |
| **Total .NET** | **2,076; zero failed/skipped** |
| Registered native suites | **65/65** |

Sequential native execution, both native sample builds with zero warnings/errors,
Activity Monitor's five sections and lifetime checks, and the original API/integrity
stages pass. Report artifact `10912598983` and source artifact `10913257322` preserve
full input/logs/TRX/inventories. Counts were inspected in completed job logs, not
independently extracted from local TRX archives.

[Platform run 36263498816](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36263498816)
had **four completed successful jobs out of six** at this recorded snapshot:
Windows/Ubuntu builds/tests, native Linux/NuGet consumers, and Windows App SDK
build/package publication. Browser builds and packing passed; publication is still
running and browser execution is not yet accepted. macOS is queued with no steps.
Prior revisions' browser/macOS results are not substituted. Later job states may be
recorded in the PR description without rewriting this historical observation.

The new consumer is wired into the passed isolated/sequential/native-package routes;
its individual marker was not independently extracted from their archives in this
continuation. Windows publication is not Windows OS runtime acceptance, and browser
automation is not physical-device, universal-browser, IME or external accessibility
acceptance. Build, reference packs, dependency snapshot, trimmed binding and contract
reproducibility workflows also pass for the tested implementation.

## Performance: reproducible savings, explicit latency regressions

[Scalar comparison 36263496161](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36263496161)
uses exact starting/retained sources, one public harness and ABBA process order.
Four processes complete ten workloads with fifty samples per workload/revision under
inherited runtime defaults. Every sample, p95, per-pass median, checksum and release
check is preserved. Raw text creation falls **256 to 120 bytes (-53.125%)** and raw
checkbox creation **232 to 96 bytes (-58.621%)**. Each saves 136 bytes; typed and
constant controls have unchanged allocation.

Timing is mixed: raw read-only text median is **33.62% slower**, typed read-only
text **7.47% slower**, typed read-only checkbox **24.35% slower**, constant text
**4.85% slower** and constant checkbox **1.62% slower**. Raw editable text and both
raw checkbox medians improve, but that does not justify a universal speedup claim.
The earlier generic-helper candidate and all its adverse controls remain preserved
at run `36263106695`; the overload change is a distinct source experiment, not a
rerun that discards unfavorable measurements. Retention is for correctness plus
repeatable allocation reduction, with these latency tradeoffs explicitly accepted
for further review rather than called solved.

[Numeric comparison 36263270092](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36263270092)
uses the original pre-numeric baseline and corrected `926cb2ea`. All ten unchanged
workloads pass, with nullable allocation restored to baseline. All pooled timing
medians are lower in this run, but large baseline-pass variation in unchanged
provider/object controls prevents strong causal timing claims. The review includes
both pass-level examples and the prior adverse `31f98578` results.

These diagnostics measure scalar cell construction or warm display queries, not
native visual layout, whole-grid sorting, startup, frame rate or GPU completion.
Allocation figures are runtime/architecture measurements, not CLR size guarantees.
No confidence intervals, statistical significance or full causal attribution claimed.

## Independent native gate and API differences

[Native run 36263498796](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36263498796)
completed both builds and all four hosts but failed the unchanged **1.10** budget.

| Operation | Uno/Avalonia time | Uno/Avalonia allocation |
| --- | ---: | ---: |
| Horizontal scroll | 2.113 | 0.695 |
| Vertical scroll | 1.891 | 1.932 |
| Distant diagonal scroll | 3.460 | 1.917 |
| Visible-row replacement | 1.600 | 2.668 |
| Visible-column resize | 2.063 | 1.516 |
| Sorting | 1.552 | 0.620 |

These synchronous UI/layout and settlement results are not combined with isolated
managed benchmarks into a whole-grid before/after improvement claim.

The fresh API inventory remains **1,845 reference / 1,888 target / 1,055 exact /
790 missing-or-different / 833 additional-or-different**. Identical Core contributes
590 dependency self-matches, not independent UI-port coverage. Dependencies resolve,
normalization collisions are zero and strict self-comparison is clean across 1,888
declarations and 15,880 supplemental records. No normalization waiver or signature
was added. Full API, ABI and behavior parity are not proven by inventory collection.

Remaining work includes those actual native/Core/inheritance contracts, measured
native text/layout and source sorting, vertical latency, hierarchy/variable heights,
broader callbacks, earlier intermittent allocation observations, physical input,
Unicode/IME, external accessibility and cross-head lifecycle/scaling acceptance.

Local container and Python execution returned ClientError. Tests/benchmarks ran in
GitHub Actions; completed logs, returned artifact metadata and Git tree identities
were inspected. No local compilation, archive extraction, locally reconstructed
source tree or independently recomputed archive digest is claimed. The final
five-path documentation-only commit uses the existing `[skip ci]` practice to avoid
superseding active product validation. Product/test commits were not skipped; this
does not make documentation-head required checks green. No merge is requested.
