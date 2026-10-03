using System;
using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Xunit;
using U = global::Uno.Controls.Models.TreeDataGrid;
using P = global::Uno.Controls.Presentation;

namespace TreeDataGrid.Uno.Tests;

public sealed class CustomCellTextOptionsTests
{
    [Fact]
    public void Existing_adapter_consumes_current_public_cell_options_without_retargeting()
    {
        var options = new U.TextColumnOptions<object> { Culture = CultureInfo.InvariantCulture };
        var source = new ScalarSource<string>("Current");
        using var cell = new U.TextCell<string>(source, false, options);
        using var adapter = P.CellColumnAdapter<object>.Adapt(cell, ownsModel: false);
        var old = adapter.TextOptions!;
        options.TextAlignment = TextAlignment.Right;
        options.TextWrapping = TextWrapping.Wrap;
        options.TextTrimming = TextTrimming.WordEllipsis;
        options.Culture = CultureInfo.GetCultureInfo("fr-FR");
        options.BeginEditGestures = U.BeginEditGestures.None;
        options.StringFormat = "[{0}]";
        var current = adapter.TextOptions!;
        Assert.NotSame(old, current);
        Assert.Equal(TextAlignment.Left, old.TextAlignment);
        Assert.Equal(cell.TextAlignment, current.TextAlignment);
        Assert.Equal(cell.TextWrapping, current.TextWrapping);
        Assert.Equal(cell.TextTrimming, current.TextTrimming);
        Assert.Same(options.Culture, current.Culture);
        Assert.Equal(cell.EditGestures, adapter.EditGestures);
        Assert.Equal(U.BeginEditGestures.None, adapter.EditGestures);
        Assert.Equal("[Current]", adapter.DisplayText);
        Assert.Same(cell, adapter.PresentationModel);
        Assert.Equal(1, source.Subscribers);
        Assert.Same(current, adapter.TextOptions);
    }

    [Fact]
    public void Culture_identity_does_not_invoke_application_equality()
    {
        var first = new HostileCulture();
        var second = new HostileCulture();
        var options = new U.TextColumnOptions<object> { Culture = first };
        using var cell = new U.TextCell<string>(new ScalarSource<string>("Text"), true, options);
        using var adapter = P.CellColumnAdapter<object>.Adapt(cell, false);
        var old = adapter.TextOptions!;
        options.Culture = second;
        var current = adapter.TextOptions!;
        Assert.NotSame(old, current);
        Assert.Same(second, current.Culture);
        Assert.Same(current, adapter.TextOptions);
    }

    [Fact]
    public void Metadata_queries_do_not_fabricate_notifications_or_new_subscriptions()
    {
        var options = new U.TextColumnOptions<object>();
        var source = new ScalarSource<string>("Before");
        using var cell = new U.TextCell<string>(source, true, options);
        using var adapter = P.CellColumnAdapter<object>.Adapt(cell, false);
        var notifications = 0;
        adapter.PropertyChanged += (_, _) => ++notifications;
        options.TextAlignment = TextAlignment.Center;
        _ = adapter.TextOptions;
        _ = adapter.EditGestures;
        Assert.Equal(0, notifications);
        Assert.Equal(1, source.Subscribers);
        source.Publish("After");
        Assert.Equal("After", adapter.DisplayText);
        Assert.Equal(3, notifications);
    }

    [Fact]
    public void Warm_metadata_and_identity_text_queries_allocate_nothing()
    {
        var options = new U.TextColumnOptions<object> { Culture = CultureInfo.InvariantCulture };
        using var cell = new U.TextCell<string>(new ScalarSource<string>("Stable"), true, options);
        using var adapter = P.CellColumnAdapter<object>.Adapt(cell, false);
        var snapshot = adapter.TextOptions;
        for (var i = 0; i < 1024; ++i) { _ = adapter.TextOptions; _ = adapter.EditGestures; _ = adapter.DisplayText; }
        var mismatches = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 4096; ++i)
        {
            if (!ReferenceEquals(snapshot, adapter.TextOptions)) ++mismatches;
            if (adapter.EditGestures != options.BeginEditGestures) ++mismatches;
            if (!ReferenceEquals("Stable", adapter.DisplayText)) ++mismatches;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
        Assert.Equal(0, mismatches);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retiring_adapter_preserves_owned_and_borrowed_cell_contracts(bool ownsModel)
    {
        var source = new ScalarSource<string>("Value");
        using var cell = new U.TextCell<string>(source, true);
        using var adapter = P.CellColumnAdapter<object>.Adapt(cell, ownsModel);
        adapter.Dispose();
        Assert.Null(adapter.TextOptions);
        Assert.Equal(U.BeginEditGestures.None, adapter.EditGestures);
        Assert.Equal(ownsModel ? 0 : 1, source.Subscribers);
        Assert.Equal(0, source.DisposeCalls);
    }

    [Fact]
    public void A_nested_metadata_query_wins_over_the_older_partial_snapshot()
    {
        var cell = new MetadataCell();
        using var adapter = P.CellColumnAdapter<object>.Adapt(cell, true);
        cell.OnWrapping = () =>
        {
            cell.Alignment = TextAlignment.Right;
            cell.Wrapping = TextWrapping.Wrap;
            _ = adapter.TextOptions;
        };
        var current = adapter.TextOptions!;
        Assert.Equal(TextAlignment.Right, current.TextAlignment);
        Assert.Equal(TextWrapping.Wrap, current.TextWrapping);
        Assert.Same(current, adapter.TextOptions);
    }

    [Fact]
    public void Retirement_from_a_metadata_getter_cannot_publish_an_old_snapshot()
    {
        var cell = new MetadataCell();
        using var adapter = P.CellColumnAdapter<object>.Adapt(cell, true);
        cell.OnWrapping = adapter.Dispose;
        cell.TrimmingReads = 0;
        Assert.Null(adapter.TextOptions);
        Assert.Equal(0, cell.TrimmingReads);
        Assert.Equal(1, cell.DisposeCalls);
        Assert.Null(adapter.TextOptions);
    }

    [Fact]
    public void Each_public_style_getter_is_evaluated_once_per_snapshot_query()
    {
        var cell = new MetadataCell();
        using var adapter = P.CellColumnAdapter<object>.Adapt(cell, true);
        cell.Alignment = TextAlignment.Right;
        cell.AlignmentReads = cell.WrappingReads = cell.TrimmingReads = 0;
        Assert.Equal(TextAlignment.Right, adapter.TextOptions!.TextAlignment);
        Assert.Equal(1, cell.AlignmentReads);
        Assert.Equal(1, cell.WrappingReads);
        Assert.Equal(1, cell.TrimmingReads);
    }

    private sealed class MetadataCell : U.ITextCell, INotifyPropertyChanged, IDisposable
    {
        internal TextAlignment Alignment;
        internal TextWrapping Wrapping;
        internal Action? OnWrapping;
        internal int AlignmentReads, WrappingReads, TrimmingReads, DisposeCalls;
        public object? Value => Text;
        public bool CanEdit => true;
        public U.BeginEditGestures EditGestures => U.BeginEditGestures.Default;
        public string? Text { get; set; } = "Value";
        public TextAlignment TextAlignment { get { ++AlignmentReads; return Alignment; } }
        public TextWrapping TextWrapping
        {
            get { ++WrappingReads; var callback = OnWrapping; OnWrapping = null; callback?.Invoke(); return Wrapping; }
        }
        public TextTrimming TextTrimming { get { ++TrimmingReads; return TextTrimming.CharacterEllipsis; } }
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public void Dispose() => ++DisposeCalls;
    }
    private sealed class HostileCulture() : CultureInfo("en-US")
    {
        public override bool Equals(object? value) => throw new InvalidOperationException("Do not call culture equality from metadata queries.");
        public override int GetHashCode() => throw new InvalidOperationException("Do not hash application cultures.");
    }
    private sealed class ScalarSource<T>(T value) : IObservable<T>, IObserver<T>, IDisposable
    {
        private IObserver<T>? _observer;
        internal int Subscribers, DisposeCalls;
        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observer = observer;
            ++Subscribers;
            observer.OnNext(value);
            return new Token(this);
        }
        internal void Publish(T next) { value = next; _observer?.OnNext(next); }
        public void OnNext(T next) => Publish(next);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
        public void Dispose() => ++DisposeCalls;
        private sealed class Token(ScalarSource<T> source) : IDisposable
        {
            private ScalarSource<T>? _source = source;
            public void Dispose()
            {
                if (_source is not { } current) return;
                _source = null;
                --current.Subscribers;
                current._observer = null;
            }
        }
    }
}
