using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls.Models;

namespace TreeDataGridDemo.Models;

public class DragDropItem : NotifyingBase
{
    private static readonly string[] s_names =
    {
        "Alex", "Jamie", "Taylor", "Jordan", "Morgan", "Casey", "Riley", "Avery",
        "Harper", "Parker", "Quinn", "Reese", "Skyler", "Cameron", "Drew", "Rowan",
    };

    private static readonly Random s_random = new(0);

    private readonly int _depth;
    private ObservableCollection<DragDropItem>? _children;
    private bool _allowDrag = true;
    private bool _allowDrop = true;

    public DragDropItem(string name, int depth = 0)
    {
        Name = name;
        _depth = depth;
    }

    public string Name { get; }

    public bool AllowDrag
    {
        get => _allowDrag;
        set => RaiseAndSetIfChanged(ref _allowDrag, value);
    }

    public bool AllowDrop
    {
        get => _allowDrop;
        set => RaiseAndSetIfChanged(ref _allowDrop, value);
    }

    public ObservableCollection<DragDropItem> Children => _children ??= CreateRandomItems(_depth + 1);

    public static ObservableCollection<DragDropItem> CreateRandomItems(int depth = 0)
    {
        var count = depth >= 2 ? 0 : s_random.Next(4, 9);
        return new ObservableCollection<DragDropItem>(Enumerable.Range(1, count)
            .Select(index => new DragDropItem($"{s_names[s_random.Next(s_names.Length)]} {index}", depth)));
    }
}
