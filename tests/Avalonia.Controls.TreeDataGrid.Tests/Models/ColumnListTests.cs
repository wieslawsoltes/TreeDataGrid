using System;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Models
{
    public class ColumnListTests
    {
        [AvaloniaFact(Timeout = 10000)]
        public void Columns_Are_Sized_At_End_Of_Measure()
        {
            var target = new ColumnList<Model>
            {
                new TextColumn<Model, string?>(null, x => x.Name, new GridLength(100, GridUnitType.Pixel)),
                new TextColumn<Model, string?>(null, x => x.Name, GridLength.Auto),
                new TextColumn<Model, string?>(null, x => x.Name, new GridLength(1, GridUnitType.Star)),
                new TextColumn<Model, string?>(null, x => x.Name, new GridLength(3, GridUnitType.Star)),
            };

            target.ViewportChanged(new Rect(0, 0, 500, 500));

            for (var row = 0; row < 10; ++row)
            {
                for (var col = 0; col < target.Count; ++col)
                {
                    target.CellMeasured(col, row, new Size(51 + row, 10));
                }
            }

            target.CommitActualWidths();

            Assert.Equal(100, target[0].ActualWidth);
            Assert.Equal(60, target[1].ActualWidth);
            Assert.Equal(85, target[2].ActualWidth);
            Assert.Equal(255, target[3].ActualWidth);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Star_Column_Leaves_Room_For_Unmeasured_Auto_Columns()
        {
            const int viewportWidth = 300;

            var target = new ColumnList<Model>
            {
                new TextColumn<Model, string?>(null, x => x.Name, GridLength.Auto),
                new TextColumn<Model, string?>(null, x => x.Name, new GridLength(1, GridUnitType.Star)),
                new TextColumn<Model, string?>(null, x => x.Country, GridLength.Auto),
            };

            target.ViewportChanged(new Rect(0, 0, viewportWidth, viewportWidth));

            var measured = new[] { 50d, 500d, 60d };
            var widthSoFar = 0d;

            for (var col = 0; col < target.Count && widthSoFar < viewportWidth; ++col)
            {
                var size = target.CellMeasured(col, 0, new Size(measured[col], 10));
                widthSoFar += size.Width;
            }

            target.CommitActualWidths();

            Assert.Equal(50, target[0].ActualWidth);
            Assert.Equal(viewportWidth - 50 - 60, target[1].ActualWidth);
            Assert.Equal(60, target[2].ActualWidth);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Star_Column_Respects_Min_Width_Of_Unmeasured_Auto_Columns()
        {
            const int viewportWidth = 300;

            var target = new ColumnList<Model>
            {
                new TextColumn<Model, string?>(null, x => x.Name, GridLength.Auto),
                new TextColumn<Model, string?>(null, x => x.Name, new GridLength(1, GridUnitType.Star)),
                new TextColumn<Model, string?>(null, x => x.Country, GridLength.Auto),
            };

            target.ViewportChanged(new Rect(0, 0, viewportWidth, viewportWidth));

            target.CellMeasured(0, 0, new Size(50, 10));
            target.CellMeasured(1, 0, new Size(500, 10));

            target.CommitActualWidths();

            var unmeasuredAutoColumn = Assert.IsType<TextColumn<Model, string?>>(target[2]);

            Assert.Equal(50, target[0].ActualWidth);
            Assert.Equal(viewportWidth - 50 - unmeasuredAutoColumn.Options.MinWidth.Value, target[1].ActualWidth);
            Assert.Equal(unmeasuredAutoColumn.Options.MinWidth.Value, target[2].ActualWidth);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Layout_Is_Invalidated_At_End_Of_Measure_If_AutoSized_Column_Changes_Width()
        {
            var target = new ColumnList<Model>
            {
                new TextColumn<Model, string?>(null, x => x.Name, GridLength.Auto),
                new TextColumn<Model, string?>(null, x => x.Country, GridLength.Auto),
            };


            target.ViewportChanged(new Rect(0, 0, 500, 500));

            for (var row = 0; row < 10; ++row)
            {
                for (var col = 0; col < target.Count; ++col)
                {
                    target.CellMeasured(col, row, new Size(40, 10));
                }
            }

            target.CommitActualWidths();

            target.CellMeasured(0, 1, new Size(50, 10));

            var raised = 0;
            target.LayoutInvalidated += (s, e) => ++raised;

            target.CommitActualWidths();

            Assert.Equal(1, raised);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Clean_Commit_Does_Not_Suppress_Later_Measurement_And_Viewport_Changes()
        {
            var starColumn = new TextColumn<Model, string?>(
                null,
                x => x.Country,
                new GridLength(1, GridUnitType.Star));
            var target = new ColumnList<Model>
            {
                new TextColumn<Model, string?>(
                    null,
                    x => x.Name,
                    new GridLength(50, GridUnitType.Pixel)),
                new TextColumn<Model, string?>(null, x => x.Country, GridLength.Auto),
                starColumn,
            };

            target.ViewportChanged(new Rect(0, 0, 300, 500));
            target.CellMeasured(0, 0, new Size(50, 10));
            target.CellMeasured(1, 0, new Size(80, 10));
            target.CellMeasured(2, 0, new Size(80, 10));
            target.CommitActualWidths();
            target.CommitActualWidths();

            Assert.Equal(50, target[0].ActualWidth);
            Assert.Equal(80, target[1].ActualWidth);
            Assert.Equal(170, target[2].ActualWidth);

            target.CellMeasured(1, 1, new Size(100, 10));
            target.CommitActualWidths();

            Assert.Equal(100, target[1].ActualWidth);
            Assert.Equal(150, target[2].ActualWidth);

            target.CommitActualWidths();
            target.ViewportChanged(new Rect(0, 0, 400, 500));

            Assert.Equal(250, target[2].ActualWidth);

            starColumn.Options.MinWidth = new GridLength(300, GridUnitType.Pixel);
            target.CellMeasured(2, 1, new Size(100, 10));
            target.CommitActualWidths();

            Assert.Equal(300, target[2].ActualWidth);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Clean_Commit_Does_Not_Suppress_Later_Column_Collection_Changes()
        {
            var target = new ColumnList<Model>
            {
                new TextColumn<Model, string?>(
                    null,
                    x => x.Name,
                    new GridLength(50, GridUnitType.Pixel)),
                new TextColumn<Model, string?>(
                    null,
                    x => x.Country,
                    new GridLength(1, GridUnitType.Star)),
            };

            target.ViewportChanged(new Rect(0, 0, 200, 500));
            target.CellMeasured(0, 0, new Size(50, 10));
            target.CellMeasured(1, 0, new Size(100, 10));
            target.CommitActualWidths();
            target.CommitActualWidths();

            target.RemoveAt(0);
            target.CommitActualWidths();
            Assert.Equal(200, target[0].ActualWidth);

            target.Insert(
                0,
                new TextColumn<Model, string?>(
                    null,
                    x => x.Name,
                    new GridLength(100, GridUnitType.Pixel)));
            target.CommitActualWidths();

            Assert.Equal(100, target[0].ActualWidth);
            Assert.Equal(100, target[1].ActualWidth);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Star_Column_With_Auto_MinWidth_Initializes_From_First_Measurement()
        {
            var options = new TextColumnOptions<Model> { MinWidth = GridLength.Auto };
            var target = CreateFixedAndStarColumns(options);

            target.ViewportChanged(new Rect(0, 0, 200, 500));
            var measured = target.CellMeasured(1, 0, new Size(80, 10));
            target.CommitActualWidths();

            Assert.Equal(80, measured.Width);
            Assert.Equal(150, target[1].ActualWidth);
        }

        [AvaloniaFact(Timeout = 10000)]
        public void Star_Column_With_Auto_MaxWidth_Initializes_From_First_Measurement()
        {
            var options = new TextColumnOptions<Model> { MaxWidth = GridLength.Auto };
            var target = CreateFixedAndStarColumns(options);

            target.ViewportChanged(new Rect(0, 0, 200, 500));
            target.CellMeasured(1, 0, new Size(80, 10));
            target.CommitActualWidths();

            Assert.Equal(80, target[1].ActualWidth);
        }

        private static ColumnList<Model> CreateFixedAndStarColumns(TextColumnOptions<Model> options)
        {
            return new ColumnList<Model>
            {
                new TextColumn<Model, string?>(
                    null,
                    x => x.Name,
                    new GridLength(50, GridUnitType.Pixel)),
                new TextColumn<Model, string?>(
                    null,
                    x => x.Country,
                    new GridLength(1, GridUnitType.Star),
                    options),
            };
        }

        private class Model
        {
            public string? Name { get; set; }
            public string? Country { get; set; }
        }
    }
}
