using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using TreeDataGridDemo.Models;

namespace TreeDataGridDemo.ViewModels;

internal class DragDropPageViewModel
{
    public DragDropPageViewModel()
    {
        var source = new HierarchicalTreeDataGridSource<DragDropItem>(DragDropItem.CreateRandomItems())
        {
            Columns =
            {
                new HierarchicalExpanderColumn<DragDropItem>(
                    new TextColumn<DragDropItem, string>("Name", x => x.Name, new GridLength(1, GridUnitType.Star)),
                    x => x.Children),
                new CheckBoxColumn<DragDropItem>("Allow Drag", x => x.AllowDrag, (model, value) => model.AllowDrag = value),
                new CheckBoxColumn<DragDropItem>("Allow Drop", x => x.AllowDrop, (model, value) => model.AllowDrop = value),
            }
        };

        source.RowSelection!.SingleSelect = false;
        Source = source;
    }

    public ITreeDataGridSource<DragDropItem> Source { get; }
}
