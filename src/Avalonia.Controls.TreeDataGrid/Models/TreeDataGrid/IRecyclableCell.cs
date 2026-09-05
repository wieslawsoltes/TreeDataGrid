namespace Avalonia.Controls.Models.TreeDataGrid
{
    // View cell models may opt into pooling only if they can release their row and its
    // subscriptions without destroying the binding expression needed for a later retarget.
    internal interface IRecyclableCell
    {
        bool TrySuspend();
    }

    internal interface IRecyclingCellRows
    {
        bool TryRecycleCell(IColumn column, ICell cell);
    }
}
