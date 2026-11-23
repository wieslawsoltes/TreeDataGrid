using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;

namespace TreeDataGridUnoSample;

/// <summary>Public package-consumer coverage for ownership and wide-grid pool turnover.</summary>
internal static class PresentationPoolRuntimeChecks
{
    internal static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        await ColumnFactoryRuntimeChecks.RunAsync(grid);
        foreach (var operation in new[] { "dispose", "suspend-resume", "columns", "rows" })
            VerifyFactoryRetirement(operation);

        var items = new ObservableCollection<Item>(Enumerable.Range(0, 100).Select(index => new Item($"Row {index:D3}")));
        using var source = new FlatTreeDataGridSource<Item>(items);
        for (var column = 0; column < 128; ++column)
            source.Columns.Add(ValueColumn<Item, string>.FromDelegate($"C{column}", static item => item.Name,
                propertyName: nameof(Item.Name), setter: static (item, value) => item.Name = value, width: new(128)));

        using (var view = TreeDataGridPresentation.Create(source))
        {
            for (var column = 0; column < source.Columns.Count; ++column)
            {
                var nativeColumn = (CellColumn)view.Columns[column];
                var first = view.RealizeCell(column, 0);
                view.RecycleCell(nativeColumn, first);
                Check(items[0].ObserverCount == 0, "Pooling retained the previous model subscription.");
                var second = view.RealizeCell(column, 1);
                Check(ReferenceEquals(first, second), $"An exhausted earlier column bucket disabled reuse at column {column}.");
                Check(Equals(second.Value, items[1].Name), "A reused public cell retained another row's value.");
                var changed = $"Edited {column:D3}";
                second.Write(changed);
                Check(items[1].Name == changed, "A reused cell did not write to the current Core model.");
                view.RecycleCell(nativeColumn, second);
                var third = view.RealizeCell(column, 0);
                Check(ReferenceEquals(first, third) && Equals(third.Value, items[0].Name), "Repeated pool turnover lost identity or row binding.");
                third.Dispose();
            }
        }
        Check(items.All(item => item.ObserverCount == 0), "The direct presentation left model subscriptions attached.");

        var previousOptions = grid.PresentationOptions;
        grid.Model = null;
        try
        {
            grid.PresentationOptions = null;
            grid.Model = source;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Task.Delay(120);
            grid.UpdateLayout();
            foreach (var column in new[] { 0, 32, 64, 96, 127, 0 })
            {
                Check(grid.BringCellIntoView(50, column), $"Wide-grid navigation failed at column {column}.");
                await Task.Delay(100);
                grid.UpdateLayout();
                var cell = grid.TryGetCell(column, 50) as global::Uno.Controls.Primitives.TreeDataGridCell;
                Check(cell is not null && ReferenceEquals(cell.RowModel, items[50]), "The native control references an obsolete row after pool turnover.");
                Check(ShowcaseRuntimeChecks.Descendants(cell!).OfType<TextBlock>().Any(text => text.Text == items[50].Name),
                    "Wide-grid recycling left stale native text.");
                Check(grid.RowsPresenter!.RealizedCells.Count() < 2048, "The native grid stopped bounding its realized viewport.");
            }
        }
        finally
        {
            grid.Model = null;
            grid.PresentationOptions = previousOptions;
        }
        Check(items.All(item => item.ObserverCount == 0), "Removing the wide grid's source leaked subscriptions.");
        Console.WriteLine("UNO_RUNTIME_PRESENTATION_POOL_PASSED: four factory retirement modes, 128-column repeated public reuse and writeback, original Core identity, native wide-grid navigation, bounded realization and complete subscription cleanup");
    }

    private static void VerifyFactoryRetirement(string operation)
    {
        var items = new ObservableCollection<Item> { new("Original") };
        using var source = new FlatTreeDataGridSource<Item>(items);
        var definition = ValueColumn<Item, string>.FromDelegate("Name", static item => item.Name);
        definition.PresentationKey = "callback";
        source.Columns.Add(definition);
        var column = new CallbackColumn(definition);
        var options = new TreeDataGridPresentationOptions();
        options.Columns.Add("callback", _ => column);
        using var view = TreeDataGridPresentation.Create(source, options);
        column.Callback = () =>
        {
            if (operation == "dispose") view.Dispose();
            else if (operation == "suspend-resume") { view.Suspend(); view.Resume(); }
            else if (operation == "columns") { definition.IsVisible = false; definition.IsVisible = true; }
            else items[0] = new("Replacement");
        };
        Exception? failure = null;
        try { view.RealizeCell(0, 0); }
        catch (Exception error) { failure = error; }
        Check(failure is InvalidOperationException, $"A retired factory result was published after {operation}: {failure}.");
        Check(column.Created is { Disposals: 1, Value: null }, "The rejected factory result did not release its captured model exactly once.");
        Check(source.Rows.Count == 1, "Retiring a presentation disposed or changed its borrowed Core source.");
        if (operation != "dispose")
        {
            column.Callback = null;
            var recovered = view.RealizeCell(0, 0);
            Check(ReferenceEquals(recovered.Value, items[0]), "The next factory invocation did not recover with the current model.");
            recovered.Dispose();
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        private PropertyChangedEventHandler? _changed;
        public string Name { get => _name; set { _name = value; _changed?.Invoke(this, new(nameof(Name))); } }
        internal int ObserverCount => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
    private sealed class CallbackColumn(IColumn model) : CellColumn(model)
    {
        internal Action? Callback;
        internal CapturedValue? Created;
        public override CellValue CreateCell(IRow row)
        {
            Created = new(row.Model);
            Callback?.Invoke();
            return Created;
        }
    }
    private sealed class CapturedValue(object? model) : CellValue
    {
        private object? _model = model;
        internal int Disposals;
        public override object? Value => _model;
        public override bool CanEdit => false;
        public override void Write(object? value) => throw new NotSupportedException();
        public override void Dispose() { ++Disposals; _model = null; }
    }
}
