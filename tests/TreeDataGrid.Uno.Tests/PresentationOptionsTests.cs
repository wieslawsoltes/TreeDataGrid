using System;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class PresentationOptionsTests
{
    [Fact]
    public void Typed_options_receive_typed_Core_column_and_preserve_identity()
    {
        using var source = Source();
        var options = Options();
        IColumn<Item>? received = null;
        options.Columns["Name"] = column =>
        {
            received = column;
            return new ValueCellColumn<Item, string>((ValueColumn<Item, string>)column, CellKind.Text);
        };
        using var view = TreeDataGridPresentation.Create(source, options);
        var typed = Assert.IsType<TreeDataGridPresentation<Item>>(view);
        Assert.Same(source, typed.Model);
        Assert.Same(source.Columns[0], received);
        Assert.Same(source.Rows, typed.Model.Rows);
        Assert.Same(source.Rows[0], typed.Rows[0]);
        using var cell = typed.RealizeCell(0, 0);
        Assert.Equal("first", cell.Value);
    }

    [Fact]
    public void Typed_options_reject_wrong_source_type_before_calling_factories()
    {
        using var source = Source();
        var options = new TreeDataGridPresentationOptions<Other>();
        var called = false;
        options.Columns["Name"] = _ => { called = true; throw new InvalidOperationException(); };
        Assert.Throws<ArgumentException>(() => TreeDataGridPresentation.Create(source, options));
        Assert.False(called);
        Assert.Single(source.Rows);
    }

    [Fact]
    public void Public_generic_presentation_has_optional_typed_configuration()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("first")]);
        source.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name));
        using var view = new TreeDataGridPresentation<Item>(source, null);
        Assert.Same(source, view.Model);
        Assert.Single(view.NativeColumns);
    }

    [Fact]
    public void Options_factory_is_invoked_once_and_can_delegate_to_typed_options()
    {
        using var source = Source();
        var options = new Policy(Options());
        using var view = TreeDataGridPresentation.Create(source, options);
        Assert.Equal(1, options.Calls);
        Assert.Same(source, view.Model);
    }

    [Fact]
    public void Null_policy_result_is_not_silently_replaced_by_defaults()
    {
        using var source = Source();
        Assert.Throws<InvalidOperationException>(() => TreeDataGridPresentation.Create(source, new NullPolicy()));
    }

    [Fact]
    public void Factories_are_case_sensitive_and_failures_do_not_dispose_the_Core_source()
    {
        using var source = Source();
        var options = Options();
        options.Columns["name"] = options.Columns["Name"];
        options.Columns.Remove("Name");
        Assert.Throws<InvalidOperationException>(() => TreeDataGridPresentation.Create(source, options));
        options.Columns["Name"] = _ => throw new InvalidOperationException("Factory failure");
        Assert.Throws<InvalidOperationException>(() => TreeDataGridPresentation.Create(source, options));
        using var repaired = TreeDataGridPresentation.Create(source, Options());
        Assert.Same(source.Rows, repaired.Model.Rows);
        Assert.Same(source.Rows[0], repaired.Rows[0]);
        Assert.Single(repaired.NativeColumns);
    }

    [Fact]
    public void Typed_factory_changes_are_used_when_a_presentation_key_changes()
    {
        using var source = Source();
        var options = Options();
        using var view = TreeDataGridPresentation.Create(source, options);
        var original = view.NativeColumns[0];
        options.Columns["Updated"] = column => new ValueCellColumn<Item, string>(
            (ValueColumn<Item, string>)column, CellKind.Text, new TextCellOptions { StringFormat = "[{0}]" });
        source.Columns[0].PresentationKey = "Updated";
        Assert.NotSame(original, view.NativeColumns[0]);
        Assert.Equal("[first]", view.NativeColumns[0].FormatValue("first"));
    }

    private static TreeDataGridPresentationOptions<Item> Options()
    {
        var options = new TreeDataGridPresentationOptions<Item>();
        options.Columns["Name"] = column => new ValueCellColumn<Item, string>((ValueColumn<Item, string>)column, CellKind.Text);
        return options;
    }
    private static FlatTreeDataGridSource<Item> Source()
    {
        var source = new FlatTreeDataGridSource<Item>([new("first")]);
        source.Columns.Add(new TextColumn<Item, string>("Name", x => x.Name) { PresentationKey = "Name" });
        return source;
    }
    private sealed class Policy(TreeDataGridPresentationOptions<Item> typed) : ITreeDataGridPresentationOptions
    {
        public int Calls { get; private set; }
        public TreeDataGridPresentation Create(ITreeDataGridSource model) { ++Calls; return typed.Create(model); }
    }
    private sealed class NullPolicy : ITreeDataGridPresentationOptions
    {
        public TreeDataGridPresentation Create(ITreeDataGridSource model) => null!;
    }
    private sealed record Item(string Name);
    private sealed record Other(string Name);
}
