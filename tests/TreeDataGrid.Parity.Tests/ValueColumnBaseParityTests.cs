using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using Xunit;
using A = Avalonia.Controls.Models.TreeDataGrid;
using U = Uno.Controls.Models.TreeDataGrid;
using AB = Avalonia.Experimental.Data;
using UB = Uno.Experimental.Data;
using AV = Avalonia.Data;
using UV = Uno.Data;

namespace TreeDataGrid.Parity.Tests;

public sealed class ValueColumnBaseParityTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(null, 4)]
    [InlineData(4, null)]
    [InlineData(2, 7)]
    [InlineData(7, 2)]
    [InlineData(4, 4)]
    public void Default_sort_delegates_match_the_reference(int? first, int? second)
    {
        var a = new Original(new());
        var u = new Native(new());
        var x = first is { } f ? new Model(f) : null;
        var y = second is { } s ? new Model(s) : null;
        foreach (var direction in new[] { ListSortDirection.Ascending, ListSortDirection.Descending })
        {
            Assert.Equal(Math.Sign(a.GetComparison(direction)!(x, y)), Math.Sign(u.GetComparison(direction)!(x, y)));
            Assert.Same(u.GetComparison(direction), u.GetComparison(direction));
        }
        Assert.Null(a.GetComparison((ListSortDirection)7));
        Assert.Null(u.GetComparison((ListSortDirection)7));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sort_policy_is_captured_but_layout_options_remain_live(bool allowSort)
    {
        Comparison<Model?> compare = static (_, _) => 17;
        var ao = new A.ColumnOptions<Model> { CanUserSortColumn = allowSort, CompareAscending = compare };
        var uo = new U.ColumnOptions<Model> { CanUserSortColumn = allowSort, CompareAscending = compare };
        var a = new Original(ao);
        var u = new Native(uo);
        Assert.Same(ao, a.Options);
        Assert.Same(uo, u.Options);
        Assert.Equal(a.GetComparison(ListSortDirection.Ascending), u.GetComparison(ListSortDirection.Ascending));
        ao.CanUserSortColumn = uo.CanUserSortColumn = !allowSort;
        ao.CompareAscending = uo.CompareAscending = static (_, _) => 99;
        Assert.Equal(allowSort, u.GetComparison(ListSortDirection.Ascending) is not null);
        Assert.Equal(a.GetComparison(ListSortDirection.Ascending), u.GetComparison(ListSortDirection.Ascending));
        ao.MinWidth = new Avalonia.Controls.GridLength(81);
        uo.MinWidth = new Microsoft.UI.Xaml.GridLength(81);
        Assert.Equal(((A.IUpdateColumnLayout)a).MinActualWidth, ((U.IUpdateColumnLayout)u).MinActualWidth);
        ao.CanUserResizeColumn = uo.CanUserResizeColumn = false;
        Assert.Equal(((A.IColumn)a).CanUserResize, ((U.IColumn)u).CanUserResize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Pixel_auto_star_layout_uses_the_existing_native_engine(int unit)
    {
        var a = new Original(new(), new Avalonia.Controls.GridLength(unit == 0 ? 73 : 1, (Avalonia.Controls.GridUnitType)unit));
        var u = new Native(new(), new Microsoft.UI.Xaml.GridLength(unit == 0 ? 73 : 1, (Microsoft.UI.Xaml.GridUnitType)unit));
        var al = (A.IUpdateColumnLayout)a;
        var ul = (U.IUpdateColumnLayout)u;
        Assert.Equal(al.CellMeasured(95, 0), ul.CellMeasured(95, 0));
        if (unit == (int)Microsoft.UI.Xaml.GridUnitType.Star)
        {
            al.CalculateStarWidth(400, 2);
            ul.CalculateStarWidth(400, 2);
        }
        al.CommitActualWidth(); ul.CommitActualWidth();
        Assert.Equal(a.ActualWidth, u.ActualWidth);
        Assert.Equal(al.MinActualWidth, ul.MinActualWidth);
        Assert.Equal(al.MaxActualWidth, ul.MaxActualWidth);
    }

    [Fact]
    public void Protected_binding_factory_observes_and_writes_the_same_Core_model()
    {
        var model = new Model(2);
        var a = new Original(new());
        var u = new Native(new());
        var ae = a.Bind(model);
        using var ue = u.Bind(model);
        var ar = new Recorder<AV.BindingValue<int>>();
        var ur = new Recorder<UV.BindingValue<int>>();
        using var sa = ae.Subscribe(ar);
        using var su = ue.Subscribe(ur);
        Assert.Equal(ar.Last.Value, ur.Last.Value);
        model.Number = 7;
        Assert.Equal(ar.Last.Value, ur.Last.Value);
        ue.OnNext(11);
        Assert.Equal(11, model.Number);
        Assert.Equal(ar.Last.Value, ur.Last.Value);
        ae.OnNext(13);
        Assert.Equal(13, model.Number);
        Assert.Equal(ar.Last.Value, ur.Last.Value);
        sa.Dispose(); su.Dispose();
        Assert.Equal(0, model.Subscribers);
    }

    [Fact]
    public void Explicit_selector_and_binding_remain_distinct_and_borrowed()
    {
        Func<Model, int> selector = static model => -model.Number;
        var ab = AB.TypedBinding<Model>.OneWay(model => model.Number);
        var ub = UB.TypedBinding<Model>.OneWay(model => model.Number);
        var a = new Original(selector, ab);
        var u = new Native(selector, ub);
        Assert.Same(selector, u.ValueSelector);
        Assert.Same(ub, u.Binding);
        Assert.Same(ab, a.Binding);
        var model = new Model(12);
        Assert.Equal(-12, u.ValueSelector(model));
        using var ue = u.Bind(model);
        var ae = a.Bind(model);
        var ur = new Recorder<UV.BindingValue<int>>();
        var ar = new Recorder<AV.BindingValue<int>>();
        using var su = ue.Subscribe(ur);
        using var sa = ae.Subscribe(ar);
        Assert.Equal(12, ur.Last.Value);
        Assert.Equal(ar.Last.Value, ur.Last.Value);
        Assert.Equal(Math.Sign(a.GetComparison(ListSortDirection.Ascending)!(model, new(20))),
            Math.Sign(u.GetComparison(ListSortDirection.Ascending)!(model, new(20))));
    }

    [Fact]
    public void Existing_base_options_reference_forwards_both_reads_and_writes()
    {
        var options = new U.ColumnOptions<Model>();
        var column = new Native(options);
        var baseOptions = ((Uno.Controls.Presentation.CellColumnBase<Model>)column).Options;
        options.MinWidth = new(42);
        options.MaxWidth = new(150);
        options.CanUserResizeColumn = false;
        Assert.Equal(options.MinWidth, baseOptions.MinWidth);
        Assert.Equal(options.MaxWidth, baseOptions.MaxWidth);
        Assert.False(baseOptions.CanUserResizeColumn);
        baseOptions.MinWidth = new(61);
        baseOptions.MaxWidth = null;
        baseOptions.CanUserResizeColumn = null;
        Assert.Equal(61, options.MinWidth.Value);
        Assert.Null(options.MaxWidth);
        Assert.Null(options.CanUserResizeColumn);
    }

    [Fact]
    public void Warm_comparison_and_policy_queries_do_not_allocate()
    {
        var column = new Native(new());
        var x = new Model(1);
        var y = new Model(2);
        var layout = (U.IUpdateColumnLayout)column;
        for (var i = 0; i < 1024; ++i) { column.GetComparison(ListSortDirection.Ascending); _ = layout.MinActualWidth; }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) { column.GetComparison(ListSortDirection.Ascending); _ = layout.MinActualWidth; }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(column.GetComparison(ListSortDirection.Ascending)!(x, y) < 0);
    }

    private sealed class Original : A.ColumnBase<Model, int>
    {
        public Original(A.ColumnOptions<Model> options, Avalonia.Controls.GridLength? width = null)
            : base("Number", model => model.Number, static (model, value) => model.Number = value, width, options) { }
        public Original(Func<Model, int> selector, AB.TypedBinding<Model, int> binding)
            : base("Number", selector, binding, null, null) { }
        public Avalonia.Experimental.Data.Core.TypedBindingExpression<Model, int> Bind(Model model) => CreateBindingExpression(model);
        public override A.ICell CreateCell(A.IRow<Model> row) => new A.TextCell<int>(ValueSelector(row.Model));
    }
    private sealed class Native : U.ColumnBase<Model, int>
    {
        public Native(U.ColumnOptions<Model> options, Microsoft.UI.Xaml.GridLength? width = null)
            : base("Number", model => model.Number, static (model, value) => model.Number = value, width, options) { }
        public Native(Func<Model, int> selector, UB.TypedBinding<Model, int> binding)
            : base("Number", selector, binding, null, null) { }
        public Uno.Experimental.Data.Core.TypedBindingExpression<Model, int> Bind(Model model) => CreateBindingExpression(model);
        public override U.ICell CreateCell(TreeDataGridCore.Models.IRow<Model> row) => new U.TextCell<int>(ValueSelector(row.Model));
    }
    private sealed class Recorder<T> : IObserver<T>
    {
        public T Last = default!;
        public void OnNext(T value) => Last = value;
        public void OnCompleted() { }
        public void OnError(Exception error) => throw error;
    }
    private sealed class Model(int number) : INotifyPropertyChanged
    {
        private int _number = number;
        private PropertyChangedEventHandler? _changed;
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public int Number { get => _number; set { _number = value; _changed?.Invoke(this, new(nameof(Number))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
}
