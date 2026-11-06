---
title: "Filtering"
---

# Filtering

Filtering is available on `FlatTreeDataGridSource<TModel>` and `HierarchicalTreeDataGridSource<TModel>`,
both the Avalonia sources and the shared `TreeDataGridCore` sources used by the Core-model Avalonia
views and the Uno control.

## Basic Usage

```csharp
Source.Filter(x => x.Name.Contains(_searchText, StringComparison.CurrentCultureIgnoreCase));
```

Pass `null` to remove the filter:

```csharp
Source.Filter(null);
```

## Refresh Existing Filters

If your predicate depends on external state, re-run it with `RefreshFilter`:

```csharp
public string SearchText
{
    get => _searchText;
    set
    {
        _searchText = value;
        Source.RefreshFilter();
    }
}
```

## Behavior

- Row model indexes refer to the filtered items while a filter is applied.
- A hierarchical filter applies at every level; an expander is shown only when a child matches.
- Hierarchical rows are recreated by `RefreshFilter`, so expansion that is not bound to the model resets.
- `IsFiltered` reports whether a predicate is applied. Row moves (drag and drop) are rejected while filtered.

## Important

- Filtering is currently part of the code `Source` workflow.
- There is no separate XAML-only filtering API yet.
- Hierarchical filters are evaluated per item; parent items are not automatically kept visible just because a child matches.
