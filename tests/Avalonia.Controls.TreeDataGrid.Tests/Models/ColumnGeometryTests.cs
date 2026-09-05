using System;
using System.Runtime.CompilerServices;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.TreeDataGridTests.Models;

public class ColumnGeometryTests
{
    [AvaloniaFact]
    public void Observing_a_column_does_not_keep_its_old_view_collection_alive()
    {
        var column = Column(20);
        var weak = CreateTemporaryColumns(column);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(weak.IsAlive);
        GC.KeepAlive(column);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateTemporaryColumns(IColumn<Model> column)
    {
        var columns = new ColumnList<Model> { column };
        columns.GetColumnAt(10);
        return new WeakReference(columns);
    }

    [AvaloniaFact]
    public void Geometry_tracks_widths_and_collection_changes()
    {
        var columns = new MovableColumns();
        var random = new Random(815);
        for (var i = 0; i < 50; ++i)
            columns.Add(Column(random.Next(0, 100)));

        for (var step = 0; step < 100; ++step)
        {
            AssertGeometry(columns);
            var index = random.Next(columns.Count);
            switch (step % 5)
            {
                case 0: columns.SetColumnWidth(index, new GridLength(random.Next(0, 100))); break;
                case 1: columns.Insert(index, Column(random.Next(0, 100))); break;
                case 2: columns.Move(index, random.Next(columns.Count)); break;
                case 3: columns[index] = Column(random.Next(0, 100)); break;
                case 4: columns.RemoveAt(index); break;
            }
        }

        columns.Clear();
        AssertGeometry(columns);
        columns.Add(Column(30));
        AssertGeometry(columns);
    }

    [AvaloniaFact]
    public void Geometry_tracks_auto_and_star_commits_and_direct_width_changes()
    {
        var columns = new MovableColumns
        {
            Column(20),
            new TextColumn<Model, string>(null, x => x.Name, GridLength.Auto),
            new TextColumn<Model, string>(null, x => x.Name, GridLength.Star),
            Column(40),
        };

        AssertGeometry(columns); // NaN ends the known prefix but not the width estimate.
        columns.ViewportChanged(new Rect(0, 0, 300, 100));
        columns.CellMeasured(1, 0, new Size(50, 20));
        columns.CommitActualWidths();
        AssertGeometry(columns);
        columns.ViewportChanged(new Rect(0, 0, 500, 100));
        AssertGeometry(columns);
        columns.CellMeasured(1, 1, new Size(100, 20));
        columns.CommitActualWidths();
        AssertGeometry(columns);
        ((IUpdateColumnLayout)columns[0]).SetWidth(new GridLength(70));
        AssertGeometry(columns);

        // Synchronous layout observers must see the newly committed geometry.
        columns.LayoutInvalidated += (_, _) => AssertGeometry(columns);
        columns.SetColumnWidth(0, new GridLength(90));
    }

    private static void AssertGeometry(MovableColumns columns)
    {
        var total = 0.0;
        var measured = 0;
        foreach (var column in columns)
        {
            if (!double.IsNaN(column.ActualWidth) && column.ActualWidth > 0)
            {
                total += column.ActualWidth;
                ++measured;
            }
        }
        Assert.Equal(measured == 0 ? -1 : total / measured,
            ((IColumnViewportEstimator)columns).EstimateElementSize());

        for (var x = -1.0; x <= total + 1; x += 1)
            Assert.Equal(LinearLookup(columns, x), columns.GetColumnAt(x));
        Assert.Equal((-1, -1.0), columns.GetColumnAt(double.NaN));
        Assert.Equal((-1, -1.0), columns.GetColumnAt(double.PositiveInfinity));
    }

    private static (int, double) LinearLookup(MovableColumns columns, double x)
    {
        var start = 0.0;
        for (var i = 0; i < columns.Count; ++i)
        {
            var width = columns[i].ActualWidth;
            if (x >= start && x < start + width)
                return (i, start);
            if (double.IsNaN(width))
                break;
            start += width;
        }
        return (-1, -1);
    }

    private static TextColumn<Model, string> Column(double width) =>
        new(null, x => x.Name, new GridLength(width));

    private sealed class MovableColumns : ColumnList<Model>
    {
        public void Move(int oldIndex, int newIndex) => MoveItem(oldIndex, newIndex);
    }

    private sealed class Model
    {
        public string Name => "Name";
    }
}
