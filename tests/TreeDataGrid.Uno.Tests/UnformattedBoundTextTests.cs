using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class UnformattedBoundTextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unformatted_integer_text_does_not_pay_the_public_object_value_box(bool live)
    {
        using var fixture = new Fixture<int>(123456789, live);
        long chars = 0;
        for (var i = 0; i < 8192; ++i) { _ = fixture.Text.Text; _ = BoxedOracle(fixture.Cell.Value); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 8192; ++i) chars += BoxedOracle(fixture.Cell.Value)!.Length;
        var boxed = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        long actualChars = 0;
        for (var i = 0; i < 8192; ++i) actualChars += fixture.Text.Text!.Length;
        var typed = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(chars, actualChars);
        if (RuntimeFeature.IsDynamicCodeCompiled)
            Assert.True(typed < boxed, $"Text allocated {typed}; object-valued oracle allocated {boxed}.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Mutable_struct_ToString_uses_a_copy_and_does_not_modify_the_binding_value(bool live)
    {
        using var fixture = new Fixture<MutableText>(new MutableText(7), live);
        Assert.Equal("8", fixture.Text.Text);
        Assert.Equal("8", fixture.Text.Text);
        Assert.Equal(7, ((MutableText)fixture.Cell.Value!).Counter);
        Assert.Equal(7, fixture.First.Value.Counter);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_ToString_exception_propagates_and_later_values_recover(bool live)
    {
        var error = new InvalidOperationException("ToString failed");
        using var fixture = new Fixture<CallbackText>(new CallbackText(() => throw error), live);
        Assert.Same(error, Record.Exception(() => _ = fixture.Text.Text));
        fixture.First.Value = new CallbackText(() => "recovered");
        Assert.Equal("recovered", fixture.Text.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reentrant_ToString_keeps_the_current_read_snapshot_and_next_source_value(bool live)
    {
        using var fixture = new Fixture<CallbackText>(new CallbackText(() => "initial"), live);
        fixture.First.Value = new CallbackText(() =>
        {
            fixture.First.Value = new CallbackText(() => "new");
            return "old snapshot";
        });
        Assert.Equal("old snapshot", fixture.Text.Text);
        Assert.Equal("new", fixture.Text.Text);
        Assert.Equal(1, fixture.First.Subscribers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retarget_writeback_and_cleanup_keep_the_original_Core_owner(bool live)
    {
        using var fixture = new Fixture<int>(123, live);
        fixture.Second.Value = 456;
        Assert.True(fixture.Column.TryReuseCell(fixture.Cell, (IRow<Model<int>>)fixture.Source.Rows[1]));
        Assert.Equal(0, fixture.First.Subscribers);
        Assert.Equal("456", fixture.Text.Text);
        fixture.First.Value = 999;
        Assert.Equal("456", fixture.Text.Text);
        fixture.Cell.Write(789);
        Assert.Equal(789, fixture.Second.Value);
        Assert.Equal("789", fixture.Text.Text);
        fixture.Cell.Dispose();
        Assert.Equal(0, fixture.Second.Subscribers);
        Assert.Equal(2, fixture.Source.Rows.Count);
    }

    [Fact]
    public void Live_format_can_toggle_between_raw_and_composite_without_replacing_the_cell()
    {
        using var fixture = new Fixture<int>(12345, true);
        var column = Assert.IsType<U.TextColumn<Model<int>, int>>(fixture.Column);
        column.Options.StringFormat = "[{0}]";
        Assert.Equal("[12345]", fixture.Text.Text);
        column.Options.StringFormat = null!;
        Assert.Equal("12345", fixture.Text.Text);
        column.Options.StringFormat = "{0}";
        Assert.Equal("12345", fixture.Text.Text);
        Assert.Equal(1, fixture.First.Subscribers);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string? BoxedOracle(object? value) => value?.ToString();
    private struct MutableText(int value)
    {
        public int Counter = value;
        public override string ToString() => (++Counter).ToString(CultureInfo.InvariantCulture);
    }
    private sealed class CallbackText(Func<string> callback)
    {
        public override string ToString() => callback();
    }
    private sealed class Model<T> : INotifyPropertyChanged
    {
        private T? _value;
        private PropertyChangedEventHandler? _handlers;
        private static readonly PropertyChangedEventArgs Changed = new(nameof(Value));
        public T? Value { get => _value; set { _value = value; _handlers?.Invoke(this, Changed); } }
        public int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _handlers += value; remove => _handlers -= value; }
    }
    private sealed class Fixture<T> : IDisposable
    {
        public readonly Model<T> First, Second;
        public readonly FlatTreeDataGridSource<Model<T>> Source;
        public readonly ValueCellColumn<Model<T>, T?> Column;
        public readonly CellValue Cell;
        public U.ITextCell Text => (U.ITextCell)Cell;
        public Fixture(T? value, bool live)
        {
            First = new() { Value = value }; Second = new();
            Source = new([First, Second]);
            var definition = new ValueColumn<Model<T>, T?>("Value", x => x.Value, (x, v) => x.Value = v);
            Source.Columns.Add(definition);
            Column = live ? new U.TextColumn<Model<T>, T>(definition, new() { StringFormat = null! })
                : new ValueCellColumn<Model<T>, T?>(definition, CellKind.Text);
            Cell = Column.CreateCell(Source.Rows[0]);
        }
        public void Dispose()
        {
            Cell.Dispose(); Column.Dispose(); Source.Dispose();
            Assert.Equal(0, First.Subscribers + Second.Subscribers);
        }
    }
}
