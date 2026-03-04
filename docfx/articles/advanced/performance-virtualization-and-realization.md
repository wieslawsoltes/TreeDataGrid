# Performance, Virtualization, and Realization

TreeDataGrid performance is driven by presenter virtualization and control recycling.

## Presenter Stack

The main pipeline is:

- [`TreeDataGridPresenterBase<TItem>`](xref:Avalonia.Controls.Primitives.TreeDataGridPresenterBase`1): virtualized realization base.
- [`TreeDataGridColumnarPresenterBase<TItem>`](xref:Avalonia.Controls.Primitives.TreeDataGridColumnarPresenterBase`1): column-aware layout and width commit.
- [TreeDataGridRowsPresenter](xref:Avalonia.Controls.Primitives.TreeDataGridRowsPresenter): virtualized rows.
- [TreeDataGridCellsPresenter](xref:Avalonia.Controls.Primitives.TreeDataGridCellsPresenter): per-row virtualized cells.
- [TreeDataGridColumnHeadersPresenter](xref:Avalonia.Controls.Primitives.TreeDataGridColumnHeadersPresenter): virtualized headers.

## Realization Lifecycle

At runtime presenters:

1. compute visible range from effective viewport
2. recycle unrealized controls before/after visible range
3. create or reuse controls for needed indexes
4. realize model state onto controls
5. run arrange pass and keep scroll anchors stable

`BringIntoView(index)` can realize a non-visible target temporarily to force accurate scroll positioning.

## Two-Pass Measurement

Column presenters can run two measure passes:

- pass 1: natural size discovery
- pass 2: constrained final width after column width commit

This is important for star/auto sizing and avoids layout drift between headers and cells.

## Practical Optimization Rules

- Prefer built-in source and column types unless profiling shows a bottleneck.
- Keep templates lightweight in high-cardinality columns.
- Avoid storing references to realized row/cell controls long-term.
- Use model state, not visual state, as the source of truth.

## Programmatic Navigation Example

```csharp
// Scroll to row 10_000 without forcing realization of all previous rows.
var row = grid.RowsPresenter?.BringIntoView(10_000) as TreeDataGridRow;
row?.Focus();

// Ensure a specific column is visible.
grid.ColumnHeadersPresenter?.BringIntoView(8);
```

## Diagnosing Virtualization Issues

Common symptoms and causes:

- high allocation while scrolling
Cause: custom templates/factory prevent effective recycle reuse.

- stale selection visuals on reused rows/cells
Cause: custom control keeps per-row state that is not reset on realize/unrealize.

- wrong column widths after dynamic updates
Cause: custom column implementation does not correctly implement layout update contract.

## API Coverage Checklist

- <xref:Avalonia.Controls.Primitives.TreeDataGridPresenterBase`1>
- <xref:Avalonia.Controls.Primitives.TreeDataGridColumnarPresenterBase`1>
- <xref:Avalonia.Controls.Primitives.TreeDataGridRowsPresenter>
- <xref:Avalonia.Controls.Primitives.TreeDataGridCellsPresenter>
- <xref:Avalonia.Controls.Primitives.TreeDataGridColumnHeadersPresenter>

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## Related

- [Primitives Overview](primitives-overview.md)
- [Custom Element Factory](custom-element-factory.md)
- [Diagnostics and Testing](diagnostics-and-testing.md)
- [Troubleshooting Guide](../guides/troubleshooting.md)
- [Namespace: Avalonia.Controls.Primitives](../reference/namespace-primitives.md)
