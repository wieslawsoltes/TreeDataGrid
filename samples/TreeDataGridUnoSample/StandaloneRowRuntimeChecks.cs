using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using Uno.Controls.Selection;
using Windows.Foundation;
using UICell = Uno.Controls.Models.TreeDataGrid.ICell;

namespace TreeDataGridUnoSample;

/// <summary>Deferred native checks for independently hosted compatible containers.</summary>
internal static class StandaloneRowRuntimeChecks
{
    public static async Task RunAsync(MainPage page)
    {
        var previous = page.Content;
        var models = new[] { new Item("First"), new Item("Second") };
        using var source = new FlatTreeDataGridSource<Item>(models);
        for (var i = 0; i < 8; ++i)
            source.Columns.Add(new TreeDataGridCore.Models.TextColumn<Item, string>($"Column {i}", x => x.Name, (x, value) => x.Name = value, width: new(120)));
        using var view = TreeDataGridPresentation.Create(source);
        var factory = new Factory();
        var row = new CustomRow();
        var scroll = new ScrollViewer
        {
            Width = 240, Height = 48, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = row,
        };
        TreeDataGridCellsPresenter? direct = null;
        try
        {
            // Retarget the flat source's shared IRow during the compatible cell hook.
            factory.BeforeRealize = () => _ = source.Rows[1].Model;
            row.Realize(factory, new Selection(), view.Columns, view.Rows, 0);
            page.Content = scroll;
            await Settle();
            Check(row.Realizations == 1 && row.RowIndex == 0 && ReferenceEquals(row.Model, models[0]) && row.IsSelected,
                "Standalone row realization lost its model, selection or indexed hook.");
            var presenter = row.CellsPresenter!;
            var initial = presenter.RealizedCells.ToArray();
            Check(initial.Length is > 0 and < 8 && initial.All(cell => ReferenceEquals(cell.RowModel, models[0]) && cell.IsEffectivelySelected),
                "Standalone cells were not virtualized or lost the captured Core model/row selection.");
            Check(ReferenceEquals(presenter.Items, view.Columns) && ReferenceEquals(presenter.Rows, view.Rows) &&
                ReferenceEquals(presenter.ElementFactory, factory), "The row did not configure its compatible presenter properties.");
            var first = (TreeDataGridTextCell)row.TryGetCell(0)!;
            first.Value = "Written through standalone row";
            Check(models[0].Name == first.Value, "Standalone cell editing did not write to the original Core model.");
            var created = factory.Created;
            var unloads = 0;
            foreach (var cell in initial) cell.Unloaded += (_, _) => ++unloads;

            row.Unrealize();
            Check(models[0].Subscribers == 0 && row.Model is null && row.RowIndex == -1 && presenter.RowIndex == -1,
                "Standalone row unrealization retained its binding or row state.");
            row.Realize(factory, null, view.Columns, view.Rows, 1);
            await Settle();
            Check(factory.Created == created && unloads == 0 && initial.All(cell => ReferenceEquals(cell.Parent, presenter)),
                "Standalone row reuse detached retained controls or recreated their native containers.");
            Check(presenter.RealizedCells.All(cell => ReferenceEquals(cell.RowModel, models[1]) && !cell.IsEffectivelySelected),
                "Standalone row reuse retained the previous model or selection.");

            scroll.ChangeView(600, null, null, true);
            await Settle();
            Check(presenter.RealizedCells.Count is > 0 and < 8 && presenter.RealizedCells.Any(cell => cell.ColumnIndex >= 5),
                "The independently hosted presenter did not follow the horizontal viewport.");
            Check(unloads == 0, "Horizontal recycling detached a retained standalone cell.");
            row.Unrealize();
            Check(models.All(item => item.Subscribers == 0), "Standalone scrolling/recycling retained a Core binding.");

            direct = new TreeDataGridCellsPresenter { Items = view.Columns, Rows = view.Rows, ElementFactory = factory };
            direct.Realize(0);
            scroll.Content = direct;
            scroll.ChangeView(0, null, null, true);
            await Settle();
            Check(direct.RowIndex == 0 && direct.RealizedCells.Count > 0 &&
                direct.RealizedCells.All(cell => ReferenceEquals(cell.RowModel, models[0])),
                "A directly configured cells presenter required a parent TreeDataGridRow.");
            direct.UpdateRowIndex(1);
            Check(direct.RealizedCells.All(cell => cell.RowIndex == 1), "UpdateRowIndex did not update standalone cell indexes.");
            direct.Unrealize();
            Check(models.All(item => item.Subscribers == 0), "Direct presenter cleanup retained bindings after an index change.");

            // Application code may retire a presenter from inside its realization hook.
            factory.BeforeRealize = () => direct.Unrealize();
            direct.Realize(0);
            direct.Measure(new Size(239, 40));
            Check(direct.RowIndex == -1 && direct.RealizedCells.Count == 0 && models.All(item => item.Subscribers == 0),
                "Reentrant retirement published a stale cell or leaked its row-owned model.");
            Console.WriteLine("UNO_RUNTIME_STANDALONE_ROWS_PASSED: compatible row/presenter API, shared Core models, writeback, selection, parent-retained recycling, horizontal viewport, direct configuration and reentrant cleanup");
        }
        finally
        {
            factory.BeforeRealize = null;
            try { direct?.Unrealize(); }
            finally { try { row.Unrealize(); } finally { page.Content = previous; } }
        }

        async Task Settle() { await Task.Delay(100); scroll.UpdateLayout(); }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed partial class CustomRow : TreeDataGridRow
    {
        public int Realizations { get; private set; }
        protected override void OnRealized(int rowIndex) { ++Realizations; base.OnRealized(rowIndex); }
    }

    private sealed class Selection : ITreeDataGridSelectionInteraction
    {
        public event EventHandler? SelectionChanged { add { } remove { } }
        public bool IsRowSelected(int rowIndex) => rowIndex == 0;
    }

    private sealed class Factory : TreeDataGridElementFactory
    {
        public int Created { get; private set; }
        public Action? BeforeRealize { get; set; }
        protected override Control CreateElement(object? data)
        {
            if (data is UICell) { ++Created; return new CustomCell(this); }
            return base.CreateElement(data);
        }
        protected override string GetDataRecycleKey(object? data) => data is UICell ? typeof(CustomCell).FullName! : base.GetDataRecycleKey(data);
    }

    private sealed partial class CustomCell(Factory factory) : TreeDataGridTextCell
    {
        public override void Realize(TreeDataGridElementFactory elementFactory, ITreeDataGridSelectionInteraction? selection,
            UICell model, int columnIndex, int rowIndex)
        {
            factory.BeforeRealize?.Invoke();
            base.Realize(elementFactory, selection, model, columnIndex, rowIndex);
        }
    }

    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public int Subscribers { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
        public string Name
        {
            get => name;
            set { if (name != value) { name = value; _changed?.Invoke(this, new(nameof(Name))); } }
        }
    }
}
