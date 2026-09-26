namespace Uno.Controls.Models.TreeDataGrid;

/// <summary>A column whose model values can participate in incremental text search.</summary>
public interface ITextSearchableColumn<TModel>
{
    /// <summary>Gets whether incremental text search is enabled for the column.</summary>
    bool IsTextSearchEnabled { get; }

    // Like the Avalonia contract, evaluation belongs to the presentation engine.
    internal string? SelectValue(TModel model);
}
