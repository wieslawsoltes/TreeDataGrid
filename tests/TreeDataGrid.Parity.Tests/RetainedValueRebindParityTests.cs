using System;
using System.ComponentModel;
using Xunit;
using A = Avalonia.Controls.Models.TreeDataGrid;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Parity.Tests;

public sealed class RetainedValueRebindParityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Retained_text_rebind_matches_reference_value_error_and_observation_policy(bool editable, bool failGetter)
    {
        var a = editable
            ? new A.TextColumn<Item, string?>("Text", x => x.Text, (x, value) => x.Text = value)
            : new A.TextColumn<Item, string?>("Text", x => x.Text);
        using var u = editable
            ? new U.TextColumn<Item, string?>("Text", x => x.Text, (x, value) => x.Text = value)
            : new U.TextColumn<Item, string?>("Text", x => x.Text);
        var firstA = new Item("First");
        var firstU = new Item("First");
        var secondA = new Item("Second");
        var secondU = new Item("Second");
        if (failGetter)
        {
            secondA.Failure = new InvalidOperationException("Reference getter");
            secondU.Failure = new InvalidOperationException("Uno getter");
        }
        var av = a.CreateCell(new Row(firstA));
        var uv = u.CreateCell(new Row(firstU));
        try
        {
            Assert.True(a.TryReuseCell(av, new Row(secondA)));
            Assert.True(u.TryReuseCell(uv, new Row(secondU)));
            Assert.Equal(av.Value, uv.Value);
            Assert.Equal(failGetter ? "First" : "Second", uv.Value);
            Assert.Equal(av.CanEdit, uv.CanEdit);
            Assert.Equal(0, firstA.Subscribers);
            Assert.Equal(0, firstU.Subscribers);
            firstA.Text = firstU.Text = "Retired must not publish";
            Assert.Equal(failGetter ? "First" : "Second", uv.Value);
            secondA.Failure = secondU.Failure = null;
            secondA.Text = secondU.Text = "Recovered";
            Assert.Equal(av.Value, uv.Value);
            Assert.Equal("Recovered", uv.Value);
            if (editable)
            {
                ((A.ITextCell)av).Text = "Committed";
                ((U.ITextCell)uv).Text = "Committed";
                Assert.Equal(secondA.Text, secondU.Text);
                Assert.Equal("Committed", secondU.Text);
            }
            Assert.True(secondA.Subscribers > 0);
            Assert.True(secondU.Subscribers > 0);
        }
        finally
        {
            try { ((IDisposable)av).Dispose(); }
            finally { uv.Dispose(); }
        }
        Assert.Equal(0, secondA.Subscribers);
        Assert.Equal(0, secondU.Subscribers);
    }

    private sealed class Item(string? text) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _handlers;
        internal Exception? Failure;
        internal int Subscribers => _handlers?.GetInvocationList().Length ?? 0;
        public string? Text
        {
            get => Failure is { } error ? throw error : text;
            set { text = value; _handlers?.Invoke(this, new(nameof(Text))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged { add => _handlers += value; remove => _handlers -= value; }
    }
    private sealed class Row(Item model) : A.IRow<Item>
    {
        public Item Model => model;
        public object? Header => null;
        public Avalonia.Controls.GridLength Height { get; set; } = Avalonia.Controls.GridLength.Auto;
        public void UpdateModelIndex(int delta) { }
    }
}
