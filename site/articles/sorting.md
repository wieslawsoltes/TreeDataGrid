---
title: "Sorting"
---

# Sorting

Users can sort by clicking a column header when both of these allow it:

- `TreeDataGrid.CanUserSortColumns`
- the column's own `CanUserSortColumn`

## Disable Sorting in XAML

```xml
<TreeDataGridTextColumn Header="Name"
                        Binding="{Binding Name}"
                        CanUserSortColumn="False"/>
```

## Custom Comparisons

The fluent API uses `Comparison<object?>` delegates for custom sorting:

```csharp
source.WithTextColumn("Name", x => x.Name, o =>
{
    o.CompareAscending = (a, b) =>
        string.Compare(((Person?)a)?.Name, ((Person?)b)?.Name, StringComparison.CurrentCulture);
});
```

## Clear Sorting

Both source types expose `ClearSort`:

```csharp
Source.ClearSort();
```
