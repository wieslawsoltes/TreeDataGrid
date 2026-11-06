using System;
using System.ComponentModel;
using System.Globalization;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Data;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class TypedCellAllocationAndReentrancyTests
{
    [Fact]
    public void Warm_text_write_notification_and_identity_formatting_path_allocates_nothing()
    {
        var source = new ScalarSource<string>("Even");
        using var cell = new TextCell<string>(source, false,
            new TextColumnOptions<object> { Culture = CultureInfo.InvariantCulture });
        var notifications = 0;
        var mismatches = 0;
        PropertyChangedEventArgs? valueArgs = null, textArgs = null, errorArgs = null;
        cell.PropertyChanged += (_, args) =>
        {
            ++notifications;
            switch (args.PropertyName)
            {
                case "Value": valueArgs ??= args; if (!ReferenceEquals(valueArgs, args)) ++mismatches; break;
                case "Text": textArgs ??= args; if (!ReferenceEquals(textArgs, args)) ++mismatches; break;
                case "Error": errorArgs ??= args; if (!ReferenceEquals(errorArgs, args)) ++mismatches; break;
                default: ++mismatches; break;
            }
        };
        for (var i = 0; i < 1024; ++i) { cell.Value = (i & 1) == 0 ? "Even" : "Odd"; _ = cell.Text; }
        notifications = 0;
        source.Writes = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i)
        {
            cell.Value = (i & 1) == 0 ? "Even" : "Odd";
            if (!ReferenceEquals(cell.Text, cell.Value)) ++mismatches;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(4096, source.Writes);
        Assert.Equal(12288, notifications);
        Assert.Equal(0, mismatches);
    }

    [Fact]
    public void Warm_checkbox_write_notifications_allocate_nothing()
    {
        var source = new ScalarSource<bool?>(false);
        using var cell = new CheckBoxCell(source, false, true);
        var notifications = 0;
        cell.PropertyChanged += (_, _) => ++notifications;
        for (var i = 0; i < 1024; ++i) cell.Value = (i & 1) == 0;
        notifications = 0;
        source.Writes = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i) cell.Value = (i & 1) == 0;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(4096, source.Writes);
        Assert.Equal(8192, notifications);
    }

    [Fact]
    public void Identity_format_still_invokes_a_custom_culture_formatter()
    {
        var source = new ScalarSource<string>("Value");
        var culture = new CustomCulture();
        var options = new TextColumnOptions<object> { Culture = culture };
        using var cell = new TextCell<string>(source, true, options);
        Assert.Equal("Custom:Value", cell.Text);
        Assert.Equal(1, culture.Calls);
        options.Culture = CultureInfo.InvariantCulture;
        options.StringFormat = "[{0,8}]";
        Assert.Equal(string.Format(CultureInfo.InvariantCulture, "[{0,8}]", "Value"), cell.Text);
        options.StringFormat = "{1}";
        Assert.Throws<FormatException>(() => cell.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_local_assignment_supersedes_an_inflight_fallback_diagnostic(bool checkbox)
    {
        if (checkbox)
        {
            var source = new ScalarSource<bool?>(false);
            using var cell = new CheckBoxCell(source, true, true);
            cell.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == "Value" && cell.Value == true) cell.Value = null;
            };
            source.Publish(BindingValue<bool?>.BindingError(new Exception("Stale"), true));
            Assert.Null(cell.Value);
            Assert.Null(cell.Error);
        }
        else
        {
            var source = new ScalarSource<string>("Initial");
            using var cell = new TextCell<string>(source, true);
            cell.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == "Value" && cell.Value == "Fallback") cell.Value = "Newer";
            };
            source.Publish(BindingValue<string>.BindingError(new Exception("Stale"), "Fallback"));
            Assert.Equal("Newer", cell.Value);
            Assert.Null(cell.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_nested_source_error_supersedes_an_inflight_fallback_error(bool checkbox)
    {
        var newer = new InvalidOperationException("Newer source failure");
        if (checkbox)
        {
            var source = new ScalarSource<bool?>(false);
            using var cell = new CheckBoxCell(source, true, true);
            cell.PropertyChanged += (_, args) => { if (args.PropertyName == "Value") source.OnError(newer); };
            source.Publish(BindingValue<bool?>.BindingError(new Exception("Stale"), true));
            Assert.True(cell.Value);
            Assert.Same(newer, cell.Error);
        }
        else
        {
            var source = new ScalarSource<string>("Initial");
            using var cell = new TextCell<string>(source, true);
            cell.PropertyChanged += (_, args) => { if (args.PropertyName == "Value") source.OnError(newer); };
            source.Publish(BindingValue<string>.BindingError(new Exception("Stale"), "Fallback"));
            Assert.Equal("Fallback", cell.Value);
            Assert.Same(newer, cell.Error);
        }
    }

    private sealed class ScalarSource<T>(T initial) : IObservable<BindingValue<T>>, IObserver<BindingValue<T>>
    {
        private IObserver<BindingValue<T>>? _observer;
        private BindingValue<T> _current = initial;
        public int Writes;
        public IDisposable Subscribe(IObserver<BindingValue<T>> observer)
        {
            if (_observer is not null) throw new InvalidOperationException("Single-observer fixture");
            _observer = observer;
            observer.OnNext(_current);
            return new Subscription(this);
        }
        public void Publish(BindingValue<T> value) { _current = value; _observer?.OnNext(value); }
        public void OnNext(BindingValue<T> value) { ++Writes; Publish(value); }
        public void OnError(Exception error) => _observer?.OnError(error);
        public void OnCompleted() { }
        private sealed class Subscription(ScalarSource<T> source) : IDisposable
        {
            public void Dispose() => source._observer = null;
        }
    }
    private sealed class CustomCulture() : CultureInfo("en-US"), ICustomFormatter
    {
        public int Calls;
        public override object? GetFormat(Type? type) => type == typeof(ICustomFormatter) ? this : base.GetFormat(type);
        public string Format(string? format, object? value, IFormatProvider? provider) { ++Calls; return "Custom:" + value; }
    }
}
