using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Presentation;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(IColumns), typeof(TreeDataGrid), new PropertyMetadata(null));
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows), typeof(ITreeDataGridRows), typeof(TreeDataGrid), new PropertyMetadata(null));
    public static readonly DependencyProperty PresentationProperty = DependencyProperty.Register(
        nameof(Presentation), typeof(TreeDataGridPresentation), typeof(TreeDataGrid), new PropertyMetadata(null));
    public static readonly DependencyProperty ScrollProperty = DependencyProperty.Register(
        nameof(Scroll), typeof(ScrollViewer), typeof(TreeDataGrid), new PropertyMetadata(null));

    public IColumns? Columns => (IColumns?)GetValue(ColumnsProperty);
    public ITreeDataGridRows? Rows => (ITreeDataGridRows?)GetValue(RowsProperty);

    private bool PublishPresentationProperties(int revision)
    {
        var presentation = _presentation;
        SetValue(ColumnsProperty, presentation?.Columns);
        if (revision != _presentationRevision) return false;
        SetValue(RowsProperty, presentation?.Rows);
        if (revision != _presentationRevision) return false;
        SetValue(PresentationProperty, presentation);
        return revision == _presentationRevision;
    }
}
