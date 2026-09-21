namespace Uno.Controls.Models.TreeDataGrid;

public interface IExpander : global::TreeDataGridCore.Models.IExpander
{
    new bool IsExpanded { get; set; }
    new bool ShowExpander { get; }
}

/// <summary>An expander cell bound directly to a framework-neutral row.</summary>
public interface IExpanderCellPresentation : ICell, IExpander
{
    object? Content { get; }
    global::TreeDataGridCore.Models.IRow Row { get; }
}
