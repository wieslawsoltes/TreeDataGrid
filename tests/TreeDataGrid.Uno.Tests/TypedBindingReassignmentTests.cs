using System;
using System.ComponentModel;
using System.Reflection;
using Uno.Data;
using Uno.Experimental.Data;
using Uno.Experimental.Data.Core;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class TypedBindingReassignmentTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void Same_assignment_preserves_instructions_but_advances_cell_revision(int property, bool forCell)
    {
        var descriptor = CreateDescriptor();
        var a = new Node(11); var b = new Node(23);
        using var first = Create(descriptor, a, forCell);
        var revision = descriptor.CellRevision;
        Reassign(descriptor, property);
        Assert.Equal(unchecked(revision + 1), descriptor.CellRevision);
        using var second = Create(descriptor, b, forCell);
        Assert.NotSame(first, second);
        Assert.Same(Field(first, "_column"), Field(second, "_column"));
        Assert.Same(Field(first, "_links"), Field(second, "_links"));
        Assert.NotSame(descriptor.Links, Field(first, "_links"));
        var fa = new Recorder(); var sb = new Recorder();
        using var sa = first.Subscribe(fa); using var ss = second.Subscribe(sb);
        Assert.Equal(11, fa.Last); Assert.Equal(23, sb.Last);
        first.OnNext(31);
        Assert.Equal(31, a.Number); Assert.Equal(23, b.Number);
        first.Dispose();
        Assert.Equal(0, a.Subscribers); Assert.Equal(1, b.Subscribers);
        b.Number = 41; Assert.Equal(41, sb.Last);
        second.Dispose(); Assert.Equal(0, b.Subscribers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Equal_but_distinct_delegates_invalidate_the_plan(bool writer)
    {
        var descriptor = CreateDescriptor();
        using var first = descriptor.Instance(new Node(7));
        if (writer)
        {
            var replacement = new Action<Node, int>(Write);
            Assert.NotSame(descriptor.Write, replacement);
            Assert.True(descriptor.Write!.Equals(replacement));
            descriptor.Write = replacement;
        }
        else
        {
            var replacement = new Func<Node, int>(Read);
            Assert.NotSame(descriptor.Read, replacement);
            Assert.True(descriptor.Read!.Equals(replacement));
            descriptor.Read = replacement;
        }
        using var second = descriptor.Instance(new Node(9));
        Assert.NotSame(Field(first, "_column"), Field(second, "_column"));
        Assert.Same(Field(first, "_links"), Field(second, "_links"));
    }

    [Fact]
    public void Reassigned_mutated_array_is_checked_before_reuse()
    {
        var root = new Node(1) { Child = new Node(2) };
        var descriptor = CreateDescriptor();
        using var old = descriptor.Instance(root);
        var links = descriptor.Links!;
        links[0] = static x => x.Child!;
        descriptor.Links = links;
        using var current = descriptor.Instance(root);
        Assert.NotSame(Field(old, "_links"), Field(current, "_links"));
        var a = new Recorder(); var b = new Recorder();
        using var sa = old.Subscribe(a); using var sb = current.Subscribe(b);
        var before = b.Count;
        root.Number = 5;
        Assert.Equal(5, a.Last); Assert.Equal(before, b.Count);
        root.Child.Number = 7;
        Assert.Equal(5, b.Last);
        old.Dispose(); current.Dispose();
        Assert.Equal(0, root.Subscribers + root.Child.Subscribers);
    }

    [Fact]
    public void Invalid_same_array_assignment_is_rejected_and_repair_keeps_prior_snapshot()
    {
        var descriptor = CreateDescriptor();
        var links = descriptor.Links!;
        var original = links[0];
        using var old = descriptor.Instance(new Node(7));
        links[0] = null!;
        descriptor.Links = links;
        Assert.Equal("links", Assert.Throws<ArgumentException>(() => descriptor.Instance(new Node(9))).ParamName);
        links[0] = original;
        descriptor.Links = links;
        using var repaired = descriptor.Instance(new Node(11));
        Assert.Same(Field(old, "_links"), Field(repaired, "_links"));
        Assert.Same(Field(old, "_column"), Field(repaired, "_column"));
    }

    [Fact]
    public void A_different_array_keeps_its_existing_snapshot_invalidation_contract()
    {
        var descriptor = CreateDescriptor();
        using var old = descriptor.Instance(new Node(3));
        descriptor.Links = (Func<Node, object>[])descriptor.Links!.Clone();
        using var current = descriptor.Instance(new Node(4));
        Assert.NotSame(Field(old, "_links"), Field(current, "_links"));
        Assert.Same(Field(old, "_column"), Field(current, "_column"));
    }

    [Fact]
    public void Revisions_advance_for_all_same_assignments_even_with_no_plan()
    {
        var descriptor = CreateDescriptor();
        var revision = descriptor.CellRevision;
        for (var i = 0; i < 4096; ++i)
        {
            Reassign(descriptor, 0);
            Reassign(descriptor, 1);
            Reassign(descriptor, 2);
        }
        Assert.Equal(unchecked(revision + 4096 * 3), descriptor.CellRevision);
    }

    [Theory]
    [InlineData(BindingMode.Default)]
    [InlineData(BindingMode.OneTime)]
    [InlineData(BindingMode.OneWay)]
    [InlineData(BindingMode.TwoWay)]
    [InlineData(BindingMode.OneWayToSource)]
    public void Reassignment_preserves_effective_mode_and_observation(BindingMode mode)
    {
        var descriptor = CreateDescriptor(); var root = new Node(7);
        using var old = descriptor.Instance(root, mode);
        for (var property = 0; property < 3; ++property) Reassign(descriptor, property);
        using var current = descriptor.Instance(root, mode);
        var observer = new Recorder();
        using var subscription = current.Subscribe(observer);
        root.Number = 13;
        Assert.Equal(mode == BindingMode.OneTime ? 7 : 13, observer.Last);
        Assert.Same(Field(old, "_column"), Field(current, "_column"));
        Assert.Same(Field(old, "_links"), Field(current, "_links"));
        current.Dispose(); Assert.Equal(0, root.Subscribers);
    }

    [Fact]
    public void Reassignment_never_bypasses_invalid_descriptor_validation()
    {
        var descriptor = CreateDescriptor();
        using var old = descriptor.Instance(new Node(7));
        descriptor.Read = null;
        descriptor.Read = null;
        Assert.Throws<InvalidOperationException>(() => descriptor.Instance(new Node(8)));
        descriptor.Read = Read;
        descriptor.Write = null;
        descriptor.Write = null;
        Assert.Throws<InvalidOperationException>(() => descriptor.Instance(new Node(8), BindingMode.TwoWay));
        descriptor.Links = null;
        descriptor.Links = null;
        Assert.Throws<InvalidOperationException>(() => descriptor.Instance(new Node(8), BindingMode.OneTime));
    }

    private static TypedBinding<Node, int> CreateDescriptor() => new()
    {
        Read = new Func<Node, int>(Read),
        Write = new Action<Node, int>(Write),
        Links = [static x => x],
    };
    private static int Read(Node node) => node.Number;
    private static void Write(Node node, int value) => node.Number = value;
    private static void Reassign(TypedBinding<Node, int> descriptor, int property)
    {
        switch (property)
        {
            case 0: var read = descriptor.Read; descriptor.Read = read; break;
            case 1: var write = descriptor.Write; descriptor.Write = write; break;
            default: var links = descriptor.Links; descriptor.Links = links; break;
        }
    }
    private static TypedBindingExpression<Node, int> Create(TypedBinding<Node, int> descriptor, Node root, bool cell) =>
        cell ? descriptor.InstanceForCell(root) : descriptor.Instance(root);
    private static object Field(TypedBindingExpression<Node, int> expression, string name) =>
        typeof(TypedBindingExpression<Node, int>).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(expression)!;
    private sealed class Recorder : IObserver<BindingValue<int>>
    {
        internal int Last, Count;
        public void OnNext(BindingValue<int> value) { Last = value.Value; ++Count; }
        public void OnError(Exception error) => throw error;
        public void OnCompleted() { }
    }
    private sealed class Node(int number) : INotifyPropertyChanged
    {
        private int _number = number;
        private PropertyChangedEventHandler? _handlers;
        private static readonly PropertyChangedEventArgs Changed = new(nameof(Number));
        internal Node? Child;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public int Number { get => _number; set { _number = value; _handlers?.Invoke(this, Changed); } }
        public event PropertyChangedEventHandler? PropertyChanged { add => _handlers += value; remove => _handlers -= value; }
    }
}
