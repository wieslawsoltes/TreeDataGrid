using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Presentation;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Core = global::TreeDataGridCore;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests;

public class CoreCellRecyclingTests
{
    [AvaloniaFact]
    public void Pooled_binding_releases_nested_subscriptions_and_retargets_edits()
    {
        var first = new Node("First");
        var second = new Node("Second");
        using var source = Source(first, second);
        using var view = new TreeDataGridPresentation<Node>(source);
        var cell = (ITextCell)view.Rows.RealizeCell(view.Columns[0], 0, 0);
        Assert.True(first.State.SubscriberCount > 0);
        Assert.True(((IRecyclingCellRows)view.Rows).TryRecycleCell(view.Columns[0], cell));
        Assert.Equal(0, first.State.SubscriberCount);
        Assert.Null(cell.Value);

        first.State.Name = "Old row changed";
        Assert.Null(cell.Value);
        Assert.Same(cell, view.Rows.RealizeCell(view.Columns[0], 0, 1));
        Assert.Equal("Second", cell.Text);
        second.State.Name = "Updated";
        Assert.Equal("Updated", cell.Text);
        cell.Text = "Edited";
        Assert.Equal("Edited", second.State.Name);
        Assert.Equal("Old row changed", first.State.Name);
        view.Rows.UnrealizeCell(cell, 0, 1);
        Assert.Equal(0, second.State.SubscriberCount);
    }

    [AvaloniaFact]
    public void Checkboxes_release_subscriptions_and_retarget()
    {
        var first = new Node("First");
        var second = new Node("Second");
        second.State.Enabled = true;
        using var source = Source(first, second);
        source.Columns.Add(new Core.Models.CheckBoxColumn<Node>("Enabled", x => x.State.Enabled,
            (x, value) => x.State.Enabled = value));
        using var view = new TreeDataGridPresentation<Node>(source);
        var cell = Assert.IsType<CheckBoxCell>(view.Rows.RealizeCell(view.Columns[2], 2, 0));
        Assert.True(((IRecyclingCellRows)view.Rows).TryRecycleCell(view.Columns[2], cell));
        Assert.Equal(0, first.State.SubscriberCount);
        Assert.Same(cell, view.Rows.RealizeCell(view.Columns[2], 2, 1));
        Assert.True(cell.Value);
        cell.Value = false;
        Assert.False(second.State.Enabled);
        view.Rows.UnrealizeCell(cell, 2, 1);
    }

    [AvaloniaFact]
    public void Pools_do_not_mix_column_bindings_or_views()
    {
        using var source = Source(new Node("First"));
        using var firstView = new TreeDataGridPresentation<Node>(source);
        using var secondView = new TreeDataGridPresentation<Node>(source);
        var cell = firstView.Rows.RealizeCell(firstView.Columns[0], 0, 0);
        Assert.True(((IRecyclingCellRows)firstView.Rows).TryRecycleCell(firstView.Columns[0], cell));
        var otherColumn = firstView.Rows.RealizeCell(firstView.Columns[1], 1, 0);
        var otherView = secondView.Rows.RealizeCell(secondView.Columns[0], 0, 0);
        Assert.NotSame(cell, otherColumn);
        Assert.Equal("Other", ((ITextCell)otherColumn).Text);
        Assert.NotSame(cell, otherView);
        firstView.Rows.UnrealizeCell(otherColumn, 1, 0);
        secondView.Rows.UnrealizeCell(otherView, 0, 0);
        Assert.Same(cell, firstView.Rows.RealizeCell(firstView.Columns[0], 0, 0));
        firstView.Rows.UnrealizeCell(cell, 0, 0);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Hiding_columns_and_suspending_views_clear_pools(bool suspend)
    {
        using var source = Source(new Node("First"));
        using var view = new TreeDataGridPresentation<Node>(source);
        var column = view.Columns[0];
        var cell = view.Rows.RealizeCell(column, 0, 0);
        Assert.True(((IRecyclingCellRows)view.Rows).TryRecycleCell(column, cell));
        if (suspend)
        {
            view.Suspend();
            view.Resume();
        }
        else
        {
            source.Columns[0].IsVisible = false;
            source.Columns[0].IsVisible = true;
        }
        var replacement = view.Rows.RealizeCell(view.Columns[0], 0, 0);
        Assert.NotSame(cell, replacement);
        view.Rows.UnrealizeCell(replacement, 0, 0);
    }

    [AvaloniaFact]
    public void Pool_evicts_models_when_its_budget_is_exceeded()
    {
        using var source = Source(new Node("First"));
        using var view = new TreeDataGridPresentation<Node>(source);
        var cells = Enumerable.Range(0, CellModelPool<Node>.MaximumCells + 1)
            .Select(_ => view.Rows.RealizeCell(view.Columns[0], 0, 0)).ToArray();
        foreach (var cell in cells)
            Assert.True(((IRecyclingCellRows)view.Rows).TryRecycleCell(view.Columns[0], cell));
        Assert.Same(cells[^1], view.Rows.RealizeCell(view.Columns[0], 0, 0));
        var replacement = view.Rows.RealizeCell(view.Columns[0], 0, 0);
        Assert.DoesNotContain(replacement, cells);
        view.Rows.UnrealizeCell(cells[^1], 0, 0);
        view.Rows.UnrealizeCell(replacement, 0, 0);
    }

    [AvaloniaFact]
    public void Pooled_cells_do_not_retain_the_original_row_or_value()
    {
        var (view, weakRow) = CreatePoolWithRemovedRow();
        using (view)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.False(weakRow.IsAlive);
            GC.KeepAlive(view);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (TreeDataGridPresentation<Node>, WeakReference) CreatePoolWithRemovedRow()
    {
        var original = new Node("Original");
        var items = new ObservableCollection<Node> { original, new("Replacement") };
        var source = Source(items.ToArray());
        source.Items = items;
        var view = new TreeDataGridPresentation<Node>(source);
        var cell = view.Rows.RealizeCell(view.Columns[0], 0, 0);
        Assert.True(((IRecyclingCellRows)view.Rows).TryRecycleCell(view.Columns[0], cell));
        items.RemoveAt(0);
        // Flat Core rows reuse a row cursor; move it away from the removed item too.
        _ = source.Rows[0];
        return (view, new WeakReference(original));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Horizontal_scrolling_reuses_models_and_detach_releases_subscriptions(bool insertColumn)
    {
        var item = new Node("First");
        using var source = Source(item);
        for (var i = 2; i < 20; ++i)
            source.Columns.Add(new Core.Models.TextColumn<Node, string>("More", x => x.State.Name,
                new Core.GridLength(100)));
        var grid = new TreeDataGrid { Model = source, Template = TestTemplates.TreeDataGridTemplate() };
        var window = new TestWindow(grid, new Size(200, 100))
        {
            Styles =
            {
                TestTemplates.TreeDataGridRowStyle,
                new Style(x => x.OfType<TreeDataGridRow>())
                { Setters = { new Setter(TreeDataGridRow.HeightProperty, 20.0) } },
            },
        };
        try
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var row = (TreeDataGridRow)grid.RowsPresenter!.TryGetElement(0)!;
            var original = ((TreeDataGridCell)row.CellsPresenter!.TryGetElement(0)!).Model;
            if (insertColumn)
            {
                source.Columns.Insert(0, new Core.Models.TextColumn<Node, string>("Inserted", x => x.Other,
                    new Core.GridLength(100)));
                window.UpdateLayout();
            }
            grid.Scroll!.Offset = new Vector(300, 0);
            window.UpdateLayout();
            var originalIndex = insertColumn ? 1 : 0;
            Assert.Null(row.CellsPresenter.TryGetElement(originalIndex));
            grid.Scroll.Offset = new Vector(originalIndex * 100, 0);
            window.UpdateLayout();
            var rebound = (TreeDataGridCell)row.CellsPresenter.TryGetElement(originalIndex)!;
            Assert.Same(original, rebound.Model);
            Assert.Equal("First", ((ITextCell)rebound.Model!).Text);
            window.Content = null;
            window.UpdateLayout();
            Assert.Equal(0, item.State.SubscriberCount);
        }
        finally { window.Close(); }
    }

    private static Core.FlatTreeDataGridSource<Node> Source(params Node[] items)
    {
        var source = new Core.FlatTreeDataGridSource<Node>(items);
        source.Columns.Add(new Core.Models.TextColumn<Node, string>("Name", x => x.State.Name,
            (x, value) => x.State.Name = value, new Core.GridLength(100)));
        source.Columns.Add(new Core.Models.TextColumn<Node, string>("Other", x => x.Other,
            new Core.GridLength(100)));
        return source;
    }

    public sealed class Node
    {
        public Node(string name) => State.Name = name;
        public State State { get; } = new();
        public string Other => "Other";
    }

    public sealed class State : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        private string _name = "";
        private bool _enabled;
        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public string Name { get => _name; set { _name = value; _changed?.Invoke(this, new(nameof(Name))); } }
        public bool Enabled { get => _enabled; set { _enabled = value; _changed?.Invoke(this, new(nameof(Enabled))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
}
