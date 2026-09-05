using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;

namespace Uno.Controls.Primitives;

/// <summary>A parented, reusable Uno cell control over a Core row.</summary>
public partial class TreeDataGridCell : Grid
{
    private readonly TextBlock _text = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new(8, 0, 8, 0) };
    private readonly CheckBox _check = new() { VerticalAlignment = VerticalAlignment.Center, MinWidth = 0, Margin = new(8, 0, 8, 0) };
    private readonly ContentPresenter _content = new() { VerticalAlignment = VerticalAlignment.Stretch };
    private readonly Button _expander = new() { MinWidth = 0, MinHeight = 0, Padding = new(0), Width = 20, Height = 24 };
    private readonly Grid _valueHost = new();
    private ExpanderCellValue? _expanderValue;
    private CellValue? _value;
    private bool _updating;
    private bool _rebinding;

    public TreeDataGridCell()
    {
        ColumnDefinitions.Add(new() { Width = Microsoft.UI.Xaml.GridLength.Auto });
        ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        Children.Add(_expander);
        SetColumn(_valueHost, 1);
        Children.Add(_valueHost);
        _valueHost.Children.Add(_text);
        _valueHost.Children.Add(_check);
        _valueHost.Children.Add(_content);
        _expander.Click += (_, _) => { if (_expanderValue is { } value) value.IsExpanded = !value.IsExpanded; };
        _check.Checked += OnCheckChanged;
        _check.Unchecked += OnCheckChanged;
        _check.Indeterminate += OnCheckChanged;
    }

    public CellValue? Value => _value;
    public IRow? Row { get; private set; }
    /// <summary>The realized model, captured because flat Core rows can be ephemeral.</summary>
    public object? RowModel { get; private set; }
    public int RowIndex { get; private set; } = -1;
    public int ColumnIndex { get; private set; } = -1;
    public CellColumn? Column { get; private set; }
    public virtual void BeginRebind() => _rebinding = true;
    public virtual void EndRebind(bool realized)
    {
        _rebinding = false;
        if (realized) UpdateValue();
        else ClearContent();
    }

    internal void UpdateIndexes(int row, int column) { RowIndex = row; ColumnIndex = column; }

    public virtual void Realize(CellColumn column, CellValue value, IRow row, int columnIndex, int rowIndex, DataTemplate? template)
    {
        Column = column;
        _value = value;
        _expanderValue = value as ExpanderCellValue;
        Row = row;
        RowModel = row.Model;
        ColumnIndex = columnIndex;
        RowIndex = rowIndex;
        var kind = column.ContentKind;
        // An expander wraps the inner content contract; the value remains the same
        // Core row and the native children stay attached while it is recycled.
        if (kind == CellKind.Template && template is null)
            throw new InvalidOperationException($"No Uno cell template is registered for '{column.Model.PresentationKey}'.");
        _expander.Visibility = _expanderValue is null ? Visibility.Collapsed : Visibility.Visible;
        _expander.Margin = new((row as IIndentedRow)?.Indent * 20 ?? 0, 0, 0, 0);
        _text.Visibility = kind == CellKind.Text ? Visibility.Visible : Visibility.Collapsed;
        _check.Visibility = kind == CellKind.CheckBox ? Visibility.Visible : Visibility.Collapsed;
        _content.Visibility = kind == CellKind.Template ? Visibility.Visible : Visibility.Collapsed;
        if (!ReferenceEquals(_content.ContentTemplate, template)) _content.ContentTemplate = template;
        value.PropertyChanged += OnValueChanged;
        Visibility = Visibility.Visible;
        if (!_rebinding) UpdateValue();
    }

    public virtual void Unrealize()
    {
        if (_value is not null) _value.PropertyChanged -= OnValueChanged;
        _value = null;
        _expanderValue = null;
        Row = null;
        RowModel = null;
        RowIndex = ColumnIndex = -1;
        Column = null;
        if (!_rebinding) ClearContent();
    }
    private void ClearContent()
    {
        _content.Content = null;
        _text.Text = string.Empty;
        Visibility = Visibility.Collapsed;
    }
    private void OnValueChanged(object? sender, PropertyChangedEventArgs e) { if (!_rebinding) UpdateValue(); }
    private void UpdateValue()
    {
        _updating = true;
        try
        {
            _text.Text = _value?.Value?.ToString() ?? string.Empty;
            _check.IsChecked = _value?.Value as bool?;
            _check.IsEnabled = _value?.CanEdit == true;
            _content.Content = _value?.Value;
            if (_expanderValue is { } expanded)
            {
                _expander.Content = expanded.IsExpanded ? "−" : "+";
                _expander.Opacity = expanded.ShowExpander ? 1 : 0;
                _expander.IsHitTestVisible = expanded.ShowExpander;
            }
        }
        finally { _updating = false; }
    }
    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_updating && _value?.CanEdit == true) _value.Write(_check.IsChecked);
    }
}
