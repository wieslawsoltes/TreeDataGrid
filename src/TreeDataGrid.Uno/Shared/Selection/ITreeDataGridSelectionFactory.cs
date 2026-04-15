namespace Avalonia.Controls.Selection
{
    internal interface ITreeDataGridSelectionFactory
    {
        ITreeDataGridSelection CreateRowSelectionModel();
        ITreeDataGridSelection CreateCellSelectionModel();
    }
}
