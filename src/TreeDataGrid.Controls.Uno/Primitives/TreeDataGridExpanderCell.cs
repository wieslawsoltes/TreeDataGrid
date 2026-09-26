using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TreeDataGridCore.Models;
using Uno.Controls.Presentation;

namespace Uno.Controls.Primitives;

public class TreeDataGridExpanderCell : TreeDataGridCell
{
    public static readonly DependencyProperty IndentProperty = DependencyProperty.Register(
        nameof(Indent), typeof(int), typeof(TreeDataGridExpanderCell), new PropertyMetadata(0));
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(TreeDataGridExpanderCell), new PropertyMetadata(false, ExpandedChanged));
    public static readonly DependencyProperty ShowExpanderProperty = DependencyProperty.Register(
        nameof(ShowExpander), typeof(bool), typeof(TreeDataGridExpanderCell), new PropertyMetadata(false));
    private int _synchronizing;
    private Border? _innerHost;
    private TreeDataGridCell? _innerCell;
    private TreeDataGridElementFactory? _standaloneFactory;
    private DataTemplate? _displayTemplate;
    private DataTemplate? _editingTemplate;
    private bool _innerRebinding;
    private bool _unrealizing;
    private bool _updatingInner;
    private long _editingChangedToken;
    private long _validationChangedToken;
    public TreeDataGridExpanderCell() => DefaultStyleKey = typeof(TreeDataGridExpanderCell);
    public int Indent { get => (int)GetValue(IndentProperty); private set => SetValue(IndentProperty, value); }
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public bool ShowExpander { get => (bool)GetValue(ShowExpanderProperty); private set => SetValue(ShowExpanderProperty, value); }
    protected override bool UsesInnerCellControl => true;
    public override bool IsEditing { get => _innerCell?.IsEditing == true; protected set => base.IsEditing = value; }
    public override bool HasValidationError { get => _innerCell?.HasValidationError == true; protected set => base.HasValidationError = value; }
    public override Exception? EditError => _innerCell?.EditError;
    public override string EditingText
    {
        get => _innerCell?.EditingText ?? string.Empty;
        set => (_innerCell ?? throw new InvalidOperationException("The inner cell has not been realized.")).EditingText = value;
    }
    internal override TreeDataGridCell GetEditingTarget() => _innerCell?.GetEditingTarget() ?? this;
    public override bool BeginEdit()
    {
        ApplyTemplate();
        return _innerCell?.BeginEdit() == true;
    }
    public override bool CommitEdit() => _innerCell?.CommitEdit() ?? true;
    public override void CancelEdit() => _innerCell?.CancelEdit();

    protected override void OnApplyTemplate()
    {
        var oldHost = _innerHost;
        _innerHost = null;
        ReleaseInner(oldHost);
        base.OnApplyTemplate();
        _innerHost = GetTemplateChild("PART_InnerCellHost") as Border ?? GetTemplateChild("PART_Content") as Border;
        if (_innerHost is not null) _innerHost.Visibility = Visibility.Visible;
        UpdateInner();
    }
    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
    {
        UpdateInner();
        return base.MeasureOverride(availableSize);
    }
    public override void Realize(CellColumn column, CellValue value, IRow row, int columnIndex, int rowIndex,
        DataTemplate? template, DataTemplate? editingTemplate = null)
    {
        if (value is not ExpanderCellValue)
            throw new InvalidOperationException("An expander requires an expander cell model.");
        _displayTemplate = template;
        _editingTemplate = editingTemplate;
        base.Realize(column, value, row, columnIndex, rowIndex, template, editingTemplate);
        UpdateInner();
    }
    public override void BeginRebind()
    {
        base.BeginRebind();
        if (_innerCell is { } inner)
        {
            _innerRebinding = true;
            inner.BeginRebind();
        }
    }
    public override void EndRebind(bool realized)
    {
        var innerRebinding = _innerRebinding;
        _innerRebinding = false;
        try { if (innerRebinding && _innerCell is { } inner) inner.EndRebind(realized && inner.RowIndex >= 0); }
        finally { base.EndRebind(realized); }
    }
    public override void Unrealize()
    {
        _unrealizing = true;
        try
        {
            try { _innerCell?.Unrealize(); }
            finally
            {
                if (_innerCell is { } inner) inner.Presenter = null;
                base.Unrealize();
            }
        }
        finally { _unrealizing = false; }
    }
    internal override void UpdateIndexes(int row, int column)
    {
        base.UpdateIndexes(row, column);
        if (_innerCell is { RowIndex: >= 0 } inner) inner.UpdateIndexes(row, column);
    }
    protected override void UpdateState()
    {
        base.UpdateState();
        if (_innerCell is { } inner)
        {
            // The outer cell owns selection/current borders. The child owns
            // selected foreground and its editor/validation visuals.
            inner.IsRowSelected = true;
            inner.IsSelected = IsSelected;
            inner.IsCurrent = false;
        }
    }
    protected override void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The realized inner control updates its public scalar state before
        // publishing the outer cell's value event. Do not publish twice through
        // the expander model's forwarding subscription.
        if (e.PropertyName == nameof(CellValue.Value) && ViewModel is ExpanderCellValue model &&
            (e is not CellContentChangedEventArgs change || !ReferenceEquals(change.Owner, model)) &&
            _innerCell is { } inner && ReferenceEquals(inner.ViewModel, model.Inner)) return;
        base.OnModelPropertyChanged(sender, e);
    }

    private void UpdateInner()
    {
        if (_updatingInner) { InvalidateMeasure(); return; }
        _updatingInner = true;
        try { UpdateInnerCore(); }
        finally { _updatingInner = false; }
    }
    private void UpdateInnerCore()
    {
        if (_unrealizing || _innerHost is not { } host || ViewModel is not ExpanderCellValue model ||
            Column is not { } outerColumn || Row is not { } row || RowIndex < 0) return;
        if (!model.HasContent) { ReleaseInner(host); return; }
        // A third-party expander model can come from an ordinary custom column;
        // in that case its per-cell templates/options supply the inner metadata.
        var column = outerColumn.InnerColumn ?? outerColumn;
        var content = model.Inner;
        if (_innerCell is { } current && ReferenceEquals(current.ViewModel, content) && current.RowIndex == RowIndex)
            return;
        var version = RealizationVersion;
        var factory = ContainerFactory ?? Presenter?.Owner?.ElementFactory ?? (_standaloneFactory ??= new());
        var inner = _innerCell;
        if (inner is not null)
        {
            var reusable = factory.CanReuseElement(inner, content.PresentationModel);
            if (!Current()) return;
            if (!reusable) { ReleaseInner(host); inner = null; }
        }
        if (!Current()) return;
        if (inner is null)
        {
            var created = factory.GetOrCreateElement(content.PresentationModel, host);
            if (created is not TreeDataGridCell cell || cell.RowIndex >= 0 ||
                (cell.Parent is not null && !ReferenceEquals(cell.Parent, host)))
                throw new InvalidOperationException("The expander factory must return an unrealized cell belonging to no other parent.");
            if (!Current()) return;
            inner = cell;
            _innerCell = inner;
            _editingChangedToken = inner.RegisterPropertyChangedCallback(IsEditingProperty, InnerEditingChanged);
            _validationChangedToken = inner.RegisterPropertyChangedCallback(HasValidationErrorProperty, InnerEditingChanged);
            host.Child = inner;
            if (!Current()) return;
        }
        var localRebind = false;
        var innerVersion = inner.RealizationVersion;
        try
        {
            if (inner.RowIndex >= 0)
            {
                // Content can change while the outer row stays realized. Reuse
                // a compatible child with the same synchronous retention path.
                localRebind = true;
                inner.BeginRebind();
                inner.Unrealize();
                if (!Current()) return;
            }
            inner.Presenter = Presenter;
            inner.ContainerFactory = factory;
            inner.OwningCell = OwningCell ?? this;
            var modelTemplate = content.GetCellTemplate(inner);
            if (!Current()) return;
            var template = modelTemplate ?? column.GetCellTemplate(inner) ?? _displayTemplate;
            if (!Current()) return;
            var modelEditingTemplate = content.GetCellEditingTemplate(inner);
            if (!Current()) return;
            var editing = modelEditingTemplate ?? column.GetCellEditingTemplate(inner) ?? _editingTemplate;
            if (!Current()) return;
            inner.Realize(column, content, row, ColumnIndex, RowIndex, template, editing);
            innerVersion = inner.RealizationVersion;
            if (!Current()) return;
            UpdateState();
        }
        catch
        {
            if (Current()) { localRebind = false; ReleaseInner(host); }
            throw;
        }
        finally
        {
            if (localRebind && inner.RealizationVersion == innerVersion)
                inner.EndRebind(Current() && ReferenceEquals(inner.ViewModel, content) && inner.RowIndex >= 0);
        }
        bool Current() => !_unrealizing && ReferenceEquals(ViewModel, model) && ReferenceEquals(model.Inner, content) &&
            RealizationVersion == version && ReferenceEquals(_innerHost, host);
    }

    private void InnerEditingChanged(DependencyObject sender, DependencyProperty property)
    {
        if (sender is not TreeDataGridCell inner || !ReferenceEquals(inner, _innerCell)) return;
        if (property == IsEditingProperty) base.IsEditing = inner.IsEditing;
        else base.HasValidationError = inner.HasValidationError;
    }

    private void ReleaseInner(Border? host)
    {
        var inner = _innerCell;
        _innerCell = null;
        _innerRebinding = false;
        if (inner is null) return;
        inner.UnregisterPropertyChangedCallback(IsEditingProperty, _editingChangedToken);
        inner.UnregisterPropertyChangedCallback(HasValidationErrorProperty, _validationChangedToken);
        base.IsEditing = false;
        base.HasValidationError = false;
        try { inner.Unrealize(); }
        finally
        {
            try { inner.EndRebind(false); }
            finally
            {
                inner.Presenter = null;
                inner.OwningCell = null;
                if (ReferenceEquals(host?.Child, inner)) host!.Child = null;
            }
        }
    }
    protected override void UpdateValue()
    {
        var model = ViewModel as ExpanderCellValue;
        var expanded = model?.IsExpanded == true;
        var show = model?.ShowExpander == true;
        var indent = ((model?.Row ?? Row) as IIndentedRow)?.Indent ?? 0;
        if (!ReferenceEquals(ViewModel, model)) return;
        ++_synchronizing;
        try
        {
            if (Indent != indent) Indent = indent;
            if (!ReferenceEquals(ViewModel, model)) return;
            if (ShowExpander != show) ShowExpander = show;
            if (!ReferenceEquals(ViewModel, model)) return;
            if (IsExpanded != expanded) IsExpanded = expanded;
        }
        finally { --_synchronizing; }
        if (ReferenceEquals(ViewModel, model)) { base.UpdateValue(); UpdateInner(); }
    }
    protected override void ClearContent()
    {
        ++_synchronizing;
        try { IsExpanded = false; ShowExpander = false; Indent = 0; }
        finally { --_synchronizing; }
        base.ClearContent();
    }
    private static void ExpandedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var cell = (TreeDataGridExpanderCell)sender;
        if (cell._synchronizing != 0) return;
        if (cell.ViewModel is ExpanderCellValue model)
        {
            try { model.IsExpanded = (bool)e.NewValue; }
            finally { cell.UpdateValue(); }
        }
    }
}
