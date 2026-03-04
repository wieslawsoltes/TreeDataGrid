# Custom Element Factory

Use [TreeDataGridElementFactory](xref:Avalonia.Controls.Primitives.TreeDataGridElementFactory) when you need to control which visual controls are created for rows, cells, and headers, and how those controls are recycled.

## When to Customize

Customize the factory when you need one of these:

- specialized cell controls with extra behavior
- custom recycling strategy (for expensive visuals)
- different control choice for custom `ICell` or `IRow` implementations

For standard scenarios, keep the default factory.

## How the Factory Pipeline Works

The grid and presenters call the factory through these stages:

1. resolve a recycle key from model data (`GetDataRecycleKey`)
2. try to reuse a pooled control with a compatible parent
3. otherwise create a new control (`CreateElement`)
4. when unrealized, return control to pool (`RecycleElement`)

Built-in model to control mapping in the default implementation:

- [CheckBoxCell](xref:Avalonia.Controls.Models.TreeDataGrid.CheckBoxCell) -> [TreeDataGridCheckBoxCell](xref:Avalonia.Controls.Primitives.TreeDataGridCheckBoxCell)
- [TemplateCell](xref:Avalonia.Controls.Models.TreeDataGrid.TemplateCell) -> [TreeDataGridTemplateCell](xref:Avalonia.Controls.Primitives.TreeDataGridTemplateCell)
- [IExpanderCell](xref:Avalonia.Controls.Models.TreeDataGrid.IExpanderCell) -> [TreeDataGridExpanderCell](xref:Avalonia.Controls.Primitives.TreeDataGridExpanderCell)
- [ICell](xref:Avalonia.Controls.Models.TreeDataGrid.ICell) -> [TreeDataGridTextCell](xref:Avalonia.Controls.Primitives.TreeDataGridTextCell)
- [IColumn](xref:Avalonia.Controls.Models.TreeDataGrid.IColumn) -> [TreeDataGridColumnHeader](xref:Avalonia.Controls.Primitives.TreeDataGridColumnHeader)
- [IRow](xref:Avalonia.Controls.Models.TreeDataGrid.IRow) -> [TreeDataGridRow](xref:Avalonia.Controls.Primitives.TreeDataGridRow)

## Minimal Custom Factory

```csharp
using System;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;

public sealed class MyTreeDataGridElementFactory : TreeDataGridElementFactory
{
    protected override Control CreateElement(object? data)
    {
        return data switch
        {
            // Keep existing defaults where possible.
            CheckBoxCell => new TreeDataGridCheckBoxCell(),
            TemplateCell => new TreeDataGridTemplateCell(),
            IExpanderCell => new TreeDataGridExpanderCell(),
            ICell => new TreeDataGridTextCell(),
            IColumn => new TreeDataGridColumnHeader(),
            IRow => new TreeDataGridRow(),
            _ => throw new NotSupportedException($"Unsupported model type: {data?.GetType().FullName}")
        };
    }

    protected override string GetDataRecycleKey(object? data)
    {
        // Pool by control category, not by model type instance.
        return data switch
        {
            CheckBoxCell => nameof(TreeDataGridCheckBoxCell),
            TemplateCell => nameof(TreeDataGridTemplateCell),
            IExpanderCell => nameof(TreeDataGridExpanderCell),
            ICell => nameof(TreeDataGridTextCell),
            IColumn => nameof(TreeDataGridColumnHeader),
            IRow => nameof(TreeDataGridRow),
            _ => base.GetDataRecycleKey(data),
        };
    }
}
```

Apply it:

```csharp
grid.ElementFactory = new MyTreeDataGridElementFactory();
```

## Recycling Rules

Use stable recycle keys. If keys are too specific, reuse drops and allocations rise. If keys are too broad, incompatible controls can be reused incorrectly.

Recommended approach:

- key by compatible control shape (row, text-cell, checkbox-cell, header)
- avoid keying by transient runtime state
- keep `CreateElement` and key methods symmetrical

## Common Failure Modes

- `NotSupportedException` during scrolling
Cause: custom model type is returned by source but factory mapping was not updated.

- stale visuals after reuse
Cause: custom control keeps state that is not reset during realize/unrealize cycle.

- heavy memory churn
Cause: recycle key prevents effective pooling.

## API Coverage Checklist

- <xref:Avalonia.Controls.Primitives.TreeDataGridElementFactory>

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## Related

- [Primitives Overview](primitives-overview.md)
- [Performance, Virtualization, and Realization](performance-virtualization-and-realization.md)
- [Troubleshooting Guide](../guides/troubleshooting.md)
- [Namespace: Avalonia.Controls.Primitives](../reference/namespace-primitives.md)
