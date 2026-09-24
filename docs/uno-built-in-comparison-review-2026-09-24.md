# Uno built-in column comparison audit and implementation

Date: 2026-09-24 UTC. PR #26, `codex/uno-core-port`.
**This is a verified subset, not complete API or performance parity.**

Starting head: `469e6f6605128bfaff271385b12c8d81b361e8ea`.
Product: `527a86443dd88afdf1cf0772498fa0fb9a9eae46`.
Product tree: `663ba1e44eab8487b5d1c8a58066275fcaa74453`.
Validated merge: `e7925b37be66cc4a5bdbe29fa65447c1b1ca7873`.
[Exact checkpoint](uno-ci-checkpoint-527a8644.json) records completed evidence and
unresolved acceptance. Documentation is committed after product platform success.

| Commit | Implementation |
| --- | --- |
| `90108e63bbef65e13d2e8033debf90a712aa2d18` | Public built-in selector/comparison APIs and nineteen differential cases |
| `527a86443dd88afdf1cf0772498fa0fb9a9eae46` | Native/sequential consumer and independent Core sorting verification |

## Audit finding

`CheckBoxColumn.IsThreeState` and built-in `TryReuseCell` are already inherited
public APIs. Adding duplicate declarations would not implement missing behavior.
The genuine gap addressed here is the missing public raw selector and comparison
API on built-in text/checkbox/template view columns.

The partial `ValueCellColumn<TModel,TValue>` now exposes:

```csharp
public Func<TModel, TValue> ValueSelector { get; }
public virtual Comparison<TModel?>? GetComparison(ListSortDirection direction);
```

ValueSelector returns the actual Core definition's cached getter without display
formatting. Property access creates no row or subscription and invokes no model
getter. Core may lazily compile its existing expression on first request; later
queries reuse the delegate. Existing pooled binding ownership is unchanged.

Value comparisons capture construction-time CanUserSortColumn and explicit
ascending/descending delegates, matching the reference value-column contract.
Default delegates are allocated only when requested and cached by the view. Null
models and values use the reference ordering, including descending operand order;
unknown directions return null. Ordinary native layout does not create comparers.

TemplateColumn overrides GetComparison to read live explicit options instead.
There is no default comparison of arbitrary model objects, no template-resource
lookup, and no implicit suppression by CanUserSortColumn. This matches the reference
template view API; source/UI sorting permission remains a separate policy.

The reviewed reference implementations are the value-column base, CheckBoxColumn
and TemplateColumn in `src/Avalonia.Controls.TreeDataGrid`. Differential tests
reference both actual compiled libraries, not copies of their implementations.

## Source sorting and ownership

The new APIs operate on the native view. They do not overwrite Core options or
intercept source.SortBy. Shared Core continues owning row order, source identity,
hierarchy and selection mappings. The integration test intentionally configures
the view to sort in the opposite order from Core, verifying that adding the view
API cannot silently change source behavior.

For ordinary use, configure the intended source policy on Core and request the
view utility explicitly when needed:

```csharp
using System;
using System.ComponentModel;
using TreeDataGridCore;
using Core = TreeDataGridCore.Models;
using View = Uno.Controls.Models.TreeDataGrid;

public sealed class Person
{
    public string? Name { get; set; }
}

public static class ComparisonExample
{
    public static string? Run()
    {
        Comparison<Person?> byName = static (left, right) =>
            StringComparer.Ordinal.Compare(left?.Name, right?.Name);
        var definition = Core.ValueColumn<Person, string?>.FromDelegate(
            "Name", static person => person.Name,
            propertyName: nameof(Person.Name),
            options: new() { CompareAscending = byName });
        using var source = new FlatTreeDataGridSource<Person>(
            new[] { new Person { Name = "Beta" }, new Person { Name = "Alpha" } });
        source.Columns.Add(definition);
        using var view = new View.TextColumn<Person, string?>(definition,
            new() { StringFormat = "Person: {0}", CompareAscending = byName });
        var compare = view.GetComparison(ListSortDirection.Ascending)!;
        if (compare(new Person { Name = "Alpha" }, new Person { Name = "Beta" }) >= 0)
            throw new InvalidOperationException("Unexpected comparison.");
        source.SortBy(definition, ListSortDirection.Ascending);
        return view.ValueSelector((Person)source.Rows[0].Model!);
    }
}
```

The example illustrates the tested API but is not an additional executed case.
Its result is raw "Alpha", not formatted "Person: Alpha". The native regression
uses notification-capable models and actual editing controls.

## Executed coverage

`BuiltInColumnComparisonTests` adds nineteen actual-framework cases/checks:
nullable numeric and null-model ordering in both directions; nullable/nonnullable
checkbox state and selectors; captured permissions and comparer identities;
invalid directions; raw getter identity versus formatted display; live template
comparers; separate view/Core policy; noncomparable values; and warm allocation.

The 4,096-iteration string-comparison/query loop records zero managed bytes on the
executing thread. This excludes cold compilation, numeric comparer behavior and
arbitrary application callbacks. It is not whole-grid performance acceptance.

`builtin-column-comparison` is a new native suite also run sequentially in the
published native and trimmed browser consumer. It uses 160 actual Core rows and
checks selector identity, captured view policy, live template policy, independent
Core order, native editor writeback/re-sort, distant rendered text, bounded
realization and complete subscription cleanup.

[Functional run 35992479692](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35992479692),
job `107609596104`, passes all fifteen stages on an unchanged checkout:
228 Core + 594 Uno + 536 Avalonia + 41 sample-state + 87 differential =
**1,486 .NET cases**, zero failed/skipped; **58/58 native suites**; sequential
showcase/recovery; two native builds with zero warnings/errors; and all Activity
Monitor sections/lifetime checks. Separate Python audit tests pass 12 cases;
metadata semantics and normalization pass 39 and 57 checks respectively.

Artifact `10804807227`, SHA-256
`1ee9fc1484fdb7a8e2043b0db24f7d3c63c6aa8b66f1ebc0cfd6a4d5a8982343`,
preserves full inventories, source fingerprints, TRX and native logs.

[Platform run 35992479554](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35992479554)
is fully successful: Windows/Linux/macOS desktop jobs; native Linux regression
and package-consumer execution; Windows App SDK builds/packaging/publication;
browser builds, trimmed publication and execution of both published consumers.
Browser-dispatched pointer/keyboard routes pass at device scales 1 and 2. These
are bounded Chromium checks, not physical hardware, universal browser, IME,
external screen-reader or universal DPI acceptance. Build, reproducibility and
executed trimmed-binding checks also pass; their IDs are in the exact checkpoint.

## Performance still fails

[Paired run 35992479581](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/35992479581),
job `107609593861`, builds both hosts and completes all four AB/BA processes with
valid frames and unchanged sources. The **unchanged 1.10 median timing/allocation
budget remains failed**.

| Workload | Avalonia median ms | Uno median ms | Uno/Avalonia |
| --- | ---: | ---: | ---: |
| Horizontal scroll | 0.45470 | 2.08330 | 4.58 |
| Vertical scroll | 1.40635 | 2.49975 | 1.78 |
| Distant diagonal scroll | 4.18555 | 10.14755 | 2.42 |
| Replace visible row | 2.78815 | 3.94500 | 1.41 |
| Resize visible column | 4.69060 | 6.01030 | 1.28 |
| Sort | 45.16825 | 63.61265 | 1.41 |

Artifact `10805160762`, SHA-256
`a3a5d86d0d3be2376df47194fb7736aaf41f11da8cb57d36d680b5f5cc79c5e5`,
retains raw allocation/p95/settlement results. Sorting allocates less but remains
slower. These are synchronous UI/settlement metrics, not GPU completion or frame
rate. No controlled revision before/after experiment was run here. The standard
source-sorting workload does not isolate the new view comparison APIs, so no
whole-grid speedup is claimed from this continuation or different hosted machines.

## Remaining contracts

The unchanged auditor records 1,845 baseline / 1,831 target declarations,
**1,010 exact normalized matches, 835 missing-or-different baseline and 821
additional-or-different target entries**. Dependencies fully resolve and strict
self-comparison passes. Supplemental metadata remains explicit: 13,465 baseline,
15,724 target, 3,825 exact, 9,640 missing-or-different and 11,899 additional-or-different.
All baseline declarations and raw differences remain accounted for, with no
automatic Core equivalence acceptance or assertion that counts measure completion.

Built-in columns still lack the reference ColumnBase mutable Binding descriptor
and protected binding-factory surface. Adding an unused property would not fix
that: cells must consume descriptor changes with correct pooled binding lifetime.
Existing custom ColumnBase bindings remain available through the documented custom
column API. The full legacy combined inheritance hierarchy is not restored here.

Genuine signature/inheritance/attribute contracts, tested Core substitutions,
remaining callback semantics, measured native performance, hierarchy/variable-height
and mixed-mutation workloads, and physical input, IME, screen-reader and cross-head
scaling acceptance remain open. The empty unmatched-owner category is not complete
API parity.

Local shell/Python execution returned ClientError. Authored sources were pushed
through GitHub and tested on unchanged CI inputs; unknown unrelated local files
could not be inspected. No Core ownership rule, dependency, rendering setting,
assertion, trimming diagnostic or performance threshold was weakened.

```sh
dotnet test tests/TreeDataGrid.Parity.Tests/TreeDataGrid.Parity.Tests.csproj \
  -c Release -p:TreeDataGridUnoTargetFrameworks=net10.0 \
  --filter FullyQualifiedName~BuiltInColumnComparisonTests
TreeDataGridUnoSampleTargetFrameworks=net10.0-desktop python3 build/validate-uno-linux.py
python3 build/run-uno-native-suites.py --suite builtin-column-comparison
python3 build/run-native-parity.py --pairs 2 --columns 64 --iterations 25 --max-ratio 1.10
```
