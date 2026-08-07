namespace TreeDataGridDemo.Models
{
    public sealed class TemplateColumnItem
    {
        public required string Name { get; init; }

        public required string Type { get; init; }

        public required string Details { get; init; }

        public bool IsFlagged { get; init; }
    }
}
