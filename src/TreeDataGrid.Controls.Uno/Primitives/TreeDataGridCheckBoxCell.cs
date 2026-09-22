using Microsoft.UI.Xaml;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;

namespace Uno.Controls.Primitives;

public class TreeDataGridCheckBoxCell : TreeDataGridCell
{
    protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        new global::Uno.Controls.Automation.Peers.TreeDataGridCheckBoxCellAutomationPeer(this);

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(bool?), typeof(TreeDataGridCheckBoxCell), new PropertyMetadata(null, ValueChanged));
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(TreeDataGridCheckBoxCell), new PropertyMetadata(false, AppearanceChanged));
    public static readonly DependencyProperty IsThreeStateProperty = DependencyProperty.Register(
        nameof(IsThreeState), typeof(bool), typeof(TreeDataGridCheckBoxCell), new PropertyMetadata(false, AppearanceChanged));
    private int _synchronizing;
    public TreeDataGridCheckBoxCell() : base(CellKind.CheckBox) => DefaultStyleKey = typeof(TreeDataGridCheckBoxCell);
    public new bool? Value { get => (bool?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public bool IsThreeState { get => (bool)GetValue(IsThreeStateProperty); set => SetValue(IsThreeStateProperty, value); }
    protected override bool? DisplayCheckBoxValue => Value;
    protected override bool DisplayCheckBoxIsReadOnly => IsReadOnly || ViewModel is { CanWrite: false };
    protected override bool DisplayCheckBoxIsThreeState => IsThreeState;
    protected override void OnCheckBoxValueChanged(bool? value) => Value = value;

    public override void Realize(CellColumn column, CellValue value, IRow row, int columnIndex, int rowIndex,
        DataTemplate? template, DataTemplate? editingTemplate = null)
    {
        ++_synchronizing;
        try
        {
            if (IsReadOnly == value.CanWrite) IsReadOnly = !value.CanWrite;
            var threeState = value.IsThreeState ?? column.IsThreeState;
            if (IsThreeState != threeState) IsThreeState = threeState;
        }
        finally { --_synchronizing; }
        base.Realize(column, value, row, columnIndex, rowIndex, template, editingTemplate);
    }
    protected override void UpdateValue()
    {
        var model = ViewModel;
        var value = model?.Value as bool?;
        if (!ReferenceEquals(ViewModel, model)) return;
        ++_synchronizing;
        try { if (Value != value) Value = value; }
        finally { --_synchronizing; }
        if (ReferenceEquals(ViewModel, model)) base.UpdateValue();
    }
    protected override void ClearContent()
    {
        ++_synchronizing;
        try { Value = null; }
        finally { --_synchronizing; }
        base.ClearContent();
    }
    private static void ValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var cell = (TreeDataGridCheckBoxCell)sender;
        if (cell._synchronizing != 0) return;
        if (cell.ViewModel is { } model)
        {
            try { if (!cell.IsReadOnly && model.CanWrite) model.Write(e.NewValue); }
            finally { cell.UpdateValue(); }
        }
        else cell.RefreshCellPresentation();
    }
    private static void AppearanceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var cell = (TreeDataGridCheckBoxCell)sender;
        if (cell._synchronizing == 0) cell.RefreshCellPresentation();
    }
}
