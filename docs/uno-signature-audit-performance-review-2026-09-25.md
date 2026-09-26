# Signature audit correctness and performance experiment review

Review date: 2026-09-25 UTC. Starting head `07c57154`; audit correction `15eec42d`;
measured candidate `d2ad0cb9`; candidate checkpoint `c52cf980`;
retained implementation after withdrawing the candidate: `13e2ff79`.

[Current checklist](uno-current-work.md) · [Execution checkpoint](uno-ci-checkpoint-13e2ff79.json) ·
[Previous audit review](uno-api-audit-review-2026-09-25.md) ·
[Previous Avalonia source/performance review](uno-performance-audit-review-2026-09-25.md)

## What the 834 means

The compiled assemblies independently reproduce **834 missing-or-different baseline
declared metadata shapes**. The count is correct for that declared-surface question;
it is not 834 absent features or a feature-completion percentage.

| Scope | Baseline | Target | Exact | Missing/different | Additional/different |
| --- | ---: | ---: | ---: | ---: | ---: |
| Historical all-input inventory | 1,845 | 1,847 | 1,011 | 834 | 836 |
| Identical shared Core dependency | 590 | 590 | 590 | 0 | 0 |
| Independent UI assemblies | 1,255 | 1,257 | 421 | 834 | 836 |

Identical Core bytes must not be counted as 590 independently ported UI contracts.
Scopes reconcile and there are no normalized collisions. The mutually exclusive
partition is 214 changed declarations at the same identity, 43 absent exported type
identities, 316 members of absent types, 118 members not declared on matched types,
and 143 overload/parameter identity differences. These sum to 834. The previous
review's 351 same-name Core-relocation candidates and eight generated exports are
triage, not accepted equivalences. The 148 inherited candidates overlap the partition
and must not be subtracted from it.

Both sides contain `ITextCell.TextAlignment`, for example, but its return types are
`Avalonia.Media.TextAlignment` and `Microsoft.UI.Xaml.TextAlignment`. Reporting that
signature difference is correct; reporting the property as entirely unimplemented
would be misleading. `ColumnBase<TModel>.Header` is not declared at the same target
owner but has an inherited candidate. Actual lookup, hiding, attributes, constraints
and behavior need consumer evidence before accepting an adaptation. Core relocation
must not be 'fixed' by creating a second source or selection engine.

All raw differences and candidate records remain. Namespace normalization was not
broadened and this continuation accepts no blanket native/Core/inheritance waiver.

## A real reader blind spot: signature custom modifiers

Ordinary C# symbol display omits CLR signature custom modifiers. The preceding
reader could report identical declared and supplemental strings for two emitted
signatures differing only in such a modifier. That is a false negative, even though
fixing it does not change the repository's current raw 834 declaration count.

`ApiSignatureMetadata` records ordered `modreq`/`modopt` sequences at return-type,
return-reference, parameter-type/reference, property, field and accessor sites;
array/pointer elements; function-pointer returns/parameters; and constructed generic
arguments, including arguments belonging to an enclosing constructed generic type.
Required versus optional, duplicates and ordering are preserved. Calling conventions,
varargs, hide-by-name metadata, unmanaged convention sequences, array rank and
SZARRAY status are included. Referenced modifiers and nested signature types are
checked for unresolved metadata.

This richer data belongs to the supplemental inventory. Private tooling corrections
do not rewrite stable C# declaration strings or conceal earlier differences. Output
schema 6 records `signatureMetadataChecks`. Normalization and candidate-integrity
checks remain intact. Supplemental entries gain signature detail; unchanged entry
counts do not imply unchanged content.

### Production-reader checks and negative control

`ApiSignatureChecks` adds **62 checks**, not 62 xUnit cases. Twelve raw PE signature
sites each verify that different optional modifiers have identical declared C#
shapes but different supplemental signatures, retain the actual modifier and resolve
its dependency. Further cases cover order, duplicates, required/optional distinction,
determinism, by-reference sites, cdecl/stdcall, missing modifier dependencies,
compiler-emitted init/volatile/readonly/function-pointer metadata, inherited members
and interface contracts. Fixtures are real managed PE images passed to the actual
`Surface.Read`; no fixture methods are executed.

A local negative control disabled only the new signature-append hook while retaining
the emitted regression fixtures. It failed at `return: supplemental signature
distinguishes modifier type`, after confirming identical declared display. Restoring
the hook passes all 62 checks. This is an instrumented negative control, not a claim
that an untouched historical executable ran the new harness.

The existing 27 full-reader, 39 semantic, 57 normalization and 23 Python integrity
checks continue to pass. Actual local product runs return 0 for inventory collection,
0 for exact target self-comparison with `--strict`, and 1 for strict Avalonia-versus-
Uno comparison. Collection success is not compatibility success. Strict mode remains
directional: missing baseline contracts or unresolved metadata fail; additive target
members alone are not missing baseline contracts.

Commands using the repository's pinned SDK and restored inputs:

```sh
dotnet run --project tools/TreeDataGrid.ApiAudit -c Release -- --self-test

dotnet test tests/TreeDataGrid.Uno.Tests/TreeDataGrid.Uno.Tests.csproj \
  -c Release -p:TreeDataGridUnoTargetFrameworks=net10.0

dotnet test tests/TreeDataGrid.Parity.Tests/TreeDataGrid.Parity.Tests.csproj -c Release

# Supply compiled inputs and their matching framework reference directories.
dotnet run --project tools/TreeDataGrid.ApiAudit -c Release -- \
  "$AVALONIA_DLL" "$UNO_DLL" "$CORE_DLL" artifacts/api-review

dotnet run --project tools/TreeDataGrid.ApiAudit -c Release -- \
  "$UNO_DLL" "$UNO_DLL" "$CORE_DLL" artifacts/api-self --strict

dotnet run --project tools/TreeDataGrid.ApiAudit -c Release -- \
  "$AVALONIA_DLL" "$UNO_DLL" "$CORE_DLL" artifacts/api-strict --strict
```

Set `TREEDATAGRID_API_BASELINE_REFERENCES` and `TREEDATAGRID_API_TARGET_REFERENCES`
to matching reference/output directories. Review raw reports, dependency resolution
and supplemental files together. This is not a complete ABI/source/behavior prover:
assembly/module attributes, full assembly-qualified type/forwarder equivalence,
inherited attribute interpretation, dependency-property defaults, overload
applicability, native input and method bodies remain separate acceptance surfaces.

## Avalonia source review and the first rejected experiment

The actual reference `TreeDataGridCellsPresenter.cs` retains cells when a row leaves
the viewport, then uses `BeginRebind`, `Unrealize`, `IReusableCellRows.TryReuseCell`,
`Realize` and `EndRebind` when the row container is reused. Its presenter also uses
native measure validity and previous constraints. Bounded ranges, compatible
containers, model reuse and geometry caches avoid rebuilding the entire viewport.
Many corresponding mechanisms already exist in Uno and are not newly authored work.

An aggressive Uno prototype preserved the column range during same-pass row rebind.
A new loaded test exposed a late value lease after callback-driven source retirement;
the experimental guard was repaired, then benchmarked. Unrestricted mixed-axis
travel regressed badly. Restricting the prototype to matching column windows still
produced mixed timing and more lifecycle complexity. **That prototype was rejected
and never pushed.** The leak belonged to the rejected prototype; it is not presented
as a separately shipped fix to the baseline product.

Those exploratory measurements used uncommitted local source, not the exact-revision
ABBA protocol. They establish no statistical speedup or parity result. No native
measurement is skipped using a size-only cache: font, theme, wrapping, template and
descendant invalidation must continue to reach Uno's actual native measure pipeline.

## The second candidate: reusable retirement snapshots, then withdrawal

Uno's `TreeDataGridPresenterBase.FlushRetiredLayout` allocates
`_previousConstraints.Keys.ToArray()` to isolate cleanup from callbacks that mutate
ownership and source identity. Iterating the live dictionary instead would sacrifice
existing reentrancy guarantees.

The candidate kept the snapshot but reused its backing storage. A presenter-owned
`RetirementSnapshot<Control>` copied keys, exposed only the occupied read-only span,
and cleared every occupied reference in `finally`. It rejected overlapping capture,
including empty snapshots. Capacity was retained at its high-water size, never old
control references. This traded a persistent per-presenter buffer for fewer arrays;
it was not a zero-allocation grid or concurrent collection.

The presenter integration was four lines. Twelve candidate-only tests covered capture
isolation, recursive capture, exceptional cleanup, growing/shrinking storage and
cleared backing-array references. Four cases required zero measuring-thread allocation
for 4,096 warmed capture/clear cycles at 0/1/32/300 owners. Reflection inspected the
actual retained array rather than merely an empty logical span.

**The cache and its twelve newly introduced helper tests were removed in `13e2ff79`
after the controlled results below.** The exact original presenter blob was restored.
All tests predating this continuation remain unchanged. The final net runtime-library
source diff against `07c57154` is empty; no extra retained buffer remains. Candidate
source and execution results are preserved in Git history and artifacts.

## Exact-revision ABBA result and rejection decision

[Run 36197169685](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36197169685)
compares baseline `15eec42d` and candidate `d2ad0cb9` on one runner in baseline/candidate/
candidate/baseline order, alternating Avalonia/Uno order within each pass. All 16
hosts complete, with 100 Uno samples per operation/revision and eight identical
ordered Uno frame sequences. All four original 1.10 gates fail; successful collection
is not parity acceptance. Artifact `10890289852` retains 54 files with raw frames,
per-pass outcomes, timing, allocation, p95 and settlement data.

| Operation | Baseline ms | Candidate ms | Median time change | Baseline bytes | Candidate bytes |
| --- | ---: | ---: | ---: | ---: | ---: |
| Horizontal scroll | 1.10470 | 1.30635 | +18.25% | 15,688 | 15,688 |
| Vertical scroll | 2.80620 | 3.20625 | +14.26% | 191,904 | 191,664 |
| Distant diagonal | 10.61615 | 11.07215 | +4.30% | 971,672 | 970,472 |
| Visible-row replacement | 4.53065 | 4.37960 | -3.33% | 101,248 | 101,168 |
| Visible-column resize | 7.14500 | 8.55820 | +19.78% | 132,528 | 132,528 |
| Sort | 68.22350 | 74.71365 | +9.51% | 1,038,616 | 1,037,416 |

The cache saved only 80 bytes per replacement, 240 per vertical operation and 1,200
per diagonal/sort operation. Five timing medians worsened. Paired reference values
do not justify dismissing those observations. Pooled medians are not confidence
intervals and do not establish a complete causal breakdown, but these measurements
do not justify extra lifecycle storage for such small savings. The candidate was
withdrawn rather than rebranded as a universal speedup or retained by relaxing a gate.
Its twelve discarded cases are not counted in final retained test totals.

The fixed-text workload has 10,000 rows, 64 columns, an 800x480 viewport, 32-pixel rows,
14-pixel DejaVu Sans, headers disabled and cache length zero. It does not establish
hierarchy/variable-height, GPU-completion, frame-rate or physical-input parity. The
final source still contains the earlier validated horizontal visibility optimization;
its previous result is documented separately and is not recounted as new work here.

## Retained coverage and execution boundaries

Four new direct-framework tests compare actual Avalonia and Uno retained text-cell
reuse across editable/read-only and throwing/healthy getters. They verify the
last-good-value policy, old-root detachment, recovery, current-model writeback and
final cleanup. These validate an existing reuse contract, not automatic waivers for
inherited API candidates.

One loaded scenario extends `cell-lifecycle`: five disjoint/reverse vertical windows,
stable native controls, actual Core row/text identity, immediate old public identity
retirement, offscreen observation cleanup, live updates/editing, mixed-axis travel,
callback-driven source retirement and recovery. Its marker is
`UNO_RUNTIME_VERTICAL_RETIREMENT_PASSED`. The native registry stays at 65 suites.

Candidate checkpoint `c52cf980` passed all fifteen validation stages, 1,874 .NET cases
(228 Core, 894 Uno, 536 Avalonia, 41 sample-state, 175 direct-framework), 65 native
suites, the loaded vertical scenario and 62 signature checks. Final `13e2ff79` removes
the twelve cache-only cases and retains the four direct-framework cases and loaded
scenario. The execution checkpoint distinguishes its independent final-head results
from candidate results; candidate success is not substituted for final acceptance.

Local execution became available in this continuation. SDK 10.0.201 and public
packages came from the archived toolchain. The offline host used available net8.0
reference pack 8.0.31 rather than downloading requested 8.0.25; the target framework
did not change. Both exploratory variants used that local configuration. GitHub
Actions runs the unchanged repository pins for independent platform validation.

Ten candidate implementation/test files were byte-compared with its downloaded CI
source. Archive and source.tar SHA-256 digests were independently checked; its merge
tree matches `c52cf980`. Final retained files/tree verification is recorded separately
in the checkpoint. No prior Core implementation, preexisting assertion, rendering
option, trimming diagnostic or benchmark threshold was weakened.

Full API adaptation and 1.10 performance parity are not established. Remaining work
includes genuine API mappings with consumer proofs, expensive native text/measure
and source-sort paths, broader mixed-callback coverage, hierarchy/variable-height
benchmarks, the prior undiagnosed intermittent allocation observation, physical
input/drag, Unicode/IME, external accessibility and cross-head lifecycle/scaling.
No merge or public release is part of this continuation.
