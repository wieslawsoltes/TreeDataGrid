using System;
using System.Collections.Generic;
using Xunit;
using UI = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Uno.Tests;

public class ObservableCellWriteTests
{
    [Fact]
    public void Text_commit_retries_the_same_rejected_input_and_keeps_the_edit_buffer()
    {
        var binding = new Binding<int>(12);
        using var cell = new UI.TextCell<int>(binding, false);
        var rejection = new InvalidOperationException("Rejected");
        binding.Write = _ => throw rejection;
        cell.BeginEdit();
        cell.Text = "18";
        for (var attempt = 1; attempt <= 2; ++attempt)
        {
            Assert.Same(rejection, Assert.Throws<InvalidOperationException>(cell.EndEdit));
            Assert.Equal(attempt, binding.Writes);
            Assert.Equal(12, cell.Value);
            Assert.Equal("18", cell.Text);
        }
        binding.Write = null;
        cell.EndEdit();
        Assert.Equal(3, binding.Writes);
        Assert.Equal(18, cell.Value);
        Assert.Equal("18", cell.Text);
        cell.EndEdit();
        Assert.Equal(3, binding.Writes);
    }

    [Fact]
    public void Cancelling_a_failed_edit_displays_the_previous_value()
    {
        var binding = new Binding<int>(12) { Write = _ => throw new InvalidOperationException() };
        using var cell = new UI.TextCell<int>(binding, false);
        cell.BeginEdit();
        cell.Text = "18";
        Assert.Throws<InvalidOperationException>(cell.EndEdit);
        cell.CancelEdit();
        Assert.Equal(12, cell.Value);
        Assert.Equal("12", cell.Text);
        cell.EndEdit();
        Assert.Equal(1, binding.Writes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public void Checkbox_rejects_and_retries_without_caching_the_rejected_state(bool? proposed)
    {
        var binding = new Binding<bool?>(false) { Write = _ => throw new InvalidOperationException() };
        using var cell = new UI.CheckBoxCell(binding, false, true);
        for (var attempt = 1; attempt <= 2; ++attempt)
        {
            Assert.Throws<InvalidOperationException>(() => cell.Value = proposed);
            Assert.False(cell.Value);
            Assert.Equal(attempt, binding.Writes);
        }
        binding.Write = null;
        cell.Value = proposed;
        Assert.Equal(proposed, cell.Value);
        Assert.Equal(3, binding.Writes);
    }

    [Fact]
    public void Failed_text_writer_does_not_rollback_a_newer_source_value_and_equal_retry_is_not_lost()
    {
        var binding = new Binding<int>(12);
        using var cell = new UI.TextCell<int>(binding, false);
        binding.Write = _ => { binding.Push(20); throw new InvalidOperationException(); };
        Assert.Throws<InvalidOperationException>(() => cell.Value = 18);
        Assert.Equal(20, cell.Value);
        binding.Write = null;
        cell.Value = 20;
        Assert.Equal(2, binding.Writes);
        cell.Value = 20;
        Assert.Equal(2, binding.Writes);
    }

    [Fact]
    public void Failed_checkbox_writer_does_not_rollback_source_normalization()
    {
        var binding = new Binding<bool?>(false);
        using var cell = new UI.CheckBoxCell(binding, false, true);
        binding.Write = _ => { binding.Push(null); throw new InvalidOperationException(); };
        Assert.Throws<InvalidOperationException>(() => cell.Value = true);
        Assert.Null(cell.Value);
        binding.Write = null;
        cell.Value = null;
        Assert.Equal(2, binding.Writes);
        cell.Value = null;
        Assert.Equal(2, binding.Writes);
    }

    [Fact]
    public void Source_update_inside_value_notification_supersedes_the_pending_write()
    {
        var binding = new Binding<int>(12);
        using var cell = new UI.TextCell<int>(binding, false);
        cell.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(cell.Value) && cell.Value == 18) binding.Push(31);
        };
        cell.Value = 18;
        Assert.Equal(31, cell.Value);
        Assert.Equal(0, binding.Writes);
    }

    [Fact]
    public void Nested_text_assignment_writes_only_the_current_proposal()
    {
        var binding = new Binding<int>(12);
        using var cell = new UI.TextCell<int>(binding, false);
        cell.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(cell.Text) && cell.Value == 18) cell.Value = 31;
        };
        cell.Value = 18;
        Assert.Equal(31, cell.Value);
        Assert.Equal(1, binding.Writes);
        Assert.Equal(31, binding.LastWrite);
    }

    [Fact]
    public void Checkbox_notification_cannot_write_a_superseded_proposal()
    {
        var binding = new Binding<bool?>(false);
        using var cell = new UI.CheckBoxCell(binding, false, true);
        cell.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(cell.Value) && cell.Value == true) binding.Push(null);
        };
        cell.Value = true;
        Assert.Null(cell.Value);
        Assert.Equal(0, binding.Writes);
    }

    [Fact]
    public void Disposal_inside_value_notification_stops_further_notifications_and_writeback()
    {
        var binding = new Binding<int>(12);
        using var cell = new UI.TextCell<int>(binding, false);
        var names = new List<string?>();
        cell.PropertyChanged += (_, args) => { names.Add(args.PropertyName); cell.Dispose(); };
        cell.Value = 18;
        Assert.Equal(new[] { "Value" }, names);
        Assert.Equal(0, binding.Writes);
        Assert.Equal(0, binding.Subscribers);
    }

    [Fact]
    public void Disposed_writer_failure_cannot_resurrect_a_pending_edit()
    {
        var binding = new Binding<int>(12);
        using var cell = new UI.TextCell<int>(binding, false);
        binding.Write = _ => { cell.Dispose(); throw new InvalidOperationException(); };
        cell.BeginEdit();
        cell.Text = "018";
        Assert.Throws<InvalidOperationException>(cell.EndEdit);
        Assert.Equal("18", cell.Text); // No resurrected "018" edit buffer.
        Assert.Equal(0, binding.Subscribers);
        Assert.Throws<ObjectDisposedException>(cell.BeginEdit);
        Assert.Throws<ObjectDisposedException>(() => cell.Text = "20");
        cell.EndEdit();
        Assert.Equal(1, binding.Writes);
    }

    [Fact]
    public void Value_and_text_notifications_still_precede_writeback()
    {
        var binding = new Binding<int>(12);
        using var cell = new UI.TextCell<int>(binding, false);
        var events = new List<string?>();
        binding.Write = _ => events.Add("Write");
        cell.PropertyChanged += (_, args) => events.Add(args.PropertyName);
        cell.Value = 18;
        Assert.Equal(new[] { "Value", "Text", "Write" }, events);
    }

    [Fact]
    public void Rollback_callback_failure_preserves_both_errors()
    {
        var binding = new Binding<int>(12);
        using var cell = new UI.TextCell<int>(binding, false);
        var writer = new InvalidOperationException("Writer");
        var rollback = new ArgumentException("Rollback");
        binding.Write = _ => throw writer;
        cell.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(cell.Value) && cell.Value == 12) throw rollback;
        };
        var error = Assert.Throws<AggregateException>(() => cell.Value = 18);
        Assert.Equal(new Exception[] { writer, rollback }, error.InnerExceptions);
        Assert.Equal(12, cell.Value);
    }

    [Fact]
    public void Successful_nested_write_is_not_marked_failed_by_the_retired_writer()
    {
        var binding = new Binding<int>(12);
        using var cell = new UI.TextCell<int>(binding, false);
        binding.Write = value =>
        {
            if (value == 18) { cell.Value = 31; throw new InvalidOperationException(); }
            binding.Push(value);
        };
        Assert.Throws<InvalidOperationException>(() => cell.Value = 18);
        Assert.Equal(31, cell.Value);
        Assert.Equal(2, binding.Writes);
        cell.Value = 31;
        Assert.Equal(2, binding.Writes);
    }

    private sealed class Binding<T>(T initial) : IObservable<T>, IObserver<T>
    {
        private readonly List<IObserver<T>> _observers = new();
        private T _value = initial;
        public Action<T>? Write;
        public T? LastWrite { get; private set; }
        public int Writes { get; private set; }
        public int Subscribers => _observers.Count;
        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observers.Add(observer);
            observer.OnNext(_value);
            return new Subscription(() => _observers.Remove(observer));
        }
        public void Push(T value)
        {
            _value = value;
            foreach (var observer in _observers.ToArray()) observer.OnNext(value);
        }
        public void OnNext(T value)
        {
            ++Writes;
            LastWrite = value;
            if (Write is { } write) write(value); else Push(value);
        }
        public void OnError(Exception error)
        {
            foreach (var observer in _observers.ToArray()) observer.OnError(error);
        }
        public void OnCompleted() { }
    }
    private sealed class Subscription(Action action) : IDisposable
    {
        private Action? _action = action;
        public void Dispose() { var callback = _action; _action = null; callback?.Invoke(); }
    }
}
