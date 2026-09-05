using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class CellBindingTests
{
    [Fact]
    public void Nested_owner_replacement_and_null_recovery_update_value()
    {
        var old = new Node { Name = "old" };
        var root = new Node { Child = old };
        using var binding = Bind(root, new ValueColumn<Node, string?>("Name", x => x.Child!.Name));
        Assert.Equal("old", binding.Value);
        old.Name = "changed";
        Assert.Equal("changed", binding.Value);
        var next = new Node { Name = "new" };
        root.Child = next;
        Assert.Equal(0, old.Subscribers);
        Assert.Equal("new", binding.Value);
        root.Child = null;
        Assert.Null(binding.Value);
        Assert.IsType<NullReferenceException>(binding.Error);
        Assert.Equal(0, next.Subscribers);
        root.Child = next;
        Assert.Equal("new", binding.Value);
        Assert.Null(binding.Error);
    }

    [Fact]
    public void Aliased_owners_have_one_subscription_and_dispose_once()
    {
        var root = new Node { Name = "a" };
        root.Child = root;
        var column = new ValueColumn<Node, string?>("Alias", x => x.Name + x.Child!.Name);
        var binding = Bind(root, column);
        Assert.Equal(1, root.Subscribers);
        root.Name = "b";
        Assert.Equal("bb", binding.Value);
        binding.Dispose();
        binding.Dispose();
        Assert.Equal(0, root.Subscribers);
    }

    [Fact]
    public void Retarget_detaches_old_chain_and_two_way_write_targets_new_row()
    {
        var first = new Node { Child = new Node { Name = "one" } };
        var second = new Node { Child = new Node { Name = "two" } };
        var column = new ValueColumn<Node, string?>("Name", x => x.Child!.Name, (x, value) => x.Child!.Name = value);
        using var binding = Bind(first, column);
        binding.Retarget(second);
        Assert.Equal(0, first.Subscribers);
        Assert.Equal(0, first.Child.Subscribers);
        binding.Write("updated");
        Assert.Equal("one", first.Child.Name);
        Assert.Equal("updated", second.Child.Name);
        Assert.Equal("updated", binding.Value);
    }

    [Fact]
    public void Suspended_binding_can_be_reused_without_retaining_its_first_row()
    {
        var column = new ValueColumn<Node, string?>("Name", x => x.Child!.Name);
        using var binding = new CellBinding<Node, string?>(column, static () => { });
        var weak = AttachAndSuspend(binding);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(weak.IsAlive);
        var replacement = new Node { Child = new Node { Name = "replacement" } };
        binding.Retarget(replacement);
        Assert.Equal("replacement", binding.Value);
    }

    [Fact]
    public void Computed_selector_tracks_both_branches()
    {
        var root = new Node { Name = "a", Child = new Node { Name = "b" } };
        using var binding = Bind(root, new ValueColumn<Node, string?>("Computed", x => x.Name + x.Child!.Name));
        root.Child.Name = "c";
        Assert.Equal("ac", binding.Value);
        root.Name = "d";
        Assert.Equal("dc", binding.Value);
    }

    [Fact]
    public void Collection_indexer_refreshes_when_item_is_replaced()
    {
        var root = new Node();
        root.Children.Add(new Node { Name = "first" });
        using var binding = Bind(root, new ValueColumn<Node, string?>("Item", x => x.Children[0].Name));
        var previous = root.Children[0];
        root.Children[0] = new Node { Name = "second" };
        Assert.Equal("second", binding.Value);
        Assert.Equal(0, previous.Subscribers);
    }

    [Fact]
    public void Empty_collection_recovers_when_a_new_item_arrives()
    {
        var root = new Node();
        using var binding = Bind(root, new ValueColumn<Node, string?>("Item", x => x.Children[0].Name));
        Assert.NotNull(binding.Error);
        root.Children.Add(new Node { Name = "added" });
        Assert.Equal("added", binding.Value);
        Assert.Null(binding.Error);
        var previous = root.Children[0];
        root.Children.Clear();
        Assert.Null(binding.Value);
        Assert.NotNull(binding.Error);
        Assert.Equal(0, previous.Subscribers);
    }

    [Fact]
    public void Two_presentations_have_independent_subscriptions()
    {
        var root = new Node { Child = new Node { Name = "initial" } };
        var column = new ValueColumn<Node, string?>("Item", x => x.Child!.Name);
        using var first = Bind(root, column);
        using var second = Bind(root, column);
        Assert.Equal(2, root.Subscribers);
        first.Suspend();
        root.Child.Name = "changed";
        Assert.Null(first.Value);
        Assert.Equal("changed", second.Value);
        Assert.Equal(1, root.Subscribers);
    }

    [Fact]
    public void Delegate_setter_without_notification_is_read_back()
    {
        var value = "first";
        var column = ValueColumn<Node, string>.FromDelegate("Value", _ => value, setter: (_, next) => value = next);
        using var binding = Bind(new Node(), column);
        binding.Write("second");
        Assert.Equal("second", binding.Value);
    }

    [Fact]
    public void Read_only_binding_rejects_write()
    {
        using var binding = Bind(new Node(), new ValueColumn<Node, string?>("Name", x => x.Name));
        Assert.False(binding.CanWrite);
        Assert.Throws<InvalidOperationException>(() => binding.Write("invalid"));
    }

    [Fact]
    public void Suspend_removes_subscriptions_and_clears_value()
    {
        var root = new Node { Child = new Node { Name = "first" } };
        using var binding = Bind(root, new ValueColumn<Node, string?>("Name", x => x.Child!.Name));
        binding.Suspend();
        Assert.Null(binding.Value);
        Assert.Equal(0, root.Subscribers);
        Assert.Equal(0, root.Child.Subscribers);
        Assert.Throws<InvalidOperationException>(() => binding.Write("invalid"));
    }

    private static CellBinding<Node, TValue> Bind<TValue>(Node model, ValueColumn<Node, TValue> column)
    {
        var binding = new CellBinding<Node, TValue>(column, static () => { });
        binding.Retarget(model);
        return binding;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AttachAndSuspend(CellBinding<Node, string?> binding)
    {
        var root = new Node { Child = new Node { Name = "first" } };
        var result = new WeakReference(root);
        binding.Retarget(root);
        binding.Suspend();
        return result;
    }

    private sealed class Node : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        private string? _name;
        private Node? _child;
        public int Subscribers { get; private set; }
        public string? Name { get => _name; set { _name = value; _changed?.Invoke(this, new(nameof(Name))); } }
        public Node? Child { get => _child; set { _child = value; _changed?.Invoke(this, new(nameof(Child))); } }
        public ObservableCollection<Node> Children { get; } = new();
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++Subscribers; }
            remove { _changed -= value; --Subscribers; }
        }
    }
}
