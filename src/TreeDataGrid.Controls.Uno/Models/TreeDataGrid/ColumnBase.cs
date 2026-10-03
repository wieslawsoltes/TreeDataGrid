// Column contract adapted from Avalonia TreeDataGrid (MIT).
// Copyright (c) .NET Foundation and Contributors.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Uno.Controls.Presentation;
using Uno.Experimental.Data;
using Uno.Experimental.Data.Core;

namespace Uno.Controls.Models.TreeDataGrid;

/// <summary>Compatibility base for custom native columns over shared Core rows.</summary>
/// <remarks>
/// The existing CellColumnBase owns layout. Options remain the caller's live
/// policy object, also visible through a base-class options reference. This is
/// an ICellColumn view, not a second Core column/source implementation.
/// </remarks>
public abstract class ColumnBase<TModel> : CellColumnBase<TModel>, IColumn<TModel>
{
    public ColumnBase(object? header, GridLength? width, ColumnOptions<TModel> options)
        : base(header, width, new CellColumnOptions(options ?? throw new ArgumentNullException(nameof(options))))
        => Options = options;

    public new ColumnOptions<TModel> Options { get; }

    /// <summary>Gets or sets the header using the existing notifying layout state.</summary>
    public new object? Header
    {
        get => base.Header;
        set => base.Header = value;
    }

    /// <summary>Gets or sets caller-owned column metadata.</summary>
    public new object? Tag
    {
        get => base.Tag;
        set => base.Tag = value;
    }

    /// <summary>Gets or sets the sort indicator; this does not sort the source.</summary>
    public new ListSortDirection? SortDirection
    {
        get => base.SortDirection;
        set => base.SortDirection = value;
    }
    public abstract Comparison<TModel?>? GetComparison(ListSortDirection direction);

    // Reimplement at this level: the older CellColumnBase accepts factories with
    // no comparison. Its default interface body must not shadow this virtual API.
    Comparison<TModel?>? IColumn<TModel>.GetComparison(ListSortDirection direction) => GetComparison(direction);
    ICell IColumn<TModel>.CreateCell(TreeDataGridCore.Models.IRow<TModel> row) =>
        ((ICellColumn<TModel>)this).CreateCell(row);
}

/// <summary>Native value-column extension contract with reusable typed bindings and sort delegates.</summary>
/// <remarks>
/// Sorting policy is captured at construction, as in the reference contract;
/// mutable sizing options remain live. The supplied selector and binding may
/// intentionally differ. Each CreateBindingExpression call owns independent
/// observations through the existing binding engine, not through a row cache.
/// </remarks>
public abstract class ColumnBase<TModel, TValue> : ColumnBase<TModel> where TModel : class
{
    private readonly Comparison<TModel?>? _sortAscending;
    private readonly Comparison<TModel?>? _sortDescending;

    public ColumnBase(object? header, Expression<Func<TModel, TValue?>> getter,
        Action<TModel, TValue?>? setter, GridLength? width, ColumnOptions<TModel> options)
        : this(header, CreateExpressionBinding(getter, setter), width, options) { }

    private ColumnBase(object? header,
        (Func<TModel, TValue?> Read, TypedBinding<TModel, TValue?> Binding) expression,
        GridLength? width, ColumnOptions<TModel> options)
        : this(header, expression.Read, expression.Binding, width, options) { }

    private static (Func<TModel, TValue?> Read, TypedBinding<TModel, TValue?> Binding) CreateExpressionBinding(
        Expression<Func<TModel, TValue?>> getter, Action<TModel, TValue?>? setter)
    {
        ArgumentNullException.ThrowIfNull(getter);
        // Only ordinary member paths have a compilation that is safe to share.
        // Extension nodes may execute stateful Reduce callbacks; preserve the
        // original selector-then-binding compilation order for all other shapes.
        var share = IsParameterMemberPath(getter.Body, getter.Parameters[0]);
        var selector = share ? null : getter.Compile(preferInterpretation: !RuntimeFeature.IsDynamicCodeCompiled);
        var binding = setter is null ? TypedBinding<TModel>.OneWay(getter) : TypedBinding<TModel>.TwoWay(getter, setter);
        // Capture the delegate, not a lookup through the mutable descriptor.
        // A later Binding.Read change must not alter this column's sort selector.
        return (selector ?? binding.Read!, binding);
    }

    private static bool IsParameterMemberPath(Expression expression, ParameterExpression parameter)
    {
        while (true)
        {
            switch (expression)
            {
                case MemberExpression { Expression: { } owner }:
                    expression = owner;
                    break;
                case UnaryExpression { Method: null, NodeType: ExpressionType.Convert or
                    ExpressionType.ConvertChecked or ExpressionType.TypeAs } conversion:
                    expression = conversion.Operand;
                    break;
                default:
                    return ReferenceEquals(expression, parameter);
            }
        }
    }

    public ColumnBase(object? header, Func<TModel, TValue?> valueSelector,
        TypedBinding<TModel, TValue?> binding, GridLength? width, ColumnOptions<TModel>? options)
        : base(header, width, options ?? new())
    {
        ValueSelector = valueSelector ?? throw new ArgumentNullException(nameof(valueSelector));
        Binding = binding ?? throw new ArgumentNullException(nameof(binding));
        if (options?.CanUserSortColumn != false)
        {
            _sortAscending = options?.CompareAscending ?? DefaultSortAscending;
            _sortDescending = options?.CompareDescending ?? DefaultSortDescending;
        }
    }

    public Func<TModel, TValue?> ValueSelector { get; }
    public TypedBinding<TModel, TValue?> Binding { get; }

    public override Comparison<TModel?>? GetComparison(ListSortDirection direction) => direction switch
    {
        ListSortDirection.Ascending => _sortAscending,
        ListSortDirection.Descending => _sortDescending,
        _ => null,
    };

    protected TypedBindingExpression<TModel, TValue?> CreateBindingExpression(TModel model) =>
        Binding.InstanceForCell(model);

    private int DefaultSortAscending(TModel? first, TModel? second)
    {
        if (first is null || second is null) return Comparer<TModel>.Default.Compare(first, second);
        var firstValue = ValueSelector(first);
        var secondValue = ValueSelector(second);
        return Comparer<TValue?>.Default.Compare(firstValue, secondValue);
    }
    private int DefaultSortDescending(TModel? first, TModel? second)
    {
        if (first is null || second is null) return -Comparer<TModel>.Default.Compare(first, second);
        // Preserve first/second getter evaluation even when value ordering is
        // reversed. Reversing calls changes side effects and the first exception.
        var firstValue = ValueSelector(first);
        var secondValue = ValueSelector(second);
        return Comparer<TValue?>.Default.Compare(secondValue, firstValue);
    }
}
