using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Controls.Presentation;
using Uno.Controls.Primitives;

namespace Uno.Controls;

public partial class TreeDataGrid
{
    public static readonly DependencyProperty ElementFactoryProperty = DependencyProperty.Register(
        nameof(ElementFactory), typeof(TreeDataGridElementFactory), typeof(TreeDataGrid),
        new PropertyMetadata(null, ElementFactoryChanged));
    private static readonly Func<CellColumn, TreeDataGridCell> DefaultCellFactory = static _ => new();
    private Func<CellColumn, TreeDataGridCell> _cellFactory = DefaultCellFactory;
    private bool _initializingElementFactory;

    public TreeDataGridElementFactory ElementFactory
    {
        get
        {
            if (GetValue(ElementFactoryProperty) is TreeDataGridElementFactory factory) return factory;
            factory = CreateDefaultElementFactory() ?? throw new InvalidOperationException("The default element factory cannot be null.");
            _initializingElementFactory = true;
            try { SetValue(ElementFactoryProperty, factory); }
            finally { _initializingElementFactory = false; }
            return factory;
        }
        set { ArgumentNullException.ThrowIfNull(value); SetValue(ElementFactoryProperty, value); }
    }

    protected virtual TreeDataGridElementFactory CreateDefaultElementFactory() => new();

    /// <summary>Legacy Uno cell-only customization. Prefer ElementFactory for new code.</summary>
    public Func<CellColumn, TreeDataGridCell> CellFactory
    {
        get => _cellFactory;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_cellFactory, value)) return;
            _cellFactory = value;
            ResetElementContainers();
        }
    }
    internal bool HasCustomCellFactory => !ReferenceEquals(_cellFactory, DefaultCellFactory);

    internal T CreateElement<T>(object data, FrameworkElement parent, TreeDataGridElementFactory? factory = null) where T : Control
    {
        var element = (factory ?? ElementFactory).GetOrCreateElement(data, parent);
        if (element is not T typed)
            throw new InvalidOperationException($"The element factory must create a {typeof(T).Name} for this data.");
        if (element.Parent is not null && !ReferenceEquals(element.Parent, parent))
            throw new InvalidOperationException("The element factory returned an element owned by another parent.");
        if (element is TreeDataGridCell { RowIndex: >= 0 } or TreeDataGridRow { RowIndex: >= 0 } or TreeDataGridColumnHeader { ColumnIndex: >= 0 })
            throw new InvalidOperationException("The element factory returned an already realized element.");
        return typed;
    }

    private static void ElementFactoryChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var grid = (TreeDataGrid)sender;
        if (!grid._initializingElementFactory) grid.ResetElementContainers();
    }

    private void ResetElementContainers()
    {
        var revision = ++_presentationRevision;
        CancelEdit();
        if (revision != _presentationRevision) return;
        _presenter?.Reset();
        if (revision != _presentationRevision) return;
        _headers?.Update(null, _geometry, 0, 0);
        if (revision != _presentationRevision) return;
        _presenter?.SetPresentation(_loaded ? _presentation : null, _geometry);
        UpdateColumns();
    }
}
