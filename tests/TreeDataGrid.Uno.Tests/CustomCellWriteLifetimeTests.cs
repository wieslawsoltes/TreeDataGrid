using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Xunit;
using Core = TreeDataGridCore;
using P = global::Uno.Controls.Presentation;
using U = global::Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class CustomCellWriteLifetimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Retirement_during_callbacks_prevents_the_write(bool ownsModel, bool duringConversion)
    {
        var model = new Model("Before");
        var cell = new ProbeCell(model);
        using var adapter = P.CellColumnAdapter<Model>.Adapt(cell, ownsModel);
        var input = new ConvertedText(() =>
        {
            if (duringConversion) adapter.Dispose();
            return "Obsolete";
        });
        if (!duringConversion) cell.BeforePermission = adapter.Dispose;
        adapter.Write(input);
        Assert.Equal("Before", model.Text);
        Assert.Equal(0, cell.Writes);
        Assert.Equal(duringConversion ? 1 : 0, input.Calls);
        Assert.Equal(ownsModel ? 1 : 0, cell.DisposeCalls);
        Assert.Equal(0, cell.Subscribers);
        Assert.False(adapter.CanWrite);
        Assert.False(adapter.CanEdit);
        adapter.Dispose();
        Assert.Equal(ownsModel ? 1 : 0, cell.DisposeCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_newer_nested_assignment_wins(bool duringConversion)
    {
        var model = new Model("Before");
        var cell = new ProbeCell(model);
        using var adapter = P.CellColumnAdapter<Model>.Adapt(cell, false);
        Action newerWrite = () => adapter.Write("Nested");
        var input = new ConvertedText(() =>
        {
            if (duringConversion) newerWrite();
            return "Obsolete";
        });
        if (!duringConversion) cell.BeforePermission = newerWrite;
        adapter.Write(input);
        Assert.Equal("Nested", model.Text);
        Assert.Equal(1, cell.Writes);
        adapter.Write("Later");
        Assert.Equal("Later", model.Text);
        Assert.Equal(2, cell.Writes);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public void Reuse_attempt_supersedes_an_older_conversion(int reuseOutcome, int targetIndex)
    {
        var first = new Model("First");
        var second = new Model("Second");
        using var source = new Core.FlatTreeDataGridSource<Model>(new[] { first, second });
        var definition = new Core.Models.TextColumn<Model, string?>("Text", model => model.Text);
        source.Columns.Add(definition);
        var innerColumn = new RetargetColumn { ReuseOutcome = reuseOutcome };
        using var column = new P.CellColumnAdapter<Model>(definition, innerColumn);
        using var adapter = column.CreateCell(source.Rows[0]);
        var cell = Assert.IsType<ProbeCell>(adapter.PresentationModel);
        var input = new ConvertedText(() =>
        {
            var requestedRow = source.Rows[targetIndex];
            if (reuseOutcome == 2)
                Assert.Same(innerColumn.ReuseFailure, Record.Exception(() => column.TryReuseCell(adapter, requestedRow)));
            else
                Assert.Equal(reuseOutcome == 0, column.TryReuseCell(adapter, requestedRow));
            return "Obsolete";
        });
        adapter.Write(input);
        Assert.Equal(1, input.Calls);
        Assert.Equal(0, cell.Writes);
        Assert.Equal("First", first.Text);
        Assert.Equal("Second", second.Text);
        Assert.Same(targetIndex == 0 ? first : second, cell.Model);
        if (reuseOutcome == 0)
        {
            adapter.Write("Current");
            Assert.Equal("Current", cell.Model.Text);
            Assert.Equal(1, cell.Writes);
        }
        adapter.Dispose();
        Assert.Equal(1, cell.DisposeCalls);
        Assert.Equal(0, cell.Subscribers);
    }

    [Fact]
    public void Disposed_adapter_rejects_input_before_application_callbacks()
    {
        var cell = new ProbeCell(new Model("Before"));
        using var adapter = P.CellColumnAdapter<Model>.Adapt(cell, false);
        adapter.Dispose();
        cell.PermissionReads = 0;
        var input = new ConvertedText(() => throw new InvalidOperationException("Conversion must not run."));
        Assert.Throws<ObjectDisposedException>(() => adapter.Write(input));
        Assert.Equal(0, input.Calls);
        Assert.Equal(0, cell.PermissionReads);
        Assert.Equal(0, cell.Writes);
        Assert.Equal(0, cell.DisposeCalls);
    }

    [Fact]
    public void Read_only_rejection_does_not_convert_the_input()
    {
        var cell = new ProbeCell(new Model("Before")) { Writable = false };
        using var adapter = P.CellColumnAdapter<Model>.Adapt(cell, false);
        var input = new ConvertedText(() => "Converted");
        Assert.Throws<InvalidOperationException>(() => adapter.Write(input));
        Assert.Equal(0, input.Calls);
        Assert.Equal(0, cell.Writes);
        cell.Writable = true;
        adapter.Write("Recovered");
        Assert.Equal("Recovered", cell.Text);
        Assert.Equal(1, cell.Writes);
    }

    [Fact]
    public void Conversion_cannot_bypass_new_read_only_state()
    {
        var cell = new ProbeCell(new Model("Before"));
        using var adapter = P.CellColumnAdapter<Model>.Adapt(cell, false);
        var input = new ConvertedText(() => { cell.Writable = false; return "Rejected"; });
        Assert.Throws<InvalidOperationException>(() => adapter.Write(input));
        Assert.Equal(1, input.Calls);
        Assert.Equal("Before", cell.Text);
        Assert.Equal(0, cell.Writes);
    }

    [Fact]
    public void Conversion_failure_preserves_identity_and_allows_recovery()
    {
        var cell = new ProbeCell(new Model("Before"));
        using var adapter = P.CellColumnAdapter<Model>.Adapt(cell, false);
        var failure = new FormatException("Expected conversion failure.");
        var input = new ConvertedText(() => throw failure);
        Assert.Same(failure, Assert.Throws<FormatException>(() => adapter.Write(input)));
        Assert.Equal(0, cell.Writes);
        Assert.Equal("Before", cell.Text);
        adapter.Write("Recovered");
        Assert.Equal("Recovered", cell.Text);
        Assert.Equal(1, cell.Writes);
    }

    [Fact]
    public void Warm_string_writes_allocate_no_managed_storage()
    {
        var cell = new ProbeCell(new Model("Before"));
        using var adapter = P.CellColumnAdapter<Model>.Adapt(cell, false);
        for (var iteration = 0; iteration < 1024; ++iteration)
            adapter.Write((iteration & 1) == 0 ? "Even" : "Odd");
        cell.Writes = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 4096; ++iteration)
            adapter.Write((iteration & 1) == 0 ? "Even" : "Odd");
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(4096, cell.Writes);
        Assert.Equal("Odd", cell.Text);
    }

    private sealed class Model(string? text) { internal string? Text { get; set; } = text; }
    private sealed class ProbeCell(Model model) : U.ITextCell, INotifyPropertyChanged, IDisposable
    {
        internal Model Model = model;
        internal Action? BeforePermission;
        internal bool Writable = true;
        internal int PermissionReads, Writes, Subscribers, DisposeCalls;
        public object? Value => Model.Text;
        public bool CanEdit
        {
            get
            {
                ++PermissionReads;
                var callback = BeforePermission;
                BeforePermission = null;
                callback?.Invoke();
                return Writable;
            }
        }
        public U.BeginEditGestures EditGestures => U.BeginEditGestures.Default;
        public string? Text { get => Model.Text; set { ++Writes; Model.Text = value; } }
        public TextAlignment TextAlignment => TextAlignment.Left;
        public TextWrapping TextWrapping => TextWrapping.NoWrap;
        public TextTrimming TextTrimming => TextTrimming.None;
        public event PropertyChangedEventHandler? PropertyChanged { add => ++Subscribers; remove => --Subscribers; }
        public void Dispose() => ++DisposeCalls;
    }
    private sealed class ConvertedText(Func<string> convert) : IFormattable
    {
        internal int Calls;
        public string ToString(string? format, IFormatProvider? provider) { ++Calls; return convert(); }
    }
    private sealed class RetargetColumn : P.CellColumnBase<Model>, P.ICellColumn<Model>
    {
        internal int ReuseOutcome;
        internal readonly Exception ReuseFailure = new InvalidOperationException("Expected custom reuse failure.");
        internal RetargetColumn() : base("Text", new GridLength(80), new()) { }
        public override U.ICell CreateCell(Core.Models.IRow<Model> row) => new ProbeCell(row.Model!);
        bool P.ICellColumn<Model>.TryReuseCell(U.ICell cell, Core.Models.IRow<Model> row)
        {
            if (cell is not ProbeCell probe) return false;
            probe.Model = row.Model!;
            if (ReuseOutcome == 2) throw ReuseFailure;
            return ReuseOutcome == 0;
        }
    }
}
