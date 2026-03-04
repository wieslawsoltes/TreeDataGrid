---
title: "Text Column Guide"
---

# Text Column Guide

`TextColumn<TModel, TValue>` displays typed values as text and optionally supports editing and text search.

## Constructors

Read-only:

```csharp
new TextColumn<Person, string>("First Name", x => x.FirstName)
```

Editable:

```csharp
new TextColumn<Person, string>(
    "First Name",
    x => x.FirstName,
    (m, v) => m.FirstName = v)
```

With width and options:

```csharp
new TextColumn<Person, int>(
    "Age",
    x => x.Age,
    width: new GridLength(120),
    options: new TextColumnOptions<Person>
    {
        StringFormat = "{0} years",
        TextAlignment = Avalonia.Media.TextAlignment.Right,
    })
```

## Column Options

`TextColumnOptions<TModel>` extends `ColumnOptions<TModel>`.

Text-specific options:

- `IsTextSearchEnabled`
- `StringFormat`
- `Culture`
- `TextTrimming`
- `TextWrapping`
- `TextAlignment`

Inherited options include:

- `CanUserSortColumn`
- `CanUserResizeColumn`
- `CompareAscending` / `CompareDescending`
- `MinWidth`, `MaxWidth`
- `BeginEditGestures`

## Editing Behavior

A `TextColumn` is editable only when constructor includes setter.

Edit lifecycle in cells:

- begins via configured gesture (for example `F2`, `DoubleTap`)
- commits on Enter / focus loss
- cancels on Escape

## Search Behavior

When `IsTextSearchEnabled = true`, row selection keyboard text search can match values from this column.

Example:

```csharp
new TextColumn<Person, string>(
    "Country",
    x => x.Name,
    options: new TextColumnOptions<Person>
    {
        IsTextSearchEnabled = true,
    })
```

## Formatting Notes

`StringFormat` is applied via `string.Format(Culture, StringFormat, value)`.

For nullable values, ensure format handles null cases gracefully.

## Common Patterns

Numeric with right alignment:

```csharp
new TextColumn<Order, decimal>(
    "Total",
    x => x.Total,
    options: new TextColumnOptions<Order>
    {
        StringFormat = "{0:N2}",
        TextAlignment = Avalonia.Media.TextAlignment.Right,
    })
```

Long text wrapping:

```csharp
new TextColumn<Article, string?>(
    "Extract",
    x => x.Extract,
    width: GridLength.Star,
    options: new TextColumnOptions<Article>
    {
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        TextTrimming = Avalonia.Media.TextTrimming.None,
    })
```

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.Models.TreeDataGrid.TextColumn`2>
- <xref:Avalonia.Controls.Models.TreeDataGrid.TextColumnOptions`1>

## Related

- [Editing and Begin Edit Gestures](editing-and-begin-edit-gestures.md)
- [Sorting and Column Widths](sorting-and-column-widths.md)
- [Columns, Cells, and Rows](../concepts/columns-cells-rows.md)
- [Troubleshooting Guide](troubleshooting.md)
