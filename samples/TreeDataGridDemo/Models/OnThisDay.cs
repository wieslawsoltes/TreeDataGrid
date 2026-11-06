namespace TreeDataGridDemo.Models
{
    internal class OnThisDay
    {
        public OnThisDayEvent[]? Selected { get; set; }
    }

    internal class OnThisDayEvent
    {
        public string? Text { get; set; }
        public int Year { get; set; }
        public OnThisDayArticle[]? Pages { get; set; }
    }

    // Shared feed data; image loading lives in a platform-specific partial.
    internal partial class OnThisDayArticle
    {
        public string? Type { get; set; }
        public OnThisDayTitles? Titles { get; set; }
        public OnThisDayImage? Thumbnail { get; set; }
        public string? Description { get; set; }
        public string? Extract { get; set; }
    }

    internal class OnThisDayTitles
    {
        public string? Normalized { get; set; }
    }

    internal class OnThisDayImage
    {
        public string? Source { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
