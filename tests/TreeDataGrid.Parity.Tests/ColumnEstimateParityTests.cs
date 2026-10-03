using System;
using System.ComponentModel;
using Xunit;
using A = Avalonia.Controls.Models.TreeDataGrid;
using U = Uno.Controls.Models.TreeDataGrid;

namespace TreeDataGrid.Parity.Tests;

public sealed class ColumnEstimateParityTests
{
    [Theory]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1d)]
    [InlineData(0d)]
    [InlineData(10d)]
    [InlineData(100d)]
    [InlineData(500d)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    public void Stable_mixed_column_estimates_match_the_actual_reference(double constraint)
    {
        var random = new Random(71423);
        foreach (var count in new[] { 0, 1, 2, 4, 33, 129, 1024 })
        {
            var reference = new A.ColumnListBase<Column>();
            var native = new U.ColumnListBase<Column>();
            try
            {
                for (var index = 0; index < count; ++index)
                {
                    var kind = random.Next(3);
                    var actual = random.Next(5) == 0 ? double.NaN : random.Next(1000) / 8d;
                    var column = new Column(kind, actual, random.Next(40) / 4d);
                    reference.Add(column);
                    native.Add(column);
                }
                Assert.Equal(reference.GetEstimatedWidth(constraint), native.GetEstimatedWidth(constraint));
                if (count > 0)
                {
                    // An unchanged collection must not turn live custom widths
                    // into a cached estimate. No framework layout object is used.
                    reference[0].Actual = 0;
                    Assert.Equal(reference.GetEstimatedWidth(constraint), native.GetEstimatedWidth(constraint));
                    reference[0].Actual = double.NaN;
                    Assert.Equal(reference.GetEstimatedWidth(constraint), native.GetEstimatedWidth(constraint));
                    var duplicate = reference[0];
                    reference.Add(duplicate);
                    native.Add(duplicate);
                    Assert.Equal(reference.GetEstimatedWidth(constraint), native.GetEstimatedWidth(constraint));
                    reference.RemoveAt(0);
                    native.RemoveAt(0);
                    Assert.Equal(reference.GetEstimatedWidth(constraint), native.GetEstimatedWidth(constraint));
                }
            }
            finally
            {
                reference.Clear();
                native.Clear();
            }
        }
    }

    // One stable data fixture implements both *public* layout contracts. Only
    // framework GridLength values differ; both algorithms read the same inputs.
    private sealed class Column(int kind, double actual, double minimum) : A.IUpdateColumnLayout, U.IUpdateColumnLayout
    {
        internal double Actual = actual;
        public double ActualWidth => Actual;
        public double MinActualWidth => minimum;
        public double MaxActualWidth => double.PositiveInfinity;
        public bool StarWidthWasConstrained => false;
        public bool? CanUserResize => true;
        public object? Header => null;
        public ListSortDirection? SortDirection { get; set; }
        public object? Tag { get; set; }
        Avalonia.Controls.GridLength A.IColumn.Width => kind switch
        {
            0 => Avalonia.Controls.GridLength.Auto,
            1 => new(23),
            _ => new(2, Avalonia.Controls.GridUnitType.Star),
        };
        Microsoft.UI.Xaml.GridLength U.IColumn.Width => kind switch
        {
            0 => Microsoft.UI.Xaml.GridLength.Auto,
            1 => new(23),
            _ => new(2, Microsoft.UI.Xaml.GridUnitType.Star),
        };
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public double CellMeasured(double width, int rowIndex) => throw new InvalidOperationException("An estimate must not measure cells.");
        public bool CommitActualWidth() => throw new InvalidOperationException("An estimate must not commit widths.");
        public void CalculateStarWidth(double availableWidth, double totalStars) => throw new InvalidOperationException("An estimate must not solve layout.");
        void A.IUpdateColumnLayout.SetWidth(Avalonia.Controls.GridLength width) => throw new InvalidOperationException("An estimate must not change policy.");
        void U.IUpdateColumnLayout.SetWidth(Microsoft.UI.Xaml.GridLength width) => throw new InvalidOperationException("An estimate must not change policy.");
    }
}
