using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridTemplateCell : TreeDataGridCell
{
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(object), typeof(TreeDataGridTemplateCell), new PropertyMetadata(null));
    public static readonly DependencyProperty ContentTemplateProperty = DependencyProperty.Register(
        nameof(ContentTemplate), typeof(DataTemplate), typeof(TreeDataGridTemplateCell), new PropertyMetadata(null, TemplatesChanged));
    public static readonly DependencyProperty EditingTemplateProperty = DependencyProperty.Register(
        nameof(EditingTemplate), typeof(DataTemplate), typeof(TreeDataGridTemplateCell), new PropertyMetadata(null, TemplatesChanged));
    private int _synchronizing;
    private int _valueRefreshVersion;
    private DataTemplate? _sourceContentTemplate;
    public TreeDataGridTemplateCell() : base(CellKind.Template) => DefaultStyleKey = typeof(TreeDataGridTemplateCell);
    public object? Content { get => GetValue(ContentProperty); private set => SetValue(ContentProperty, value); }
    public DataTemplate? ContentTemplate { get => (DataTemplate?)GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }
    public DataTemplate? EditingTemplate { get => (DataTemplate?)GetValue(EditingTemplateProperty); set => SetValue(EditingTemplateProperty, value); }
    public override void Realize(CellColumn column, CellValue value, IRow row, int columnIndex, int rowIndex,
        DataTemplate? template, DataTemplate? editingTemplate = null)
    {
        if (IsUnrealizing) throw new System.InvalidOperationException("Cell unrealization is in progress.");
        ++_synchronizing;
        try
        {
            // Preserve a caller's ContentTemplate override while the source
            // template is unchanged, matching Avalonia's source-template cache.
            if (!ReferenceEquals(_sourceContentTemplate, template))
            {
                _sourceContentTemplate = template;
                ContentTemplate = template;
            }
            EditingTemplate = editingTemplate;
        }
        finally { --_synchronizing; }
        DataContext = value.PresentationModel;
        base.Realize(column, value, row, columnIndex, rowIndex, ContentTemplate, EditingTemplate);
    }
    public override void Unrealize() => base.Unrealize();
    // DataContext is part of the same retirement transaction as base-owned
    // fields. Its callbacks must not adopt a new model after the guard expires.
    internal override void ClearRetiredModelContext() => DataContext = null;
    protected override void UpdateValue()
    {
        if (IsUnrealizing) return;
        var realization = RealizationVersion;
        var refresh = unchecked(++_valueRefreshVersion);
        var model = ViewModel;
        var content = model?.Value;
        if (!Current()) return;
        if (!ReferenceEquals(Content, content)) Content = content;
        if (Current()) base.UpdateValue();

        bool Current() => !IsUnrealizing && realization == RealizationVersion &&
            refresh == _valueRefreshVersion && ReferenceEquals(ViewModel, model);
    }
    protected override void ClearContent()
    {
        Content = null;
        base.ClearContent();
    }
    private static void TemplatesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var cell = (TreeDataGridTemplateCell)sender;
        if (cell._synchronizing == 0) cell.SetCellTemplates(cell.ContentTemplate, cell.EditingTemplate);
    }
}
