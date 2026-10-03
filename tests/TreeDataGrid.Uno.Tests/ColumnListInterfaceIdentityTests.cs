using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Xunit;
using Core = TreeDataGridCore;
using P = Uno.Controls.Presentation;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class ColumnListInterfaceIdentityTests
{
    [Fact]
    public void Every_native_collection_route_preserves_original_legacy_columns()
    {
        var first = new Legacy(); var second = new Legacy();
        var columns = new U.ColumnList<Model> { first, second };
        try
        {
            IReadOnlyList<P.ICellColumn<Model>> factories = columns;
            IReadOnlyList<U.IColumn<Model>> typed = columns;
            IReadOnlyList<U.IColumn> untyped = columns;
            U.IColumns native = columns;
            Assert.Same(first, factories[0]);
            Assert.Same(first, untyped[0]);
            Assert.Same(first, native[0]);
            Assert.NotSame(first, typed[0]);
            Assert.Same(first, Assert.Single(factories.Take(1)));
            Assert.Same(first, Assert.Single(untyped.Take(1)));
            Assert.Same(first, Assert.Single(native.Take(1)));
            Assert.Same(first, ((IEnumerable)columns).Cast<object>().First());
            Assert.Same(typed[0], typed.First());
            Assert.All(native, column => Assert.IsAssignableFrom<U.IUpdateColumnLayout>(column));
            var saved = typed[1];
            columns.RemoveAt(0);
            Assert.Same(second, untyped[0]); Assert.Same(second, native[0]);
            Assert.Same(saved, typed[0]);
        }
        finally { columns.Clear(); }
    }

    [Fact]
    public void Typed_and_native_enumerators_observe_the_same_mutation_boundary()
    {
        var original = new Legacy();
        var columns = new U.ColumnList<Model> { original };
        using var typed = ((IEnumerable<U.IColumn<Model>>)columns).GetEnumerator();
        using var native = ((IEnumerable<U.IColumn>)columns).GetEnumerator();
        Assert.True(typed.MoveNext()); Assert.True(native.MoveNext());
        Assert.Same(original, native.Current); Assert.NotSame(original, typed.Current);
        columns.Clear();
        Assert.Throws<InvalidOperationException>(() => native.MoveNext());
        Assert.Throws<InvalidOperationException>(() => typed.MoveNext());
    }

    private sealed class Model { }
    private sealed class Legacy : P.ICellColumn<Model>
    {
        public double ActualWidth => 80;
        public bool? CanUserResize => true;
        public object? Header => "Legacy";
        public GridLength Width { get; private set; } = new(80);
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        public double MinActualWidth => 0;
        public double MaxActualWidth => double.PositiveInfinity;
        public bool StarWidthWasConstrained => false;
        public double CellMeasured(double width, int rowIndex) => width;
        public void CalculateStarWidth(double availableWidth, double totalStars) { }
        public bool CommitActualWidth() => false;
        public void SetWidth(GridLength width) => Width = width;
        public U.ICell CreateCell(Core.Models.IRow<Model> row) => throw new NotSupportedException();
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
    }
}
