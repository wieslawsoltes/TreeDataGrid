using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using Xunit;
using A = Avalonia.Controls.Models.TreeDataGrid;
using U = global::Uno.Controls.Models.TreeDataGrid;
using AB = Avalonia.Experimental.Data;
using UB = global::Uno.Experimental.Data;

namespace TreeDataGrid.Parity.Tests;

/// <summary>Compare application-visible evaluation, not just pure getter results.</summary>
public sealed class ComparisonEvaluationOrderTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Both_directions_evaluate_first_then_second_exactly_once(int family)
    {
        foreach (var direction in Directions)
        {
            var (reference, native) = Create(family, direction);
            var trace = new List<string>();
            var first = new Probe("first", 1, trace);
            var second = new Probe("second", 0, trace);
            var expected = reference(first, second);
            Assert.Equal(new[] { "first", "second" }, trace);
            trace.Clear();
            var actual = native(first, second);
            Assert.Equal(new[] { "first", "second" }, trace);
            Assert.Equal(expected, actual);
            Assert.Equal(2, first.Reads);
            Assert.Equal(2, second.Reads);
            Assert.Equal(direction == ListSortDirection.Ascending ? 1 : -1, Math.Sign(actual));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void A_first_getter_mutation_is_visible_to_the_second_getter(int family)
    {
        foreach (var direction in Directions)
        {
            var (reference, native) = Create(family, direction);
            var trace = new List<string>();
            var first = new Probe("first", 1, trace);
            var second = new Probe("second", 0, trace);
            first.BeforeReturn = () => second.Number = 1;
            var expected = reference(first, second);
            Assert.Equal(0, expected);
            Assert.Equal(new[] { "first", "second" }, trace);
            trace.Clear();
            second.Number = 0;
            var actual = native(first, second);
            Assert.Equal(expected, actual);
            Assert.Equal(new[] { "first", "second" }, trace);
            Assert.Equal(1, second.Number);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Getter_failures_keep_reference_identity_and_short_circuit_order(int family)
    {
        foreach (var direction in Directions)
        foreach (var failedIndex in new[] { 0, 1 })
        {
            var (reference, native) = Create(family, direction);
            var trace = new List<string>();
            var failure = new InvalidOperationException("Getter " + failedIndex);
            var first = new Probe("first", 1, trace);
            var second = new Probe("second", 0, trace);
            (failedIndex == 0 ? first : second).Failure = failure;
            Assert.Same(failure, Record.Exception(() => reference(first, second)));
            var expectedTrace = trace.ToArray();
            Assert.Equal(failedIndex == 0 ? new[] { "first" } : new[] { "first", "second" }, expectedTrace);
            trace.Clear();
            Assert.Same(failure, Record.Exception(() => native(first, second)));
            Assert.Equal(expectedTrace, trace);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Aliased_models_are_read_twice_without_reversing_returned_values(int family)
    {
        var (reference, native) = Create(family, ListSortDirection.Descending);
        var probe = new Probe("same", 0);
        probe.BeforeReturn = () => ++probe.Number;
        var expected = reference(probe, probe);
        Assert.True(expected > 0);
        Assert.Equal(2, probe.Number);
        probe.Number = 0;
        var actual = native(probe, probe);
        Assert.Equal(expected, actual);
        Assert.Equal(2, probe.Number);
        Assert.Equal(4, probe.Reads);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Warm_builtin_comparisons_preserve_zero_managed_allocation(int family)
    {
        var (_, ascending) = Create(family, ListSortDirection.Ascending);
        var (_, descending) = Create(family, ListSortDirection.Descending);
        var first = new Probe("first", 1);
        var second = new Probe("second", 0);
        for (var i = 0; i < 1024; ++i) { ascending(first, second); descending(first, second); }
        var sum = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) sum += ascending(first, second) + descending(first, second);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(0, sum);
        Assert.Equal(10240, first.Reads);
        Assert.Equal(10240, second.Reads);
    }

    private static readonly ListSortDirection[] Directions = [ListSortDirection.Ascending, ListSortDirection.Descending];

    private static (Comparison<Probe?> Reference, Comparison<Probe?> Native) Create(int family, ListSortDirection direction)
    {
        switch (family)
        {
            case 0:
                Expression<Func<Probe, int>> expression = model => model.ReadNumber();
                return (new ReferenceColumn(expression).GetComparison(direction)!, new NativeColumn(expression).GetComparison(direction)!);
            case 1:
                Func<Probe, int> getter = static model => model.ReadNumber();
                return (new ReferenceColumn(getter).GetComparison(direction)!, new NativeColumn(getter).GetComparison(direction)!);
            case 2:
                return (new A.TextColumn<Probe, int>("Value", model => model.ReadNumber()).GetComparison(direction)!,
                    new U.TextColumn<Probe, int>("Value", model => model.ReadNumber()).GetComparison(direction)!);
            case 3:
                return (new A.CheckBoxColumn<Probe>("Flag", model => model.ReadBoolean()).GetComparison(direction)!,
                    new U.CheckBoxColumn<Probe>("Flag", model => model.ReadBoolean()).GetComparison(direction)!);
            case 4:
                return (new A.CheckBoxColumn<Probe>("Flag", model => model.ReadNullableBoolean()).GetComparison(direction)!,
                    new U.CheckBoxColumn<Probe>("Flag", model => model.ReadNullableBoolean()).GetComparison(direction)!);
            default: throw new ArgumentOutOfRangeException(nameof(family));
        }
    }

    private sealed class Probe(string name, int number, List<string>? trace = null)
    {
        internal int Number = number;
        internal int Reads;
        internal Exception? Failure;
        internal Action? BeforeReturn;
        public int ReadNumber()
        {
            ++Reads;
            trace?.Add(name);
            var result = Number;
            BeforeReturn?.Invoke();
            if (Failure is { } failure) throw failure;
            return result;
        }
        public bool ReadBoolean() => ReadNumber() != 0;
        public bool? ReadNullableBoolean() => ReadNumber() != 0;
    }

    private sealed class ReferenceColumn : A.ColumnBase<Probe, int>
    {
        internal ReferenceColumn(Expression<Func<Probe, int>> getter) : base("Value", getter, null, null, new()) { }
        internal ReferenceColumn(Func<Probe, int> getter) : base("Value", getter,
            new AB.TypedBinding<Probe, int> { Read = getter, Links = [] }, null, new()) { }
        public override A.ICell CreateCell(A.IRow<Probe> row) => throw new NotSupportedException("Comparison-only fixture.");
    }
    private sealed class NativeColumn : U.ColumnBase<Probe, int>
    {
        internal NativeColumn(Expression<Func<Probe, int>> getter) : base("Value", getter, null, null, new()) { }
        internal NativeColumn(Func<Probe, int> getter) : base("Value", getter,
            new UB.TypedBinding<Probe, int> { Read = getter, Links = [] }, null, new()) { }
        public override U.ICell CreateCell(TreeDataGridCore.Models.IRow<Probe> row) =>
            throw new NotSupportedException("Comparison-only fixture.");
    }
}
