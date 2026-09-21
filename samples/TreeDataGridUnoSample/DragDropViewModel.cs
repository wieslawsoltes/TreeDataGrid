using System;
using System.Collections.ObjectModel;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridDemo.Models;

namespace TreeDataGridUnoSample;

/// <summary>The Avalonia drag/drop model, presented by the actual shared Core source.</summary>
public sealed class DragDropViewModel : IDisposable
{
    public DragDropViewModel()
    {
        Source = new(CreateItems());
        Source.Columns.Add(new HierarchicalExpanderColumn<DragDropItem>(
            new TextColumn<DragDropItem, string>("Name", x => x.Name, width: GridLength.Star), x => x.Children));
        Source.Columns.Add(new CheckBoxColumn<DragDropItem>("Allow Drag", x => x.AllowDrag, (model, value) => model.AllowDrag = value, width: new(130)));
        Source.Columns.Add(new CheckBoxColumn<DragDropItem>("Allow Drop", x => x.AllowDrop, (model, value) => model.AllowDrop = value, width: new(130)));
        Source.RowSelection!.SingleSelect = false;
    }
    public HierarchicalTreeDataGridSource<DragDropItem> Source { get; }
    public void Reset()
    {
        Source.ClearSort();
        Source.Items = CreateItems();
    }
    private static ObservableCollection<DragDropItem> CreateItems()
    {
        // Stable, nonempty initial data while sharing the original model class.
        // Its lazy child collection and AllowDrag/AllowDrop properties are unchanged.
        var result = new ObservableCollection<DragDropItem>();
        foreach (var name in new[] { "Design", "Engineering", "Research", "Operations" })
        {
            var parent = new DragDropItem(name);
            parent.Children.Clear();
            for (var i = 1; i <= 6; ++i)
            {
                var child = new DragDropItem($"{name} task {i}");
                child.Children.Clear();
                parent.Children.Add(child);
            }
            result.Add(parent);
        }
        return result;
    }
    public void Dispose() => Source.Dispose();
}
