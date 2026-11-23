using System;
using System.Collections.Generic;
using System.ComponentModel;
using TreeDataGridCore.Models;
using Xunit;
using global::Uno.Controls.Presentation;

namespace TreeDataGrid.Uno.Tests;

public sealed class CellBindingFailureContractTests
{
    [Fact]
    public void Replacing_same_type_error_notifies_the_public_bound_cell()
    {
        var first = new InvalidOperationException("First diagnostic.");
        var second = new InvalidOperationException("Replacement diagnostic.");
        var model = new Probe { Text = null, Failure = first };
        using var cell = new BoundCell<Probe, string?>(CreateColumn(), new RowAdapter(model), canPool: true);
        Assert.Same(first, cell.Error);
        var names = new List<string?>();
        var publishedErrors = new List<Exception?>();
        cell.PropertyChanged += (_, args) => { names.Add(args.PropertyName); publishedErrors.Add(cell.Error); };
        model.Failure = second;
        model.Notify();
        Assert.Equal(new[] { "Value", "Error" }, names);
        Assert.All(publishedErrors, error => Assert.Same(second, error));
        Assert.Same(second, cell.Error);
        names.Clear();
        publishedErrors.Clear();
        model.Notify();
        Assert.Empty(names);
        Assert.Empty(publishedErrors);
        model.Failure = null;
        model.Notify();
        Assert.Equal(new[] { "Value", "Error" }, names);
        Assert.All(publishedErrors, error => Assert.Null(error));
        Assert.Null(cell.Error);
        Assert.Null(cell.Value);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Write_and_refresh_failures_preserve_identity_and_order(bool failWrite, bool failNotification)
    {
        var writeFailure = new InvalidOperationException("Setter failed.");
        var notificationFailure = new InvalidOperationException("Observer failed.");
        var model = new Probe { Text = "Before" };
        var armed = false;
        var column = CreateColumn((row, value) =>
        {
            row.Text = value;
            if (failWrite) throw writeFailure;
        });
        using var binding = new CellBinding<Probe, string?>(column, () =>
        {
            if (armed && failNotification) throw notificationFailure;
        });
        binding.Retarget(model);
        armed = true;
        var actual = Record.Exception(() => binding.Write("After"));
        if (failWrite && failNotification)
        {
            var aggregate = Assert.IsType<AggregateException>(actual);
            Assert.Collection(aggregate.InnerExceptions,
                error => Assert.Same(writeFailure, error), error => Assert.Same(notificationFailure, error));
        }
        else if (failWrite) Assert.Same(writeFailure, actual);
        else if (failNotification) Assert.Same(notificationFailure, actual);
        else Assert.Null(actual);
        Assert.Equal("After", model.Text);
        Assert.Equal("After", binding.Value);
        Assert.Null(binding.Error);
        failWrite = false;
        failNotification = false;
        binding.Write("Recovered");
        Assert.Equal("Recovered", model.Text);
        Assert.Equal("Recovered", binding.Value);
    }

    [Fact]
    public void Error_change_detection_does_not_invoke_exception_callbacks()
    {
        var first = new UninspectableException();
        var second = new UninspectableException();
        var model = new Probe { Failure = first };
        var notifications = 0;
        using var binding = new CellBinding<Probe, string?>(CreateColumn(), () => ++notifications);
        binding.Retarget(model);
        Assert.Same(first, binding.Error);
        model.Failure = second;
        model.Notify();
        Assert.Equal(2, notifications);
        Assert.Same(second, binding.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retired_getter_failure_is_not_published(bool dispose)
    {
        var model = new Probe { Failure = new InvalidOperationException("Obsolete getter failure.") };
        var notifications = 0;
        using var binding = new CellBinding<Probe, string?>(CreateColumn(), () => ++notifications);
        model.BeforeRead = () => { if (dispose) binding.Dispose(); else binding.Suspend(); };
        binding.Retarget(model);
        Assert.Null(binding.Value);
        Assert.Null(binding.Error);
        Assert.Equal(0, notifications);
        Assert.Equal(0, model.SubscriberCount);
        if (dispose) Assert.Throws<ObjectDisposedException>(() => binding.Retarget(model));
        else
        {
            model.Failure = null;
            model.Text = "Current";
            binding.Retarget(model);
            Assert.Equal("Current", binding.Value);
            Assert.Null(binding.Error);
            Assert.Equal(1, notifications);
            Assert.Equal(1, model.SubscriberCount);
        }
    }

    [Fact]
    public void Warm_successful_writes_allocate_no_managed_storage()
    {
        var model = new Probe { Text = "Even" };
        var notifications = 0;
        using var binding = new CellBinding<Probe, string?>(
            CreateColumn(static (row, value) => row.Text = value), () => ++notifications);
        binding.Retarget(model);
        for (var iteration = 0; iteration < 1024; ++iteration)
            binding.Write((iteration & 1) == 0 ? "Even" : "Odd");
        notifications = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 4096; ++iteration)
            binding.Write((iteration & 1) == 0 ? "Even" : "Odd");
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(4096, notifications);
        Assert.Equal("Odd", binding.Value);
        Assert.Equal(1, model.SubscriberCount);
        binding.Suspend();
        Assert.Equal(0, model.SubscriberCount);
    }

    private static ValueColumn<Probe, string?> CreateColumn(Action<Probe, string?>? setter = null) =>
        ValueColumn<Probe, string?>.FromDelegate("Text", static row => row.Read(), propertyName: nameof(Probe.Text), setter: setter);

    private sealed class Probe : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs TextChanged = new(nameof(Text));
        private PropertyChangedEventHandler? _changed;
        internal string? Text { get; set; }
        internal Exception? Failure { get; set; }
        internal Action? BeforeRead { get; set; }
        internal int SubscriberCount { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _changed += value; ++SubscriberCount; }
            remove { _changed -= value; --SubscriberCount; }
        }
        internal string? Read()
        {
            var callback = BeforeRead;
            BeforeRead = null;
            callback?.Invoke();
            if (Failure is { } failure) throw failure;
            return Text;
        }
        internal void Notify() => _changed?.Invoke(this, TextChanged);
    }

    private sealed class RowAdapter(Probe model) : IRow
    {
        public object? Header => null;
        public object? Model => model;
        public TreeDataGridCore.GridLength Height { get; set; } = TreeDataGridCore.GridLength.Auto;
    }
    private sealed class UninspectableException : Exception
    {
        public override string Message => throw new InvalidOperationException("Comparison must not inspect Message.");
        public override bool Equals(object? other) => throw new InvalidOperationException("Comparison must not invoke Equals.");
        public override int GetHashCode() => throw new InvalidOperationException("Comparison must not invoke GetHashCode.");
    }
}
