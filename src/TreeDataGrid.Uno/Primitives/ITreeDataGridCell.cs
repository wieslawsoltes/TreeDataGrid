using Uno.Controls.Models.TreeDataGrid;

namespace Uno.Controls.Primitives
{
    internal interface ITreeDataGridCell
    {
        int ColumnIndex { get; }
        int RowIndex { get; }
        void Realize(TreeDataGrid owner, TreeDataGridElementFactory factory, ICell model, int columnIndex, int rowIndex);
        void UpdateRowIndex(int rowIndex);
        void UpdateSelection();
        double MeasureDesiredWidth();
        void Unrealize();
    }
}
