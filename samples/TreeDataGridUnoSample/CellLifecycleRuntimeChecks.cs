using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Deferred native checks for the public cell lifecycle contract.</summary>
internal static class CellLifecycleRuntimeChecks
{
    public static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        NativeObserverAllocationRuntimeChecks.Run();
        await HorizontalRecyclingVisibilityRuntimeChecks.RunAsync(grid);
        await VerticalRetirementRuntimeChecks.RunAsync(grid);
        await PresenterIndexRuntimeChecks.RunAsync(grid);
        grid.Model = null;
        var items = new ObservableCollection<Item>(Enumerable.Range(0, 200).Select(i => new Item($"Cell {i:000}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        source.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name, (x, value) => x.Name = value, width: new(240)));
        var realized = new Dictionary<TreeDataGridCell, (int Row, int Column, object? Model)>();
        var valueChanges = new List<(object? Model, object? Value)>();
        var prepared = 0;
        var cleared = 0;
        var clearOnPrepared = false;
        grid.CellPrepared += OnPrepared;
        grid.CellClearing += OnClearing;
        grid.CellValueChanged += OnValueChanged;
        try
        {
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Task.Delay(200);
            Check(prepared > 0 && realized.Count == grid.RowsPresenter!.RealizedCells.Count, "Prepared events do not cover realized cells.");
            Check(grid.TryGetCell(0, 0) is TreeDataGridCell, "Public index lookup did not return a realized cell.");
            var first = (TreeDataGridCell)grid.TryGetCell(0, 0)!;
            Check(grid.TryGetRow(first, out var row) && ReferenceEquals(row.Model, items[0]), "Visual row lookup lost model identity.");
            Check(grid.TryGetRowModel<Item>(first, out var model) && ReferenceEquals(model, items[0]), "Typed row lookup lost model identity.");
            var original = items[0];
            original.Name = "External change";
            Check(valueChanges.Count == 1 && ReferenceEquals(valueChanges[0].Model, original) && Equals(valueChanges[0].Value, original.Name),
                "External binding update did not publish one current value event.");

            valueChanges.Clear();
            Check(grid.BeginEdit(0, 0), "Lifecycle edit did not start.");
            first.EditingText = "Committed";
            Check(valueChanges.Count == 0, "Buffered editing published a value change before commit.");
            Check(grid.CommitEdit(), "Lifecycle edit did not commit.");
            Check(valueChanges.Count == 1 && Equals(valueChanges[0].Value, "Committed"), "Commit did not publish exactly one final value event.");
            valueChanges.Clear();
            Check(grid.BeginEdit(0, 0), "Cancel lifecycle edit did not start.");
            first.EditingText = "Cancelled";
            grid.CancelEdit();
            Check(valueChanges.Count == 0 && original.Name == "Committed", "Cancel published or wrote a buffered value.");

            var oldPrepared = prepared;
            var oldCleared = cleared;
            items[0] = new Item("Replacement");
            await Task.Delay(100);
            Check(prepared == oldPrepared + 1 && cleared == oldCleared + 1, "Replacement did not balance cell lifecycle events.");
            Check(ReferenceEquals(first, grid.TryGetCell(0, 0)) && ReferenceEquals(first.RowModel, items[0]), "Replacement discarded the cell or its new identity.");
            original.Name = "Old source must be detached";
            Check(valueChanges.Count == 0, "An obsolete row model published through a recycled cell.");

            grid.Scroll!.ChangeView(null, 1600, null, true);
            await Task.Delay(150);
            Check(realized.Count == grid.RowsPresenter!.RealizedCells.Count, "Scroll lifecycle bookkeeping disagrees with realization.");
            grid.Model = null;
            Check(realized.Count == 0 && prepared == cleared, "Source removal did not balance prepared/clearing events.");

            // Event handlers are application code: replacing the source while
            // preparation is in flight must not leave that old cell registered.
            clearOnPrepared = true;
            grid.Model = source;
            grid.UpdateLayout();
            await Task.Delay(100);
            Check(!clearOnPrepared && grid.Model is null && realized.Count == 0 && prepared == cleared,
                "Reentrant source removal during CellPrepared retained an obsolete realization.");
            Console.WriteLine("UNO_RUNTIME_CELL_LIFECYCLE_PASSED: prepared/clearing identity, lookup, external value, edit commit/cancel, retained replacement, scrolling, source removal, prepared-source reentrancy");
        }
        finally
        {
            grid.CellPrepared -= OnPrepared;
            grid.CellClearing -= OnClearing;
            grid.CellValueChanged -= OnValueChanged;
            grid.Model = null;
        }

        void OnPrepared(object? sender, TreeDataGridCellEventArgs e)
        {
            var cell = (TreeDataGridCell)e.Cell;
            Check(cell.RowIndex == e.RowIndex && cell.ColumnIndex == e.ColumnIndex && cell.Model is not null,
                "CellPrepared observed invalid cell/index state.");
            Check(ReferenceEquals(grid.TryGetCell(e.ColumnIndex, e.RowIndex), cell), "CellPrepared ran before index lookup was available.");
            Check(!realized.ContainsKey(cell), "CellPrepared repeated without CellClearing.");
            realized.Add(cell, (e.RowIndex, e.ColumnIndex, cell.RowModel));
            ++prepared;
            if (clearOnPrepared) { clearOnPrepared = false; grid.Model = null; }
        }
        void OnClearing(object? sender, TreeDataGridCellEventArgs e)
        {
            var cell = (TreeDataGridCell)e.Cell;
            Check(realized.Remove(cell, out var previous), "CellClearing was not paired with CellPrepared.");
            Check(previous.Row == e.RowIndex && previous.Column == e.ColumnIndex && ReferenceEquals(previous.Model, cell.RowModel) && cell.Model is not null,
                "CellClearing lost the old realized identity before notification.");
            ++cleared;
        }
        void OnValueChanged(object? sender, TreeDataGridCellEventArgs e)
        {
            var cell = (TreeDataGridCell)e.Cell;
            Check(!cell.IsEditing && cell.RowIndex == e.RowIndex && cell.ColumnIndex == e.ColumnIndex && realized.ContainsKey(cell),
                "CellValueChanged observed an editing or obsolete realization.");
            valueChanges.Add((cell.RowModel, cell.Model?.Value));
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
