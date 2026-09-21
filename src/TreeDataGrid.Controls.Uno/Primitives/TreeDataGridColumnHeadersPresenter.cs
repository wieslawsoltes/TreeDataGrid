using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Presentation;
using Windows.Foundation;

namespace Uno.Controls.Primitives;

/// <summary>Reference columnar presenter over shared view columns and native headers.</summary>
public partial class TreeDataGridColumnHeadersPresenter : TreeDataGridColumnarPresenterBase<IColumn>
{
    private TreeDataGridPresentation? _presentation;
    private ColumnGeometry? _geometry;
    private double _offset;
    private double _viewport;
    private int _revision;
    private readonly HashSet<TreeDataGridColumnHeader> _headers = new();
    internal TreeDataGrid? Owner { get; set; }

    protected override Orientation Orientation => Orientation.Horizontal;
    protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        new global::Uno.Controls.Automation.Peers.TreeDataGridColumnHeadersPresenterAutomationPeer(this);

    // Include the focused, offscreen header as the pre-existing Uno lookup did.
    public int RealizedCount => _headers.Count;
    public IReadOnlyCollection<TreeDataGridColumnHeader> RealizedHeaders => _headers;
    public new TreeDataGridColumnHeader? TryGetElement(int index) =>
        base.TryGetElement(index) as TreeDataGridColumnHeader ??
        _headers.FirstOrDefault(header => header.ColumnIndex == index && header.Visibility == Visibility.Visible);

    internal void ReleaseFocusRetention() => InvalidateMeasure();

    internal void Update(TreeDataGridPresentation? presentation, ColumnGeometry geometry, double offset, double viewport)
    {
        var revision = ++_revision;
        _presentation = presentation;
        _geometry = geometry;
        _offset = offset;
        _viewport = viewport;
        ElementFactory = Owner?.ElementFactory;
        if (revision != _revision) return;
        Items = presentation?.Columns;
        if (revision != _revision) return;
        var generation = PresenterGeneration;
        foreach (var header in RealizedHeaders)
        {
            header.SetOwner(Owner);
            if (revision != _revision || generation != PresenterGeneration) return;
            if (Owner is { } owner && !ReferenceEquals(header.Style, owner.ColumnHeaderStyle))
                header.Style = owner.ColumnHeaderStyle;
            if (revision != _revision || generation != PresenterGeneration) return;
        }
        InvalidateMeasure();
    }

    protected override Rect? GetParentPresenterViewPort() => Owner is null
        ? base.GetParentPresenterViewPort() : new Rect(_offset, 0, _viewport, Math.Max(32, ActualHeight));

    protected override Rect GetMeasureViewport(Rect viewport) => Owner is null
        ? base.GetMeasureViewport(viewport) : new Rect(_offset, 0, _viewport, viewport.Height);

    protected override Control GetElementFromFactory(IColumn item, int index) => Owner is { } owner
        ? owner.CreateElement<TreeDataGridColumnHeader>(item, this, ElementFactory)
        : base.GetElementFromFactory(item, index);

    protected override void RealizeElement(Control element, IColumn column, int index)
    {
        var header = element as TreeDataGridColumnHeader ??
            throw new InvalidOperationException("The column factory must create a TreeDataGridColumnHeader.");
        EnsureCurrentLayout();
        var columns = Items as IColumns ??
            throw new InvalidOperationException("Column headers require an IColumns collection.");
        header.SetOwner(Owner);
        EnsureCurrentLayout();
        header.Realize(columns, index);
        EnsureCurrentLayout();
        _headers.Add(header);
        if (Owner is { } owner) header.Style = owner.ColumnHeaderStyle;
    }

    protected override void UpdateElementIndex(Control element, int oldIndex, int newIndex) =>
        ((TreeDataGridColumnHeader)element).UpdateColumnIndex(newIndex);

    protected override void UnrealizeElement(Control element)
    {
        var header = (TreeDataGridColumnHeader)element;
        _headers.Remove(header);
        header.Unrealize();
    }

    protected override Size MeasureElement(int index, Control element, Size availableSize) =>
        MeasureColumnElement(index, -1, element, availableSize);

    protected override Size MeasureOverride(Size availableSize)
    {
        Size result;
        bool repeat;
        do
        {
            var revision = _revision;
            var presentation = _presentation;
            result = base.MeasureOverride(availableSize);
            // The grid's existing constrained solver remains the width authority
            // shared with rows; public column measurement records natural widths.
            repeat = revision == _revision && ReferenceEquals(_presentation, presentation) &&
                Owner?.CommitColumnMeasurements() == true;
        } while (repeat);
        return result;
    }

    protected override Rect ArrangeElement(int index, Control element, Rect rect)
    {
        var width = Items is IColumns columns && (uint)index < (uint)columns.Count ? columns[index].ActualWidth : rect.Width;
        if (Owner is not null && _geometry is { } geometry && _presentation is { } presentation &&
            (uint)index < (uint)presentation.Columns.Count)
            rect = new Rect(geometry.Start(index), rect.Y, geometry.Width(index), rect.Height);
        else if (double.IsFinite(width))
            rect = new Rect(rect.X, rect.Y, Math.Max(0, width), rect.Height);
        return base.ArrangeElement(index, element, rect);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        (Items as IColumns)?.CommitActualWidths();
        return base.ArrangeOverride(finalSize);
    }
}
