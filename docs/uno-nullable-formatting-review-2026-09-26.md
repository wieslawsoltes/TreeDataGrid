# Nullable-safe numeric text formatting

2026-09-26 UTC. Correction: `926cb2ea0f7afc110e4671141c8fa62b640fd455`.
The numeric optimization was already present at this continuation's starting
commit `31f98578`; its implementation and previous gains are not newly counted here.

## Reproduced allocation regression

[Prior comparison 36258842105](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36258842105)
ran the pre-numeric baseline `603b461e` and numeric candidate `31f98578`. Although
nonnullable primitives avoided their composite-format argument boxes, nullable
integer display allocated **88 bytes rather than the original 64 bytes** per
query. The extra 24 bytes appeared in both candidate passes. The same run recorded
slower string (+5.10%), nullable (+4.45%) and custom-provider (+19.71%) medians.
These results remain in artifact `10911213693`; they are not superseded as if they
had never happened.

The numeric helper performed runtime `value is string` and `value is null` tests
before rejecting arbitrary value types. For Nullable<T> those tests could cause
boxing before the fallback boxed the argument again. There is no benefit in asking
whether an unboxed value-type specialization is a string.

## Dispatch correction and behavior

The guarded identity-format route now tests the generic type first:

```csharp
if (typeof(T) == typeof(string))
    return (string?)(object?)value ?? string.Empty;
if (typeof(T).IsValueType)
    return FormatIdentityValue(culture, value);
if (value is string text)
    return text;
if (value is null)
    return string.Empty;
```

This occurs only after resolving CurrentCulture and checking both the exact
`"{0}"` format and the exact `CultureInfo` runtime type. A derived culture can
supply an ICustomFormatter; it continues through the original composite formatter.
For a string specialization the guarded cast is a reference conversion, not a
value-type box. Object-typed strings/nulls keep their established identity shortcuts.

The existing sixteen exact numeric type arms still call their built-in formatter
without an object argument. Nullable types, enums and unknown structs do not enter
those arms; they use composite formatting once. No arbitrary IFormattable or user
conversion is substituted. Native typed/object-valued paths, live NumberFormatInfo,
custom format strings, provider callbacks and original errors retain their existing
contracts. No formatted value, culture or model is cached.

The correction adds six nullable allocation/value cases: int, long and decimal,
each with a present and absent value. They compare 8,192 warm calls against a
no-inlining composite-format oracle, requiring identical values/character counts
and no greater allocation on dynamic-code-compiled runtimes. A seventh case checks
string and object-typed string/null identity. Earlier numeric and paired-framework
assertions are unchanged. These seven cases are not additional API declarations.

## Exact-revision comparison

[Run 36263270092](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36263270092)
compares exact pre-numeric baseline `603b461eaf16339232628b1490a9847e7fb1cca4`
with `926cb2ea0f7afc110e4671141c8fa62b640fd455`. The original ten-workload
public harness and collector are unchanged. Each host warms eight batches and
measures fifteen batches of 8,192 queries. Process order is baseline/candidate/
candidate/baseline: thirty samples per workload/revision. All hosts, value checks,
source checks and library fingerprint checks passed. Runtime defaults are inherited;
no tiering or ReadyToRun override was introduced.

Environment: .NET 10.0.12, X64 Ubuntu 24.04.5 LTS, workstation GC. Artifact
`10912268901` retains all twelve files, raw samples, p95 and per-pass medians.
This is warm cell-model display querying, not native visual layout, construction,
startup, whole-grid throughput, frame rate or GPU completion.

| Workload | Baseline ns/query | Corrected ns/query | Baseline bytes | Corrected bytes |
| --- | ---: | ---: | ---: | ---: |
| Scalar int, identity | 102.49 | 51.24 | 64 | 40 |
| Scalar double, identity | 244.09 | 210.05 | 64 | 40 |
| Scalar decimal, identity | 227.77 | 93.53 | 72 | 40 |
| Scalar string, identity | 34.59 | 27.03 | 0 | 0 |
| Scalar nullable int, identity | 146.81 | 100.20 | **64** | **64** |
| Scalar int, explicit format | 282.35 | 176.70 | 80 | 80 |
| Custom culture/provider | 161.50 | 108.15 | 176 | 176 |
| Live native int cell model | 136.13 | 48.83 | 64 | 40 |
| Core-backed int cell model | 138.42 | 45.30 | 64 | 40 |
| Object-valued column format | 141.82 | 89.23 | 64 | 64 |

These are pooled medians, not confidence intervals. In this run all pooled timing
medians are lower, but several baseline passes differ substantially. For example,
custom-provider baseline passes are 207.32 and 105.92 ns, against candidate passes
109.01 and 107.67 ns. Boxed-column baseline passes are 169.63 and 89.17 ns, against
89.65 and 88.33 ns. Their favorable pooled timing is not reliable proof that an
unchanged fallback became faster. No adverse earlier run or current raw sample is
dropped, and no statistical significance or full causal attribution is claimed.

The firm targeted result is allocation: nullable display returns to the original
64 bytes in every measured pass, while the already-existing int/double/decimal and
native numeric paths retain their 24/32-byte argument-box saving. Different-runner
absolute times are not compared directly against the prior run to claim an extra
speedup. This correction still creates the formatted result string; it is not
zero-allocation number display.

## Reproduction and remaining acceptance

```bash
python3 build/compare-numeric-formatting.py \
  --baseline 603b461eaf16339232628b1490a9847e7fb1cca4 \
  --candidate 926cb2ea0f7afc110e4671141c8fa62b640fd455 \
  --output artifacts/numeric-formatting-independent
```

Use a new output directory. All input revisions, source/runtime-library hashes and
measurement controls remain in the report. Library/benchmark changes elsewhere
between these revisions must not be described as isolated format-only startup or
construction gains: the harness explicitly measures warm display queries.

The independent 1.10 native-grid performance budget remains a separate gate.
No public signature, normalization mapping, Core ownership, rendering setting or
preexisting assertion was changed by this correction. Full API and runtime parity
are not established. Current combined functional/platform results belong to the
latest source checkpoint, not automatically to every earlier experimental revision.
