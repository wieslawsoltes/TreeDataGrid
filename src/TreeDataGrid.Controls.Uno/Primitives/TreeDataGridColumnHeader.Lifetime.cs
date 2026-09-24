using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using Microsoft.UI.Xaml;
using Uno.Controls.Models.TreeDataGrid;
using Uno.Controls.Presentation;

namespace Uno.Controls.Primitives;

public partial class TreeDataGridColumnHeader
{
    private static readonly object s_true = true, s_false = false;
    private static readonly object s_visible = Visibility.Visible, s_collapsed = Visibility.Collapsed;
    private static readonly object s_ascending = ListSortDirection.Ascending, s_descending = ListSortDirection.Descending;
    private IColumn? _observedModel;
    private bool _realizing, _unrealizing, _pendingUnrealize;
    private int _refreshVersion;

    private static object? BoxSortDirection(ListSortDirection? value) => value switch
    {
        null => null,
        ListSortDirection.Ascending => s_ascending,
        ListSortDirection.Descending => s_descending,
        _ => value,
    };

    private void RealizeHeader(IColumns columns, int columnIndex)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (_realizing || _unrealizing || _model is not null)
            throw new InvalidOperationException("Column header is already realized or changing lifetime.");
        // Acquire before Count/indexer/event accessors: all may execute application code.
        _realizing = true;
        var revision = unchecked(++_realizationVersion);
        Exception? failure = null;
        try
        {
            var count = columns.Count;
            if (revision != _realizationVersion) return;
            if ((uint)columnIndex >= (uint)count) throw new ArgumentOutOfRangeException(nameof(columnIndex));
            var model = columns[columnIndex] ?? throw new InvalidOperationException("A column collection returned null.");
            if (revision != _realizationVersion) return;
            _columns = columns;
            _model = model;
            ColumnIndex = columnIndex;
            // Remember the attempted attachment before entering the accessor. If
            // it cancels and then attaches, deferred cleanup removes that attachment.
            _observedModel = model;
            model.PropertyChanged += OnModelPropertyChanged;
            if (revision != _realizationVersion || !ReferenceEquals(model, _model)) return;
            RefreshProperties();
        }
        catch (Exception error)
        {
            failure = error;
            RetireHeaderIdentity();
            _pendingUnrealize = true;
            throw;
        }
        finally
        {
            _realizing = false;
            if (_pendingUnrealize)
            {
                try { ClearHeaderRealization(); }
                catch (Exception cleanup) when (failure is not null) { throw new AggregateException(failure, cleanup); }
            }
        }
    }

    private void UpdateHeaderIndex(int columnIndex)
    {
        if (_realizing || _unrealizing || _pendingUnrealize || _model is null || _columns is not { } columns)
            throw new InvalidOperationException("Column header is not available for reindexing.");
        var revision = _realizationVersion;
        var count = columns.Count;
        if (revision != _realizationVersion || !ReferenceEquals(columns, _columns)) return;
        if ((uint)columnIndex >= (uint)count) throw new ArgumentOutOfRangeException(nameof(columnIndex));
        if (ColumnIndex == columnIndex) return;
        ColumnIndex = columnIndex;
        unchecked { ++_realizationVersion; ++_refreshVersion; }
    }

    private void RefreshProperties()
    {
        if (_unrealizing || _pendingUnrealize) return;
        var realization = _realizationVersion;
        var refresh = unchecked(++_refreshVersion);
        var model = _model;
        var owner = _owner;
        // Capture a getter's result and validate BEFORE setting a DP. Checking
        // only after the setter is too late: an obsolete value is already visible.
        var header = model?.Header;
        if (!IsHeaderRefreshCurrent(realization, refresh, model, owner)) return;
        Header = header;
        if (!IsHeaderRefreshCurrent(realization, refresh, model, owner)) return;
        Content = header;
        if (!IsHeaderRefreshCurrent(realization, refresh, model, owner)) return;
        var template = (model as CellColumn)?.HeaderTemplate;
        if (!IsHeaderRefreshCurrent(realization, refresh, model, owner)) return;
        if (template is not null) ContentTemplate = template;
        else ClearValue(ContentTemplateProperty);
        if (!IsHeaderRefreshCurrent(realization, refresh, model, owner)) return;
        var selector = (model as CellColumn)?.HeaderTemplateSelector;
        if (!IsHeaderRefreshCurrent(realization, refresh, model, owner)) return;
        if (selector is not null) ContentTemplateSelector = selector;
        else ClearValue(ContentTemplateSelectorProperty);
        if (!IsHeaderRefreshCurrent(realization, refresh, model, owner)) return;
        var canResize = model?.CanUserResize ?? owner?.CanUserResizeColumns ?? false;
        if (!IsHeaderRefreshCurrent(realization, refresh, model, owner)) return;
        CanUserResize = canResize;
        if (!IsHeaderRefreshCurrent(realization, refresh, model, owner)) return;
        var direction = model?.SortDirection;
        if (!IsHeaderRefreshCurrent(realization, refresh, model, owner)) return;
        SortDirection = direction;
        if (!IsHeaderRefreshCurrent(realization, refresh, model, owner)) return;
        // Keep the DP assignment and its local-value semantics. Cache immutable
        // boxes, not visual-state transitions or application template behavior.
        SetValue(VisibilityProperty, model is null ? s_collapsed : s_visible);
    }

    private bool IsHeaderRefreshCurrent(int realization, int refresh, IColumn? model, TreeDataGrid? owner) =>
        !_unrealizing && !_pendingUnrealize && realization == _realizationVersion && refresh == _refreshVersion &&
        ReferenceEquals(model, _model) && ReferenceEquals(owner, _owner);

    private void RetireHeaderIdentity()
    {
        unchecked { ++_realizationVersion; ++_refreshVersion; }
        _owner = null;
        _columns = null;
        _model = null;
        ColumnIndex = -1;
        _resizing = false;
    }

    private void UnrealizeHeader()
    {
        if (_unrealizing) return;
        RetireHeaderIdentity();
        if (_realizing)
        {
            // A returning subscription accessor must finish before removal. Its
            // logical identity is already retired and cannot publish or be reused.
            _pendingUnrealize = true;
            return;
        }
        ClearHeaderRealization();
    }

    private void ClearHeaderRealization()
    {
        _pendingUnrealize = false;
        _unrealizing = true;
        var observed = _observedModel;
        _observedModel = null;
        List<Exception>? errors = null;
        try
        {
            try { if (observed is not null) observed.PropertyChanged -= OnModelPropertyChanged; }
            catch (Exception error) { (errors ??= new()).Add(error); }
            ClearHeaderProperty(HeaderProperty, null, ref errors);
            ClearHeaderProperty(ContentProperty, null, ref errors);
            ClearHeaderProperty(ContentTemplateProperty, null, ref errors);
            ClearHeaderProperty(ContentTemplateSelectorProperty, null, ref errors);
            ClearHeaderProperty(CanUserResizeProperty, s_false, ref errors);
            ClearHeaderProperty(SortDirectionProperty, null, ref errors);
            ClearHeaderProperty(VisibilityProperty, s_collapsed, ref errors);
        }
        finally { _unrealizing = false; }
        if (errors is { Count: 1 }) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors is not null) throw new AggregateException("Column header cleanup failed.", errors);
    }

    private void ClearHeaderProperty(DependencyProperty property, object? value, ref List<Exception>? errors)
    {
        try { SetValue(property, value); }
        catch (Exception error) { (errors ??= new()).Add(error); }
    }
}
