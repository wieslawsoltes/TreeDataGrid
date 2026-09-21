using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Data;
using TreeDataGridCore.Models;

namespace Uno.Controls.Presentation;

internal interface INativeExpanderBindings<TModel>
{
    bool HasChildrenBinding { get; }
    IDisposable? SubscribeToHasChildren(TModel model, Action changed);
}

/// <summary>Native binding accessors over the actual Core hierarchy and row ownership.</summary>
internal sealed class DeclarativeExpanderColumn<TModel> : HierarchicalExpanderColumn<TModel>,
    IModelExpansionObserver<TModel>, IModelChildrenObserver<TModel>, INativeExpanderBindings<TModel> where TModel : class
{
    private readonly NativeColumnBinding? _hasChildren;
    private readonly NativeColumnBinding? _expanded;
    private readonly Binding _childrenBinding;
    private readonly Binding? _hasChildrenBinding;
    private readonly Binding? _expandedBinding;
    internal DeclarativeExpanderColumn(IColumn<TModel> inner, Binding children,
        Binding? hasChildren, Binding? expanded, DeclarativeSourceContext? context)
        : base(inner, CreateChildSelector(children, context))
    {
        _childrenBinding = children;
        _hasChildrenBinding = hasChildren;
        _expandedBinding = expanded;
        if (hasChildren is not null) { _hasChildren = new(hasChildren); context?.Own(_hasChildren); }
        if (expanded is not null) { _expanded = new(expanded); context?.Own(_expanded); }
    }
    public bool HasChildrenBinding => _hasChildrenBinding is not null;
    public override bool HasChildren(TModel model) => _hasChildren is null ? base.HasChildren(model) :
        Convert.ToBoolean(_hasChildren.ReadSnapshot(model), CultureInfo.CurrentCulture);
    public override bool? GetModelIsExpanded(TModel model) => _expanded is null ? null :
        Convert.ToBoolean(_expanded.ReadSnapshot(model), CultureInfo.CurrentCulture);
    public override void SetModelIsExpanded(IExpanderRow<TModel> row)
    {
        if (_expanded?.CanWrite == true) _expanded.WriteSnapshot(row.Model, row.IsExpanded);
    }
    public IDisposable? SubscribeToExpansion(TModel model, Action changed) => _expandedBinding is null ? null :
        NativeColumnBinding.Observe(_expandedBinding, model, changed);
    public IDisposable? SubscribeToChildren(TModel model, Action changed) => NativeColumnBinding.Observe(_childrenBinding, model, changed);
    public IDisposable? SubscribeToHasChildren(TModel model, Action changed) => _hasChildrenBinding is null ? null :
        NativeColumnBinding.Observe(_hasChildrenBinding, model, changed);

    private static Func<TModel, IEnumerable<TModel>?> CreateChildSelector(Binding children, DeclarativeSourceContext? context)
    {
        var accessor = new NativeColumnBinding(children);
        context?.Own(accessor);
        var projections = new ConditionalWeakTable<IEnumerable, DeclarativeItemsSource>();
        return model =>
        {
            if (accessor.ReadSnapshot(model) is not IEnumerable items) return null;
            // Preserve stable collection identity across reads. Creating a new
            // Cast iterator per read would look like child collection replacement
            // to Core and lose notifications/mutable-list drag support.
            if (typeof(TModel) == typeof(object))
                return (IEnumerable<TModel>)(object)projections.GetValue(items, static value => new(value));
            return items as IEnumerable<TModel> ?? throw new InvalidOperationException("The child collection has a different model type.");
        };
    }
}
