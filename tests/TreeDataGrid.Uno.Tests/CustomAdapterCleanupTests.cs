using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Xunit;
using Core = TreeDataGridCore;
using P = global::Uno.Controls.Presentation;
using U = global::Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class CustomAdapterCleanupTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void Cell_cleanup_attempts_owned_disposal_and_preserves_failures(bool owns, bool removeFails, bool disposeFails)
    {
        var cell = new FaultCell();
        var adapter = P.CellColumnAdapter<Model>.Adapt(cell, owns);
        var notifications = 0;
        adapter.PropertyChanged += (_, _) => ++notifications;
        cell.FailRemove = removeFails;
        cell.FailDispose = disposeFails;
        cell.OnRemove = adapter.Dispose;
        var expected = new List<Exception>();
        if (removeFails) expected.Add(cell.RemoveFailure);
        if (owns && disposeFails) expected.Add(cell.DisposeFailure);
        AssertFailures(Record.Exception(adapter.Dispose), expected);
        Assert.Equal(0, cell.Subscribers);
        Assert.Equal(1, cell.Removes);
        Assert.Equal(owns ? 1 : 0, cell.Disposals);
        Assert.Equal(0, notifications);
        Assert.Null(adapter.TextOptions);
        Assert.False(adapter.CanWrite);
        Assert.False(adapter.CanEdit);
        adapter.Dispose();
        Assert.Equal(1, cell.Removes);
        Assert.Equal(owns ? 1 : 0, cell.Disposals);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void Failed_cell_subscription_rolls_back_without_hiding_construction_failure(bool owns, bool removeFails, bool disposeFails)
    {
        var cell = new FaultCell { FailAdd = true, FailRemove = removeFails, FailDispose = disposeFails };
        var expected = new List<Exception> { cell.AddFailure };
        if (removeFails) expected.Add(cell.RemoveFailure);
        if (owns && disposeFails) expected.Add(cell.DisposeFailure);
        AssertFailures(Record.Exception(() => P.CellColumnAdapter<Model>.Adapt(cell, owns)), expected);
        Assert.Equal(0, cell.Subscribers);
        Assert.Equal(1, cell.Removes);
        Assert.Equal(owns ? 1 : 0, cell.Disposals);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Column_cleanup_releases_ownership_even_when_unsubscription_throws(bool removeFails, bool disposeFails)
    {
        var definition = new Core.Models.TextColumn<Model, string>("Text", model => model.Text);
        var column = new FaultColumn();
        var adapter = new P.CellColumnAdapter<Model>(definition, column);
        var notifications = 0;
        adapter.PropertyChanged += (_, _) => ++notifications;
        column.FailRemove = removeFails;
        column.FailDispose = disposeFails;
        column.OnRemove = adapter.Dispose;
        var expected = new List<Exception>();
        if (removeFails) expected.Add(column.RemoveFailure);
        if (disposeFails) expected.Add(column.DisposeFailure);
        AssertFailures(Record.Exception(adapter.Dispose), expected);
        Assert.Equal(0, column.Subscribers);
        Assert.Equal(0, notifications);
        Assert.Equal(1, column.Removes);
        Assert.Equal(1, column.Disposals);
        adapter.Dispose();
        Assert.Equal(1, column.Removes);
        Assert.Equal(1, column.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_column_subscription_does_not_retain_the_incomplete_adapter(bool removeFails)
    {
        var definition = new Core.Models.TextColumn<Model, string>("Text", model => model.Text);
        var column = new FaultColumn { FailAdd = true, FailRemove = removeFails };
        var expected = new List<Exception> { column.AddFailure };
        if (removeFails) expected.Add(column.RemoveFailure);
        AssertFailures(Record.Exception(() => new P.CellColumnAdapter<Model>(definition, column)), expected);
        Assert.Equal(0, column.Subscribers);
        Assert.Equal(1, column.Removes);
        // Construction failed: the caller/factory still owns its view object.
        Assert.Equal(0, column.Disposals);
    }

    private static void AssertFailures(Exception? actual, IReadOnlyList<Exception> expected)
    {
        if (expected.Count == 0) { Assert.Null(actual); return; }
        if (expected.Count == 1) { Assert.Same(expected[0], actual); return; }
        Assert.IsType<AggregateException>(actual);
        var leaves = new List<Exception>();
        Collect(actual!, leaves);
        Assert.Equal(expected.Count, leaves.Count);
        for (var index = 0; index < expected.Count; ++index) Assert.Same(expected[index], leaves[index]);
        static void Collect(Exception error, List<Exception> output)
        {
            if (error is AggregateException aggregate)
                foreach (var inner in aggregate.InnerExceptions) Collect(inner, output);
            else output.Add(error);
        }
    }

    private sealed class Model { public string Text => "Text"; }
    private abstract class FaultSource : INotifyPropertyChanged, IDisposable
    {
        private static readonly PropertyChangedEventArgs Changed = new("Value");
        private PropertyChangedEventHandler? _handlers;
        internal readonly Exception AddFailure = new InvalidOperationException("add failure");
        internal readonly Exception RemoveFailure = new InvalidOperationException("remove failure");
        internal readonly Exception DisposeFailure = new InvalidOperationException("dispose failure");
        internal bool FailAdd, FailRemove, FailDispose;
        internal int Removes, Disposals;
        internal Action? OnRemove;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _handlers += value; if (FailAdd) throw AddFailure; }
            remove
            {
                ++Removes;
                _handlers -= value;
                var callback = OnRemove;
                OnRemove = null;
                callback?.Invoke();
                // An accessor may already have captured the handler before it
                // was detached. Retired adapters must ignore this final callback.
                value?.Invoke(this, Changed);
                if (FailRemove) throw RemoveFailure;
            }
        }
        public void Dispose() { ++Disposals; if (FailDispose) throw DisposeFailure; }
    }
    private sealed class FaultCell : FaultSource, U.ITextCell
    {
        public object? Value => Text;
        public bool CanEdit => true;
        public U.BeginEditGestures EditGestures => U.BeginEditGestures.Default;
        public string? Text { get; set; } = "Text";
        public TextAlignment TextAlignment => TextAlignment.Left;
        public TextWrapping TextWrapping => TextWrapping.NoWrap;
        public TextTrimming TextTrimming => TextTrimming.None;
    }
    private sealed class FaultColumn : FaultSource, P.ICellColumn<Model>
    {
        public double ActualWidth => 80;
        public bool? CanUserResize => true;
        public object? Header => "Text";
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
        public U.ICell CreateCell(Core.Models.IRow<Model> row) => new FaultCell();
    }
}
