using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Primitives;

public class CellRebindLifecycleTests
{
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Hooks_bracket_replacement_or_reuse_without_changing_lifecycle_order(bool reuse, bool nullValue)
    {
        using var fixture = new Fixture(reuse, nullValue);
        var original = fixture.Cell.Model;
        fixture.Presenter.Unrealize();
        Assert.Empty(fixture.Events); // Row-level deferred unrealization is not a cell rebind yet.
        fixture.Presenter.Realize(1);
        Assert.Equal(new[] { "begin", "unrealize", "reuse", "realize", "end:True" }, fixture.Events);
        Assert.Equal(reuse, ReferenceEquals(original, fixture.Cell.Model));
        Assert.Equal(1, fixture.Cell.RowIndex);
        Assert.Equal(0, fixture.Cell.ColumnIndex);
        if (nullValue) Assert.Null(fixture.Cell.Model!.Value);
    }

    [AvaloniaTheory]
    [InlineData("begin")]
    [InlineData("unrealize")]
    [InlineData("reuse")]
    [InlineData("realize")]
    public void Failed_callbacks_always_end_the_rebind_with_failure(string failure)
    {
        using var fixture = new Fixture(true);
        fixture.Failure = failure;
        fixture.Presenter.Unrealize();
        Assert.Throws<InvalidOperationException>(() => fixture.Presenter.Realize(1));
        var expected = new[] { "begin", "unrealize", "reuse", "realize" };
        Assert.Equal(expected.Take(Array.IndexOf(expected, failure) + 1).Append("end:False"), fixture.Events);
        fixture.Failure = null;
    }

    [AvaloniaFact]
    public void Finalizing_an_idle_row_does_not_call_rebind_hooks()
    {
        using var fixture = new Fixture(false);
        fixture.Presenter.Unrealize();
        var cell = fixture.Cell;
        fixture.Presenter.FinalizeUnrealize();
        Assert.Equal(new[] { "unrealize" }, fixture.Events);
        Assert.Null(cell.Model);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Prepared_clearing_indexes_and_disposal_keep_their_order(bool reuse)
    {
        using var fixture = new Fixture(reuse, recordLifecycle: true);
        fixture.Presenter.Unrealize();
        fixture.Presenter.Realize(1);
        var expected = new List<string> { "begin", "unrealize", "clearing:0", "index", "reuse" };
        if (!reuse) expected.AddRange(new[] { "dispose", "create" });
        expected.AddRange(new[] { "realize", "prepared:1", "index", "end:True" });
        Assert.Equal(expected, fixture.Events);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ordinary_unrealization_preserves_existing_model_disposal_order(bool rowRemoved)
    {
        using var fixture = new Fixture(false, recordLifecycle: true);
        if (rowRemoved)
            fixture.Presenter.UnrealizeOnRowRemoved();
        else
        {
            fixture.Presenter.Unrealize();
            fixture.Presenter.FinalizeUnrealize();
        }
        Assert.Equal(rowRemoved
            ? new[] { "dispose", "unrealize", "clearing:0", "index" }
            : new[] { "unrealize", "clearing:0", "dispose", "index" }, fixture.Events);
    }

    private sealed class Fixture : IDisposable
    {
        public List<string> Events { get; } = new();
        public string? Failure;
        public bool RecordLifecycle;
        public TreeDataGridCellsPresenter Presenter { get; }
        public HookCell Cell => (HookCell)Presenter.RealizedElements[0]!;
        private readonly TestWindow _window;
        public void Record(string name)
        {
            Events.Add(name);
            if (Failure == name) throw new InvalidOperationException(name);
        }
        public Fixture(bool reuse, bool nullValue = false, bool recordLifecycle = false)
        {
            RecordLifecycle = recordLifecycle;
            var columns = new ColumnList<object> { new HookColumn(this, reuse, nullValue) };
            Presenter = new TreeDataGridCellsPresenter
            {
                ElementFactory = new HookFactory(this),
                Items = columns,
                Rows = new AnonymousSortableRows<object>(new TreeDataGridItemsSourceView<object>(new[] { new object(), new object() }), null),
            };
            Presenter.Realize(0);
            Control root = Presenter;
            if (recordLifecycle)
            {
                var grid = new TreeDataGrid
                {
                    Template = new FuncControlTemplate<TreeDataGrid>((_, _) => Presenter)
                };
                grid.CellClearing += (_, e) => Record("clearing:" + e.RowIndex);
                grid.CellPrepared += (_, e) => Record("prepared:" + e.RowIndex);
                Presenter.ChildIndexChanged += (_, _) => Record("index");
                root = grid;
            }
            _window = new TestWindow(root, new Size(100, 100));
            _window.UpdateLayout();
            Events.Clear();
        }
        public void Dispose() { Failure = null; _window.Content = null; _window.Close(); }
    }

    private sealed class HookColumn(Fixture fixture, bool reuse, bool nullValue) : LayoutTestColumn<object>("Value"), IReusableCellColumn<object>
    {
        public override ICell CreateCell(IRow<object> row)
        {
            if (!fixture.RecordLifecycle) return nullValue ? new LayoutTestCell(null!) : base.CreateCell(row);
            fixture.Record("create");
            return new RecordingModel(fixture);
        }

        bool IReusableCellColumn<object>.TryReuseCell(ICell cell, IRow<object> row)
        {
            fixture.Record("reuse");
            return reuse;
        }
    }

    private sealed class RecordingModel(Fixture fixture) : TextCell<string>("value"), IDisposable
    {
        void IDisposable.Dispose() { fixture.Record("dispose"); base.Dispose(); }
    }

    private sealed class HookFactory(Fixture fixture) : TestElementFactory
    {
        protected override Control CreateElement(object? data) => new HookCell(fixture);
        protected override string GetDataRecycleKey(object? data) => typeof(HookCell).FullName!;
    }

    private sealed class HookCell(Fixture fixture) : LayoutTestCellControl
    {
        public override void BeginRebind() => fixture.Record("begin");
        public override void EndRebind(bool realized) => fixture.Record("end:" + realized);
        public override void Unrealize() { fixture.Record("unrealize"); base.Unrealize(); }
        public override void Realize(TreeDataGridElementFactory factory, ITreeDataGridSelectionInteraction? selection,
            ICell model, int columnIndex, int rowIndex)
        {
            fixture.Record("realize");
            base.Realize(factory, selection, model, columnIndex, rowIndex);
        }
    }
}
