using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TreeDataGridDemo.Models;
using Xunit;

namespace TreeDataGridUnoSample.Tests;

public class WikipediaViewModelTests
{
    [Fact]
    public async Task Load_UsesUserAgentDateAndSharedDto_AndSkipsEventsWithoutPages()
    {
        using var client = WikipediaViewModel.CreateClient(new Handler((request, _) =>
        {
            Assert.Contains("TreeDataGridUnoSample/1.0", request.Headers.UserAgent.ToString());
            Assert.EndsWith("/02/03", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(Json("""
                {"selected":[{"text":"No pages"},{"pages":[{"titles":{"normalized":"Article"},"extract":"Body"}]}]}
                """));
        }));
        using var model = new WikipediaViewModel(client);
        await model.ReloadAsync(new DateTimeOffset(2026, 2, 3, 0, 0, 0, TimeSpan.Zero));
        var article = Assert.Single(model.Source.Items);
        Assert.IsType<OnThisDayArticle>(model.Source.Rows[0].Model);
        Assert.Equal("Article", article.Titles!.Normalized);
        Assert.Equal("Body", article.Extract);
        Assert.StartsWith("1 virtualized Wikipedia articles", model.Status);
        Assert.False(model.IsLoading);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"selected\":[]}")]
    [InlineData("{\"selected\":[{\"pages\":null}]}")]
    public async Task EmptyFeed_IsReportedWithoutInventedLiveArticles(string json)
    {
        using var client = WikipediaViewModel.CreateClient(new Handler((_, _) => Task.FromResult(Json(json))));
        using var model = new WikipediaViewModel(client);
        model.ShowOffline();
        await model.ReloadAsync();
        Assert.Empty(model.Source.Items);
        Assert.StartsWith("0 virtualized Wikipedia articles", model.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "denied")]
    [InlineData(HttpStatusCode.OK, "invalid JSON")]
    public async Task FailedFeed_ShowsExplicitOfflineFallback(HttpStatusCode status, string body)
    {
        using var client = WikipediaViewModel.CreateClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) })));
        using var model = new WikipediaViewModel(client);
        await model.ReloadAsync();
        Assert.Equal(240, model.Source.Rows.Count);
        Assert.StartsWith("Network unavailable; showing 240 synthetic offline rows.", model.Status);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task OfflineSwitch_CancelsRequest_AndLateResultCannotOverwriteRows()
    {
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        using var client = WikipediaViewModel.CreateClient(new Handler((_, cancellation) => { token = cancellation; return response.Task; }));
        using var model = new WikipediaViewModel(client);
        var load = model.ReloadAsync();
        Assert.True(model.IsLoading);
        model.ShowOffline();
        var items = model.Source.Items;
        var status = model.Status;
        Assert.True(token.IsCancellationRequested);
        response.SetResult(Json("{\"selected\":[]}"));
        await load;
        Assert.Same(items, model.Source.Items);
        Assert.Equal(status, model.Status);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task Reload_CancelsEarlierRequest_AndOnlyNewestResultIsCommitted()
    {
        var first = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var client = WikipediaViewModel.CreateClient(new Handler((_, _) => ++calls == 1 ? first.Task : Task.FromResult(Json("""
            {"selected":[{"pages":[{"titles":{"normalized":"Newest"}}]}]}
            """))));
        using var model = new WikipediaViewModel(client);
        var oldLoad = model.ReloadAsync();
        await model.ReloadAsync();
        first.SetResult(Json("{\"selected\":[]}"));
        await oldLoad;
        Assert.Equal("Newest", Assert.Single(model.Source.Items).Titles!.Normalized);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task Dispose_CancelsPendingLoad_AndPreventsNewLoads()
    {
        using var client = WikipediaViewModel.CreateClient(new Handler(async (_, cancellation) =>
        {
            await Task.Delay(Timeout.Infinite, cancellation);
            return Json("{}");
        }));
        var model = new WikipediaViewModel(client);
        var load = model.ReloadAsync();
        model.Dispose();
        await load;
        Assert.False(model.IsLoading);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => model.ReloadAsync());
        Assert.Throws<ObjectDisposedException>(model.ShowOffline);
        model.Dispose();
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
