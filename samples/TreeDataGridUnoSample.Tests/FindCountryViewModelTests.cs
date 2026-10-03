using System.ComponentModel;
using System.Linq;
using Xunit;

namespace TreeDataGridUnoSample.Tests;

public class FindCountryViewModelTests
{
    [Fact]
    public void Find_uses_displayed_index_after_sort_and_clear()
    {
        using var model = new FindCountryViewModel();
        var country = model.AllCountries.First(x => x.Name == "Poland");
        model.SelectedCountry = country;
        var initial = model.DisplayedRow;
        model.Source.SortBy(model.Source.Columns[0], ListSortDirection.Descending);
        Assert.Same(country, model.Source.Rows[model.DisplayedRow].Model);
        Assert.NotEqual(initial, model.DisplayedRow);
        model.Source.ClearSort();
        Assert.Equal(initial, model.DisplayedRow);
    }

    [Fact]
    public void Filtering_preserves_complete_catalog_and_selected_model()
    {
        using var model = new FindCountryViewModel();
        var count = model.AllCountries.Count;
        var country = model.AllCountries.First(x => x.Name == "Poland");
        model.SelectedCountry = country;
        model.FilterText = "afghanistan";
        Assert.Equal(-1, model.DisplayedRow);
        Assert.Contains("not displayed", model.Status);
        Assert.Same(country, model.SelectedCountry);
        Assert.Single(model.Source.Rows);
        Assert.Equal(count, model.AllCountries.Count);
        model.FilterText = "";
        Assert.Same(country, model.Source.Rows[model.DisplayedRow].Model);
        model.SelectedCountry = null;
        Assert.Equal(-1, model.DisplayedRow);
    }
}
