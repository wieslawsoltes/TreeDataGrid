using TreeDataGridUnoSamples;
using Xunit;

namespace TreeDataGridUnoSample.Tests;

public sealed class BrowserInputLaunchTests
{
    [Theory]
    [InlineData("?browser-input=1", true)]
    [InlineData("?browser-input=true", true)]
    [InlineData("?browser-input", true)]
    [InlineData("?browser-input=0", false)]
    [InlineData("?browser-input=javascript%3Aalert(1)", false)]
    public void Browser_input_is_an_explicit_boolean_launch_flag(string query, bool enabled)
    {
        var flags = SampleRunContext.ParseBrowserArguments(query);
        Assert.Equal(enabled, System.Array.IndexOf(flags, "--browser-input") >= 0);
        Assert.DoesNotContain("--smoke", flags);
    }
}
