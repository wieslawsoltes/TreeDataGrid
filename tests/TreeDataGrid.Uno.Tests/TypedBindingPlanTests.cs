using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Uno.Data;
using Uno.Experimental.Data;
using Uno.Experimental.Data.Core;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class TypedBindingPlanTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Same_descriptor_shares_instructions_not_roots_observers_or_lifetime(bool cell)
    {
        var firstRoot = new Node(17); var secondRoot = new Node(29);
        var descriptor = Descriptor();
        using var first = Create(descriptor, firstRoot, cell);
        using var second = Create(descriptor, secondRoot, cell);
        Assert.NotSame(first, second);
        Assert.Same(Field(first, "_column"), Field(second, "_column"));
        Assert.Same(Field(first, "_links"), Field(second, "_links"));
        Assert.NotSame(descriptor.Links, Field(first, "_links"));
        var a = new Recorder(); var b = new Recorder();
        using var sa = first.Subscribe(a); using var sb = second.Subscribe(b);
        Assert.Equal(17, a.Last.Value); Assert.Equal(29, b.Last.Value);
        Assert.Equal(1, firstRoot.Subscribers); Assert.Equal(1, secondRoot.Subscribers);
        firstRoot.Number = 43;
        Assert.Equal(43, a.Last.Value); Assert.Equal(29, b.Last.Value);
        first.Dispose();
        Assert.Equal(0, firstRoot.Subscribers); Assert.Equal(1, secondRoot.Subscribers);
        second.OnNext(61);
        Assert.Equal(61, secondRoot.Number); Assert.Equal(43, firstRoot.Number);
        second.Dispose(); Assert.Equal(0, secondRoot.Subscribers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Replaced_read_or_write_cannot_modify_existing_expression_instructions(bool write)
    {
        var descriptor = Descriptor(); var root = new Node(7);
        using var first = descriptor.Instance(root);
        var a = new Recorder(); using var sa = first.Subscribe(a);
        if (write) descriptor.Write = (node, value) => node.Number = value * 10;
        else descriptor.Read = node => node.Number * 10;
        Assert.Null(DescriptorField(descriptor, "_expressionColumn"));
        using var second = descriptor.Instance(root);
        Assert.NotSame(Field(first, "_column"), Field(second, "_column"));
        var b = new Recorder(); using var sb = second.Subscribe(b);
        if (write)
        {
            first.OnNext(2); Assert.Equal(2, root.Number);
            second.OnNext(3); Assert.Equal(30, root.Number);
        }
        else
        {
            Assert.Equal(7, a.Last.Value); Assert.Equal(70, b.Last.Value);
            root.Number = 9;
            Assert.Equal(9, a.Last.Value); Assert.Equal(90, b.Last.Value);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Link_array_replacement_or_in_place_edit_produces_a_new_owned_snapshot(bool inPlace)
    {
        var root = new Node(1) { Child = new Node(2) };
        var descriptor = Descriptor();
        using var first = descriptor.Instance(root);
        if (inPlace) descriptor.Links![0] = node => node.Child!;
        else descriptor.Links = [node => node.Child!];
        using var second = descriptor.Instance(root);
        Assert.NotSame(Field(first, "_links"), Field(second, "_links"));
        var a = new Recorder(); var b = new Recorder();
        using var sa = first.Subscribe(a); using var sb = second.Subscribe(b);
        Assert.Equal(1, root.Subscribers); Assert.Equal(1, root.Child.Subscribers);
        var beforeA = a.Count; var beforeB = b.Count;
        root.Number = 5;
        Assert.True(a.Count > beforeA); Assert.Equal(beforeB, b.Count);
        root.Child.Number = 6;
        Assert.True(b.Count > beforeB);
        first.Dispose(); second.Dispose();
        Assert.Equal(0, root.Subscribers + root.Child.Subscribers);
    }

    [Fact]
    public void Invalid_in_place_link_does_not_poison_old_expressions_or_repaired_plan()
    {
        var descriptor = Descriptor(); var root = new Node(7);
        var original = descriptor.Links![0];
        using var first = descriptor.Instance(root);
        descriptor.Links[0] = null!;
        Assert.Equal("links", Assert.Throws<ArgumentException>(() => descriptor.Instance(root)).ParamName);
        var a = new Recorder(); using var sa = first.Subscribe(a);
        root.Number = 11; Assert.Equal(11, a.Last.Value);
        descriptor.Links[0] = original;
        using var recovered = descriptor.Instance(root);
        Assert.Same(Field(first, "_column"), Field(recovered, "_column"));
        Assert.Same(Field(first, "_links"), Field(recovered, "_links"));
    }

    [Theory]
    [InlineData(BindingMode.OneTime)]
    [InlineData(BindingMode.OneWay)]
    [InlineData(BindingMode.TwoWay)]
    [InlineData(BindingMode.OneWayToSource)]
    [InlineData(BindingMode.Default)]
    public void Effective_instance_mode_selects_the_correct_observation_plan(BindingMode mode)
    {
        var descriptor = Descriptor(); var root = new Node(13);
        using var first = descriptor.Instance(root, mode);
        using var second = descriptor.Instance(root, mode);
        Assert.Same(Field(first, "_links"), Field(second, "_links"));
        var observer = new Recorder(); using var subscription = first.Subscribe(observer);
        var before = observer.Count;
        root.Number = 19;
        Assert.Equal(mode == BindingMode.OneTime ? before : before + 1, observer.Count);
        Assert.Equal(mode == BindingMode.OneTime ? 13 : 19, observer.Last.Value);
        first.Dispose(); Assert.Equal(0, root.Subscribers);
    }

    [Fact]
    public void One_time_ignores_invalid_link_elements_but_still_requires_initialized_links()
    {
        var descriptor = Descriptor(); descriptor.Links = [null!];
        using var first = descriptor.Instance(new Node(1), BindingMode.OneTime);
        Assert.Empty((Array)Field(first, "_links"));
        Assert.Throws<ArgumentException>(() => descriptor.Instance(new Node(1)));
        descriptor.Links = null;
        Assert.Throws<InvalidOperationException>(() => descriptor.Instance(new Node(1), BindingMode.OneTime));
    }

    [Fact]
    public void Fallback_remains_expression_local_while_instructions_are_shared()
    {
        var descriptor = Descriptor(); descriptor.FallbackValue = 17;
        using var first = descriptor.Instance(null);
        descriptor.FallbackValue = 29;
        using var second = descriptor.Instance(null);
        using var third = descriptor.InstanceForCell(null, fallback: new Optional<int>(43));
        Assert.Same(Field(first, "_column"), Field(second, "_column"));
        Assert.Same(Field(first, "_column"), Field(third, "_column"));
        var a = new Recorder(); var b = new Recorder(); var c = new Recorder();
        using var sa = first.Subscribe(a); using var sb = second.Subscribe(b); using var sc = third.Subscribe(c);
        Assert.True(a.Last.HasError); Assert.True(b.Last.HasError); Assert.True(c.Last.HasError);
        Assert.Equal(17, a.Last.Value); Assert.Equal(29, b.Last.Value); Assert.Equal(43, c.Last.Value);
    }

    [Fact]
    public void Separate_descriptors_and_direct_constructors_do_not_share_instruction_storage()
    {
        var descriptor = Descriptor(); var other = Descriptor(); var root = new Node(5);
        using var a = descriptor.Instance(root); using var b = other.Instance(root);
        Assert.NotSame(Field(a, "_column"), Field(b, "_column"));
        var source = new Root(root);
        using var c = new TypedBindingExpression<Node, int>(source, descriptor.Read!, descriptor.Write, descriptor.Links!, default);
        using var d = new TypedBindingExpression<Node, int>(source, descriptor.Read!, descriptor.Write, descriptor.Links!, default);
        Assert.NotSame(Field(c, "_column"), Field(d, "_column"));
        Assert.NotSame(Field(c, "_links"), Field(d, "_links"));
        Assert.NotSame(descriptor.Links, Field(c, "_links"));
    }

    [Fact]
    public void Shared_plan_does_not_change_retarget_suspend_or_recovery()
    {
        var descriptor = Descriptor(); var a = new Node(1); var b = new Node(2);
        using var first = descriptor.InstanceForCell(a); using var second = descriptor.InstanceForCell(a);
        var fa = new Recorder(); var sb = new Recorder();
        using var s1 = first.Subscribe(fa); using var s2 = second.Subscribe(sb);
        Assert.Equal(2, a.Subscribers);
        first.SuspendRoot(); Assert.Equal(1, a.Subscribers);
        first.SetRoot(b); Assert.Equal(2, fa.Last.Value); Assert.Equal(1, sb.Last.Value);
        b.Number = 3; Assert.Equal(3, fa.Last.Value); Assert.Equal(1, sb.Last.Value);
        second.Dispose(); Assert.Equal(0, a.Subscribers); Assert.Equal(1, b.Subscribers);
        first.Dispose(); Assert.Equal(0, b.Subscribers);
    }

    [Fact]
    public void Invalid_modes_and_missing_accessors_are_checked_even_after_a_plan_was_cached()
    {
        var descriptor = Descriptor(); var root = new Node(1);
        using var first = descriptor.Instance(root);
        Assert.Throws<ArgumentOutOfRangeException>(() => descriptor.Instance(root, (BindingMode)99));
        descriptor.Write = null;
        Assert.Throws<InvalidOperationException>(() => descriptor.Instance(root, BindingMode.TwoWay));
        descriptor.Read = null;
        Assert.Throws<InvalidOperationException>(() => descriptor.Instance(root));
        descriptor.Read = node => node.Number; descriptor.Links = null;
        Assert.Throws<InvalidOperationException>(() => descriptor.Instance(root));
    }

    [Fact]
    public void Descriptor_source_and_mode_do_not_override_explicit_instance_arguments()
    {
        var descriptor = Descriptor(); var root = new Node(17);
        descriptor.Source = new Node(99); descriptor.Mode = BindingMode.OneTime;
        using var first = descriptor.Instance(root);
        var a = new Recorder(); using var s = first.Subscribe(a);
        root.Number = 29; Assert.Equal(29, a.Last.Value);
        Assert.Equal(1, root.Subscribers);
    }

    private static TypedBinding<Node, int> Descriptor() => new()
    {
        Read = node => node.Number,
        Write = (node, value) => node.Number = value,
        Links = [node => node],
        Mode = BindingMode.TwoWay,
    };
    private static TypedBindingExpression<Node, int> Create(TypedBinding<Node, int> descriptor, Node root, bool cell) =>
        cell ? descriptor.InstanceForCell(root) : descriptor.Instance(root);
    private static object Field(TypedBindingExpression<Node, int> expression, string name) =>
        typeof(TypedBindingExpression<Node, int>).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(expression)!;
    private static object? DescriptorField(TypedBinding<Node, int> descriptor, string name) =>
        typeof(TypedBinding<Node, int>).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(descriptor);
    private sealed class Recorder : IObserver<BindingValue<int>>
    {
        public BindingValue<int> Last; public int Count;
        public void OnNext(BindingValue<int> value) { Last = value; ++Count; }
        public void OnError(Exception error) => throw error;
        public void OnCompleted() { }
    }
    private sealed class Root(Node node) : IObservable<Node?>, IDisposable
    {
        public IDisposable Subscribe(IObserver<Node?> observer) { observer.OnNext(node); return this; }
        public void Dispose() { }
    }
    private sealed class Node(int number) : INotifyPropertyChanged
    {
        private int _number = number;
        private PropertyChangedEventHandler? _handlers;
        private static readonly PropertyChangedEventArgs Changed = new(nameof(Number));
        public Node? Child { get; set; }
        public int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public int Number { get => _number; set { _number = value; _handlers?.Invoke(this, Changed); } }
        public event PropertyChangedEventHandler? PropertyChanged { add => _handlers += value; remove => _handlers -= value; }
    }
}
