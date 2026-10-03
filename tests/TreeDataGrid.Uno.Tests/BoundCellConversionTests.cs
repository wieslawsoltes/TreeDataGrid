using System;
using System.Globalization;
using TreeDataGridCore.Models;
using Xunit;
using global::Uno.Controls.Presentation;

namespace TreeDataGrid.Uno.Tests;

public sealed class BoundCellConversionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Convertible_cannot_write_after_a_lifetime_change(int mode)
    {
        var first = new Model<int>(1);
        var second = new Model<int>(2);
        var writes = 0;
        using var cell = Create(first, (_, _) => ++writes);
        var input = new Convertible(() =>
        {
            switch (mode)
            {
                case 0: Assert.True(cell.TryRetarget(new Row(second))); break;
                case 1: Assert.True(cell.TryRetarget(new Row(first))); break;
                case 2: Assert.True(cell.TrySuspend()); break;
                case 3: cell.Dispose(); break;
            }
            return 99;
        });
        cell.Write(input);
        Assert.Equal(1, input.Calls);
        Assert.Equal(0, writes);
        Assert.Equal(1, first.Value);
        Assert.Equal(2, second.Value);
        if (mode != 3)
        {
            cell.TryRetarget(new Row(second));
            cell.Write(42);
            Assert.Equal(42, second.Value);
            Assert.Equal(1, writes);
        }
    }

    [Fact]
    public void Newer_nested_typed_write_supersedes_the_older_conversion()
    {
        var model = new Model<int>(1);
        var writes = 0;
        using var cell = Create(model, (_, _) => ++writes);
        cell.Write(new Convertible(() => { cell.Write(42); return 99; }));
        Assert.Equal(42, model.Value);
        Assert.Equal(42, cell.Value);
        Assert.Equal(1, writes);
    }

    [Fact]
    public void Newer_nested_converted_write_supersedes_the_older_conversion()
    {
        var model = new Model<int>(1);
        var writes = 0;
        using var cell = Create(model, (_, _) => ++writes);
        cell.Write(new Convertible(() => { cell.Write("42"); return 99; }));
        Assert.Equal(42, model.Value);
        Assert.Equal(42, cell.Value);
        Assert.Equal(1, writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Enum_text_conversion_observes_lifetime_and_nested_writes(bool sameModel)
    {
        var first = new Model<Choice>(Choice.First);
        var second = new Model<Choice>(Choice.Second);
        var writes = 0;
        using var cell = Create(first, (_, _) => ++writes);
        cell.Write(new EnumInput(() =>
        {
            cell.TryRetarget(new Row(sameModel ? first : second));
            return nameof(Choice.Third);
        }));
        Assert.Equal(0, writes);
        Assert.Equal(Choice.First, first.Value);
        Assert.Equal(Choice.Second, second.Value);
        cell.Write(new EnumInput(() => { cell.Write(Choice.First); return nameof(Choice.Third); }));
        Assert.Equal(1, writes);
        Assert.Equal(Choice.First, cell.Value);
    }

    [Fact]
    public void Failure_after_retarget_preserves_exception_and_new_binding()
    {
        var first = new Model<int>(1);
        var second = new Model<int>(2);
        var failure = new InvalidOperationException("Conversion failed.");
        using var cell = Create(first);
        var actual = Assert.Throws<InvalidOperationException>(() => cell.Write(new Convertible(() =>
        {
            cell.TryRetarget(new Row(second));
            throw failure;
        })));
        Assert.Same(failure, actual);
        Assert.Equal(2, cell.Value);
        Assert.Equal(1, first.Value);
        cell.Write("42");
        Assert.Equal(42, second.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Inactive_or_readonly_bindings_do_not_run_conversion(int mode)
    {
        var model = new Model<int>(1);
        var column = ValueColumn<Model<int>, int>.FromDelegate("Value", x => x.Value,
            setter: mode == 0 ? null : static (x, v) => x.Value = v);
        using var cell = new BoundCell<Model<int>, int>(column, new Row(model), true);
        if (mode == 1) cell.TrySuspend();
        if (mode == 2) cell.Dispose();
        var input = new Convertible(static () => 99);
        var failure = Record.Exception(() => cell.Write(input));
        if (mode == 2) Assert.IsType<ObjectDisposedException>(failure);
        else Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(0, input.Calls);
        Assert.Equal(1, model.Value);
    }

    [Fact]
    public void Nullable_numeric_and_enum_conversions_preserve_the_existing_contract()
    {
        var number = new Model<decimal?>(null);
        using var numeric = Create(number, culture: CultureInfo.GetCultureInfo("de-DE"));
        numeric.Write("1,5");
        Assert.Equal(1.5m, number.Value);
        numeric.Write(null);
        Assert.Null(number.Value);
        Assert.Throws<FormatException>(() => numeric.Write("not a decimal"));
        numeric.Write("2,5");
        Assert.Equal(2.5m, number.Value);
        var enumeration = new Model<Choice?>(null);
        using var enums = Create(enumeration);
        enums.Write("Second");
        Assert.Equal(Choice.Second, enumeration.Value);
        enums.Write(null);
        Assert.Null(enumeration.Value);
    }

    [Fact]
    public void Warm_typed_bound_cell_writes_allocate_nothing()
    {
        var model = new Model<string>("Even");
        var writes = 0;
        using var cell = Create(model, (_, _) => ++writes);
        for (var i = 0; i < 1024; ++i) cell.Write((i & 1) == 0 ? "Even" : "Odd");
        writes = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) cell.Write((i & 1) == 0 ? "Even" : "Odd");
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(4096, writes);
        Assert.Equal("Odd", cell.Value);
    }

    private static BoundCell<Model<T>, T> Create<T>(Model<T> model, Action<Model<T>, T>? written = null, CultureInfo? culture = null)
    {
        var column = ValueColumn<Model<T>, T>.FromDelegate("Value", x => x.Value, setter: (x, v) =>
        {
            x.Value = v;
            written?.Invoke(x, v);
        });
        return new(column, new Row(model), true, culture);
    }
    private sealed class Model<T>(T value) { internal T Value = value; }
    private sealed class Row(object model) : IRow
    {
        public object? Header => null;
        public object? Model => model;
        public TreeDataGridCore.GridLength Height { get; set; } = TreeDataGridCore.GridLength.Auto;
    }
    private enum Choice { First, Second, Third }
    private sealed class EnumInput(Func<string> value) { public override string ToString() => value(); }
    private sealed class Convertible(Func<int> convert) : IConvertible
    {
        internal int Calls;
        public int ToInt32(IFormatProvider? provider) { ++Calls; return convert(); }
        public TypeCode GetTypeCode() => TypeCode.Object;
        public object ToType(Type conversionType, IFormatProvider? provider) => throw new NotSupportedException();
        public bool ToBoolean(IFormatProvider? provider) => throw new NotSupportedException();
        public byte ToByte(IFormatProvider? provider) => throw new NotSupportedException();
        public char ToChar(IFormatProvider? provider) => throw new NotSupportedException();
        public DateTime ToDateTime(IFormatProvider? provider) => throw new NotSupportedException();
        public decimal ToDecimal(IFormatProvider? provider) => throw new NotSupportedException();
        public double ToDouble(IFormatProvider? provider) => throw new NotSupportedException();
        public short ToInt16(IFormatProvider? provider) => throw new NotSupportedException();
        public long ToInt64(IFormatProvider? provider) => throw new NotSupportedException();
        public sbyte ToSByte(IFormatProvider? provider) => throw new NotSupportedException();
        public float ToSingle(IFormatProvider? provider) => throw new NotSupportedException();
        public string ToString(IFormatProvider? provider) => throw new NotSupportedException();
        public ushort ToUInt16(IFormatProvider? provider) => throw new NotSupportedException();
        public uint ToUInt32(IFormatProvider? provider) => throw new NotSupportedException();
        public ulong ToUInt64(IFormatProvider? provider) => throw new NotSupportedException();
    }
}
