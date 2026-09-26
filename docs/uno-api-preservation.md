# API preservation and review-evidence validation

## Full Linux validation

Use the SDK and native packages specified by `.github/workflows/uno-validation-report.yml`:

```bash
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop \
  python3 -u build/validate-uno-linux.py
```

All nineteen stages must pass. The final evidence join reads actual TRX counters
and individual outcomes, every registered native suite and its own pass marker,
review scenario markers, complete API preservation and the rejected negative
control. Inventory collection or target self-comparison alone is not compatibility.
Platform and native-performance workflows remain independent.

## Compare complete compiled declarations

```bash
python3 build/check-uno-api-regression.py \
  --before /path/to/trusted-complete-audit \
  --after /path/to/candidate-complete-audit \
  --output artifacts/api-preservation.json
```

Each input contains the production schema-7 `summary.json`, `avalonia.json` and
`uno.json`. Reference records and normalization policies must agree. Every previous
target normalized shape, complete raw record and exact reference match must remain.
Adding a member cannot compensate for losing another. Non-reference target exports
are protected as well. Dependencies, scope accounting, declaring assemblies,
input fingerprints, unique JSON fields and recomputed counts are checked fail-closed.

Binary hash changes are allowed when declarations are preserved. Existing native/
Core/type/inheritance differences remain visible; the gate does not subtract them
or certify supplemental metadata, ABI, lookup semantics or runtime behavior.

## Execute baseline defect reproductions

```bash
python3 -u build/run-uno-review-regressions.py
```

The runner pins ancestor `5be0e5638cfd6be70ffe88839b695b790f013235`. Both inputs use
one SDK and the same production reader. Reader source trees must match; a new
normalizer cannot be silently applied to both inputs to hide a loss of compatibility.
Advancing the declaration floor or changing the reader/reference requires explicit
maintainer review, preserving the preceding evidence and applicable negative controls.

A same-count removed-export control mutates an actual compiled inventory and must
fail. It is not an emitted replacement assembly. Only the two new fixtures are
then copied into baseline test projects: 22 Core and twelve paired factory cases.
No baseline runtime source is patched. All cases must execute without skips, the
candidate must pass, and the baseline must show actual failures rather than a
compile/setup error. The runner removes overlays and verifies clean tracked inputs.

```bash
python3 build/test-uno-api-regression.py -v
python3 build/test-uno-review-preservation.py -v
python3 build/test-uno-review-validation.py -v
```

The new tests reject same-count substitution, non-reference export removal, raw/
owner rewrites, changed policies/reference records, unresolved dependencies,
duplicate keys/shapes, forged counts and incomplete evidence. An additive contract
can pass preservation without being represented as complete framework parity.

## Regenerate and verify historical benchmark evidence

The read-only repair workflow uses the original pinned comparison artifact. From
an already extracted copy and the recorded GitHub artifact-provenance JSON:

```bash
python3 build/test-uno-evidence-render.py -v
python3 build/test-uno-evidence-record.py -v
python3 build/render-uno-comparison-evidence.py \
  --input /path/to/extracted-original-comparison \
  --output artifacts/recomputed-evidence \
  --checkpoint docs/uno-raw-text-checkpoint-b38ca0f7.json \
  --artifact-metadata /path/to/artifact-provenance.json
python3 build/check-uno-evidence-record.py \
  --checkpoint docs/uno-raw-text-checkpoint-b38ca0f7.json \
  --derived artifacts/recomputed-evidence/comparison-evidence.json \
  --metadata /path/to/artifact-provenance.json
```

Output must be outside the input archive. All four hosts, workloads and samples
participate. The renderer recomputes and reconciles exact medians, p95 and per-pass
medians. The committed-record check allows only tiny floating-point roundoff in
unit conversion and requires exact allocation figures. Generated Markdown includes
pooled and per-pass/p95 tables; JSON includes source/reader/library identities and
a hash/size manifest for every extracted file.

The sixteen renderer/record test methods reject the reviewed transcription and
provenance failures. The original erroneous checkpoint is preserved in
`docs/archive/uno-raw-text-checkpoint-before-evidence-repair.json`; its corrected
counterpart retains the historical platform-status snapshot as historical.
An expired or unavailable original artifact is a retrieval failure, not permission
to substitute another revision's data. Regeneration is not a new benchmark run.

Full API/performance parity and physical input, IME, external accessibility and
untested cross-head behavior remain independent acceptance obligations.
