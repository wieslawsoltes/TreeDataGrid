using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;
using IColumn = Uno.Controls.Models.TreeDataGrid.IColumn;

namespace TreeDataGridUnoSample;

/// <summary>Native factory checks authored for the post-implementation validation pass.</summary>
internal static class ElementFactoryRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        grid.Model = null;
        var previous = grid.ElementFactory;
        var previousCellFactory = grid.CellFactory;
        var factory = new Factory();
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 200).Select(i => new Item($"Item {i:000}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        source.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name, width: new(240)));
        factory.OnRealizeCell = () => { _ = source.Rows[source.Rows.Count - 1]; };
        try
        {
            grid.ElementFactory = factory;
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Task.Delay(150);
            grid.UpdateLayout();
            Check(grid.TryGetRow(0) is CustomRow, "The row factory was not used.");
            Check(grid.TryGetCell(0, 0) is CustomCell, "The cell factory was not used.");
            Check(grid.ColumnHeadersPresenter!.RealizedHeaders.Count > 0 && grid.ColumnHeadersPresenter!.RealizedHeaders.All(x => x is CustomHeader), "The header factory was not used.");
            var row = grid.TryGetRow(0)!;
            var customRow = (CustomRow)row;
            Check(ReferenceEquals(row.Rows, grid.Presentation!.Rows) && ReferenceEquals(row.Rows![0], source.Rows[0]) &&
                ReferenceEquals(row.GetValue(TreeDataGridRow.RowsProperty), row.Rows) &&
                ReferenceEquals(row.Columns, grid.Presentation!.Columns) && ReferenceEquals(row.GetValue(TreeDataGridRow.ColumnsProperty), row.Columns) &&
                ReferenceEquals(row.ElementFactory, factory) && ReferenceEquals(row.GetValue(TreeDataGridRow.ElementFactoryProperty), factory),
                "Row dependency properties did not expose the actual rows, columns and factory.");
            Check(customRow.LastRealized == 0 && customRow.Realizations > 0,
                "The indexed row-realized override was not invoked.");
            var cell = (TreeDataGridCell)grid.TryGetCell(0, 0)!;
            Check(((CustomCell)cell).CompatibleRealizations == 1 && ReferenceEquals(((CustomCell)cell).LastFactory, factory) &&
                ReferenceEquals(((CustomCell)cell).LastModel, cell.Model),
                "Grid realization bypassed or duplicated the Avalonia-compatible cell override.");
            Check(ReferenceEquals(cell.RowModel, items[0]),
                "A compatible realization override changed the captured model by accessing Core's flyweight row.");
            var parent = cell.Parent;
            var unloaded = ((CustomCell)cell).Unloads;
            items[0] = new Item("Item replacement");
            grid.UpdateLayout();
            await Task.Delay(50);
            Check(ReferenceEquals(row, grid.TryGetRow(0)) && ReferenceEquals(cell, grid.TryGetCell(0, 0)),
                "Compatible custom containers were discarded on replacement.");
            Check(ReferenceEquals(parent, cell.Parent), "Compatible cell recycling changed its native parent.");
            Check(((CustomCell)cell).Unloads == unloaded, "Compatible cell recycling detached and reattached its control.");
            Check(((CustomCell)cell).CompatibleRealizations == 2 && ReferenceEquals(((CustomCell)cell).LastModel, cell.Model),
                "Recycling did not invoke the compatible cell override exactly once for the replacement.");
            Check(customRow.LastUnrealized == 0 && customRow.LastReason == TreeDataGridRowUnrealizeReason.Recycle &&
                customRow.Realizations > 1, "Row recycling did not use Avalonia-compatible indexed lifecycle hooks.");

            items[0] = new Item("Other replacement");
            grid.UpdateLayout();
            await Task.Delay(50);
            Check(grid.TryGetRow(0) is OtherRow && grid.TryGetCell(0, 0) is OtherCell,
                "Custom recycling keys did not reject incompatible row/cell containers.");
            Check(row.Parent is null && cell.Parent is null, "Incompatible containers retained obsolete parents.");
            Check(row.Rows is null && row.Columns is null && row.ElementFactory is null,
                "A released row retained its presentation or factory dependency properties.");

            // Change only the cell key, while retaining the compatible row.
            items[1] = new Item("Other cell only");
            grid.UpdateLayout();
            await Task.Delay(50);
            Check(grid.TryGetRow(1) is CustomRow && grid.TryGetCell(0, 1) is OtherCell,
                "A retained row reused an incompatible custom cell.");

            var localRow = grid.TryGetRow(0)!;
            var localRowParent = localRow.Parent;
            var unchangedCell = grid.TryGetCell(0, 1);
            var localFactory = new RowFactory(false);
            localRow.ElementFactory = localFactory;
            grid.UpdateLayout();
            await Task.Delay(50);
            Check(grid.TryGetCell(0, 0) is LocalCell && ReferenceEquals(grid.TryGetRow(0), localRow) &&
                ReferenceEquals(localRow.Parent, localRowParent) && ReferenceEquals(grid.TryGetCell(0, 1), unchangedCell),
                "A row-level factory was ignored or unnecessarily replaced another row/container.");
            var finalFactory = new RowFactory(true);
            var reassigned = false;
            void OnLocalClearing(object? sender, Uno.Controls.TreeDataGridCellEventArgs args)
            {
                if (args.RowIndex != 0 || reassigned) return;
                reassigned = true;
                localRow.ElementFactory = finalFactory;
                grid.UpdateLayout();
            }
            grid.CellClearing += OnLocalClearing;
            try { localRow.ElementFactory = new RowFactory(false); }
            finally { grid.CellClearing -= OnLocalClearing; }
            grid.UpdateLayout();
            await Task.Delay(50);
            Check(reassigned && ReferenceEquals(localRow.ElementFactory, finalFactory) && grid.TryGetCell(0, 0) is AlternateLocalCell,
                "A nested row-factory assignment lost its final factory or presenter attachment.");
            localRow.ElementFactory = null;
            grid.UpdateLayout();
            await Task.Delay(50);
            Check(grid.TryGetCell(0, 0) is OtherCell && ReferenceEquals(grid.TryGetCell(0, 1), unchangedCell),
                "Clearing a row-level factory did not restore the owning grid's factory.");

            var replacement = new Factory();
            grid.ElementFactory = replacement;
            grid.UpdateLayout();
            await Task.Delay(50);
            Check(replacement.Created > 0 && !ReferenceEquals(row, grid.TryGetRow(0)),
                "Changing ElementFactory did not replace native containers.");

            // The old Uno delegate remains supported and may be restored by callers.
            grid.CellFactory = _ => new LegacyCell();
            grid.UpdateLayout();
            await Task.Delay(50);
            Check(grid.TryGetCell(0, 0) is LegacyCell, "The legacy CellFactory delegate stopped working.");
            grid.CellFactory = previousCellFactory;
            grid.Model = null;

            var reentrant = new Factory { OnCreateCell = () => grid.Model = null };
            grid.ElementFactory = reentrant;
            grid.Model = source;
            grid.UpdateLayout();
            await Task.Delay(50);
            Check(grid.Model is null && grid.RowsPresenter!.RealizedCells.Count == 0,
                "A source change inside the factory retained a stale cell.");

            var direct = new TreeDataGridElementFactory();
            var host = new Grid();
            var data = new TestValue();
            var retained = direct.GetOrCreateElement(data, host);
            host.Children.Add(retained);
            direct.RecycleElement(retained);
            Check(ReferenceEquals(retained, direct.GetOrCreateElement(data, host)) && ReferenceEquals(retained.Parent, host),
                "Direct factory recycling detached a compatible parented control.");
            direct.RecycleElement(retained);
            var duplicateRejected = false;
            try { direct.RecycleElement(retained); }
            catch (InvalidOperationException) { duplicateRejected = true; }
            Check(duplicateRejected, "Duplicate factory pool ownership was accepted.");
            var otherHost = new Grid();
            Check(ReferenceEquals(retained, direct.GetOrCreateElement(data, otherHost)) && retained.Parent is null,
                "The direct factory did not apply Avalonia's cross-panel fallback when no same-parent element existed.");
            otherHost.Children.Add(retained);
            direct.RecycleElement(retained);
            Check(ReferenceEquals(retained, direct.GetOrCreateElement(data, otherHost)) && ReferenceEquals(retained.Parent, otherHost),
                "Direct checkout did not remove the previous fallback ownership.");
            Console.WriteLine("UNO_RUNTIME_ELEMENT_FACTORY_PASSED: custom rows/headers/cells, compatible retained parent, incompatible keys, factory replacement, legacy delegate, source reentrancy, same-parent pool/cross-panel fallback");
        }
        finally
        {
            grid.Model = null;
            grid.CellFactory = previousCellFactory;
            grid.ElementFactory = previous;
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed record Item(string Name);
    private sealed partial class CustomRow : TreeDataGridRow
    {
        public int LastRealized { get; private set; } = -1;
        public int LastUnrealized { get; private set; } = -1;
        public int Realizations { get; private set; }
        public TreeDataGridRowUnrealizeReason LastReason { get; private set; }
        protected override void OnRealized(int rowIndex)
        {
            Check(rowIndex == RowIndex && Model is not null, "OnRealized observed an obsolete row identity.");
            LastRealized = rowIndex;
            ++Realizations;
            base.OnRealized(rowIndex);
        }
        protected override void OnUnrealizing(int rowIndex, TreeDataGridRowUnrealizeReason reason)
        {
            Check(rowIndex == RowIndex && Model is not null, "OnUnrealizing lost the old row identity.");
            LastUnrealized = rowIndex;
            LastReason = reason;
            base.OnUnrealizing(rowIndex, reason);
        }
    }
    private sealed partial class OtherRow : TreeDataGridRow { }
    private sealed partial class CustomHeader : TreeDataGridColumnHeader { }
    private sealed partial class CustomCell : TreeDataGridCell
    {
        public int Unloads { get; private set; }
        public int CompatibleRealizations { get; private set; }
        public TreeDataGridElementFactory? LastFactory { get; private set; }
        public Uno.Controls.Models.TreeDataGrid.ICell? LastModel { get; private set; }
        public Action? BeforeRealize { get; init; }
        public CustomCell() => Unloaded += (_, _) => ++Unloads;
        public override void Realize(TreeDataGridElementFactory factory, Uno.Controls.Selection.ITreeDataGridSelectionInteraction? selection,
            Uno.Controls.Models.TreeDataGrid.ICell model, int columnIndex, int rowIndex)
        {
            ++CompatibleRealizations;
            LastFactory = factory;
            LastModel = model;
            BeforeRealize?.Invoke();
            base.Realize(factory, selection, model, columnIndex, rowIndex);
        }
    }
    private sealed partial class OtherCell : TreeDataGridCell { }
    private sealed partial class LegacyCell : TreeDataGridCell { }
    private sealed partial class LocalCell : TreeDataGridCell { }
    private sealed partial class AlternateLocalCell : TreeDataGridCell { }
    private sealed class RowFactory(bool alternate) : TreeDataGridElementFactory
    {
        protected override Control CreateElement(object? data) => data is Uno.Controls.Models.TreeDataGrid.ICell
            ? alternate ? new AlternateLocalCell() : new LocalCell() : base.CreateElement(data);
        protected override string GetDataRecycleKey(object? data) => data is Uno.Controls.Models.TreeDataGrid.ICell
            ? alternate ? nameof(AlternateLocalCell) : nameof(LocalCell) : base.GetDataRecycleKey(data);
        protected override string GetElementRecycleKey(Control element) => element.GetType().Name;
    }
    private sealed class TestValue : CellValue
    {
        public override object? Value => "Item value";
        public override bool CanEdit => false;
        public override void Write(object? value) => throw new NotSupportedException();
    }
    private sealed class Factory : TreeDataGridElementFactory
    {
        public int Created { get; private set; }
        public Action? OnCreateCell { get; init; }
        public Action? OnRealizeCell { get; set; }
        protected override Control CreateElement(object? data)
        {
            ++Created;
            if (data is CellValue) OnCreateCell?.Invoke();
            return GetDataRecycleKey(data) switch
            {
                nameof(CustomRow) => new CustomRow(),
                nameof(OtherRow) => new OtherRow(),
                nameof(CustomHeader) => new CustomHeader(),
                nameof(CustomCell) => new CustomCell { BeforeRealize = OnRealizeCell },
                nameof(OtherCell) => new OtherCell(),
                _ => base.CreateElement(data),
            };
        }
        protected override string GetDataRecycleKey(object? data) => data switch
        {
            IRow { Model: Item { Name: "Other replacement" } } => nameof(OtherRow),
            IRow => nameof(CustomRow),
            IColumn => nameof(CustomHeader),
            CellValue { Value: string value } when value.StartsWith("Other", StringComparison.Ordinal) => nameof(OtherCell),
            CellValue => nameof(CustomCell),
            _ => base.GetDataRecycleKey(data),
        };
        protected override string GetElementRecycleKey(Control element) => element.GetType().Name;
    }
}
