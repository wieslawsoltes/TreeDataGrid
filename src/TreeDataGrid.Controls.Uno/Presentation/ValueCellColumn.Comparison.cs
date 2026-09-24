using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Uno.Controls.Presentation;

public partial class ValueCellColumn<TModel, TValue>
{
    private readonly bool _allowPublicSort;
    private readonly Comparison<TModel?>? _publicAscending;
    private readonly Comparison<TModel?>? _publicDescending;
    private Comparison<TModel?>? _defaultAscending;
    private Comparison<TModel?>? _defaultDescending;

    /// <summary>Gets the original shared Core definition's cached value selector.</summary>
    /// <remarks>Reading the selector does not invoke a model getter, create a row or subscribe to a model.</remarks>
    public Func<TModel, TValue> ValueSelector => _column.Getter;

    /// <summary>Gets this value view's construction-time comparison policy.</summary>
    /// <remarks>
    /// Follows the reference value-column contract, including stable delegate
    /// identity, null-model ordering and rejection of unknown directions. No
    /// change is made to the Core source's independent sorting policy. Default
    /// delegates are created only when requested, not on the native layout path.
    /// </remarks>
    public virtual Comparison<TModel?>? GetComparison(ListSortDirection direction)
    {
        if (!_allowPublicSort) return null;
        return direction switch
        {
            ListSortDirection.Ascending => _publicAscending ?? (_defaultAscending ??= CompareAscending),
            ListSortDirection.Descending => _publicDescending ?? (_defaultDescending ??= CompareDescending),
            _ => null,
        };
    }

    private int CompareAscending(TModel? first, TModel? second)
    {
        if (first is null || second is null) return Comparer<TModel>.Default.Compare(first, second);
        return Comparer<TValue>.Default.Compare(ValueSelector(first), ValueSelector(second));
    }

    private int CompareDescending(TModel? first, TModel? second)
    {
        if (first is null || second is null) return -Comparer<TModel>.Default.Compare(first, second);
        return Comparer<TValue>.Default.Compare(ValueSelector(second), ValueSelector(first));
    }
}
