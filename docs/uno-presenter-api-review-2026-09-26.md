# Presenter query contracts and canonical API comparison

Review date: 2026-09-26 UTC. Audit correction `81e94fcf`; public query implementation
`73d4c0f3`. [Current work](uno-current-work.md).

## Why the declared count changes from 834 to 825

Six old type declarations differed only because direct interface displays were
sorted by raw framework name before normalization. The audit maps Avalonia.Controls
and Uno.Controls to UI.Controls. Avalonia sorts before System/TreeDataGridCore;
Uno sorts after them. Normalizing after that raw sort manufactured different lists
from the same mapped interface set.

The declared reader now keeps the base class first, then sorts direct interface
displays by normalized name with a raw-name tie break. All interface entries and
original type spellings remain. It does not parse comma-separated display text,
sort generic arguments or parameters, modify custom modifiers, broaden namespace
mappings, or accept Core/native/inherited-member equivalences. Direct interface
lists were already treated as unordered by this declared-surface auditor; this
repairs that existing canonicalization rule, not arbitrary CLR compatibility.
Output schema 7 records the ordering policy and its production-reader checks.

The six corrected declaration identities are:

- `Uno.Controls.Models.TreeDataGrid.IColumn<TModel>`.
- `Uno.Controls.Models.TreeDataGrid.IExpanderCell`.
- `Uno.Controls.Models.TreeDataGrid.IExpanderCellPresentation`.
- `Uno.Controls.Models.TreeDataGrid.IUpdateColumnLayout`.
- `Uno.Controls.Models.TreeDataGrid.TemplateCell`.
- `Uno.Controls.Presentation.ICellColumn<TModel>`.

Sixteen emitted-PE checks exercise the actual Surface.Read path. Positive cases
cover mapped classes/interfaces, reversed declaration order, deterministic reads,
raw-name preservation and normalized-record integrity. Negative cases preserve
missing/additional interfaces, changed base classes, ordered nested generic
arguments, literal strings and method parameter sequences. Prior signature,
semantic, full-reader, normalization and Python-integrity checks are unchanged.

The other three resolved declared differences are actual newly implemented
`TryGetTotalCount(out int count)` methods. The archived comparison in
[run 36221096429](https://github.com/wieslawsoltes/TreeDataGrid/actions/runs/36221096429)
checks each of the nine old identities against an exact normalized record in the
new target inventory. Artifact `10898917534` contains complete old/new records,
not only totals. No newly missing identity appears in that comparison.

| Scope | Baseline | Target | Exact | Missing/different | Additional/different |
| --- | ---: | ---: | ---: | ---: | ---: |
| All compiled inputs | 1,845 | 1,853 | 1,020 | 825 | 833 |
| Identical shared Core | 590 | 590 | 590 | 0 | 0 |
| Independent UI assemblies | 1,255 | 1,263 | 430 | 825 | 833 |

The remaining partition is 208 changed declarations at the same identity, 43
absent exported type identities, 316 members of those types, 112 members not
declared on matched types, and 146 overload/parameter differences. These sum to
825. Counts are metadata accounting, not feature-completion percentages. The
same-name Core and inherited candidates remain review candidates, not waivers.

## Native public query APIs

`TreeDataGridCellsPresenter`, `TreeDataGridColumnHeadersPresenter` and
`TreeDataGridRowsPresenter` now each expose:

```csharp
public bool TryGetTotalCount(out int count);
public int GetChildIndex(Microsoft.UI.Xaml.DependencyObject child);
```

`TryGetTotalCount` captures Items once and reads its Count once. Unset Items returns
false and zero. A present empty list returns true and zero. Cell and header
presenters report the entire visible-column projection; the row presenter reports
the entire flattened visible-row collection. None reports the size of its realized
viewport. There is no enumeration, offscreen realization, count cache or copied
Core state. Application Count-getter exceptions are preserved.

`GetChildIndex` returns ColumnIndex for the corresponding cell/header type or
RowIndex for a row container. Another element type returns -1; unrealized and
retired containers also hold -1. Like the actual Avalonia implementation, this
method reads the supplied container index rather than checking parent membership.
An application needing ownership validation must perform that separately.

The count methods are exact portable declaration matches. The child parameter is
an explicit native adaptation from Avalonia's ILogical to WinUI DependencyObject;
those three parameter differences remain in the audit. This does not implement
Avalonia's logical-tree interface or its ChildIndexChanged event and does not
claim a cross-framework ABI match.

Typical native usage:

```csharp
var presenter = grid.RowsPresenter;
if (presenter is not null && presenter.TryGetTotalCount(out var visibleRows))
{
    // visibleRows includes offscreen rows, not only realized native containers.
    foreach (var row in presenter.RealizedRows)
    {
        var rowIndex = presenter.GetChildIndex(row);
        // Use rowIndex for the current flattened view, not as a persistent model key.
    }
}
```

## Loaded consumer verification

`PresenterIndexRuntimeChecks` runs inside the existing cell-lifecycle suite and
therefore also in sequential native and published package consumers. The standalone
checks distinguish absent/empty Items and query an int.MaxValue count-only list
whose indexer/enumerator throw. Count is read once, and a throwing getter retains
exception identity. Wrong-type and never-realized containers return -1.

The loaded 200-row/12-column fixture checks current Core model identity, distant
scrolling and reuse, a hidden/reordered column, row removal and sorting, and 4,096
repeated queries without additional realization. It validates retired container
indices and confirms the caller-owned Core source survives. The marker is
`UNO_RUNTIME_PRESENTER_INDEX_QUERIES_PASSED`. No new native suite or direct-framework
.NET test count is claimed. This is consumer behavior verification, not physical
input, external accessibility or full logical-tree interface parity.

## Remaining boundary

Strict cross-framework compatibility still fails. The new count methods and
normalization correction do not close remaining lifecycle overrides, native type
adaptations, inherited metadata, Core relocation, attributes, defaults or runtime
behavior. Every unresolved record remains available for explicit review. Native
performance and the unchanged independent 1.10 budget are separate acceptance
work; smaller declared counts are not performance evidence.
