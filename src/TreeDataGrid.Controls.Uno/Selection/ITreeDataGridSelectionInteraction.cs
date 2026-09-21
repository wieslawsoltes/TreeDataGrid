using System;
using Microsoft.UI.Xaml.Input;
using TreeDataGridCore.Models;

namespace Uno.Controls.Selection;

/// <summary>Presentation selection and native input interaction for grid containers.</summary>
public interface ITreeDataGridSelectionInteraction
{
    event EventHandler? SelectionChanged;
    bool IsCellSelected(int columnIndex, int rowIndex) => false;
    bool IsRowSelected(IRow rowModel) => false;
    bool IsRowSelected(int rowIndex) => false;
    void OnKeyDown(TreeDataGrid sender, KeyRoutedEventArgs e) { }
    void OnPreviewKeyDown(TreeDataGrid sender, KeyRoutedEventArgs e) { }
    void OnKeyUp(TreeDataGrid sender, KeyRoutedEventArgs e) { }
    // Native committed-character args replace Avalonia TextInputEventArgs.
    void OnTextInput(TreeDataGrid sender, CharacterReceivedRoutedEventArgs e) { }
    void OnPointerPressed(TreeDataGrid sender, PointerRoutedEventArgs e) { }
    void OnPointerMoved(TreeDataGrid sender, PointerRoutedEventArgs e) { }
    void OnPointerReleased(TreeDataGrid sender, PointerRoutedEventArgs e) { }
}
