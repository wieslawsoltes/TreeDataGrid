using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Uno.Controls;
using Uno.Controls.Primitives;

namespace TreeDataGridUnoSample;

/// <summary>Native binding callback/retirement checks, deferred until implementation is complete.</summary>
internal static class BindingLifetimeRuntimeChecks
{
    public static async Task RunAsync(TreeDataGrid grid, DataTemplate template)
    {
        grid.Model = null;
        grid.ItemsSource = null;
        var columns = grid.ColumnDefinitions.ToArray();
        var options = grid.PresentationOptions;
        var converter = new CallbackConverter();
        var original = new Item("Original");
        var replacement = new Item("Replacement");
        var first = new ObservableCollection<Item> { original };
        var second = new ObservableCollection<Item> { replacement };
        grid.PresentationOptions = null;
        grid.ColumnDefinitions.Clear();
        grid.ColumnDefinitions.Add(new TreeDataGridTextColumn
        {
            Header = "Native lifetime", Width = new(260),
            Binding = new Binding { Path = new PropertyPath(nameof(Item.Name)), Mode = BindingMode.TwoWay, Converter = converter },
        });
        try
        {
            grid.ItemsSource = first;
            grid.Scroll!.ChangeView(0, 0, null, true);
            await Settle();
            var old = CurrentCell().ViewModel!;
            converter.OnConvertBack = () => grid.ItemsSource = second;
            var cancelled = false;
            try { old.Write("Must not reach the old setter"); }
            catch (OperationCanceledException) { cancelled = true; }
            converter.OnConvertBack = null;
            await Settle();
            Check(cancelled && original.Name == "Original" && replacement.Name == "Replacement",
                "A retired converter write mutated its old or new row.");
            Check(original.Subscribers == 0 && old.Error is null,
                "A converter-driven source replacement retained old observers/error state.");
            var current = CurrentCell().ViewModel!;
            current.Write("Current write");
            Check(replacement.Name == "Current write" && current.Error is null,
                "A retired write poisoned the replacement binding.");

            // A setter may retire the binding and then throw. Its failure must
            // propagate without overwriting disposal/new-realization state.
            replacement.OnSet = () => { grid.ItemsSource = first; throw replacement.Failure; };
            Exception? failure = null;
            try { current.Write("Setter callback"); }
            catch (InvalidOperationException error) { failure = error; }
            replacement.OnSet = null;
            await Settle();
            Check(ReferenceEquals(failure, replacement.Failure) && replacement.Subscribers == 0 && current.Error is null,
                "A retired setter failure was lost or restored stale binding state.");
            Check(CurrentCell().ViewModel!.Error is null && original.Name == "Original",
                "A failed old setter contaminated the current cell.");

            var active = CurrentCell().ViewModel!;
            original.OnSet = () => throw original.Failure;
            failure = null;
            try { active.Write("Rejected"); }
            catch (InvalidOperationException error) { failure = error; }
            Check(ReferenceEquals(failure, original.Failure) && ReferenceEquals(active.Error, original.Failure),
                "An active setter failure did not remain available for validation.");
            original.OnSet = null;
            active.Write("Retried");
            Check(original.Name == "Retried" && active.Error is null,
                "A valid retry did not clear the current binding error.");
            original.Name = "External after retry";
            Check(Equals(active.Value, "External after retry"), "A failed/retried write left model notifications suppressed.");

            grid.ItemsSource = null;
            Check(original.Subscribers == 0 && replacement.Subscribers == 0 && grid.RowsPresenter!.RealizedCells.Count == 0,
                "Binding source removal retained subscriptions or native cells.");
#if !WINDOWS
            // Generated local ElementName and Source subjects must not install
            // an unowned native late-name subscription on a pooled probe.
            foreach (var useSource in new[] { false, true })
            {
                var subject = new ElementNameSubject();
                var named = new Item("Named value");
                var nextNamed = new Item("Next named value");
                var namedBinding = new Binding
                {
                    Path = new PropertyPath(nameof(Item.Name)), Mode = BindingMode.TwoWay, Converter = converter,
                };
                if (useSource) namedBinding.Source = subject;
                else namedBinding.ElementName = subject;
                grid.ColumnDefinitions.Clear();
                grid.ColumnDefinitions.Add(new TreeDataGridTextColumn { Header = "Named source", Width = new(260), Binding = namedBinding });
                grid.ItemsSource = first;
                await Settle();
                Check(CurrentCell().ViewModel!.Value is null, "An unresolved name fell back to the row DataContext.");
                subject.ElementInstance = named;
                await Settle();
                Check(Equals(CurrentCell().ViewModel!.Value, "Named value"), "Late local name resolution did not update the cell.");
                named.OnSet = () => throw named.Failure;
                failure = null;
                try { CurrentCell().ViewModel!.Write("Rejected named value"); }
                catch (InvalidOperationException error) { failure = error; }
                Check(ReferenceEquals(failure, named.Failure) && ReferenceEquals(CurrentCell().ViewModel!.Error, named.Failure),
                    "Named-source writeback swallowed the original setter failure.");
                named.OnSet = null;
                CurrentCell().ViewModel!.Write("Named retry");
                Check(named.Name == "Named retry" && original.Name == "External after retry",
                    "Named-source retry wrote the row instead of the resolved source.");
                subject.ElementInstance = nextNamed;
                await Settle();
                Check(named.Subscribers == 0 && Equals(CurrentCell().ViewModel!.Value, nextNamed.Name),
                    "Replacing a local name retained its old observers or value.");
                converter.OnConvertBack = () => subject.ElementInstance = null;
                cancelled = false;
                try { CurrentCell().ViewModel!.Write("Retired named value"); }
                catch (OperationCanceledException) { cancelled = true; }
                finally { converter.OnConvertBack = null; }
                Check(cancelled && nextNamed.Name == "Next named value" && nextNamed.Subscribers == 0,
                    "A name cleared in ConvertBack still received a write or retained its old observers.");
                grid.ItemsSource = null;
                var afterRemoval = new Item("Name resolved after source removal");
                subject.ElementInstance = afterRemoval;
                await Settle();
                Check(afterRemoval.Subscribers == 0 && grid.RowsPresenter!.RealizedCells.Count == 0,
                    "A late-name notification after removal reactivated a retired binding.");
            }
#endif
            var nestedOriginal = new Item("Nested owner before conversion");
            var nestedReplacement = new Item("Nested owner after conversion");
            var owner = new Owner(nestedOriginal);
            grid.ColumnDefinitions.Clear();
            grid.ColumnDefinitions.Add(new TreeDataGridTextColumn
            {
                Header = "Native endpoint identity", Width = new(260),
                Binding = new Binding { Source = owner, Path = new PropertyPath("Child.Name"), Mode = BindingMode.TwoWay, Converter = converter },
            });
            grid.ItemsSource = first;
            await Settle();
            converter.OnConvertBack = () => owner.Child = nestedReplacement;
            cancelled = false;
            try { CurrentCell().ViewModel!.Write("Obsolete nested endpoint"); }
            catch (OperationCanceledException) { cancelled = true; }
            finally { converter.OnConvertBack = null; }
            Check(cancelled && nestedOriginal.Name == "Nested owner before conversion" && nestedReplacement.Name == "Nested owner after conversion" &&
                nestedOriginal.Subscribers == 0, "A converter replaced the observed endpoint but the old owner still received its write.");
            CurrentCell().ViewModel!.Write("Nested retry");
            Check(nestedReplacement.Name == "Nested retry" && CurrentCell().ViewModel!.Error is null,
                "The replacement native endpoint could not accept a retry.");
            grid.ItemsSource = null;
            Check(nestedReplacement.Subscribers == 0, "Nested explicit-source cleanup retained its final owner.");
#if !WINDOWS
            grid.ColumnDefinitions.Clear();
            grid.ColumnDefinitions.Add(new TreeDataGridTextColumn
            {
                Header = "Relative source root", Width = new(260),
                Binding = new Binding
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.Self), Path = new PropertyPath("DataContext.Name"), Mode = BindingMode.TwoWay,
                },
            });
            grid.ItemsSource = first;
            await Settle();
            Check(Equals(CurrentCell().ViewModel!.Value, original.Name), "RelativeSource Self did not retain native probe semantics.");
            original.OnSet = () => throw original.Failure;
            failure = null;
            try { CurrentCell().ViewModel!.Write("Rejected relative source value"); }
            catch (InvalidOperationException error) { failure = error; }
            finally { original.OnSet = null; }
            Check(ReferenceEquals(failure, original.Failure), "Native-resolved relative writeback swallowed its setter failure.");
            CurrentCell().ViewModel!.Write("Relative retry");
            Check(original.Name == "Relative retry", "Native-resolved relative writeback did not recover after failure.");
            grid.ItemsSource = null;
#endif
            var plainSource = new PlainItem { Name = "No notifications" };
            grid.ColumnDefinitions.Clear();
            grid.ColumnDefinitions.Add(new TreeDataGridTextColumn
            {
                Header = "Explicit source refresh", Width = new(260),
                Binding = new Binding { Source = plainSource, Path = new PropertyPath(nameof(PlainItem.Name)), Mode = BindingMode.TwoWay },
            });
            grid.ItemsSource = first;
            await Settle();
            CurrentCell().ViewModel!.Write("Refreshed without INPC");
            Check(plainSource.Name == "Refreshed without INPC" && Equals(CurrentCell().ViewModel!.Value, plainSource.Name),
                "Explicit-source writeback failed to refresh a model without notifications.");
            grid.ItemsSource = null;
            var searchSource = new Item("Explicit search source");
            grid.ColumnDefinitions.Clear();
            grid.ColumnDefinitions.Add(new TreeDataGridTemplateColumn
            {
                Header = "Search snapshot", Width = new(260), CellTemplate = template,
                TextSearchBinding = new Binding { Source = searchSource, Path = new PropertyPath(nameof(Item.Name)), Mode = BindingMode.OneWay },
            });
            grid.ItemsSource = first;
            await Settle();
            var searchColumn = (Uno.Controls.Presentation.CellColumn)grid.Presentation!.Columns[0];
            for (var i = 0; i < 3; ++i)
            {
                searchSource.Name = $"Search {i}";
                Check(searchColumn.GetSearchText(original) == searchSource.Name && searchSource.Subscribers == 0,
                    "An explicit-source search snapshot retained subscriptions or stale values.");
            }
            grid.ItemsSource = null;
            Check(searchSource.Subscribers == 0, "Removing a template search column retained its explicit source observer.");
            Console.WriteLine("UNO_RUNTIME_BINDING_LIFETIME_PASSED: converter retirement, setter retirement/error, retry, current notifications, source cleanup, explicit-source search snapshots");
        }
        finally
        {
            converter.OnConvertBack = null;
            original.OnSet = replacement.OnSet = null;
            grid.ItemsSource = null;
            grid.Model = null;
            grid.ColumnDefinitions.Clear();
            foreach (var column in columns) grid.ColumnDefinitions.Add(column);
            grid.PresentationOptions = options;
        }
        async Task Settle() { grid.UpdateLayout(); await Task.Delay(100); grid.UpdateLayout(); }
        TreeDataGridCell CurrentCell() => (TreeDataGridCell)grid.TryGetCell(0, 0)!;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class CallbackConverter : IValueConverter
    {
        public Action? OnConvertBack;
        public object Convert(object value, Type targetType, object parameter, string language) => value;
        public object ConvertBack(object value, Type targetType, object parameter, string language) { OnConvertBack?.Invoke(); return value; }
    }
    private sealed class Item(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        private PropertyChangedEventHandler? _changed;
        public Action? OnSet;
        public InvalidOperationException Failure { get; } = new("Native setter rejected value.");
        public int Subscribers { get; private set; }
        public string Name
        {
            get => _name;
            set { OnSet?.Invoke(); _name = value; _changed?.Invoke(this, new(nameof(Name))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
    }
    private sealed class PlainItem { public string Name { get; set; } = string.Empty; }
    private sealed class Owner(Item child) : INotifyPropertyChanged
    {
        private Item _child = child;
        public Item Child { get => _child; set { _child = value; PropertyChanged?.Invoke(this, new(nameof(Child))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
