using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public sealed class ColumnFactoryTransactionTests
{
    [Fact]
    public void Returning_factory_cannot_claim_a_newer_presentation_key()
    {
        using var fixture = new Fixture();
        var first = fixture.Source.Columns[0];
        fixture.Callback = () => first.PresentationKey = "New";
        first.PresentationKey = "Old";
        Assert.Equal(new[] { "Old", "New" }, fixture.Created.Select(x => x.Key));
        Assert.Equal(1, fixture.Created[0].Disposals);
        Assert.Same(fixture.Created[1], fixture.View.NativeColumns[0]);
        Assert.Equal(1, fixture.MaximumDepth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Callback_mutations_publish_only_the_latest_order_and_visibility(int mutation)
    {
        using var fixture = new Fixture();
        var first = fixture.Source.Columns[0];
        var added = Column("Added");
        fixture.Callback = () =>
        {
            switch (mutation)
            {
                case 0: fixture.Source.Columns.RemoveAt(0); break;
                case 1: fixture.Source.Columns[0] = added; break;
                case 2: fixture.Source.Columns.Move(0, 1); break;
                case 3: fixture.Source.Columns.Add(added); break;
                case 4: first.IsVisible = false; break;
            }
        };
        var published = new List<IColumn[]>();
        fixture.View.ColumnsChanged += (_, _) => published.Add(fixture.View.NativeColumns.Select(x => x.Model).ToArray());
        first.PresentationKey = "Old";
        var expected = fixture.Source.Columns.Where(x => x.IsVisible).Cast<IColumn>().ToArray();
        Assert.Equal(expected, Assert.Single(published));
        Assert.Equal(expected, fixture.View.NativeColumns.Select(x => x.Model));
        Assert.Equal(1, fixture.Created[0].Disposals);
        Assert.Equal(1, fixture.MaximumDepth);
        using var cell = fixture.View.RealizeCell(0, 0);
        Assert.NotNull(cell);
    }

    [Fact]
    public void Factory_retirement_releases_returning_value_without_resurrecting_columns()
    {
        using var fixture = new Fixture();
        fixture.Callback = fixture.View.Dispose;
        fixture.Source.Columns[0].PresentationKey = "Old";
        Assert.Empty(fixture.View.NativeColumns);
        Assert.Equal(1, Assert.Single(fixture.Created).Disposals);
        Assert.Throws<ObjectDisposedException>(() => fixture.View.RealizeCell(0, 0));
        Assert.Equal(2, fixture.Source.Columns.Count);
        Assert.Single(fixture.Source.Rows);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Factory_suspension_cannot_publish_a_returning_old_generation(bool resumeInside)
    {
        using var fixture = new Fixture();
        var original = fixture.View.NativeColumns[0];
        fixture.Callback = () => { fixture.View.Suspend(); if (resumeInside) fixture.View.Resume(); };
        fixture.Source.Columns[0].PresentationKey = "Old";
        Assert.Equal(1, fixture.Created[0].Disposals);
        if (!resumeInside)
        {
            Assert.Same(original, fixture.View.NativeColumns[0]);
            Assert.Throws<InvalidOperationException>(() => fixture.View.RealizeCell(0, 0));
            fixture.View.Resume();
        }
        Assert.NotSame(original, fixture.View.NativeColumns[0]);
        Assert.Same(fixture.Created[^1], fixture.View.NativeColumns[0]);
        Assert.Equal(0, fixture.Created[^1].Disposals);
        using var cell = fixture.View.RealizeCell(0, 0);
    }

    [Fact]
    public void Resume_detects_mutations_even_while_source_notifications_are_detached()
    {
        using var fixture = new Fixture();
        fixture.View.Suspend();
        fixture.Source.Columns[0].PresentationKey = "Old";
        fixture.Callback = () => fixture.Source.Columns[0].PresentationKey = "New";
        fixture.View.Resume();
        Assert.Equal(new[] { "Old", "New" }, fixture.Created.Select(x => x.Key));
        Assert.Equal(1, fixture.Created[0].Disposals);
        Assert.Same(fixture.Created[1], fixture.View.NativeColumns[0]);
    }

    [Fact]
    public void Old_pool_cleanup_cannot_overwrite_a_newer_factory_choice()
    {
        using var fixture = new Fixture();
        fixture.Source.Columns[0].PresentationKey = "Old";
        var original = fixture.Created[0];
        var cell = (ProbeCell)fixture.View.RealizeCell(0, 0);
        fixture.View.RecycleCell(original, cell);
        cell.OnDispose = () => fixture.Source.Columns[0].PresentationKey = "New";
        fixture.Source.Columns[1].PresentationKey = "Old";
        Assert.Equal(1, cell.Disposals);
        Assert.Equal("New", ((ProbeColumn)fixture.View.NativeColumns[0]).Key);
        Assert.Equal("Old", ((ProbeColumn)fixture.View.NativeColumns[1]).Key);
        Assert.Equal(1, fixture.Created[1].Disposals);
        Assert.Equal(1, original.Disposals);
    }

    [Fact]
    public void Publication_reentry_does_not_recurse_or_announce_an_obsolete_projection()
    {
        using var fixture = new Fixture();
        var reentered = false;
        ((INotifyCollectionChanged)fixture.View.Columns).CollectionChanged += (_, _) =>
        {
            if (reentered) return;
            reentered = true;
            fixture.Source.Columns[0].PresentationKey = "New";
        };
        var events = new List<string?>();
        fixture.View.ColumnsChanged += (_, _) => events.Add((fixture.View.NativeColumns[0] as ProbeColumn)?.Key);
        fixture.Source.Columns[0].PresentationKey = "Old";
        Assert.Equal(new[] { "New" }, events);
        Assert.Equal(1, fixture.Created[0].Disposals);
        Assert.Equal(1, fixture.MaximumDepth);
    }

    [Fact]
    public void Retired_column_cleanup_may_request_a_newer_projection()
    {
        using var fixture = new Fixture();
        fixture.Source.Columns[0].PresentationKey = "Old";
        fixture.Created[0].OnDispose = () => fixture.Source.Columns[0].PresentationKey = "Old";
        fixture.Source.Columns[0].PresentationKey = "New";
        Assert.Equal("Old", ((ProbeColumn)fixture.View.NativeColumns[0]).Key);
        Assert.Equal(1, fixture.Created[0].Disposals);
        Assert.Equal(1, fixture.Created[1].Disposals);
        Assert.Equal(0, fixture.Created[2].Disposals);
    }

    [Fact]
    public void Definition_metadata_mutation_during_factory_restarts_its_snapshot()
    {
        using var fixture = new Fixture();
        fixture.Callback = () => fixture.Source.Columns[0].Width = new(237);
        fixture.Source.Columns[0].PresentationKey = "Old";
        Assert.Equal(2, fixture.Created.Count);
        Assert.Equal(1, fixture.Created[0].Disposals);
        Assert.Equal(237, fixture.Created[1].CapturedWidth);
    }

    [Fact]
    public void Failed_staged_cleanup_preserves_factory_failure_identity_and_order()
    {
        using var fixture = new Fixture();
        fixture.View.Suspend();
        fixture.Source.Columns[0].PresentationKey = "Old";
        fixture.Source.Columns[1].PresentationKey = "New";
        var primary = new InvalidOperationException("second factory");
        var cleanup = new InvalidOperationException("first staged cleanup");
        fixture.Options.Columns["Old"] = model =>
        {
            var created = new ProbeColumn(model, "Old") { OnDispose = () => throw cleanup };
            fixture.Created.Add(created);
            return created;
        };
        fixture.Options.Columns["New"] = _ => throw primary;
        var error = Assert.Throws<AggregateException>(fixture.View.Resume);
        Assert.Collection(error.InnerExceptions, x => Assert.Same(primary, x), x => Assert.Same(cleanup, x));
        Assert.Equal(1, fixture.Created[0].Disposals);
        Assert.IsNotType<ProbeColumn>(fixture.View.NativeColumns[0]);
    }

    [Fact]
    public void Throwing_publication_still_notifies_layout_and_releases_retired_views()
    {
        using var fixture = new Fixture();
        fixture.Source.Columns[0].PresentationKey = "Old";
        var primary = new InvalidOperationException("collection observer");
        var cleanup = new InvalidOperationException("retired view");
        NotifyCollectionChangedEventHandler observer = (_, _) => throw primary;
        ((INotifyCollectionChanged)fixture.View.Columns).CollectionChanged += observer;
        fixture.Created[0].OnDispose = () => throw cleanup;
        var events = 0;
        fixture.View.ColumnsChanged += (_, _) => ++events;
        var error = Assert.Throws<AggregateException>(() => fixture.Source.Columns[0].PresentationKey = "New");
        Assert.Collection(error.InnerExceptions, x => Assert.Same(primary, x), x => Assert.Same(cleanup, x));
        Assert.Equal(1, events);
        Assert.Equal(1, fixture.Created[0].Disposals);
        ((INotifyCollectionChanged)fixture.View.Columns).CollectionChanged -= observer;
    }

    private static ValueColumn<Item, string> Column(string name) => new(name, item => item.Name, width: new(100));
    private sealed class Item { public string Name => "Item"; }
    private sealed class Fixture : IDisposable
    {
        internal readonly FlatTreeDataGridSource<Item> Source = new([new Item()]);
        internal readonly TreeDataGridPresentationOptions Options = new();
        internal readonly List<ProbeColumn> Created = new();
        internal readonly TreeDataGridPresentation View;
        internal Action? Callback;
        internal int Depth, MaximumDepth;
        internal Fixture()
        {
            Source.Columns.Add(Column("First")); Source.Columns.Add(Column("Second"));
            Options.Columns["Old"] = model => Create(model, "Old");
            Options.Columns["New"] = model => Create(model, "New");
            View = TreeDataGridPresentation.Create(Source, Options);
        }
        private CellColumn Create(IColumn model, string key)
        {
            ++Depth; MaximumDepth = Math.Max(MaximumDepth, Depth);
            try
            {
                var result = new ProbeColumn(model, key);
                Created.Add(result);
                var callback = Callback; Callback = null; callback?.Invoke();
                return result;
            }
            finally { --Depth; }
        }
        public void Dispose() { try { View.Dispose(); } finally { Source.Dispose(); } }
    }
    private sealed class ProbeColumn(IColumn model, string key) : CellColumn(model)
    {
        internal readonly string Key = key;
        internal readonly double CapturedWidth = model.Width.Value;
        internal int Disposals;
        internal Action? OnDispose;
        public override CellValue CreateCell(IRow row) => new ProbeCell();
        public override void Dispose() { ++Disposals; var callback = OnDispose; OnDispose = null; callback?.Invoke(); }
    }
    private sealed class ProbeCell : CellValue
    {
        internal int Disposals;
        internal Action? OnDispose;
        public override object? Value => "Item";
        public override bool CanEdit => false;
        public override void Write(object? value) => throw new NotSupportedException();
        internal override bool TrySuspend() => true;
        public override void Dispose() { ++Disposals; var callback = OnDispose; OnDispose = null; callback?.Invoke(); }
    }
}
