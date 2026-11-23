using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public class ViewRowsCompatibilityTests
{
    [Fact]
    public void Facade_returns_Core_rows_indexes_and_collection_event_payloads()
    {
        var items = new ObservableCollection<Item> { new("b"), new("a") };
        using var source = new FlatTreeDataGridSource<Item>(items);
        source.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name));
        using var view = TreeDataGridPresentation.Create(source);
        UI.ITreeDataGridRows rows = view.Rows;
        Assert.Same(source.Rows[0], rows[0]);
        Assert.Same(items[0], rows[0].Model);
        Assert.Equal(source.Rows.RowIndexToModelIndex(1), rows.RowIndexToModelIndex(1));
        NotifyCollectionChangedEventArgs? coreArgs = null;
        NotifyCollectionChangedEventArgs? viewArgs = null;
        source.Rows.CollectionChanged += (_, args) => coreArgs = args;
        rows.CollectionChanged += (_, args) => viewArgs = args;
        items.Add(new("c"));
        Assert.Same(coreArgs, viewArgs);
        Assert.Equal(3, rows.Count);
        source.SortBy(source.Columns[0], ListSortDirection.Ascending);
        Assert.Equal(NotifyCollectionChangedAction.Reset, viewArgs!.Action);
        Assert.Same(items[1], rows[0].Model);
        Assert.Equal(0, rows.ModelIndexToRowIndex(new IndexPath(1)));
        Assert.Equal((0, 0d), rows.GetRowAt(0));
        Assert.Equal((-1, -1d), rows.GetRowAt(20));
    }

    [Fact]
    public void Public_cells_bind_to_original_models_and_release_their_observation()
    {
        var item = new Item("Original");
        using var source = new FlatTreeDataGridSource<Item>([item]);
        source.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name, (model, value) => model.Name = value));
        using var view = TreeDataGridPresentation.Create(source);
        var rows = view.Rows;
        var cell = rows.RealizeCell(view.Columns[0], 0, 0);
        Assert.Equal("Original", cell.Value);
        Assert.True(item.Subscribers > 0);
        var text = Assert.IsAssignableFrom<UI.ITextCell>(cell);
        text.Text = "Written";
        Assert.Equal("Written", item.Name);
        item.Name = "External";
        Assert.Equal("External", cell.Value);
        rows.UnrealizeCell(cell, 0, 0);
        Assert.Equal(0, item.Subscribers);
    }

    [Fact]
    public void Adapted_and_direct_custom_columns_return_the_original_UI_cell()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("Item")]);
        source.Columns.Add(new TextColumn<Item, string>("Custom", x => x.Name) { PresentationKey = "Custom" });
        var supplied = new Cell();
        var column = new Column(() => supplied);
        var options = new TreeDataGridPresentationOptions<Item>();
        options.Columns["Custom"] = _ => column;
        using var view = TreeDataGridPresentation.Create(source, options);
        var rows = view.Rows;
        var cell = rows.RealizeCell(view.Columns[0], 0, 0);
        Assert.Same(supplied, cell);
        Assert.Equal(0, supplied.Subscribers);
        rows.UnrealizeCell(cell, 0, 0);
        Assert.Equal(1, supplied.Disposals);

        var direct = new Cell();
        cell = rows.RealizeCell(new Column(() => direct), 0, 0);
        Assert.Same(direct, cell);
        rows.UnrealizeCell(cell, 0, 0);
        Assert.Equal(1, direct.Disposals);
    }

    [Fact]
    public void Suspend_resume_and_dispose_control_notifications_and_new_realization()
    {
        var items = new ObservableCollection<Item> { new("Item") };
        using var source = new FlatTreeDataGridSource<Item>(items);
        source.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name));
        using var view = TreeDataGridPresentation.Create(source);
        var rows = view.Rows;
        var changes = 0;
        rows.CollectionChanged += (_, _) => ++changes;
        view.Suspend();
        items.Add(new("Suspended"));
        Assert.Equal(0, changes);
        Assert.Throws<InvalidOperationException>(() => rows.RealizeCell(view.Columns[0], 0, 0));
        view.Resume();
        items.Add(new("Resumed"));
        Assert.True(changes > 0);
        var cell = rows.RealizeCell(view.Columns[0], 0, 0);
        view.Dispose();
        changes = 0;
        items.Add(new("After dispose"));
        Assert.Equal(0, changes);
        Assert.Throws<ObjectDisposedException>(() => rows.RealizeCell(new Column(() => new Cell()), 0, 0));
        // The caller still owns an active public cell and can always release it.
        rows.UnrealizeCell(cell, 0, 0);
        Assert.Equal(0, items[0].Subscribers);
    }

    [Fact]
    public void A_cell_created_during_retirement_is_disposed_instead_of_published()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("Item")]);
        using var view = TreeDataGridPresentation.Create(source);
        var rows = view.Rows;
        var cell = new Cell();
        var column = new Column(() => { view.Suspend(); return cell; });
        Assert.Throws<OperationCanceledException>(() => rows.RealizeCell(column, 0, 0));
        Assert.Equal(1, cell.Disposals);
    }

    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        private PropertyChangedEventHandler? _changed;
        public int Subscribers { get; private set; }
        public string Name { get => _name; set { _name = value; _changed?.Invoke(this, new(nameof(Name))); } }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
    }
    private sealed class Cell : UI.ICell, INotifyPropertyChanged, IDisposable
    {
        public int Subscribers { get; private set; }
        public int Disposals { get; private set; }
        public bool CanEdit => false;
        public UI.BeginEditGestures EditGestures => UI.BeginEditGestures.None;
        public object? Value => "Custom";
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { ++Subscribers; }
            remove { --Subscribers; }
        }
        public void Dispose() => ++Disposals;
    }
    private sealed class Column(Func<UI.ICell> create) : NotifyingBase, ICellColumn<Item>
    {
        public double ActualWidth => 100;
        public bool? CanUserResize => true;
        public object? Header => "Custom";
        public Microsoft.UI.Xaml.GridLength Width { get; private set; } = new(100);
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        public double MinActualWidth => 0;
        public double MaxActualWidth => double.PositiveInfinity;
        public bool StarWidthWasConstrained => false;
        public double CellMeasured(double width, int rowIndex) => width;
        public void CalculateStarWidth(double availableWidth, double totalStars) { }
        public bool CommitActualWidth() => false;
        public void SetWidth(Microsoft.UI.Xaml.GridLength width) => Width = width;
        public UI.ICell CreateCell(IRow<Item> row) => create();
    }
}
