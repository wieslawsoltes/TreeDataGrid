using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;
using View = global::Uno.Controls.TreeDataGridItemsSourceView;
using StringView = global::Uno.Controls.TreeDataGridItemsSourceView<string>;

namespace TreeDataGrid.Uno.Tests;

public sealed class ItemsSourceViewCompatibilityTests
{
    [Fact]
    public void List_identity_and_live_indexes_are_shared_with_Core()
    {
        var items = new ObservableCollection<string> { "first", "second" };
        using var view = new StringView(items);
        Assert.IsAssignableFrom<View>(view);
        Assert.IsAssignableFrom<TreeDataGridCore.TreeDataGridItemsSourceView>(view);
        Assert.Same(items, view.Inner);
        Assert.Equal(2, view.Count);
        items[1] = "replacement";
        Assert.Equal("replacement", view[1]);
        Assert.Equal(1, view.IndexOf("replacement"));
        Assert.Equal(-1, view.IndexOf("second"));
        Assert.Equal(items, view.ToArray());
        Assert.Equal(items.ToArray(), ((IEnumerable)view).Cast<string>().ToArray());
    }

    [Fact]
    public void Non_notifying_enumerable_is_materialized_once()
    {
        var enumerations = 0;
        IEnumerable<string> Sequence()
        {
            ++enumerations;
            yield return "first";
            yield return "second";
        }
        using var view = new StringView(Sequence());
        Assert.Equal(new[] { "first", "second" }, view);
        Assert.Equal("second", view.GetAt(1));
        Assert.Equal(1, enumerations);
    }

    [Fact]
    public void Factories_preserve_views_and_return_per_contract_empty_singletons()
    {
        using var view = new StringView(new[] { "item" });
        Assert.Same(view, StringView.GetOrCreate(view));
        Assert.Same(view, View.GetOrCreate(view));
        Assert.Same(StringView.Empty, StringView.GetOrCreate(null));
        Assert.Same(View.Empty, View.GetOrCreate(null));
        Assert.Empty(StringView.Empty);
        Assert.Equal(0, View.Empty.Count);
    }

    [Fact]
    public void Factory_accepts_an_untyped_list_without_copying_it()
    {
        var items = new ArrayList { "item", null };
        using var view = StringView.GetOrCreate(items);
        Assert.Same(items, view.Inner);
        Assert.Equal("item", view[0]);
        Assert.Null(view[1]);
        Assert.Null(view.GetAt(1));
    }

    [Fact]
    public void Constructor_rejects_null_existing_view_and_unindexable_notifier()
    {
        Assert.Throws<ArgumentNullException>(() => new View(null!));
        Assert.Throws<ArgumentNullException>(() => new StringView(null!));
        using var view = new StringView(Array.Empty<string>());
        Assert.Throws<ArgumentException>(() => new View(view));
        Assert.Throws<ArgumentException>(() => new StringView(view));
        Assert.Throws<ArgumentException>(() => new View(new NonListNotifier()));
    }

    [Fact]
    public void Add_replace_move_remove_reset_keep_exact_event_identity_and_sender()
    {
        var items = new ObservableCollection<string> { "first", "second" };
        using var view = new StringView(items);
        var originals = new List<NotifyCollectionChangedEventArgs>();
        var forwarded = new List<NotifyCollectionChangedEventArgs>();
        items.CollectionChanged += (_, e) => originals.Add(e);
        view.CollectionChanged += (sender, e) =>
        {
            Assert.Same(view, sender);
            Assert.Equal(items, view.ToArray());
            forwarded.Add(e);
        };
        items.Add("third");
        items[0] = "replacement";
        items.Move(2, 0);
        items.RemoveAt(1);
        items.Clear();
        Assert.Equal(5, originals.Count);
        Assert.Equal(originals.Count, forwarded.Count);
        for (var index = 0; index < originals.Count; ++index)
            Assert.Same(originals[index], forwarded[index]);
    }

    [Fact]
    public void Removing_last_observer_and_resubscribing_does_not_duplicate_notifications()
    {
        var items = new ObservableCollection<string>();
        using var view = new StringView(items);
        var calls = 0;
        NotifyCollectionChangedEventHandler callback = (_, _) => ++calls;
        for (var index = 0; index < 4; ++index)
        {
            view.CollectionChanged += callback;
            items.Add("observed");
            Assert.Equal(index + 1, calls);
            view.CollectionChanged -= callback;
            items.Add("unobserved");
            Assert.Equal(index + 1, calls);
        }
    }

    [Fact]
    public void Disposal_detaches_borrowed_source_and_rejects_subsequent_access()
    {
        var items = new ObservableCollection<string> { "item" };
        var view = new StringView(items);
        var calls = 0;
        NotifyCollectionChangedEventHandler callback = (_, _) => ++calls;
        view.CollectionChanged += callback;
        view.Dispose();
        view.Dispose();
        items.Add("still usable");
        Assert.Equal(0, calls);
        Assert.Equal(2, items.Count);
        Assert.Throws<ObjectDisposedException>(() => view.Count);
        Assert.Throws<ObjectDisposedException>(() => view.Inner);
        Assert.Throws<ObjectDisposedException>(() => view[0]);
        Assert.Throws<ObjectDisposedException>(() => view.GetAt(0));
        Assert.Throws<ObjectDisposedException>(() => view.IndexOf("item"));
        Assert.Throws<ObjectDisposedException>(() => view.CollectionChanged += callback);
        Assert.Throws<ObjectDisposedException>(() => view.CollectionChanged -= callback);
    }

    [Fact]
    public void Public_subclass_notification_hook_preserves_event_contract()
    {
        using var view = new DerivedView(Array.Empty<object>());
        var args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
        NotifyCollectionChangedEventArgs? actual = null;
        view.CollectionChanged += (sender, e) => { Assert.Same(view, sender); actual = e; };
        view.Notify(args);
        Assert.Same(args, actual);
    }

    [Fact]
    public void Borrowed_collection_does_not_root_an_abandoned_view()
    {
        var items = new ObservableCollection<string>();
        var weak = Observe(items);
        for (var i = 0; i < 3 && weak.IsAlive; ++i)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.False(weak.IsAlive);
        items.Add("collectible");
        GC.KeepAlive(items);
    }

    [Fact]
    public void Key_mapping_reports_the_same_unsupported_contract_as_the_reference()
    {
        using var view = new StringView(new[] { "item" });
        Assert.False(view.HasKeyIndexMapping);
        Assert.Throws<NotImplementedException>(() => view.KeyFromIndex(0));
        Assert.Throws<NotImplementedException>(() => view.IndexFromKey("item"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Observe(ObservableCollection<string> items)
    {
        var view = new StringView(items);
        view.CollectionChanged += static (_, _) => { };
        return new WeakReference(view);
    }

    private sealed class DerivedView(IEnumerable source) : View(source)
    {
        public void Notify(NotifyCollectionChangedEventArgs e) => OnItemsSourceChanged(e);
    }

    private sealed class NonListNotifier : IEnumerable, INotifyCollectionChanged
    {
        public IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
        public event NotifyCollectionChangedEventHandler? CollectionChanged { add { } remove { } }
    }
}
