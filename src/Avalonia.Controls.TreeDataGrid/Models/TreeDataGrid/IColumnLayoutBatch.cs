namespace Avalonia.Controls.Models.TreeDataGrid
{
    internal interface IColumnLayoutBatch
    {
        bool IsActualWidthCommitDeferred { get; }

        void BeginActualWidthBatch();

        bool EndActualWidthBatch();

        void RequestFinalMeasure();
    }
}
