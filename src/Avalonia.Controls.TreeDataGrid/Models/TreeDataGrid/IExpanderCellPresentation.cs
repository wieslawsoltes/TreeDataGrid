namespace Avalonia.Controls.Models.TreeDataGrid
{
    /// <summary>An expander cell bound directly to a framework-neutral row.</summary>
    public interface IExpanderCellPresentation : ICell, IExpander
    {
        object? Content { get; }
        global::TreeDataGridCore.Models.IRow Row { get; }
    }
}
