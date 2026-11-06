using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridCore.Selection;
using Uno.Controls;

namespace TreeDataGridUnoSample;

/// <summary>Authored source API/reentrancy/ownership gates; run after implementation is complete.</summary>
internal static class SourceCompatibilityRuntimeChecks
{
    static SourceCompatibilityRuntimeChecks()
    {
        // These programmatic fixtures have no generated XAML metadata. Describe
        // the generated-source accessor explicitly, just as a trimmed consumer
        // must, without reopening the library's arbitrary reflection fallback.
        TreeDataGridBindingRegistry.RegisterProperty<Item, string>(nameof(Item.Name), static item => item.Name);
        TreeDataGridBindingRegistry.RegisterCollection<ObservableCollection<Item>, Item>();
    }

    internal static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        var unapplied = new Uno.Controls.TreeDataGrid();
        Check(unapplied.RowsPresenter is null && unapplied.ColumnHeadersPresenter is null && unapplied.Scroll is null &&
            unapplied.GetValue(Uno.Controls.TreeDataGrid.ScrollProperty) is null,
            "Template-part properties must be null, not throw, before the template is applied.");
        var previousModel = grid.Model;
        var previousOptions = grid.PresentationOptions;
        var definitions = grid.ColumnDefinitions.ToArray();
        using var first = new CountingSource("first");
        using var second = new CountingSource("second");
        using var newest = new CountingSource("newest");
        using var invalid = new CountingSource("invalid");
        invalid.Columns[0].PresentationKey = "Missing source presentation";
        grid.Source = null;
        grid.ItemsSource = null;
        grid.PresentationOptions = null;
        grid.ColumnDefinitions.Clear();
        grid.ColumnDefinitions.Add(new TreeDataGridTextColumn
        {
            Header = "Generated", Width = new(240),
            Binding = new Binding { Path = new PropertyPath(nameof(Item.Name)), Mode = BindingMode.OneWay },
        });
        var reenter = false;
        var throwClearing = false;
        var clearingCalls = 0;
        grid.CellClearing += OnClearing;
        try
        {
            grid.Source = first;
            await Task.Delay(100);
            Check(grid.Model is null && ReferenceEquals(grid.Source, first) && ReferenceEquals(grid.Presentation?.Model, first),
                "Source did not select the actual borrowed Core source.");
            Check(ReferenceEquals(grid.Columns, grid.Presentation!.Columns) && ReferenceEquals(grid.Rows, grid.Presentation.Rows) &&
                ReferenceEquals(grid.GetValue(Uno.Controls.TreeDataGrid.ColumnsProperty), grid.Columns) &&
                ReferenceEquals(grid.GetValue(Uno.Controls.TreeDataGrid.RowsProperty), grid.Rows) &&
                ReferenceEquals(grid.GetValue(Uno.Controls.TreeDataGrid.PresentationProperty), grid.Presentation) &&
                ReferenceEquals(grid.GetValue(Uno.Controls.TreeDataGrid.ScrollProperty), grid.Scroll!),
                "The grid did not publish its original presentation collections and scroll part through the compatible dependency properties.");
            Check(ReferenceEquals(grid.RowSelection, first.Selection), "RowSelection did not expose the Source selection instance.");
            var rowSelection = first.Selection;
            first.Selection = new TreeDataGridCellSelectionModel<Item>(first);
            Check(ReferenceEquals(grid.ColumnSelection, first.Selection), "ColumnSelection did not expose the Source cell selection.");
            first.Selection = rowSelection;

            grid.Model = second;
            Check(grid.Source is null && grid.RowSelection is null && ReferenceEquals(grid.Presentation?.Model, second),
                "Assigning Model did not clear the compatibility Source slot.");
            grid.Model = null;
            Check(grid.Presentation is null, "Clearing Model resurrected an explicitly superseded Source.");
            grid.Model = second;
            grid.Source = first;
            Check(grid.Model is null && ReferenceEquals(grid.Presentation?.Model, first), "Assigning Source did not clear Model.");

            var holder = new SourceHolder { Current = second };
            grid.SetBinding(Uno.Controls.TreeDataGrid.SourceProperty,
                new Binding { Source = holder, Path = new PropertyPath(nameof(holder.Current)), Mode = BindingMode.OneWay });
            Check(ReferenceEquals(grid.Source, second) && ReferenceEquals(grid.Presentation?.Model, second),
                "A native Source binding did not activate its value.");
            holder.Current = first;
            Check(ReferenceEquals(grid.Source, first), "Publishing Source removed the native binding.");
            holder.Current = null;
            Check(grid.Source is null && grid.Presentation is null, "A null native Source binding kept its old presentation.");
            holder.Current = second;
            Check(ReferenceEquals(grid.Source, second), "A native Source binding failed to recover after null.");
            grid.ClearValue(Uno.Controls.TreeDataGrid.SourceProperty);
            holder.Current = first;
            Check(grid.Source is null && grid.Presentation is null, "A retired Source binding reactivated the grid.");

            var item = new Item("generated one");
            grid.ItemsSource = new ObservableCollection<Item> { item };
            var generated = grid.Source!;
            Check(generated is FlatTreeDataGridSource<object> && ReferenceEquals(generated, grid.GetValue(Uno.Controls.TreeDataGrid.SourceProperty)),
                "Generated source was not published through SourceProperty.");
            var getter = (ValueColumn<object, object?>)generated.Columns[0];
            grid.Source = generated; // Same-value CLR assignment must still be explicit.
            grid.ItemsSource = new ObservableCollection<Item> { new("generated two") };
            Check(ReferenceEquals(grid.Source, generated) && Equals(getter.GetValue(item), item.Name),
                "Changing ItemsSource disposed a generated source promoted to explicit Source.");
            grid.Source = null;
            Check(grid.Source is FlatTreeDataGridSource<object> && !ReferenceEquals(grid.Source, generated),
                "Clearing explicit Source did not reveal the newest generated source.");
            ExpectDisposed(() => getter.GetValue(item));

            var promotedModel = grid.Source!;
            var promotedItem = promotedModel.Rows[0].Model!;
            var promotedGetter = (ValueColumn<object, object?>)promotedModel.Columns[0];
            grid.Model = promotedModel;
            grid.ItemsSource = new ObservableCollection<Item> { new("generated three") };
            Check(Equals(promotedGetter.GetValue(promotedItem), "generated two"), "Changing ItemsSource disposed a generated source promoted to Model.");
            grid.Model = null;
            ExpectDisposed(() => promotedGetter.GetValue(promotedItem));
            grid.ItemsSource = null;

            grid.Source = first;
            await Task.Delay(100);
            var view = grid.Presentation;
            var cell = grid.TryGetCell(0, 0);
            var rejected = false;
            try { grid.Source = invalid; } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && ReferenceEquals(grid.Source, first) && ReferenceEquals(grid.Presentation, view) &&
                ReferenceEquals(grid.TryGetCell(0, 0), cell), "A failing Source factory did not preserve the working property/view/cell.");

            reenter = true;
            grid.Source = second;
            await Task.Delay(100);
            Check(!reenter && ReferenceEquals(grid.Model, newest) && grid.Source is null && ReferenceEquals(grid.Presentation?.Model, newest),
                "An outer Source assignment overwrote a newer Model assigned by CellClearing.");
            Check(grid.RowsPresenter!.RealizedCells.Count > 0 && grid.RowsPresenter!.RealizedCells.All(x => x.RowModel is Item { Name: "newest" }),
                "An old reset erased or retained rows after nested source layout.");

            grid.Source = first;
            await Task.Delay(100);
            var oldCells = grid.RowsPresenter!.RealizedCells.ToArray();
            clearingCalls = 0;
            throwClearing = true;
            rejected = false;
            try { grid.Source = second; } catch (InvalidOperationException) { rejected = true; }
            await Task.Delay(100);
            Check(rejected && clearingCalls >= oldCells.Length && oldCells.All(x => x.Model is null && x.RowIndex == -1),
                "A throwing clearing callback skipped remaining old-cell cleanup.");
            Check(ReferenceEquals(grid.Source, first) && ReferenceEquals(grid.Presentation?.Model, first) && grid.TryGetCell(0, 0) is not null,
                "A failed source replacement did not restore a renderable prior source.");
            grid.Source = null;
            var publishedReplacement = false;
            var token = grid.RegisterPropertyChangedCallback(Uno.Controls.TreeDataGrid.RowsProperty, (_, _) =>
            {
                if (publishedReplacement || !ReferenceEquals(grid.Presentation?.Model, first)) return;
                publishedReplacement = true;
                grid.Model = newest;
            });
            try
            {
                grid.Source = first;
                Check(publishedReplacement && ReferenceEquals(grid.Model, newest) &&
                    ReferenceEquals(grid.Columns, grid.Presentation!.Columns) && ReferenceEquals(grid.Rows, grid.Presentation.Rows) &&
                    ReferenceEquals(grid.GetValue(Uno.Controls.TreeDataGrid.PresentationProperty), grid.Presentation),
                    "Publishing presentation properties overwrote a newer source installed by a Rows callback.");
            }
            finally { grid.UnregisterPropertyChangedCallback(Uno.Controls.TreeDataGrid.RowsProperty, token); }
            grid.Model = null;
            Check(first.Disposals == 0 && second.Disposals == 0 && newest.Disposals == 0 && invalid.Disposals == 0,
                "The grid disposed a caller-owned Core source.");
            Console.WriteLine("UNO_RUNTIME_SOURCE_COMPATIBILITY_PASSED: Source/Model switching, presentation dependency properties, selection aliases, native binding/null recovery/detachment, generated-source publication/promotion/retirement, factory rollback, reentrant publication/layout, throwing cleanup, borrowed ownership");
        }
        finally
        {
            grid.CellClearing -= OnClearing;
            grid.Source = null;
            grid.Model = null;
            grid.ItemsSource = null;
            grid.ColumnDefinitions.Clear();
            foreach (var definition in definitions) grid.ColumnDefinitions.Add(definition);
            grid.PresentationOptions = previousOptions;
            grid.Model = previousModel;
        }
        void OnClearing(object? sender, TreeDataGridCellEventArgs e)
        {
            ++clearingCalls;
            if (reenter)
            {
                reenter = false;
                grid.Model = newest;
                grid.UpdateLayout();
            }
            if (throwClearing) { throwClearing = false; throw new InvalidOperationException("Expected clearing failure"); }
        }
    }
    private static void ExpectDisposed(Action read)
    {
        var disposed = false;
        try { read(); } catch (ObjectDisposedException) { disposed = true; }
        Check(disposed, "A retired generated source kept its accessor resources alive.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed record Item(string Name);
    private sealed class CountingSource : FlatTreeDataGridSource<Item>, IDisposable
    {
        internal int Disposals;
        internal CountingSource(string name) : base(Enumerable.Range(0, 8).Select(_ => new Item(name)).ToArray()) =>
            Columns.Add(new TextColumn<Item, string>("Name", x => x.Name, width: new(240)));
        public new void Dispose() { ++Disposals; base.Dispose(); }
    }
    private sealed class SourceHolder : INotifyPropertyChanged
    {
        // Native SetBinding resolves this property by name; no XAML references
        // this private fixture. Preserve this one endpoint, not all sample types.
        [DynamicDependency(nameof(Current))]
        public SourceHolder() { }
        private ITreeDataGridSource? _current;
        public ITreeDataGridSource? Current { get => _current; set { _current = value; PropertyChanged?.Invoke(this, new(nameof(Current))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
