using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Uno.Controls;
using Uno.Controls.Models.TreeDataGrid;
using TreeDataGridDemo.Models;

namespace TreeDataGridDemo.ViewModels;

internal class WikipediaPageViewModel
{
    private const string UserAgent = "AvaloniaTreeDataGridUnoSample/1.0 (https://avaloniaui.net; team@avaloniaui.net)";
    private readonly ObservableCollection<OnThisDayArticle> _data = new();

    public WikipediaPageViewModel()
    {
        var wrap = new TextColumnOptions<OnThisDayArticle>
        {
            TextTrimming = Avalonia.Media.TextTrimming.None,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        };

        Source = new FlatTreeDataGridSource<OnThisDayArticle>(_data)
        {
            Columns =
            {
                new TemplateColumn<OnThisDayArticle>("Image", "WikipediaImageCell"),
                new TextColumn<OnThisDayArticle, string?>("Title", x => x.Titles!.Normalized),
                new TextColumn<OnThisDayArticle, string?>("Extract", x => x.Extract, new GridLength(1, GridUnitType.Star), wrap),
            }
        };

        _ = LoadContentAsync();
    }

    public FlatTreeDataGridSource<OnThisDayArticle> Source { get; }

    private async Task LoadContentAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

            var day = DateTimeOffset.Now.Day;
            var month = DateTimeOffset.Now.Month;
            var uri = $"https://api.wikimedia.org/feed/v1/wikipedia/en/onthisday/all/{month:00}/{day:00}";
            var json = await client.GetStringAsync(uri);
            var data = JsonSerializer.Deserialize<OnThisDay>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (data?.Selected is null)
                return;

            foreach (var article in data.Selected.SelectMany(x => x.Pages ?? Array.Empty<OnThisDayArticle>()))
                _data.Add(article);
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"Wikipedia sample request failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"Wikipedia sample parse failed: {ex.Message}");
        }
    }
}
