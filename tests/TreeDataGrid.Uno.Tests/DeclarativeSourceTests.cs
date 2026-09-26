using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Uno.Controls;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class DeclarativeSourceTests
{
    [Fact]
    public void Type_erasure_preserves_list_identity_mutations_and_collection_notifications()
    {
        var first = new object();
        var items = new ObservableCollection<object> { first };
        var adapter = new DeclarativeItemsSource(items);
        var changes = new List<NotifyCollectionChangedEventArgs>();
        adapter.CollectionChanged += (_, e) => changes.Add(e);
        Assert.Same(first, adapter[0]);
        var second = new object();
        adapter.Add(second);
        Assert.Same(second, items[1]);
        Assert.Equal(NotifyCollectionChangedAction.Add, Assert.Single(changes).Action);
        items.Move(1, 0);
        Assert.Same(second, adapter[0]);
        Assert.Equal(NotifyCollectionChangedAction.Move, changes[1].Action);
        Assert.True(adapter.Remove(first));
        Assert.Single(items);
    }

    [Fact]
    public void Non_generic_list_retains_indexes_including_null_values()
    {
        var model = new object();
        var items = new ArrayList { null, model };
        IList<object> adapter = new DeclarativeItemsSource(items);
        Assert.Equal(2, adapter.Count);
        Assert.Null(adapter[0]);
        Assert.Same(model, adapter[1]);
        Assert.Equal(1, adapter.IndexOf(model));
    }

    [Fact]
    public void Generated_source_uses_actual_Core_rows_and_observes_original_items()
    {
        var first = new object();
        var items = new ObservableCollection<object> { first };
        using var generated = DeclarativeSource.Create(items, [new TreeDataGridRowHeaderColumn()]);
        Assert.IsType<TreeDataGridCore.FlatTreeDataGridSource<object>>(generated.Source);
        using var view = TreeDataGridPresentation.Create(generated.Source);
        Assert.Same(generated.Source.Rows, view.Model.Rows);
        Assert.Same(generated.Source.Rows[0], view.Rows[0]);
        Assert.Same(first, view.Rows[0].Model);
        items.Add(new object());
        Assert.Equal(2, view.Rows.Count);
        using var header = view.RealizeCell(0, 1);
        Assert.Equal("2", header.Value);
    }

    [Fact]
    public void Native_template_resources_are_not_loaded_while_constructing_the_Core_source()
    {
        using var generated = DeclarativeSource.Create(new[] { new object() },
            [new TreeDataGridTemplateColumn("Template", "DeferredResource")]);
        Assert.IsType<TreeDataGridCore.Models.TemplateColumn<object>>(generated.Source.Columns[0]);
        using var view = TreeDataGridPresentation.Create(generated.Source);
        Assert.Equal(CellKind.Template, view.NativeColumns[0].Kind);
    }
}
