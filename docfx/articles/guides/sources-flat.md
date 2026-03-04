# Flat Source Guide

Use `FlatTreeDataGridSource<TModel>` for non-hierarchical tabular data.

This is the default choice when rows do not contain children and you want DataGrid-like behavior with typed columns.

## When to Use Flat Source

Use flat source when:

- your data is a single-level list (`IEnumerable<TModel>`)
- row index is enough (or `IndexPath` depth is always 1)
- you do not need expand/collapse behavior

Use hierarchical source when each row can expose child collections.

## Basic Setup

```csharp
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;

public class PeopleViewModel
{
    private readonly ObservableCollection<Person> _people = new()
    {
        new Person { FirstName = "Eleanor", LastName = "Pope", Age = 32 },
        new Person { FirstName = "Jeremy", LastName = "Navarro", Age = 74 },
        new Person { FirstName = "Lailah", LastName = "Velazquez", Age = 16 },
    };

    public PeopleViewModel()
    {
        Source = new FlatTreeDataGridSource<Person>(_people)
        {
            Columns =
            {
                new TextColumn<Person, string>("First Name", x => x.FirstName),
                new TextColumn<Person, string>("Last Name", x => x.LastName),
                new TextColumn<Person, int>("Age", x => x.Age),
            },
        };
    }

    public FlatTreeDataGridSource<Person> Source { get; }
}
```

```xml
<TreeDataGrid Source="{Binding Source}" />
```

## Core APIs

- `Items`: collection currently used by the source
- `Columns`: strongly typed `ColumnList<TModel>`
- `Rows`: flattened row model collection
- `Selection`: active selection model (row by default)
- `RowSelection` / `CellSelection`: typed helpers when matching selection mode
- `SortBy(...)`: requests sorting by column
- `IsSorted`: indicates active sort comparer
- `Sorted`: event raised after sorting

## Changing Items at Runtime

You can swap the items collection by assigning `Items`:

```csharp
Source.Items = new ObservableCollection<Person>(nextPage);
```

Behavior:

- row pipeline is rebound to new items
- existing selection model source is updated to new `Items` (when selection is present)

## Selection Modes

Default is row selection.

```csharp
Source.RowSelection!.SingleSelect = false;
```

Switch to cell selection:

```csharp
using Avalonia.Controls.Selection;

Source.Selection = new TreeDataGridCellSelectionModel<Person>(Source)
{
    SingleSelect = false,
};
```

Important: assigned selection model must use the same underlying `Items` source.

## Sorting

Sorting can be requested by UI header click or directly via source.

```csharp
using System.ComponentModel;

var sorted = Source.SortBy(Source.Columns[0], ListSortDirection.Ascending);
```

Notes:

- sorting works only if column provides a comparison
- `SortDirection` indicator is updated on columns by the source
- drag/drop row move is disabled while sorted

## Row Drag and Drop Semantics

`FlatTreeDataGridSource<TModel>` supports move-only row reordering through `ITreeDataGridSource.DragDropRows(...)`.

Constraints:

- effects must be `Move`
- source must not be sorted
- source indexes must be single-segment (`IndexPath` depth 1)
- `Items` must implement `IList<TModel>`

If these constraints are violated, the source throws.

## Error Cases to Expect

- assigning a selection model with different source: `InvalidOperationException`
- drag/drop with non-list source: `InvalidOperationException`
- drag/drop while sorted: `NotSupportedException`

## Recommended Pattern

- store mutable data in `ObservableCollection<TModel>`
- create columns once in source constructor/init
- keep sorting and selection logic in view model, not code-behind

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.FlatTreeDataGridSource`1>
- <xref:Avalonia.Controls.ITreeDataGridSource`1>
- <xref:Avalonia.Controls.ITreeDataGridSource>

## Related

- [Hierarchical Source Guide](sources-hierarchical.md)
- [Selection Row](selection-row.md)
- [Selection Cell](selection-cell.md)
- [Sorting and Column Widths](sorting-and-column-widths.md)
- [Troubleshooting Guide](troubleshooting.md)
