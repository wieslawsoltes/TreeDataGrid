using System;
using TreeDataGridCore;
using Xunit;
using global::Uno.Controls;

namespace TreeDataGrid.Uno.Tests;

public sealed class RowModelCompatibilityTests
{
    [Fact]
    public void Captured_row_preserves_model_identity_and_the_shared_Core_path()
    {
        var model = new object();
        var index = new IndexPath(2).Append(7);
        var row = new TreeDataGridRowModel(model, index);
        Assert.Same(model, row.Model);
        Assert.Equal(index, row.ModelIndexPath);
        Assert.Equal(typeof(IndexPath), row.ModelIndexPath.GetType());
    }

    [Fact]
    public void Null_model_and_default_path_match_the_reference_constructor_contract()
    {
        var row = new TreeDataGridRowModel(null, default);
        Assert.Null(row.Model);
        Assert.Equal(default(IndexPath), row.ModelIndexPath);
    }

    [Fact]
    public void Event_args_borrow_the_exact_supplied_row_snapshot()
    {
        var row = new TreeDataGridRowModel(new object(), new IndexPath(3));
        var args = new TreeDataGridRowModelEventArgs(row);
        Assert.IsAssignableFrom<EventArgs>(args);
        Assert.Same(row, args.Row);
    }

    [Fact]
    public void Contracts_remain_derivable_like_the_reference_types()
    {
        var model = new object();
        var row = new CustomRow(model, new IndexPath(4));
        var args = new CustomEventArgs(row);
        Assert.Same(row, args.Row);
        Assert.Same(model, args.Row.Model);
        Assert.Equal(new IndexPath(4), args.Row.ModelIndexPath);
    }

    private sealed class CustomRow(object? model, IndexPath path) : TreeDataGridRowModel(model, path);
    private sealed class CustomEventArgs(TreeDataGridRowModel row) : TreeDataGridRowModelEventArgs(row);
}
