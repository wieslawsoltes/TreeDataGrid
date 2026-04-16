using System;
#if TREE_DATAGRID_UNO
using Uno.Controls.Models.TreeDataGrid;
#else
using Avalonia.Controls.Models.TreeDataGrid;
#endif
using Avalonia.Input;

#if TREE_DATAGRID_UNO

namespace Uno.Controls.Selection

#else

namespace Avalonia.Controls.Selection

#endif
{
    /// <summary>
    /// Defines the interaction between a <see cref="TreeDataGrid"/> and an
    /// <see cref="ITreeDataGridSelection"/> model.
    /// </summary>
    public interface ITreeDataGridSelectionInteraction
    {
        public event EventHandler? SelectionChanged;

        bool IsCellSelected(int columnIndex, int rowIndex) => false;
        bool IsRowSelected(IRow rowModel) => false;
        bool IsRowSelected(int rowIndex) => false;
        public void OnKeyDown(TreeDataGrid sender, KeyEventArgs e) { }
        public void OnPreviewKeyDown(TreeDataGrid sender, KeyEventArgs e) { }
        public void OnKeyUp(TreeDataGrid sender, KeyEventArgs e) { }
        public void OnTextInput(TreeDataGrid sender, TextInputEventArgs e) { }
        public void OnPointerPressed(TreeDataGrid sender, PointerPressedEventArgs e) { }
        public void OnPointerMoved(TreeDataGrid sender, PointerEventArgs e) { }
        public void OnPointerReleased(TreeDataGrid sender, PointerReleasedEventArgs e) { }
    }
}
