---
title: "CheckBox Column Guide"
---

# CheckBox Column Guide

`CheckBoxColumn<TModel>` displays boolean values as checkboxes.

It supports:

- read-only two-state (`bool` getter only)
- editable two-state (`bool` getter + setter)
- read-only/editable three-state (`bool?` getter + optional setter)

## Constructors

Two-state read-only:

```csharp
new CheckBoxColumn<Person>("Active", x => x.IsActive)
```

Two-state editable:

```csharp
new CheckBoxColumn<Person>("Active", x => x.IsActive, (m, v) => m.IsActive = v)
```

Three-state editable:

```csharp
new CheckBoxColumn<Person>("Verified", x => x.IsVerified, (m, v) => m.IsVerified = v)
```

## Three-State Notes

The `bool?` overload enables three-state behavior (`true`, `false`, `null`).

Internally:

- two-state constructor wraps values into nullable form
- three-state constructor marks column as three-state

## Column Options

`CheckBoxColumnOptions<TModel>` inherits common `ColumnOptions<TModel>`.

Useful settings:

- `CanUserResizeColumn`
- `CanUserSortColumn`
- `CompareAscending` / `CompareDescending`
- `MinWidth`, `MaxWidth`

Example:

```csharp
new CheckBoxColumn<FileNode>(
    header: null,
    getter: x => x.IsChecked,
    setter: (m, v) => m.IsChecked = v,
    options: new CheckBoxColumnOptions<FileNode>
    {
        CanUserResizeColumn = false,
        MinWidth = new GridLength(28),
        MaxWidth = new GridLength(40),
    })
```

## Sorting

Sorting follows `ColumnOptions<TModel>` comparers when provided.

For custom sort order, set:

- `CompareAscending`
- `CompareDescending`

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.Models.TreeDataGrid.CheckBoxColumn`1>
- <xref:Avalonia.Controls.Models.TreeDataGrid.CheckBoxColumnOptions`1>

## Related

- [Sorting and Column Widths](sorting-and-column-widths.md)
- [Columns, Cells, and Rows](../concepts/columns-cells-rows.md)
- [Troubleshooting Guide](troubleshooting.md)
