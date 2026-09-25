# Current Uno completion checklist

Updated 2026-09-25 UTC. Tested implementation **f8ff3b7c**.
**Full API, behavioral and performance parity are not established.**

[Typed column guide](uno-typed-column-contract.md) ·
[Dispatch and projection review](uno-typed-column-review-2026-09-25.md) ·
[Exact execution checkpoint](uno-ci-checkpoint-f8ff3b7c.json) ·
[Previous checklist preserved unchanged](archive/uno-current-work-before-f8ff3b7c.md)

## Revisions and architecture

PR #26 remains draft on `codex/uno-core-port`, based on master `3ca47316`.
Sources, rows, hierarchy and selection remain owned by the actual shared Core
assembly. No copied Core state, merge or public release was introduced.

Starting head: `01ecd1f5c8cd1a25e892c1a81734df8cabf22f0e`.
Tested implementation: `f8ff3b7cf345d42752ac19963b54f4f929ab3885`.
Tested tree: `b0941c6d337049b09bee2532fc43ad8ac5f0880a`.
CI merge: `9c5e85e1da07e76f6cf7253e3b55739c27c90675`, with the same tree.
The final documentation checkpoint was prepared after all implementation platform
jobs, including actual published-browser execution, completed successfully.

| Commit | Work in this continuation |
| --- | --- |
| `550b8203` | Independent typed/legacy contracts, factory dispatch and initial pixel geometry |
| `8c5eccb2` | Legacy projection, ownership, allocation and constrained-width regression tests |
| `f8ff3b7c` | Exact native collection interface mappings and actual legacy factory consumer |

Incoming typed-column implementation and automation callback work are preserved
and revalidated, not counted again as authored work here.

## Corrected compatibility failures

`IColumn<TModel>` and the legacy `ICellColumn<TModel>` are independent interfaces.
Library bases implement both and route typed creation through the actual legacy
factory slot. Reimplementing a legacy explicit factory therefore no longer remaps
typed creation to an unrelated public override. Existing untyped virtual factories
and built-in/custom comparison policies remain honored. Core sorting is unchanged.

`ColumnList<TModel>` keeps original columns in its mutable/native collection.
Typed columns keep their exact identity in the read-only typed projection;
legacy-only factories receive stable, lazy, non-owning facades. Exact untyped
indexer/enumerator implementations prevent variant interface dispatch from leaking
those facades into native layout. Duplicate entries share a facade and preserve
subscription counts. Facades forward caller handlers without changing sender,
never subscribe on their own, and use a weak-key cache that does not retain removed
column/facade pairs. Clearing the list does not dispose caller-owned columns.

Value columns expose configured pixel widths before first layout, instead of NaN.
Committed constrained widths remain authoritative, including zero. Auto/star
columns remain unmeasured. No natural measurement is faked and no Core definition
is mutated. The original 130-pixel list-estimate test passes unchanged.

Cross-framework Auto checks now compare native sizing semantics: all unit flags
and the unit enum must agree, and pixel/star numeric values must still match
exactly. Auto's ignored numeric payload differs between Avalonia and WinUI. This
specific assertion correction is documented; no global API normalization waiver
or blanket claim that every assertion was unchanged is made.

## Coverage and executed consumers

This continuation adds **14 Uno unit cases**, **nine direct-framework cases**, and
**one native legacy factory/projection scenario** inside the existing
`builtin-column-comparison` suite. The registered suite count remains 65.
The preceding typed API's twelve Uno and eleven differential cases, and the
incoming automation action cases, are not newly authored coverage here.

Tests cover explicit factory dispatch on native and compatibility subclasses,
every collection interface route, native/typed identity, duplicate entries,
forwarded events, factory exceptions, caller-owned lifetime, weak-cache release,
mutation detection, warmed allocation-free indexing, and initial/committed geometry.
The consumer creates independent native cell values over the exact same Core row
through both interfaces and verifies explicit factory counts, facade identities
and complete subscription cleanup. It runs in isolated native, sequential native,
NuGet package and published trimmed-browser consumers. The enclosing scenario also
checks real rendering, native editing, Core sorting and distant virtualization.

The intermediate `8c5eccb2` checkpoint exposed the additional native-interface
identity failure. `f8ff3b7c` corrects the product without removing that assertion.
Earlier failing intermediate runs are not counted as completed final acceptance.

## Completed functional and platform evidence

[Functional run 36143814796](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36143814796)
passes all fifteen stages on unchanged committed sources: **1,786 .NET cases**,
zero failed/skipped; **65/65 native suites**; sequential native checks; both native
sample builds with zero warnings/errors; and all five Activity Monitor sections
plus lifetime checks. Core/Uno/Avalonia/sample/direct-framework totals are
228/810/536/41/171. Python audit/metadata-semantic/normalization checks are 12/39/57.
Artifact `10868642526` retains the complete evidence and raw API inventories.

[Platform run 36143814749](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36143814749)
completed **all six jobs successfully**: Windows/Linux/macOS builds and tests,
Linux native package execution, Windows App SDK build/package publication, and
trimmed browser publication and actual execution. All four browser routes passed:
showcase, Activity Monitor and browser-dispatched pointer/keyboard input at scales
1 and 2. Browser artifact `10869228311` preserves results and published consumers;
native artifact `10868268081` preserves native and package-consumer evidence.

Windows App SDK publication is not Windows OS runtime execution. Pinned browser
and browser-dispatched input checks are not physical-device, all-browser, IME,
external screen-reader or universal DPI acceptance. Binary compatibility with all
third-party precompiled factories is not certified by these source/runtime tests.
Repository Build, contract reproducibility, published trimmed binding, reference
packs and dependency snapshot workflows also passed. Exact IDs and checksums are
recorded in the execution checkpoint.

## Independent performance gate remains failed

[Paired run 36143814751](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36143814751)
completed both builds and all four alternating AB/BA hosts, with valid measurements,
but still fails the unchanged **1.10 median timing/allocation ratio budget**.

| Workload | Avalonia median ms | Uno median ms | Time ratio | Allocation ratio |
| --- | ---: | ---: | ---: | ---: |
| Horizontal scroll | 0.53155 | 2.70585 | 5.090 | 1.063 |
| Vertical scroll | 1.28570 | 2.72845 | 2.122 | 1.957 |
| Distant diagonal scroll | 2.64200 | 9.40840 | 3.561 | 1.928 |
| Replace visible row | 2.59405 | 3.92030 | 1.511 | 2.668 |
| Resize visible column | 4.84590 | 8.28055 | 1.709 | 1.516 |
| Sort | 43.68275 | 71.76715 | 1.643 | 0.620 |

Artifact `10868586955` retains raw allocations, p95 and settlement data. This is
synchronous UI/layout work and verified settlement, not GPU completion, frame rate
or all-feature performance. It is not a controlled before/after experiment of
these changes. No whole-grid speedup is claimed. Focused allocation-free indexing
does not replace the failed independent gate.

## Audit and remaining acceptance

The unchanged auditor records **1,845 baseline / 1,839 target declarations,
1,011 exact normalized matches, 834 missing-or-different baseline and 828
additional-or-different target entries**. Dependencies resolve; strict self-comparison
has no differences. Supplemental metadata is preserved: 13,465 / 15,787 entries,
3,838 exact, 9,627 missing-or-different and 11,949 additional-or-different. Counts
are not feature-completion percentages or automatically accepted Core equivalences.

Remaining gates: genuine member/signature/inheritance/attribute contracts and
explicit tested Core/native mappings; broader custom-source and mixed-callback
review; unchanged timing/allocation budgets with hierarchy/variable-height workloads;
and physical input/drag, Unicode/IME, external accessibility and cross-head
lifecycle/scaling acceptance. The port is not marked complete.

Local execution tools returned ClientError, so no local compilation, extraction
or byte-for-byte checkout verification is claimed. Builds/tests/runtime evidence
come from GitHub Actions on unchanged committed input; Git tree identity was
verified through the repository API. The existing Core ownership, trimming
settings, renderer options and performance threshold were preserved.
