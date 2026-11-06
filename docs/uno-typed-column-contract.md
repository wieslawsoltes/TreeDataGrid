# Typed Uno columns over shared Core rows

This guide describes the typed column API and the compatibility boundaries tested
in PR #26. It does not certify the entire Avalonia-to-Uno port.

## Public contracts and ownership

`Uno.Controls.Models.TreeDataGrid.IColumn<TModel>` exposes typed cell creation and
comparison on top of the native, non-generic `IColumn` metadata contract:

```csharp
public interface IColumn<TModel> : IColumn
{
    ICell CreateCell(TreeDataGridCore.Models.IRow<TModel> row);
    Comparison<TModel?>? GetComparison(ListSortDirection direction);
}
```

The supplied row is the actual shared Core row. No copied row, selection, hierarchy
or source is introduced. Built-in text, checkbox and template columns expose this
contract through `ValueCellColumn<TModel, TValue>`. Custom `CellColumnBase<TModel>`
and `ColumnBase<TModel>` subclasses expose it as well.

`ICellColumn<TModel>` remains an independent factory/layout/reuse interface. It
does not inherit `IColumn<TModel>`. Existing applications may explicitly implement
its factory while also overriding a public factory with intentionally different
behavior. Adding a typed base interface would reimplement that interface in such
subclasses and could select the public method instead of the explicit factory.
The library therefore bridges typed creation through the actual legacy interface
slot, without introducing reflection or a runtime-type registry.

A legacy-only factory does not become assignable to the new interface simply by
loading a newer library. Built-in/library-base columns implement both, and the
read-only typed column-list projection provides a non-owning facade when needed.
Binary compatibility with every third-party precompiled assembly is not claimed
by source/runtime tests.

## Built-in creation, comparison and reuse

The helper below accepts an actual Core row. Its created cell belongs to the caller:

```csharp
static void EditName(TreeDataGridCore.Models.IRow<Person> row)
{
    using var column = new Uno.Controls.Models.TreeDataGrid.TextColumn<Person, string?>(
        "Name", person => person.Name, (person, value) => person.Name = value);

    Uno.Controls.Models.TreeDataGrid.IColumn<Person> typed = column;
    var cell = typed.CreateCell(row);
    try
    {
        ((Uno.Controls.Models.TreeDataGrid.ITextCell)cell).Text = "Updated name";
        var comparison = typed.GetComparison(System.ComponentModel.ListSortDirection.Ascending);
        // comparison is a view utility. The Core source owns its sorting policy.
    }
    finally { (cell as System.IDisposable)?.Dispose(); }
}

sealed class Person
{
    public string? Name { get; set; }
}
```

The `ValueCellColumn` typed factory overload delegates to its existing untyped
virtual factory. Both interface routes also honor explicit `ICellColumn`
implementations in subclasses. Retained reuse remains available through the
existing `ICellColumn<TModel>.TryReuseCell` contract; adding typed metadata does
not replace its binding snapshot or retargeting rules.

Comparison policies preserve their existing behavior: value-column delegates are
captured according to construction-time options; template comparisons use their
live delegate policy. `ColumnBase<TModel>` forwards to its virtual comparison.
A legacy-only factory without a comparison returns null. None of these APIs
replaces an attached Core source's comparison or resolves native templates merely
to compare values.

## Typed lists and legacy-only factories

```csharp
var columns = new Uno.Controls.Models.TreeDataGrid.ColumnList<Person>();
// Populate through the existing mutable ICellColumn<Person> collection.
System.Collections.Generic.IReadOnlyList<
    Uno.Controls.Models.TreeDataGrid.IColumn<Person>> typed = columns;
Uno.Controls.Models.TreeDataGrid.IColumns native = columns;
```

Built-in and custom typed columns keep their exact object identity in both views.
For a legacy-only factory, `typed[index]` returns a cached facade while
`columns[index]` and `native[index]` return the original factory. The native object
must retain its `IUpdateColumnLayout` implementation; the facade must never leak
into native layout through a covariant list conversion.

Exact untyped list/enumerator mappings are declared beside the typed mappings.
Typed enumeration retains the base list's mutation detection. Duplicate entries
share a facade; adding/removing a duplicate does not introduce another source
subscription. Facades forward caller-added handlers without rewriting the event
sender and do not subscribe on their own. A `ConditionalWeakTable` ensures that a
removed legacy factory is not retained only because a facade points back to it.
A caller retaining the facade intentionally retains access to that factory.

Removing or clearing entries does not dispose caller-owned columns. Repeated typed
indexing after cache warm-up allocates no additional managed storage in the tested
path; initial facade creation and enumerator allocation are outside that claim.

## Initial geometry

Before the first native layout commit, value columns with an absolute width expose
that configured pixel width through `ActualWidth`. Auto and star columns remain
unmeasured. Once a width is committed, the committed constrained value wins,
including zero. This fallback neither records a natural cell measurement nor
changes the shared Core definition.

Cross-framework checks compare all unit flags and numeric pixel/star weights.
They do not treat Auto's ignored numeric payload as geometry: Avalonia and WinUI
use different payloads while exposing the same automatic-sizing unit.

## Consumer scope

`TypedColumnContractRuntimeChecks` and its legacy-factory extension execute inside
`builtin-column-comparison`, in sequential native consumers and in the published
trimmed browser consumer. They exercise interface dispatch, shared Core row identity,
comparison identity, distinct owned cell values, duplicate typed projections,
original native identities and balanced observation. The enclosing scenario also
checks actual native rendering, editing, sorting, distant navigation and cleanup.
Consult the current execution checkpoint for the exact tested commit and results.
