# Editing and Begin Edit Gestures

Editing in TreeDataGrid is controlled by:

- column/cell edit capability (`CanEdit`)
- configured gesture flags (`BeginEditGestures`)
- current selection context (`WhenSelected` flag)

## Gesture Flags

`BeginEditGestures` values:

- `None`
- `F2`
- `Tap`
- `DoubleTap`
- `WhenSelected`
- `Default` (`F2 | DoubleTap`)

`WhenSelected` acts as a modifier: gesture only begins edit when row/cell is selected.

## Configure Gestures per Column

```csharp
new TextColumn<Person, string>(
    "Name",
    x => x.FirstName,
    (m, v) => m.FirstName = v,
    options: new TextColumnOptions<Person>
    {
        BeginEditGestures = BeginEditGestures.F2 |
                            BeginEditGestures.Tap |
                            BeginEditGestures.WhenSelected,
    })
```

Works similarly for `TemplateColumnOptions<TModel>` and `CheckBoxColumnOptions<TModel>`.

## Editing Lifecycle

For editable cells:

1. begin edit (gesture)
2. update value in editor
3. commit (`Enter` / focus lost) or cancel (`Escape`)
4. model value update propagated through binding/setter
5. `CellValueChanged` may be raised by grid

## Column-Specific Notes

- `TextColumn`: editable only with setter constructor overload
- `TemplateColumn`: editable only when editing template exists
- `CheckBoxColumn`: value changes immediately when not read-only

## Keyboard Behavior

- `F2`: begin edit when enabled
- `Enter`: commit current edit
- `Escape`: cancel current edit

## Common Pitfalls

- expecting edit on tap when `Tap` not in gesture flags
- expecting edit when column is read-only (no setter / no edit template)
- using `WhenSelected` without ensuring proper selection mode/state

## Troubleshooting

- Feature behavior differs from expectations
Cause: one or more options in this scenario are configured differently (source type, column options, sort/selection/edit state).
Fix: compare your setup with the snippet in this article and verify runtime values on `Source`, `Columns`, and `Selection`.

- Data changes are not visible in UI
Cause: model or collection notifications are missing, or a replaced collection/source is not re-bound.
Fix: ensure `INotifyPropertyChanged`/`INotifyCollectionChanged` flow is active and reassign `Source` after replacing underlying collections.

## API Coverage Checklist

- <xref:Avalonia.Controls.Models.TreeDataGrid.BeginEditGestures>
- <xref:Avalonia.Controls.Models.TreeDataGrid.ICellOptions>
- <xref:Avalonia.Controls.Models.TreeDataGrid.ITemplateCellOptions>
- <xref:Avalonia.Controls.Models.TreeDataGrid.ITextCellOptions>

## Related

- [Text Column Guide](column-text.md)
- [Template Column Guide](column-template.md)
- [Events and User Interaction](events-and-user-interaction.md)
- [Troubleshooting Guide](troubleshooting.md)
