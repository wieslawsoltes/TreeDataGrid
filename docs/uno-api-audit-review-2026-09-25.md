# API audit correctness review and interpretation

Review date: 2026-09-25 UTC. Starting revision: `889bfde6`.
Audit fixes: `d42c3e3f` and `da989f8d`.
[Performance review](uno-performance-audit-review-2026-09-25.md) ·
[Current implementation checkpoint](uno-current-work.md)

## Conclusion: 834 is a declared-metadata difference count

The historical headline is reproducible, but it is **not a count of 834 missing
features or 834 unimplemented methods**. It compares directly declared public and
externally protected metadata after explicit Avalonia/Uno namespace normalization.
It includes changed inheritance, native parameter/return types, relocated Core
contracts, methods now inherited rather than redeclared, and framework-generated
exports. Signature, source-compatibility, behavioral and platform acceptance are
separate questions. A property being callable through a base does not imply that
its declaration exists at the old metadata identity.

Two real tooling defects and several integrity gaps were found and corrected.
They did not create the 834 differences in the actual current product inventory.
The corrected full pipeline reproduces that count without dropping records,
accepting a Core mapping, changing namespace normalization or removing a gate.

## Separate UI evidence from a dependency compared with itself

Both inputs explicitly contain the same `TreeDataGrid.Core.dll`. Its byte identity
is checked from the input SHA-256 fingerprints. The previous 1,011 exact matches
include **590 shared-Core self-matches**, not 1,011 matches of independently ported
UI declarations. The new accounting exposes the distinction:

| Scope | Baseline declarations | Target declarations | Exact | Missing/different | Additional/different |
| --- | ---: | ---: | ---: | ---: | ---: |
| Historical all-input inventory | 1,845 | 1,847 | 1,011 | 834 | 836 |
| Shared Core only | 590 | 590 | 590 | 0 | 0 |
| UI assemblies only | 1,255 | 1,257 | 421 | 834 | 836 |

The scopes are checked to be additive and have no normalized identity collisions
on this revision. This does not turn 421/1,255 into a feature-completion percentage:
one declaration can represent many behaviors, and inherited/platform-adapted
contracts can work without an exact declaration match. The useful correction is
to describe what the numbers actually measure, not to substitute another percentage.

`scope-accounting.json` and `scope-accounting.md` now accompany the existing raw
inventories. The historical raw counts remain present for continuity.

## Complete partition of the 834 raw differences

The existing compiled classifier retains these mutually exclusive categories:

| Declared-metadata category | Count | Meaning |
| --- | ---: | --- |
| Declaration changed at the same documentation identity | 214 | Identity exists but modifiers, type details, bases or another recorded declaration component differ. |
| Exported type absent at the old identity | 43 | A type with that exact normalized identity is not declared in the supplied target assemblies. |
| Member of an absent exported owner | 316 | The old owning type is absent; this is not 316 independent proofs of absent behavior. |
| Member not declared on a matched type | 118 | No matching directly declared member/name candidate; inherited lookup requires separate review. |
| Overload or parameter identity difference | 143 | A same-owner/name/kind candidate exists with another identity, often involving native framework types. |
| **Total** | **834** | Every raw difference remains in the inventory. |

A separate triage pass groups the 359 absent-owner/type declarations into **351
shared-Core relocation candidates** and **8 framework-generated export reviews**.
Together with 214 changed declarations, 118 undeclared members and 143 overload/
parameter differences, these still sum to 834. A same simple type name with matching
generic arity is only a search lead, not an accepted source or behavioral mapping.

The new supplemental join finds **148 raw differences with same-name inherited
member candidates**. These overlap the categories above and must not be subtracted
again. Each candidate records its actual declaring assembly, constructed declaration
and base depth. It deliberately does not certify overload applicability, name hiding,
accessibility, generic constraints, default values, custom attributes or behavior.
For example, a base member with the right name can have the wrong native event type
or be hidden by a derived overload. An inherited constructor is not invented.

Typical review directions include sizing/header properties inherited by column
compatibility bases, list methods inherited from the shared Core list, and native
controls whose protected lifecycle signatures use WinUI types rather than Avalonia
types. The full JSON/Markdown reports retain all concrete identities and candidates.
No global 'ignore inherited members', 'accept any Core type' or blanket namespace
replacement is introduced.

## Corrected production reader defect: floating-point constants

`Surface.Add` previously serialized a declared constant with the default
`System.Text.Json` numeric serializer. A public NaN or infinity constant caused
that full audit path to throw, even though the supplemental metadata helper already
handled such values. A helper-only test therefore did not cover the actual failure.

Declared constants now use the existing `ApiSemantics.ScalarText` representation.
Nonfinite single/double values and negative zero retain explicit IEEE-754 bits;
positive and negative zero remain distinguishable. Quoted string values and strings
that happen to contain namespace-like text are not rewritten by normalization.
This uses the same scalar contract as supplemental metadata rather than creating
another incompatible serializer.

`ApiSurfaceChecks` emits temporary managed assemblies with Roslyn, then invokes the
**same `Surface.Read` method used for actual product input**. Its 27 checks cover
public/protected accessibility, hidden owners, constructed generic inheritance,
explicit interface implementations, declared versus inherited inventory separation,
nullable/private-set properties, by-reference parameters, NaN/infinity/signed zero,
quoted strings, deterministic reads, identical-input comparison, Core/UI accounting,
and normalization collisions. The fixture methods and inspected application code
are never executed; this is compiled-metadata inspection, not reflection invoking
static getters. The existing 39 semantic and 57 namespace-normalization checks remain.

## Corrected review integrity defects

The Python review previously accepted a candidate whenever its `Normalized` string
appeared somewhere in the target inventory. A candidate could copy that string but
supply a fabricated assembly, raw declaration, declaring owner, metadata name or
documentation identity. It would then be presented as target evidence.

Candidates must now equal the entire verified target record. Duplicate candidates,
malformed lists/records and ambiguous normalized inventories are rejected. Collision
checks occur before dictionary construction can overwrite one declaration with
another. An identical duplicate record is harmless; two different declarations
sharing a normalized key are not. The C# producer also reports collisions and fails
closed instead of silently treating a `HashSet` collapse as exact coverage.

Summary counts must be actual JSON integers. Python's `True == 1` and `1.0 == 1`
no longer allow booleans or floating-point values to masquerade as valid counters.
Malformed inputs receive explicit validation errors instead of incidental attribute
or subscript exceptions. The previous 12 review tests remain unchanged; eleven new
integrity tests bring that suite to 23. Six fabricated-metadata mutations are
subtests of one of those eleven tests, not six extra registered cases.

The review remains a verifier/triage consumer of the compiled classifier. Validating
candidate provenance does not prove the candidate's compatibility, and accepting a
well-formed record never sets `equivalenceAccepted` to true.

## Executed original-versus-corrected regression

[Run 36182461885](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36182461885)
checks exact original `889bfde6` and corrected `da989f8d` sources in separate unchanged
worktrees. Both audit executables receive the **same compiled fixture files**, with
NaN/infinity/negative-zero constants:

| Check | Original | Corrected |
| --- | --- | --- |
| Full audit of identical constant-bearing inputs, strict mode | Exit 2: original JSON constant failure | Exit 0: zero missing/additional declarations |
| Existing Python review suite | 12/12 passed | 12/12 passed |
| New integrity suite | 11 tests ran; seven tests had assertion failures, two raised unexpected errors, two passed | 11/11 passed |
| Corrected emitted-PE reader checks | Not present | 27/27 passed |

The original unittest output records 12 assertion failures because the fabricated
candidate case has six failing subtests, plus two errors. It is not a report of 14
separate failing test methods. All original/corrected logs, fixture audit reports
and exit codes are preserved in artifact `10884832987`. No product source was
patched in either worktree. The regression workflow checks the expected original
failures explicitly rather than calling a green self-comparison sufficient proof.

## How to run and inspect the audit

Run the standalone tooling checks without requiring product assemblies:

```sh
dotnet run --project tools/TreeDataGrid.ApiAudit -c Release -- --self-test
python3 build/test-uno-parity-audit.py
```

The normal compiled audit command retains its original inputs:

```sh
dotnet run --project tools/TreeDataGrid.ApiAudit -c Release -- \
  path/to/Avalonia.Controls.TreeDataGrid.dll \
  path/to/TreeDataGrid.Controls.Uno.dll \
  path/to/TreeDataGrid.Core.dll \
  artifacts/api-audit
python3 build/audit-uno-parity.py --input artifacts/api-audit --output artifacts/parity-review
```

Use the existing baseline/target reference-directory environment variables when
running away from the built samples. An unresolved metadata type remains visible
and is not treated as a valid match. Normal report mode emits review evidence even
when differences exist. `--strict` returns 1 for baseline/supplemental differences
or unresolved dependencies; invalid input/tooling integrity errors return 2.
Additional target APIs alone are not removed and are not a backward-compatibility
failure by themselves. The existing strict self-comparison remains a separate check.

Start with `summary.json`, `scope-accounting.json`, `classified-differences.json`
and `parity-review/review.json`; then inspect the complete raw and supplemental
inventories for the actual declaration. A resolved Core mapping requires an explicit
contract decision and compiled consumer/behavioral evidence. Do not make a mapping
by lowering the raw counter or allowing a name-only match.

## Remaining audit boundaries

This is not a full binary-compatibility, C# name-resolution or behavioral prover.
Assembly/module attributes, custom modifiers, dependency-property defaults, native
input behavior, external accessibility, event ordering, renderer output and timing
are not certified by equal declared strings. Inherited candidates and attributes
are supplemental evidence, not automatic AttributeUsage or lookup equivalence.
The auditor reads compiled metadata without executing product assemblies. Its
fixtures and integrity regression validate the corrected paths, not every possible
CLR metadata construction.

The raw 834 differences are intentionally retained. Genuine API completion and
explicit, tested architectural adaptations remain work; a more accurate audit is
not a completed port. Exact final product/platform execution belongs to the current
checkpoint, separately from the tooling regression and performance measurements.
