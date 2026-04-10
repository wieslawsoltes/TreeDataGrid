---
title: "Filtering"
---

# Filtering

Filtering is available on `FlatTreeDataGridSource<TModel>` and `HierarchicalTreeDataGridSource<TModel>`.

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

## Important

- Filtering is currently part of the code `Source` workflow.
- There is no separate XAML-only filtering API yet.
- Hierarchical filters are evaluated per item; parent items are not automatically kept visible just because a child matches.
