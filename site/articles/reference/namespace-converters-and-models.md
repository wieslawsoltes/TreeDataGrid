---
title: "Namespace: Converters and Base Models"
---

# Namespace: Converters and Base Models

This page covers foundational helper APIs from:

- `Avalonia.Controls.Converters`
- `Avalonia.Controls.Models`

## Functional Groups

Use these groups to separate value conversion from collection/model helpers.

### Presentation Converter

- [IndentConverter](xref:Avalonia.Controls.Converters.IndentConverter)

### Notification and Collection Base Types

- [NotifyingBase](xref:Avalonia.Controls.Models.NotifyingBase)
- [`NotifyingListBase<T>`](xref:Avalonia.Controls.Models.NotifyingListBase`1)
- [`ReadOnlyListBase<T>`](xref:Avalonia.Controls.Models.ReadOnlyListBase`1)

## Guidance

Use these types when building internal infrastructure for custom sources, rows, and columns.

- Prefer [NotifyingBase](xref:Avalonia.Controls.Models.NotifyingBase) for simple `INotifyPropertyChanged` models.
- Use [`NotifyingListBase<T>`](xref:Avalonia.Controls.Models.NotifyingListBase`1) when range notifications are required.
- Use [`ReadOnlyListBase<T>`](xref:Avalonia.Controls.Models.ReadOnlyListBase`1) for adapter-style list projections.

## Complete Type Index

| Type | Kind | Primary Article |
|---|---|---|
| <xref:Avalonia.Controls.Converters.IndentConverter> | Class | [guides/templates-and-styling.md](../guides/templates-and-styling.md) |
| <xref:Avalonia.Controls.Models.NotifyingBase> | Class | [advanced/custom-rows-columns-pipeline.md](../advanced/custom-rows-columns-pipeline.md) |
| <xref:Avalonia.Controls.Models.NotifyingListBase`1> | Class | [advanced/custom-rows-columns-pipeline.md](../advanced/custom-rows-columns-pipeline.md) |
| <xref:Avalonia.Controls.Models.ReadOnlyListBase`1> | Class | [advanced/custom-rows-columns-pipeline.md](../advanced/custom-rows-columns-pipeline.md) |

## Related

- [Templates and Styling](../guides/templates-and-styling.md)
- [Custom Rows and Columns Pipeline](../advanced/custom-rows-columns-pipeline.md)
- [API Coverage Index](api-coverage-index.md)
