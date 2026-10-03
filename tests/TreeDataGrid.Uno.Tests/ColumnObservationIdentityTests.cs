using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Xunit;
using global::Uno.Controls.Presentation;

namespace TreeDataGrid.Uno.Tests;

public sealed class ColumnObservationIdentityTests
{
    [Fact]
    public void Captured_old_event_does_not_reach_a_readded_definitions_new_view()
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        var column = new TestColumn();
        source.Columns.Add(column);
        TreeDataGridPresentation? presentation = null;
        CellColumn? replacement = null;
        var changes = 0;
        var replace = true;
        PropertyChangedEventHandler earlierObserver = (_, args) =>
        {
            if (args.PropertyName != "Width" || !replace) return;
            replace = false;
            source.Columns.RemoveAt(0);
            source.Columns.Add(column);
            replacement = presentation!.NativeColumns[0];
            replacement.PropertyChanged += (_, args) => { if (args.PropertyName == "Width") ++changes; };
        };
        column.PropertyChanged += earlierObserver;
        using (presentation = TreeDataGridPresentation.Create(source))
        {
            var original = presentation.NativeColumns[0];
            column.NotifyWidth();
            Assert.NotSame(original, replacement);
            Assert.Equal(0, changes);
            column.NotifyWidth();
            Assert.Equal(1, changes);
        }
        column.PropertyChanged -= earlierObserver;
    }

    [Fact]
    public void Removed_definition_handler_does_not_root_its_retired_presentation()
    {
        var column = new TestColumn();
        var weak = CreateAndRetire(column);
        for (var attempt = 0; attempt < 3 && weak.IsAlive; ++attempt)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.False(weak.IsAlive);
        column.NotifyWidth();
        GC.KeepAlive(column);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndRetire(TestColumn column)
    {
        using var source = new FlatTreeDataGridSource<Item>([new()]);
        source.Columns.Add(column);
        var presentation = TreeDataGridPresentation.Create(source);
        source.Columns.RemoveAt(0);
        presentation.Dispose();
        return new WeakReference(presentation);
    }

    private sealed class Item { public string Name => "Item"; }
    private sealed class TestColumn() : TextColumn<Item, string>("Name", item => item.Name)
    {
        private static readonly PropertyChangedEventArgs WidthChanged = new("Width");
        public void NotifyWidth() => RaisePropertyChanged(WidthChanged);
    }
}
