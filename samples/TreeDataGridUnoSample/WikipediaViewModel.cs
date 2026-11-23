using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TreeDataGridCore;
using TreeDataGridCore.Models;
using TreeDataGridDemo.Models;

namespace TreeDataGridUnoSample;

/// <summary>Core-only sample state; callers start loads on the UI thread.</summary>
internal sealed class WikipediaViewModel : NotifyingBase, IDisposable
{
    private static readonly HttpClient SharedClient = CreateClient();
    private readonly HttpClient _client;
    private CancellationTokenSource? _request;
    private bool _disposed;
    private bool _isLoading;
    private string _status = "Choose live Wikipedia data or the deterministic offline fixture.";

    public WikipediaViewModel(HttpClient? client = null)
    {
        _client = client ?? SharedClient;
        Source = new(Array.Empty<OnThisDayArticle>());
        Source.Columns.Add(new TemplateColumn<OnThisDayArticle>("Image", "WikipediaImage", width: new(110)));
        Source.Columns.Add(new TemplateColumn<OnThisDayArticle>("Title", "WikipediaTitle", width: new(2, GridUnitType.Star), options: new()
        {
            CompareAscending = (x, y) => string.CompareOrdinal(x?.Titles?.Normalized, y?.Titles?.Normalized),
            CompareDescending = (x, y) => string.CompareOrdinal(y?.Titles?.Normalized, x?.Titles?.Normalized),
        }));
        Source.Columns.Add(new TemplateColumn<OnThisDayArticle>("Extract", "WikipediaExtract", width: new(4, GridUnitType.Star), options: new()
        {
            CompareAscending = (x, y) => string.CompareOrdinal(x?.Extract, y?.Extract),
            CompareDescending = (x, y) => string.CompareOrdinal(y?.Extract, x?.Extract),
        }));
    }

    public FlatTreeDataGridSource<OnThisDayArticle> Source { get; }
    public string Status { get => _status; private set => RaiseAndSetIfChanged(ref _status, value); }
    public bool IsLoading { get => _isLoading; private set => RaiseAndSetIfChanged(ref _isLoading, value); }
    public Task LoadingTask { get; private set; } = Task.CompletedTask;

    public static HttpClient CreateClient(HttpMessageHandler? handler = null)
    {
        var client = handler is null ? new HttpClient() : new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "TreeDataGridUnoSample/1.0 (+https://github.com/wieslawsoltes/TreeDataGrid)");
        return client;
    }

    public Task ReloadAsync(DateTimeOffset? date = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CancelLoad();
        var request = _request = new();
        return LoadingTask = LoadAsync(date ?? DateTimeOffset.Now, request);
    }

    private async Task LoadAsync(DateTimeOffset date, CancellationTokenSource request)
    {
        IsLoading = true;
        Status = "Loading Wikipedia's On This Day feed…";
        try
        {
            var uri = $"https://api.wikimedia.org/feed/v1/wikipedia/en/onthisday/all/{date.Month:00}/{date.Day:00}";
            var json = await _client.GetStringAsync(uri, request.Token);
            var data = JsonSerializer.Deserialize(json, OnThisDayJsonSerializerContext.Default.OnThisDay);
            if (!ReferenceEquals(_request, request)) return;
            var articles = data?.Selected?.SelectMany(x => x.Pages ?? Array.Empty<OnThisDayArticle>()).ToArray()
                ?? Array.Empty<OnThisDayArticle>();
            Source.Items = articles;
            Status = $"{articles.Length} virtualized Wikipedia articles loaded for {date:MM-dd}.";
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!ReferenceEquals(_request, request)) return;
            Source.Items = CreateOfflineItems();
            Status = $"Network unavailable; showing 240 synthetic offline rows. {error.Message}";
        }
        finally
        {
            if (ReferenceEquals(_request, request)) { _request = null; IsLoading = false; }
            request.Dispose();
        }
    }

    public void ShowOffline()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CancelLoad();
        Source.Items = CreateOfflineItems();
        Status = "240 synthetic offline rows · shared Wikipedia data types · no network required.";
    }

    private static OnThisDayArticle[] CreateOfflineItems() => Enumerable.Range(1, 240).Select(i => new OnThisDayArticle
    {
        Titles = new() { Normalized = $"Offline article {i:000}" },
        Description = "Synthetic sample data, not Wikipedia content",
        Extract = string.Join(" ", Enumerable.Repeat(
            $"Sample {i:000} exercises wrapping text, variable row heights and recycled images with the shared Core source.", 1 + i % 5)),
        Thumbnail = i % 3 == 0 ? null : new() { Source = "ms-appx:///Assets/file.png", Width = 16, Height = 16 },
    }).ToArray();

    public void CancelLoad()
    {
        var request = _request;
        _request = null;
        request?.Cancel(); // The pending operation disposes its own token source.
        if (IsLoading) Status = "Wikipedia load cancelled.";
        IsLoading = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelLoad();
        Source.Dispose();
    }
}
