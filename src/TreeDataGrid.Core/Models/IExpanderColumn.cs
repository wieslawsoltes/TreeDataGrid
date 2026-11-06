using System;
using System.Collections.Generic;

namespace TreeDataGridCore.Models
{
    /// <summary>Optional model observation supplied by a binding-aware column.</summary>
    public interface IModelExpansionObserver<TModel>
    {
        /// <summary>Observes expansion changes. The caller owns the returned subscription. Must not notify synchronously during subscription.</summary>
        IDisposable? SubscribeToExpansion(TModel model, Action changed);
    }

    /// <summary>Optional nested child-collection property observation, separate from collection changes.</summary>
    public interface IModelChildrenObserver<TModel>
    {
        /// <summary>Observes the child collection reference while a row needs it. Must not notify synchronously during subscription.</summary>
        IDisposable? SubscribeToChildren(TModel model, Action changed);
    }

    /// <summary>
    /// Defines a column whose cells show an expander to reveal nested data.
    /// </summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    public interface IExpanderColumn<TModel> : IColumn<TModel>
    {
        /// <summary>
        /// Gets a value indicating whether the column has nested data.
        /// </summary>
        /// <param name="model">The parent model.</param>
        bool HasChildren(TModel model);

        /// <summary>Reads a bound expansion state, or null when expansion belongs only to the source.</summary>
        bool? GetModelIsExpanded(TModel model) => null;

        /// <summary>
        /// Gets the child models which represent the nested data for this column.
        /// </summary>
        /// <param name="model">The parent model.</param>
        /// <returns>The child models if available.</returns>
        IEnumerable<TModel>? GetChildModels(TModel model);

        /// <summary>
        /// Called by an <see cref="IExpanderRow{TModel}"/> to write its
        /// <see cref="IExpander.IsExpanded"/> state to the underlying model.
        /// </summary>
        /// <param name="row">The row.</param>
        void SetModelIsExpanded(IExpanderRow<TModel> row);
    }
}
