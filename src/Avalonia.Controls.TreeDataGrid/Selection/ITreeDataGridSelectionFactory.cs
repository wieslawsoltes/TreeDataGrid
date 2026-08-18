#if TREE_DATAGRID_UNO
namespace Uno.Controls.Selection
#else
namespace Avalonia.Controls.Selection
#endif
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
