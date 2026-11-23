using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Xaml;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using UI = Uno.Controls.Models.TreeDataGrid;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class CellModelCompatibilityTests
{
    [Fact]
    public void Observable_text_does_not_echo_inbound_values_and_disposes_subscription()
    {
        var binding = new Binding<int>(12);
        using var cell = new UI.TextCell<int>(binding, false);
        Assert.Equal(12, cell.Value);
        Assert.Equal(0, binding.Writes);
        cell.Text = "42";
        Assert.Equal(42, cell.Value);
        Assert.Equal(1, binding.Writes);
        binding.Push(31);
        Assert.Equal("31", cell.Text);
        Assert.Equal(1, binding.Writes);
        cell.Dispose();
        cell.Dispose();
        Assert.Equal(0, binding.Subscribers);
        Assert.Equal(1, binding.Disposals);
    }

    [Fact]
    public void Text_edit_buffers_cancels_and_preserves_failed_conversion_for_retry()
    {
        var binding = new Binding<int>(12);
        using var cell = new UI.TextCell<int>(binding, false);
        cell.BeginEdit();
        Assert.Equal("12", cell.Text);
        cell.Text = "Cancelled";
        cell.CancelEdit();
        Assert.Equal("12", cell.Text);
        Assert.Equal(0, binding.Writes);
        cell.BeginEdit();
        cell.Text = "Invalid";
        Assert.Throws<FormatException>(() => cell.EndEdit());
        Assert.Equal("Invalid", cell.Text);
        Assert.Equal(12, cell.Value);
        cell.Text = "18";
        cell.EndEdit();
        Assert.Equal(18, cell.Value);
        Assert.Equal(1, binding.Writes);
    }

    [Fact]
    public void Text_formatting_nullable_conversion_and_options_are_preserved()
    {
        var binding = new Binding<decimal?>(12.5m);
        using var cell = new UI.TextCell<decimal?>(binding, false, new UI.TextColumnOptions<object>
        {
            StringFormat = "Value {0:F1}", Culture = CultureInfo.InvariantCulture,
            TextAlignment = TextAlignment.Right, TextWrapping = TextWrapping.Wrap,
        });
        Assert.Equal("Value 12.5", cell.Text);
        cell.BeginEdit();
        Assert.Equal("12.5", cell.Text);
        cell.EndEdit();
        Assert.Equal(0, binding.Writes);
        cell.Text = "14.5";
        Assert.Equal(14.5m, cell.Value);
        cell.Text = null;
        Assert.Null(cell.Value);
        Assert.Equal(TextAlignment.Right, cell.TextAlignment);
        Assert.Equal(TextWrapping.Wrap, cell.TextWrapping);
    }

    [Fact]
    public void Checkbox_is_toggleable_without_becoming_a_text_editor()
    {
        var binding = new Binding<bool?>(false);
        using var cell = new UI.CheckBoxCell(binding, false, true);
        Assert.False(cell.CanEdit);
        Assert.False(cell.IsReadOnly);
        Assert.True(cell.IsThreeState);
        Assert.Equal(UI.BeginEditGestures.None, cell.EditGestures);
        cell.Value = true;
        Assert.Equal(1, binding.Writes);
        binding.Push(null);
        Assert.Null(cell.Value);
        Assert.Equal(1, binding.Writes);
        cell.Dispose();
        Assert.Equal(0, binding.Subscribers);
    }

    [Fact]
    public void Template_transactions_use_content_not_an_unrelated_row_model()
    {
        var content = new Editable();
        var cell = new UI.TemplateCell(content, _ => throw new NotSupportedException(), _ => throw new NotSupportedException(), null);
        var editable = (IEditableObject)cell;
        editable.BeginEdit();
        editable.CancelEdit();
        editable.BeginEdit();
        editable.EndEdit();
        Assert.Equal(2, content.Begins);
        Assert.Equal(1, content.Cancels);
        Assert.Equal(1, content.Ends);
        Assert.Same(content, cell.Value);
        Assert.True(cell.CanEdit);
    }

    [Fact]
    public void Custom_column_adapter_preserves_actual_checkbox_model_kind_and_writeback()
    {
        using var source = new FlatTreeDataGridSource<object>([new()]);
        source.Columns.Add(new TextColumn<object, int>("Custom", _ => 0) { PresentationKey = "Custom" });
        var binding = new Binding<bool?>(false);
        var supplied = new UI.CheckBoxCell(binding, false, true);
        var options = new TreeDataGridPresentationOptions<object>();
        options.Columns["Custom"] = _ => new CustomColumn(() => supplied);
        using var view = TreeDataGridPresentation.Create(source, options);
        var cell = view.RealizeCell(0, 0);
        Assert.Same(supplied, cell.PresentationModel);
        Assert.Equal(CellKind.CheckBox, cell.ContentKind);
        Assert.False(cell.CanEdit);
        Assert.True(cell.CanWrite);
        Assert.True(cell.IsThreeState);
        cell.Write(true);
        Assert.True(supplied.Value);
        Assert.Equal(1, binding.Writes);
        cell.Dispose();
        Assert.Equal(0, binding.Subscribers);
    }

    [Fact]
    public void Custom_expander_keeps_original_row_model_and_tracks_content_replacement()
    {
        using var source = new FlatTreeDataGridSource<object>([new()]);
        source.Columns.Add(new TextColumn<object, int>("Custom", _ => 0) { PresentationKey = "Custom" });
        var original = new Binding<int>(12);
        var text = new UI.TextCell<int>(original, false);
        var supplied = new CustomExpander(source.Rows[0], text);
        var options = new TreeDataGridPresentationOptions<object>();
        options.Columns["Custom"] = _ => new CustomColumn(() => supplied);
        using var view = TreeDataGridPresentation.Create(source, options);
        using var cell = Assert.IsAssignableFrom<ExpanderCellValue>(view.RealizeCell(0, 0));
        Assert.Same(supplied, cell.PresentationModel);
        Assert.Same(supplied.Row, cell.Row);
        Assert.Same(text, cell.Content);
        Assert.Same(text, cell.Inner.PresentationModel);
        Assert.Equal(CellKind.Expander, cell.Kind);
        Assert.Equal(CellKind.Text, cell.ContentKind);
        cell.IsExpanded = true;
        Assert.True(supplied.IsExpanded);
        cell.Write("42");
        Assert.Equal(42, text.Value);
        var checkBinding = new Binding<bool?>(false);
        var check = new UI.CheckBoxCell(checkBinding, false, true);
        supplied.Replace(check);
        Assert.Equal(0, original.Subscribers);
        Assert.Equal(1, original.Disposals);
        Assert.Equal(CellKind.CheckBox, cell.ContentKind);
        Assert.Same(check, cell.Inner.PresentationModel);
        cell.Write(true);
        Assert.True(check.Value);
        supplied.Replace(null);
        Assert.Null(cell.Content);
        Assert.Null(cell.Inner.Value);
        Assert.Equal(0, checkBinding.Subscribers);
        cell.Dispose();
        cell.Dispose();
        Assert.Equal(1, supplied.Disposals);
        Assert.Equal(1, checkBinding.Disposals);
    }

    [Fact]
    public void Expander_adapter_does_not_double_dispose_borrowed_native_cells()
    {
        using var source = new FlatTreeDataGridSource<object>([new()]);
        source.Columns.Add(new TextColumn<object, int>("Custom", _ => 0) { PresentationKey = "Custom" });
        var original = new CountedCell();
        var supplied = new CustomExpander(source.Rows[0], original);
        var options = new TreeDataGridPresentationOptions<object>();
        options.Columns["Custom"] = _ => new CustomColumn(() => supplied);
        using var view = TreeDataGridPresentation.Create(source, options);
        using var cell = Assert.IsAssignableFrom<ExpanderCellValue>(view.RealizeCell(0, 0));
        Assert.Same(original, cell.Inner);
        var replacement = new CountedCell();
        supplied.Replace(replacement);
        Assert.Equal(1, original.Disposals);
        Assert.Equal(0, replacement.Disposals);
        var events = 0;
        cell.PropertyChanged += (_, _) => ++events;
        original.Publish();
        Assert.Equal(0, events);
        cell.Dispose();
        Assert.Equal(1, replacement.Disposals);
        replacement.Publish();
        Assert.Equal(0, events);
    }

    [Fact]
    public void Cyclic_custom_expander_content_is_rejected_and_root_is_disposed()
    {
        using var source = new FlatTreeDataGridSource<object>([new()]);
        source.Columns.Add(new TextColumn<object, int>("Custom", _ => 0) { PresentationKey = "Custom" });
        var supplied = new CustomExpander(source.Rows[0], null);
        supplied.Replace(supplied);
        var options = new TreeDataGridPresentationOptions<object>();
        options.Columns["Custom"] = _ => new CustomColumn(() => supplied);
        using var view = TreeDataGridPresentation.Create(source, options);
        Assert.Throws<InvalidOperationException>(() => view.RealizeCell(0, 0));
        Assert.Equal(1, supplied.Disposals);
    }

    [Fact]
    public void Custom_reuse_receives_original_cell_and_current_Core_row()
    {
        using var source = new FlatTreeDataGridSource<object>(["First", "Second"]);
        source.Columns.Add(new TextColumn<object, string>("Custom", x => x.ToString()!) { PresentationKey = "Custom" });
        var supplied = new UI.TextCell<string>("First");
        var column = new CustomColumn(() => supplied)
        {
            Reuse = (cell, row) =>
            {
                Assert.Same(supplied, cell);
                supplied.Value = (string)row.Model;
                return true;
            },
        };
        var options = new TreeDataGridPresentationOptions<object>();
        options.Columns["Custom"] = _ => column;
        using var view = TreeDataGridPresentation.Create(source, options);
        using var value = view.RealizeCell(0, 0);
        Assert.True(view.TryReuseCell(0, 1, value));
        Assert.Same(supplied, value.PresentationModel);
        Assert.Equal("Second", value.Value);
        Assert.Equal(1, column.ReuseCalls);
        view.RecycleCell(view.NativeColumns[0], value);
        Assert.Throws<ObjectDisposedException>(() => supplied.Value = "Retired");
    }

    [Fact]
    public void Safely_suspended_custom_native_cells_reuse_through_their_column()
    {
        using var source = new FlatTreeDataGridSource<object>(["First", "Second"]);
        source.Columns.Add(new TextColumn<object, string>("Custom", x => x.ToString()!) { PresentationKey = "Custom" });
        var supplied = new SuspendedCell { Text = "First" };
        var column = new CustomColumn(() => supplied)
        {
            Reuse = (cell, row) => { ((SuspendedCell)cell).Text = (string)row.Model; return true; },
        };
        var options = new TreeDataGridPresentationOptions<object>();
        options.Columns["Custom"] = _ => column;
        using var view = TreeDataGridPresentation.Create(source, options);
        var first = view.RealizeCell(0, 0);
        view.RecycleCell(view.NativeColumns[0], first);
        Assert.Null(supplied.Text);
        using var second = view.RealizeCell(0, 1);
        Assert.Same(first, second);
        Assert.Equal("Second", second.Value);
        Assert.Equal(1, column.ReuseCalls);
    }

    private sealed class SuspendedCell : CellValue
    {
        public string? Text;
        public override object? Value => Text;
        public override bool CanEdit => false;
        public override void Write(object? value) => throw new NotSupportedException();
        internal override bool TrySuspend() { Text = null; return true; }
        internal override bool TryRetarget(IRow row) => throw new InvalidOperationException("Column reuse was bypassed.");
    }
    private sealed class CountedCell : CellValue
    {
        public int Disposals { get; private set; }
        public override object? Value => "Native";
        public override bool CanEdit => false;
        public override void Write(object? value) => throw new NotSupportedException();
        public override void Dispose() => ++Disposals;
        public void Publish() => RaisePropertyChanged(nameof(Value));
    }
    private sealed class CustomExpander(IRow row, object? content) : NotifyingBase, UI.IExpanderCellPresentation, IDisposable
    {
        private object? _content = content;
        private bool _expanded;
        public IRow Row => row;
        public object? Content => _content;
        public object? Value => ReferenceEquals(_content, this) ? null : (_content as UI.ICell)?.Value;
        public bool CanEdit => !ReferenceEquals(_content, this) && (_content as UI.ICell)?.CanEdit == true;
        public UI.BeginEditGestures EditGestures => UI.BeginEditGestures.Default;
        public bool ShowExpander => true;
        public bool IsExpanded { get => _expanded; set => RaiseAndSetIfChanged(ref _expanded, value); }
        public int Disposals { get; private set; }
        public void Replace(object? next)
        {
            var previous = _content;
            _content = next;
            try { RaisePropertyChanged(nameof(Content)); }
            finally { if (!ReferenceEquals(previous, this) && !ReferenceEquals(previous, next)) (previous as IDisposable)?.Dispose(); }
        }
        public void Dispose()
        {
            ++Disposals;
            if (!ReferenceEquals(_content, this)) (_content as IDisposable)?.Dispose();
        }
    }

    private sealed class Binding<T>(T initial) : IObservable<T>, IObserver<T>
    {
        private readonly List<IObserver<T>> _observers = new();
        private T _value = initial;
        public int Writes { get; private set; }
        public int Disposals { get; private set; }
        public int Subscribers => _observers.Count;
        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observers.Add(observer);
            observer.OnNext(_value);
            return new Subscription(() => { _observers.Remove(observer); ++Disposals; });
        }
        public void Push(T value) { _value = value; foreach (var observer in _observers.ToArray()) observer.OnNext(value); }
        public void OnNext(T value) { ++Writes; Push(value); }
        public void OnError(Exception error) { foreach (var observer in _observers.ToArray()) observer.OnError(error); }
        public void OnCompleted() { foreach (var observer in _observers.ToArray()) observer.OnCompleted(); }
    }
    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() { var dispose = _dispose; _dispose = null; dispose?.Invoke(); }
    }
    private sealed class Editable : IEditableObject
    {
        public int Begins;
        public int Cancels;
        public int Ends;
        public void BeginEdit() => ++Begins;
        public void CancelEdit() => ++Cancels;
        public void EndEdit() => ++Ends;
    }
    private sealed class CustomColumn(Func<UI.ICell> create) : NotifyingBase, ICellColumn<object>
    {
        public Func<UI.ICell, IRow<object>, bool>? Reuse { get; init; }
        public int ReuseCalls { get; private set; }
        public double ActualWidth => 100;
        public bool? CanUserResize => true;
        public object? Header => "Custom";
        public Microsoft.UI.Xaml.GridLength Width { get; private set; } = new(100);
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        public double MinActualWidth => 0;
        public double MaxActualWidth => double.PositiveInfinity;
        public bool StarWidthWasConstrained => false;
        public double CellMeasured(double width, int rowIndex) => width;
        public void CalculateStarWidth(double availableWidth, double totalStars) { }
        public bool CommitActualWidth() => false;
        public void SetWidth(Microsoft.UI.Xaml.GridLength width) => Width = width;
        public UI.ICell CreateCell(IRow<object> row) => create();
        public bool TryReuseCell(UI.ICell cell, IRow<object> row) { ++ReuseCalls; return Reuse?.Invoke(cell, row) == true; }
    }
}
