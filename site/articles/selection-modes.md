---
title: "Selection Modes"
---

# Selection Modes

TreeDataGrid now exposes selection configuration directly on the control through `SelectionMode`.

## Values

- `Row`: single row selection
- `Row,Multiple`: multiple row selection
- `Cell`: single cell selection
- `Cell,Multiple`: multiple cell selection

## Example

```xml
<TreeDataGrid ItemsSource="{Binding People}"
              SelectionMode="Row,Multiple"/>
```

## Control-Level Event

You can listen to `TreeDataGrid.SelectionChanged` without reaching into the source selection model:

```csharp
private void OnSelectionChanged(object? sender, TreeDataGridSelectionChangedEventArgs e)
{
    foreach (var item in e.SelectedItems)
    {
        // ...
    }
}
```

The event args expose:

- `SelectedIndexes` and `DeselectedIndexes`
- `SelectedItems` and `DeselectedItems`
- `SelectedCellIndexes` and `DeselectedCellIndexes`

## Source-Based Selection

If you use the code `Source` path, the underlying row and cell selection models now raise `TreeDataGridSelectionChangedEventArgs` as well.
