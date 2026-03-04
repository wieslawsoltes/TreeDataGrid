# Working With ItemsSourceView

`TreeDataGridItemsSourceView` standardizes collection access and change notifications for TreeDataGrid internals.

It is public and useful in advanced scenarios where you build custom row/selection infrastructure.

## Why It Exists

Sources and selection internals need a predictable collection surface:

- count and indexed access
- collection changed notifications
- consistent behavior for different input `IEnumerable` types

`TreeDataGridItemsSourceView` provides that abstraction.

## Basic Usage

Untyped:

```csharp
using Avalonia.Controls;

var view = TreeDataGridItemsSourceView.GetOrCreate(items);
var count = view.Count;
var first = view[0];
```

Typed:

```csharp
var typed = TreeDataGridItemsSourceView<MyModel>.GetOrCreate(items);
MyModel? value = typed.GetAt(0);
```

## Behavior by Source Type

Given `new TreeDataGridItemsSourceView(source)`:

- if `source` is `IList`: wraps directly
- if `source` is `IEnumerable<object>`: copies to `List<object>`
- if `source` is non-generic `IEnumerable`: copies via `Cast<object>()`
- if `source` implements `INotifyCollectionChanged` but not `IList`: throws

This constraint exists because internals require stable index-based operations.

## Collection Change Subscription

```csharp
view.CollectionChanged += (_, e) =>
{
    // react to Add/Remove/Move/Replace/Reset
};
```

Remember to dispose when done:

```csharp
view.Dispose();
```

## `GetOrCreate` Pattern

Prefer factory helpers when you may already hold a view:

```csharp
var view = TreeDataGridItemsSourceView.GetOrCreate(maybeViewOrEnumerable);
```

Benefits:

- avoids double-wrapping
- returns `Empty` for null input
- keeps internals consistent

## Mapping APIs

Available utility methods:

- `GetAt(int)`
- `IndexOf(object?)`
- `Count`
- indexer `this[int]`

`KeyFromIndex` / `IndexFromKey` exist but are currently not implemented.

## Advanced Note

Flat and hierarchical sources both use typed `TreeDataGridItemsSourceView<TModel>` internally to drive row model pipelines and selection model updates.

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.TreeDataGridItemsSourceView`1>
- <xref:Avalonia.Controls.TreeDataGridItemsSourceView>

## Related

- [Flat Source Guide](sources-flat.md)
- [Hierarchical Source Guide](sources-hierarchical.md)
- [Architecture and Data Flow](../concepts/architecture-and-data-flow.md)
- [Troubleshooting Guide](troubleshooting.md)
