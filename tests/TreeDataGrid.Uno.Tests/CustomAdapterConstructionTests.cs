using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Uno.Controls.Presentation;
using Xunit;
using Core = TreeDataGridCore;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public sealed class CustomAdapterConstructionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Leaf_retirement_during_add_waits_for_the_accessor_to_finish(bool owns, bool after)
    {
        var raw = new TextProbe();
        if (after) raw.AfterAdd = handler => ((IDisposable)handler.Target!).Dispose();
        else raw.BeforeAdd = handler => ((IDisposable)handler.Target!).Dispose();
        using var value = CellColumnAdapter<Model>.Adapt(raw, owns);
        Assert.Equal(0, raw.Subscribers); Assert.Equal(1, raw.Removes); Assert.Equal(owns ? 1 : 0, raw.Disposals);
        Assert.Null(value.TextOptions); Assert.False(value.CanEdit); Assert.False(value.CanWrite);
        Assert.Throws<ObjectDisposedException>(() => value.Write("retired"));
        value.Dispose(); Assert.Equal(1, raw.Removes); Assert.Equal(owns ? 1 : 0, raw.Disposals);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Retirement_cleanup_failure_never_disposes_a_completed_model_twice(bool expander, bool owns)
    {
        var raw = expander ? (Probe)new ExpanderProbe() : new TextProbe();
        var failure = new InvalidOperationException("retired model disposal");
        raw.DisposeFailure = failure;
        raw.BeforeAdd = handler => ((IDisposable)handler.Target!).Dispose();
        var error = Record.Exception(() => CellColumnAdapter<Model>.Adapt((UI.ICell)raw, owns));
        if (owns) Assert.Same(failure, error); else Assert.Null(error);
        Assert.Equal(0, raw.Subscribers); Assert.Equal(1, raw.Removes); Assert.Equal(owns ? 1 : 0, raw.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retirement_then_add_failure_preserves_every_failure_and_releases_once(bool expander)
    {
        var raw = expander ? (Probe)new ExpanderProbe() : new TextProbe();
        raw.AddFailure = new InvalidOperationException("add");
        raw.RemoveFailure = new InvalidOperationException("remove");
        raw.DisposeFailure = new InvalidOperationException("dispose");
        raw.BeforeAdd = handler => ((IDisposable)handler.Target!).Dispose();
        var error = Record.Exception(() => CellColumnAdapter<Model>.Adapt((UI.ICell)raw, true));
        AssertFailures(error, raw.AddFailure, raw.RemoveFailure, raw.DisposeFailure);
        Assert.Equal(0, raw.Subscribers); Assert.Equal(1, raw.Removes); Assert.Equal(1, raw.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Column_retirement_during_add_rolls_back_and_leaves_failed_construction_with_caller(bool after)
    {
        var raw = new Column();
        if (after) raw.AfterAdd = handler => ((IDisposable)handler.Target!).Dispose();
        else raw.BeforeAdd = handler => ((IDisposable)handler.Target!).Dispose();
        Assert.Throws<ObjectDisposedException>(() => new CellColumnAdapter<Model>(Definition(), raw));
        Assert.Equal(0, raw.Subscribers); Assert.Equal(1, raw.Removes); Assert.Equal(0, raw.Disposals);
        raw.Dispose(); Assert.Equal(1, raw.Disposals);
    }

    [Fact]
    public void Presentation_factory_preserves_failed_adapter_construction_and_cleanup_errors()
    {
        var raw = new Column
        {
            AddFailure = new InvalidOperationException("factory add"),
            RemoveFailure = new InvalidOperationException("factory remove"),
            DisposeFailure = new InvalidOperationException("factory dispose"),
        };
        using var source = new Core.FlatTreeDataGridSource<Model>([new Model()]);
        var definition = Definition(); definition.PresentationKey = "Custom"; source.Columns.Add(definition);
        var options = new TreeDataGridPresentationOptions<Model>(); options.Columns["Custom"] = _ => raw;
        var error = Record.Exception(() => TreeDataGridPresentation.Create(source, options));
        AssertFailures(error, raw.AddFailure, raw.RemoveFailure, raw.DisposeFailure);
        Assert.Equal(0, raw.Subscribers); Assert.Equal(1, raw.Removes); Assert.Equal(1, raw.Disposals);
        Assert.Single(source.Rows);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retired_column_rejects_before_invoking_custom_factory(bool publicModel)
    {
        var raw = new Column(); using var column = new CellColumnAdapter<Model>(Definition(), raw);
        column.Dispose();
        Assert.Throws<ObjectDisposedException>(() => Create(column, publicModel));
        Assert.Equal(0, raw.Creates); Assert.Equal(1, raw.Disposals);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Retired_returning_factory_value_is_released_once(bool publicModel, bool cleanupFails)
    {
        var cell = new TextProbe(); var raw = new Column { Factory = () => cell };
        using var column = new CellColumnAdapter<Model>(Definition(), raw);
        raw.OnCreate = column.Dispose;
        if (cleanupFails) cell.DisposeFailure = new InvalidOperationException("returning cell cleanup");
        var error = Record.Exception(() => Create(column, publicModel));
        if (cleanupFails)
        {
            var errors = Assert.IsType<AggregateException>(error).InnerExceptions;
            Assert.IsType<ObjectDisposedException>(errors[0]); Assert.Same(cell.DisposeFailure, errors[1]);
        }
        else Assert.IsType<ObjectDisposedException>(error);
        Assert.Equal(1, cell.Disposals); Assert.Equal(0, cell.Subscribers); Assert.Equal(0, cell.Removes);
        Assert.Equal(1, raw.Creates); Assert.Equal(1, raw.Disposals);
    }

    [Fact]
    public void Retirement_during_cell_adaptation_releases_the_completed_adapter_once()
    {
        var cell = new TextProbe(); var raw = new Column { Factory = () => cell };
        using var column = new CellColumnAdapter<Model>(Definition(), raw);
        cell.ReadAlignment = column.Dispose;
        Assert.Throws<ObjectDisposedException>(() => column.CreateCell(new Row()));
        Assert.Equal(0, cell.Subscribers); Assert.Equal(1, cell.Removes); Assert.Equal(1, cell.Disposals);
        Assert.Equal(1, raw.Disposals);
    }

    [Fact]
    public void Retained_reuse_refresh_cannot_succeed_after_retiring_the_column()
    {
        var cell = new TextProbe(); var raw = new Column { Factory = () => cell };
        using var column = new CellColumnAdapter<Model>(Definition(), raw);
        using var value = column.CreateCell(new Row()); cell.ReadAlignment = column.Dispose;
        Assert.False(column.TryReuseCell(value, new Row()));
        Assert.Equal(1, raw.Disposals); Assert.Equal(0, cell.Disposals);
        value.Dispose(); Assert.Equal(1, cell.Disposals);
    }

    private static UI.ICell Create(CellColumnAdapter<Model> column, bool publicModel) =>
        publicModel ? column.CreateCellModel(new Row()) : column.CreateCell(new Row());
    private static Core.Models.TextColumn<Model, string> Definition() => new("Text", model => model.Text);
    private static void AssertFailures(Exception? error, params Exception[] expected)
    {
        var actual = new List<Exception>();
        static void Visit(Exception error, List<Exception> result)
        {
            if (error is AggregateException aggregate) foreach (var child in aggregate.InnerExceptions) Visit(child, result);
            else result.Add(error);
        }
        Assert.NotNull(error); Visit(error, actual); Assert.Equal(expected, actual);
    }
    private sealed class Model { public string Text => "Text"; }
    private sealed class Row : Core.Models.IRow<Model>
    {
        public Model Model { get; } = new();
        object? Core.Models.IRow.Model => Model;
        public object? Header => null;
        public Core.GridLength Height { get; set; } = Core.GridLength.Auto;
        public void UpdateModelIndex(int delta) { }
    }
    private abstract class Probe : INotifyPropertyChanged, IDisposable
    {
        private PropertyChangedEventHandler? _handlers;
        internal Action<PropertyChangedEventHandler>? BeforeAdd, AfterAdd;
        internal Exception? AddFailure, RemoveFailure, DisposeFailure;
        internal int Removes, Disposals;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { BeforeAdd?.Invoke(value!); _handlers += value; AfterAdd?.Invoke(value!); if (AddFailure is { } error) throw error; }
            remove { ++Removes; _handlers -= value; if (RemoveFailure is { } error) throw error; }
        }
        public void Dispose() { ++Disposals; if (DisposeFailure is { } error) throw error; }
    }
    private sealed class TextProbe : Probe, UI.ITextCell
    {
        internal Action? ReadAlignment;
        public object? Value => Text;
        public bool CanEdit => true;
        public UI.BeginEditGestures EditGestures => UI.BeginEditGestures.Default;
        public string? Text { get; set; } = "Text";
        public TextAlignment TextAlignment { get { var callback = ReadAlignment; ReadAlignment = null; callback?.Invoke(); return TextAlignment.Left; } }
        public TextWrapping TextWrapping => TextWrapping.NoWrap;
        public TextTrimming TextTrimming => TextTrimming.None;
    }
    private sealed class ExpanderProbe : Probe, UI.IExpanderCell
    {
        public object? Content => null;
        public Core.Models.IRow Row { get; } = new Row();
        public object? Value => null;
        public bool CanEdit => false;
        public UI.BeginEditGestures EditGestures => UI.BeginEditGestures.None;
        public bool IsExpanded { get; set; }
        public bool ShowExpander => false;
    }
    private sealed class Column : Probe, ICellColumn<Model>
    {
        internal Func<UI.ICell> Factory = () => new TextProbe();
        internal Action? OnCreate;
        internal int Creates;
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
        public UI.ICell CreateCell(Core.Models.IRow<Model> row) { ++Creates; OnCreate?.Invoke(); return Factory(); }
        public bool TryReuseCell(UI.ICell cell, Core.Models.IRow<Model> row) => true;
    }
}
