using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Xunit;
using A = Avalonia.Controls.Models.TreeDataGrid;
using AP = Avalonia.Controls.Presentation;
using U = Uno.Controls.Models.TreeDataGrid;
using UP = Uno.Controls.Presentation;

namespace TreeDataGrid.Parity.Tests;

/// <summary>Executes both actual framework libraries over the same Core models, not copied expectations.</summary>
public sealed class ColumnContractParityTests
{
    [Fact]
    public void Default_text_options_match_the_reference()
    {
        var a = new A.TextColumnOptions<Model>();
        var u = new U.TextColumnOptions<Model>();
        Assert.Equal(a.StringFormat, u.StringFormat);
        Assert.Equal(a.Culture, u.Culture);
        Assert.Equal(a.IsTextSearchEnabled, u.IsTextSearchEnabled);
        Assert.Equal(a.TextAlignment.ToString(), u.TextAlignment.ToString());
        Assert.Equal(a.TextWrapping.ToString(), u.TextWrapping.ToString());
        Assert.Equal(a.TextTrimming.ToString(), u.TextTrimming.ToString());
        Assert.Equal((int)a.BeginEditGestures, (int)u.BeginEditGestures);
        Assert.Equal(a.CanUserResizeColumn, u.CanUserResizeColumn);
        Assert.Equal(a.CanUserSortColumn, u.CanUserSortColumn);
        Assert.Equal(a.MinWidth.Value, u.MinWidth.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Original")]
    [InlineData("Zażółć 日本語 😀")]
    public void Text_and_null_format_behavior_match(string? value)
    {
        var model = new Model { Text = value };
        using var fixture = new Pair<string?>(model, row => row.Text);
        Assert.Equal(fixture.AvaloniaText.Text, fixture.UnoText.Text);
        fixture.AvaloniaOptions.StringFormat = "[{0}]";
        fixture.UnoOptions.StringFormat = "[{0}]";
        Assert.Equal(fixture.AvaloniaText.Text, fixture.UnoText.Text);
        fixture.AvaloniaOptions.StringFormat = null!;
        fixture.UnoOptions.StringFormat = null!;
        Assert.Equal(fixture.AvaloniaText.Text, fixture.UnoText.Text);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("pl-PL")]
    public void Formatting_and_search_are_distinct_reference_contracts(string cultureName)
    {
        var model = new Model { Number = 12.5m };
        using var fixture = new Pair<decimal>(model, row => row.Number);
        var culture = CultureInfo.GetCultureInfo(cultureName);
        fixture.AvaloniaOptions.StringFormat = "Amount: {0:F2}";
        fixture.UnoOptions.StringFormat = "Amount: {0:F2}";
        fixture.AvaloniaOptions.Culture = culture;
        fixture.UnoOptions.Culture = culture;
        Assert.Equal(fixture.AvaloniaText.Text, fixture.UnoText.Text);
        var a = ((A.ITextSearchableColumn<Model>)fixture.AvaloniaColumn).SelectValue(model);
        var u = ((U.ITextSearchableColumn<Model>)fixture.UnoColumn).SelectValue(model);
        Assert.Equal(model.Number.ToString(), a);
        Assert.Equal(a, u);
    }

    [Fact]
    public void Existing_cells_observe_mutated_text_style_options()
    {
        using var fixture = new Pair<string?>(new() { Text = "text" }, row => row.Text);
        fixture.AvaloniaOptions.TextAlignment = Avalonia.Media.TextAlignment.Right;
        fixture.UnoOptions.TextAlignment = Microsoft.UI.Xaml.TextAlignment.Right;
        fixture.AvaloniaOptions.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        fixture.UnoOptions.TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap;
        fixture.AvaloniaOptions.TextTrimming = Avalonia.Media.TextTrimming.WordEllipsis;
        fixture.UnoOptions.TextTrimming = Microsoft.UI.Xaml.TextTrimming.WordEllipsis;
        Assert.Equal(fixture.AvaloniaText.TextAlignment.ToString(), fixture.UnoText.TextAlignment.ToString());
        Assert.Equal(fixture.AvaloniaText.TextWrapping.ToString(), fixture.UnoText.TextWrapping.ToString());
        Assert.Equal(fixture.AvaloniaText.TextTrimming.ToString(), fixture.UnoText.TextTrimming.ToString());
    }

    [Fact]
    public void Both_bindings_observe_and_write_the_exact_shared_model()
    {
        var model = new Model { Text = "initial" };
        using var fixture = new Pair<string?>(model, row => row.Text, (row, value) => row.Text = value);
        model.Text = "external";
        Assert.Equal("external", fixture.AvaloniaText.Text);
        Assert.Equal(fixture.AvaloniaText.Text, fixture.UnoText.Text);
        fixture.AvaloniaText.Text = "from Avalonia";
        Assert.Equal("from Avalonia", model.Text);
        Assert.Equal(fixture.AvaloniaText.Text, fixture.UnoText.Text);
        fixture.UnoText.Text = "from Uno";
        Assert.Equal("from Uno", model.Text);
        Assert.Equal(fixture.AvaloniaText.Text, fixture.UnoText.Text);
        Assert.Same(model, fixture.Source.Rows[0].Model);
    }

    [Fact]
    public void Retained_cell_retargeting_updates_both_frameworks()
    {
        var first = new Model { Text = "first" };
        var next = new Model { Text = "second" };
        using var fixture = new Pair<string?>(first, row => row.Text);
        using var source = new FlatTreeDataGridSource<Model>([next]);
        var row = (IRow<Model>)source.Rows[0];
        Assert.True(((AP.ICellColumn<Model>)fixture.AvaloniaColumn).TryReuseCell(fixture.AvaloniaCell, row));
        Assert.True(((UP.ICellColumn<Model>)fixture.UnoColumn).TryReuseCell(fixture.UnoCell, row));
        first.Text = "retired";
        Assert.Equal("second", fixture.AvaloniaText.Text);
        Assert.Equal(fixture.AvaloniaText.Text, fixture.UnoText.Text);
        next.Text = "current";
        Assert.Equal("current", fixture.AvaloniaText.Text);
        Assert.Equal(fixture.AvaloniaText.Text, fixture.UnoText.Text);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Pixel_auto_and_star_layout_match_after_measurement(int kind)
    {
        var a = new A.ColumnList<Model>();
        var u = new U.ColumnList<Model>();
        for (var index = 0; index < 3; ++index)
        {
            var aw = kind == 0 ? new Avalonia.Controls.GridLength(60 + index * 20) :
                kind == 1 ? Avalonia.Controls.GridLength.Auto : new Avalonia.Controls.GridLength(index + 1, Avalonia.Controls.GridUnitType.Star);
            var uw = kind == 0 ? new Microsoft.UI.Xaml.GridLength(60 + index * 20) :
                kind == 1 ? Microsoft.UI.Xaml.GridLength.Auto : new Microsoft.UI.Xaml.GridLength(index + 1, Microsoft.UI.Xaml.GridUnitType.Star);
            a.Add(new A.TextColumn<Model, string?>("Text", row => row.Text, width: aw));
            u.Add(new U.TextColumn<Model, string?>("Text", row => row.Text, width: uw));
        }
        try
        {
            A.IColumns al = a;
            U.IColumns ul = u;
            al.ViewportChanged(new Avalonia.Rect(0, 0, 600, 100));
            ul.ViewportChanged(new Windows.Foundation.Rect(0, 0, 600, 100));
            for (var index = 0; index < 3; ++index)
            {
                al.CellMeasured(index, 0, new Avalonia.Size(40 + index * 15, 20));
                ul.CellMeasured(index, 0, new Windows.Foundation.Size(40 + index * 15, 20));
            }
            al.CommitActualWidths();
            ul.CommitActualWidths();
            Assert.Equal(al.GetEstimatedWidth(600), ul.GetEstimatedWidth(600), precision: 9);
            var offset = 0d;
            for (var index = 0; index < 3; ++index)
            {
                Assert.Equal(al[index].ActualWidth, ul[index].ActualWidth, precision: 9);
                Assert.Equal(al.GetColumnAt(offset), ul.GetColumnAt(offset));
                offset += al[index].ActualWidth;
                Assert.Equal(al.GetColumnAt(Math.BitDecrement(offset)), ul.GetColumnAt(Math.BitDecrement(offset)));
            }
            Assert.Equal(al.GetColumnAt(offset), ul.GetColumnAt(offset));
        }
        finally
        {
            foreach (var column in u) (column as IDisposable)?.Dispose();
            a.Clear(); u.Clear();
        }
    }

    private sealed class Pair<TValue> : IDisposable
    {
        internal readonly FlatTreeDataGridSource<Model> Source;
        internal readonly A.TextColumnOptions<Model> AvaloniaOptions = new();
        internal readonly U.TextColumnOptions<Model> UnoOptions = new();
        internal readonly A.TextColumn<Model, TValue> AvaloniaColumn;
        internal readonly U.TextColumn<Model, TValue> UnoColumn;
        internal readonly A.ICell AvaloniaCell;
        internal readonly U.ICell UnoCell;
        internal A.ITextCell AvaloniaText => (A.ITextCell)AvaloniaCell;
        internal U.ITextCell UnoText => (U.ITextCell)UnoCell;
        internal Pair(Model model, Expression<Func<Model, TValue?>> getter, Action<Model, TValue?>? setter = null)
        {
            Source = new([model]);
            AvaloniaColumn = setter is null ? new("Value", getter, options: AvaloniaOptions) : new("Value", getter, setter, options: AvaloniaOptions);
            UnoColumn = setter is null ? new("Value", getter, options: UnoOptions) : new("Value", getter, setter, options: UnoOptions);
            var row = (IRow<Model>)Source.Rows[0];
            AvaloniaCell = ((AP.ICellColumn<Model>)AvaloniaColumn).CreateCell(row);
            UnoCell = ((UP.ICellColumn<Model>)UnoColumn).CreateCell(row);
        }
        public void Dispose()
        {
            try { (AvaloniaCell as IDisposable)?.Dispose(); }
            finally
            {
                try { (UnoCell as IDisposable)?.Dispose(); }
                finally { UnoColumn.Dispose(); Source.Dispose(); }
            }
        }
    }
    private sealed class Model : INotifyPropertyChanged
    {
        private string? _text;
        private decimal _number;
        public string? Text { get => _text; set { _text = value; PropertyChanged?.Invoke(this, new(nameof(Text))); } }
        public decimal Number { get => _number; set { _number = value; PropertyChangedChangedNumber(); } }
        private void PropertyChangedChangedNumber() => PropertyChanged?.Invoke(this, new(nameof(Number)));
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
