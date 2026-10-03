using System.Text.Json;
using TreeDataGridUnoSamples;
using Xunit;

namespace TreeDataGridUnoSample.Tests;

public class SampleRunContextTests
{
    [Fact]
    public void Browser_options_are_allowlisted_boolean_flags_and_deduplicated()
    {
        Assert.Equal(new[] { "--demo", "--offline", "--smoke", "--wikipedia-live" }, SampleRunContext.ParseBrowserArguments(
            "?smoke=1&demo=true&offline&wikipedia-live=1&SMOKE=true&ignored=1&screenshot-dir=/tmp&source=/private"));
    }
    [Fact]
    public void False_and_arbitrary_values_do_not_enable_flags()
    {
        Assert.Empty(SampleRunContext.ParseBrowserArguments("?smoke=false&demo=0&offline=unexpected"));
        Assert.Equal(new[] { "--smoke" }, SampleRunContext.ParseBrowserArguments("?%73moke=%74rue"));
    }
    [Fact]
    public void Failure_report_preserves_details_as_JSON_data_not_executable_script()
    {
        const string error = "Quote: \" and newline\n</script>; globalThis.bad = true;";
        var json = SampleRunContext.SerializeResult(false, error);
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("complete").GetBoolean());
        Assert.False(document.RootElement.GetProperty("passed").GetBoolean());
        Assert.Equal(error, document.RootElement.GetProperty("error").GetString());
        Assert.DoesNotContain("</script>", json);
    }
    [Fact]
    public void Success_report_has_an_explicit_completion_and_null_error()
    {
        using var document = JsonDocument.Parse(SampleRunContext.SerializeResult(true, null));
        Assert.True(document.RootElement.GetProperty("complete").GetBoolean());
        Assert.True(document.RootElement.GetProperty("passed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("error").ValueKind);
    }
}
