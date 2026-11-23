using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using TreeDataGridCore;
using NotifyingBase = TreeDataGridCore.Models.NotifyingBase;
using TreeDataGridDemo.Models;
using Uno.Controls.Models.TreeDataGrid;
using GridLength = Microsoft.UI.Xaml.GridLength;
using GridUnitType = Microsoft.UI.Xaml.GridUnitType;

namespace TreeDataGridUnoSample.Demo.ViewModels
{
    internal class WikipediaPageViewModel : NotifyingBase
    {
        private readonly ObservableCollection<OnThisDayArticle> _data = new();
        private readonly HttpClient _httpClient;
        private string? _loadError;

        public WikipediaPageViewModel()
            : this(WikipediaViewModel.CreateClient())
        {
        }

        internal WikipediaPageViewModel(HttpClient httpClient)
        {
            _httpClient = httpClient;

            var wrap = new TextColumnOptions<OnThisDayArticle>
            {
                TextTrimming = TextTrimming.None,
                TextWrapping = TextWrapping.Wrap,
            };

            Source = new FlatTreeDataGridSource<OnThisDayArticle>(_data)
            {
                Columns =
                {
                    new TemplateColumn<OnThisDayArticle>("Image", "WikipediaImageCell"),
                    new TextColumn<OnThisDayArticle, string?>("Title", x => x.Titles!.Normalized),
                    new TextColumn<OnThisDayArticle, string?>("Extract", x => x.Extract, new GridLength(1, GridUnitType.Star), wrap)
                }
            };

            LoadingTask = LoadContent();
        }

        public string? LoadError
        {
            get => _loadError;
            private set => RaiseAndSetIfChanged(ref _loadError, value);
        }

        public FlatTreeDataGridSource<OnThisDayArticle> Source { get; }

        internal Task LoadingTask { get; }

        private async Task LoadContent()
        {
            try
            {
                var d = DateTimeOffset.Now.Day;
                var m = DateTimeOffset.Now.Month;
                var uri = $"https://api.wikimedia.org/feed/v1/wikipedia/en/onthisday/all/{m:00}/{d:00}";
                var s = await _httpClient.GetStringAsync(uri);
                var data = JsonSerializer.Deserialize(s, OnThisDayJsonSerializerContext.Default.OnThisDay);

                if (data?.Selected is not null)
                {
                    foreach (var article in data.Selected.SelectMany(x => x.Pages ?? Array.Empty<OnThisDayArticle>()))
                        _data.Add(article);
                }
            }
            catch (Exception e)
            {
                LoadError = $"Unable to load Wikipedia content: {e.Message}";
            }
        }
    }
}
