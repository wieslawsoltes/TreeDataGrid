using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Uno.Controls.Presentation;
using CoreModels = TreeDataGridCore.Models;

namespace Uno.Controls.Models.TreeDataGrid;

/// <summary>
/// A shared Core expander column whose inner column may be a native Uno column, matching
/// Avalonia's <c>HierarchicalExpanderColumn&lt;TModel&gt;</c> constructor.
/// </summary>
public class HierarchicalExpanderColumn<TModel> : CoreModels.HierarchicalExpanderColumn<TModel>
    where TModel : class
{
    public HierarchicalExpanderColumn(
        ICellColumn<TModel> inner,
        Func<TModel, IEnumerable<TModel>?> childSelector,
        Expression<Func<TModel, bool>>? hasChildrenSelector = null,
        Expression<Func<TModel, bool>>? isExpandedSelector = null)
        : base(ColumnListExtensions.ToCoreColumn(inner), childSelector, hasChildrenSelector, isExpandedSelector)
    {
    }

    public HierarchicalExpanderColumn(
        CoreModels.IColumn<TModel> inner,
        Func<TModel, IEnumerable<TModel>?> childSelector,
        Expression<Func<TModel, bool>>? hasChildrenSelector = null,
        Expression<Func<TModel, bool>>? isExpandedSelector = null)
        : base(inner, childSelector, hasChildrenSelector, isExpandedSelector)
    {
    }
}
