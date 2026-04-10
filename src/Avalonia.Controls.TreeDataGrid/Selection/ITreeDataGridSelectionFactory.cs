namespace Avalonia.Controls.Selection
{
    internal interface ITreeDataGridSelectionFactory
    {
        ITreeDataGridSelection CreateRowSelectionModel();
        ITreeDataGridSelection CreateCellSelectionModel();
    }

    internal interface ITreeDataGridSingleSelectSupport
    {
        bool SingleSelect { get; set; }
    }
}
