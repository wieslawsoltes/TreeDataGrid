using System;
using System.Globalization;
using Uno.Controls.Models.TreeDataGrid;
using Xunit;

namespace TreeDataGrid.Uno.Tests;

public class TextSearchContractTests
{
    [Fact]
    public void Text_Column_Separates_Reference_Search_From_Presentation_Format_And_Culture()
    {
        using var column = new TextColumn<Item, decimal>("Value", x => x.Value,
            options: new TextColumnOptions<Item>
            {
                Culture = CultureInfo.GetCultureInfo("pl-PL"),
                StringFormat = "Amount {0:0.00}",
                IsTextSearchEnabled = true,
            });
        var contract = Assert.IsAssignableFrom<ITextSearchableColumn<Item>>(column);
        Assert.True(contract.IsTextSearchEnabled);
        var model = new Item(12.5m);
        // Avalonia TextColumn's selector uses ValueSelector(model)?.ToString(),
        // not display formatting. The direct framework contract project executes
        // that public selector; retain the display assertion independently.
        Assert.Equal(model.Value.ToString(), contract.SelectValue(model));
        Assert.Equal("Amount 12,50", column.FormatValue(model.Value));
    }

    [Fact]
    public void Template_Column_Uses_Its_Live_Typed_Selector_Without_Resolving_A_Template()
    {
        var options = new TemplateColumnOptions<Item>
        {
            IsTextSearchEnabled = true,
            TextSearchValueSelector = item => $"First {item.Value.ToString(CultureInfo.InvariantCulture)}",
        };
        using var column = new TemplateColumn<Item>("Value", "Unresolved.Resource.Key", options: options);
        var contract = Assert.IsAssignableFrom<ITextSearchableColumn<Item>>(column);
        Assert.True(contract.IsTextSearchEnabled);
        Assert.Equal("First 12.5", contract.SelectValue(new(12.5m)));
        options.TextSearchValueSelector = _ => "Replacement";
        Assert.Equal("Replacement", contract.SelectValue(new(12.5m)));
        options.IsTextSearchEnabled = false;
        Assert.False(contract.IsTextSearchEnabled);
    }

    [Fact]
    public void Template_Column_With_No_Selector_Returns_No_Search_Value()
    {
        using var column = new TemplateColumn<Item>("Value", "Unresolved.Resource.Key");
        var contract = Assert.IsAssignableFrom<ITextSearchableColumn<Item>>(column);
        Assert.Null(contract.SelectValue(new(1)));
    }

    private sealed record Item(decimal Value);
}
