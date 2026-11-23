using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Uno.Controls;

namespace TreeDataGridUnoSample;

/// <summary>Failed declarative event attachment, native recovery and borrowed ownership.</summary>
internal static class DeclarativeOwnershipRuntimeChecks
{
    internal static async Task RunAsync(Uno.Controls.TreeDataGrid grid)
    {
        TreeDataGridBindingRegistry.RegisterProperty<Item, string>(nameof(Item.Name), static x => x.Name,
            static (x, value) => x.Name = value);
        TreeDataGridBindingRegistry.RegisterCollection<Items, Item>();
        var previousModel = grid.Model;
        var previousSource = grid.Source;
        var previousItems = grid.ItemsSource;
        var previousOptions = grid.PresentationOptions;
        var previousDefinitions = grid.ColumnDefinitions.ToArray();
        var failure = new InvalidOperationException("declarative subscription failure");
        var item = new Item();
        var items = new Items { item };
        try
        {
            grid.Model = null;
            grid.Source = null;
            grid.ItemsSource = null;
            grid.PresentationOptions = null;
            grid.ColumnDefinitions.Clear();
            grid.ColumnDefinitions.Add(new TreeDataGridTextColumn
            {
                Header = "Review", Width = new(250),
                Binding = new Binding { Path = new PropertyPath(nameof(Item.Name)), Mode = BindingMode.TwoWay },
            });
            items.AddFailure = failure;
            Exception? actual = null;
            try { grid.ItemsSource = items; }
            catch (Exception error) { actual = error; }
            Check(Contains(actual, failure), "The original failed event attachment was lost.");
            Check(items.Subscribers == 0, "Failed declarative construction retained a source event handler.");
            items.EmitCaptured();
            Check(grid.ItemsSource is null && grid.Presentation is null, "A failed source survived rollback or a retired callback.");

            items.AddFailure = null;
            grid.ItemsSource = items;
            await Settle();
            Check(ReferenceEquals(grid.Rows![0].Model, item), "Recovery copied the caller's model.");
            Check(grid.TryGetCell(0, 0) is { } cell && ShowcaseRuntimeChecks.Descendants(cell).OfType<TextBlock>().Any(x => x.Text == item.Name),
                "The recovered declarative model did not render through the native control.");
            Check(grid.BeginEdit(0, 0), "The recovered native cell did not enter editing.");
            grid.EditingCell!.EditingText = "Recovered edit";
            Check(grid.CommitEdit() && item.Name == "Recovered edit", "Native editing did not use the recovered source.");
            items.Add(new Item());
            await Settle();
            Check(grid.Rows!.Count == 2, "Recovered event routing lost collection updates.");
            grid.ItemsSource = null;
            Console.WriteLine($"UNO_REVIEW_RETIREMENT: collection={items.Subscribers}; model={item.Subscribers}; rows={grid.Rows?.Count}; source={grid.Source?.GetType().Name}; modelSource={grid.Model?.GetType().Name}");
            Check(items.Subscribers == 0 && item.Subscribers == 0, "Retirement leaked collection or model observers.");
            items.EmitCaptured();
            items.Add(new Item());
            Check(items.Count == 3 && grid.Presentation is null, "The caller-owned source was disposed or reactivated the retired view.");
            Console.WriteLine("UNO_RUNTIME_DECLARATIVE_OWNERSHIP_REVIEW_PASSED: failed-after-attach rollback, original failure, stale callbacks, same-list recovery, actual native rendering/editing, collection update and borrowed cleanup");
        }
        finally
        {
            grid.ItemsSource = null;
            grid.ColumnDefinitions.Clear();
            foreach (var definition in previousDefinitions) grid.ColumnDefinitions.Add(definition);
            grid.PresentationOptions = previousOptions;
            grid.ItemsSource = previousItems;
            if (previousModel is not null) grid.Model = previousModel;
            else if (previousItems is null) grid.Source = previousSource;
        }
        async Task Settle() { await Task.Delay(100); grid.UpdateLayout(); }
    }

    private static bool Contains(Exception? value, Exception expected) => ReferenceEquals(value, expected) ||
        value is AggregateException aggregate && aggregate.InnerExceptions.Any(x => Contains(x, expected));
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Items : ObservableCollection<Item>
    {
        internal Exception? AddFailure;
        private NotifyCollectionChangedEventHandler? _captured;
        internal int Subscribers;
        public override event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { base.CollectionChanged += value; ++Subscribers; _captured = value; if (AddFailure is { } error) throw error; }
            remove { base.CollectionChanged -= value; --Subscribers; }
        }
        internal void EmitCaptured() => _captured?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
    }
    private sealed class Item : INotifyPropertyChanged
    {
        private string _name = "Review row";
        private PropertyChangedEventHandler? _changed;
        public string Name { get => _name; set { _name = value; _changed?.Invoke(this, new(nameof(Name))); } }
        internal int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
}
