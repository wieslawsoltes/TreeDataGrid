using System.Net.Http;

namespace TreeDataGridDemo.Models
{
    internal static class WikipediaHttpClient
    {
        private const string UserAgent =
            "AvaloniaTreeDataGridSample/1.0 (https://avaloniaui.net; team@avaloniaui.net)";

        public static HttpClient Shared { get; } = Create();

        public static HttpClient Create(HttpMessageHandler? handler = null)
        {
            var result = handler is null ? new HttpClient() : new HttpClient(handler);
            result.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            return result;
        }
    }
}
