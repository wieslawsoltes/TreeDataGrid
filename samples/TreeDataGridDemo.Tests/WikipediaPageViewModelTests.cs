using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TreeDataGridDemo.Models;
using TreeDataGridDemo.ViewModels;
using Xunit;

namespace TreeDataGridDemo.Tests;

public class WikipediaPageViewModelTests
{
    [Fact]
    public async Task LoadContent_SendsUserAgentAndPopulatesRows()
    {
        HttpRequestMessage? request = null;
        var handler = new StubHttpMessageHandler(x =>
        {
            request = x;
            return JsonResponse("""
                {
                  "selected": [
                    {
                      "text": "An event",
                      "year": 2026,
                      "pages": [
                        {
                          "type": "standard",
                          "titles": { "normalized": "A page" },
                          "extract": "An extract"
                        }
                      ]
                    }
                  ]
                }
                """);
        });
        using var client = WikipediaHttpClient.Create(handler);
        var target = new WikipediaPageViewModel(client);

        await target.LoadingTask;

        Assert.NotNull(request);
        Assert.Contains("AvaloniaTreeDataGridSample", request!.Headers.UserAgent.ToString());
        Assert.Single(target.Source.Rows);
        Assert.Null(target.LoadError);
    }

    [Fact]
    public async Task LoadContent_ReportsHttpFailures()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Forbidden));
        using var client = WikipediaHttpClient.Create(handler);
        var target = new WikipediaPageViewModel(client);

        await target.LoadingTask;

        Assert.Empty(target.Source.Rows);
        Assert.StartsWith("Unable to load Wikipedia content:", target.LoadError);
    }

    [Fact]
    public async Task LoadContent_IgnoresEventsWithoutPages()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse("""
            {
              "selected": [
                { "text": "No associated page", "year": 2026 },
                {
                  "text": "An event with a page",
                  "year": 2025,
                  "pages": [
                    {
                      "titles": { "normalized": "A page" },
                      "extract": "An extract"
                    }
                  ]
                }
              ]
            }
            """));
        using var client = WikipediaHttpClient.Create(handler);
        var target = new WikipediaPageViewModel(client);

        await target.LoadingTask;

        Assert.Single(target.Source.Rows);
        Assert.Null(target.LoadError);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
